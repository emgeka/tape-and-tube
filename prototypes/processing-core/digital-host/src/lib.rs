// SPDX-License-Identifier: GPL-3.0-only
//! Experimental host code. The pinned, unchanged NTSCRT codec is MIT-licensed.
//! See include/tapetube_digital.h for the callback lifetime and ownership contract.
use ntscrt_codec::{DigitalParam, DigitalParamKind, DigitalSettings, DigitalVideoRenderer};
use serde_json::{Value, json};
use std::{
    collections::HashMap,
    ffi::{CString, c_char, c_void},
    panic::{AssertUnwindSafe, catch_unwind, panic_any},
    sync::{Arc, Mutex, OnceLock, TryLockError, atomic::{AtomicU64, Ordering}},
};

pub const API_VERSION: u32 = 1;
pub const RGBA8: u32 = 1;
pub const STILL: u32 = 1;
const MAX_PIXELS: usize = 16_777_216;
const MAX_CAPTURE_PIXELS: usize = 1_048_576;
const MAX_BYTES: usize = 256 << 20;
const MAX_REPLAY_REQUESTS: usize = 16_384;
const MAX_JSON: usize = 65_536;

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct FrameContext {
    pub struct_size: u32,
    pub format: u32,
    pub width: u32,
    pub height: u32,
    pub stride: usize,
    pub capture_width: u32,
    pub capture_height: u32,
    pub frame_index: i64,
    pub fps_num: u32,
    pub fps_den: u32,
    pub flags: u32,
    pub reserved: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct FrameView {
    pub struct_size: u32,
    pub format: u32,
    pub width: u32,
    pub height: u32,
    pub stride: usize,
    pub pixels: *const u8,
    pub length: usize,
}

pub type FrameSink = unsafe extern "C" fn(*mut c_void, *const FrameView) -> i32;
pub type FrameProvider = unsafe extern "C" fn(*mut c_void, i64, FrameSink, *mut c_void) -> i32;

#[derive(Clone, Copy, PartialEq, Eq)]
struct StreamIdentity {
    width: u32, height: u32, capture_width: u32, capture_height: u32,
    fps_num: u32, fps_den: u32, still: bool,
}

struct Session {
    settings: DigitalSettings,
    renderer: DigitalVideoRenderer,
    stream: Option<StreamIdentity>,
}
impl Session {
    fn reset(&mut self) { self.renderer = DigitalVideoRenderer::new(); self.stream = None; }
}
type Sessions = HashMap<u64, Arc<Mutex<Session>>>;
static SESSIONS: OnceLock<Mutex<Sessions>> = OnceLock::new();
static NEXT_HANDLE: AtomicU64 = AtomicU64::new(1);
fn sessions() -> &'static Mutex<Sessions> { SESSIONS.get_or_init(|| Mutex::new(HashMap::new())) }
fn session(handle: u64) -> Result<Arc<Mutex<Session>>, String> {
    sessions().lock().map_err(|_| "Session registry poisoned")?
        .get(&handle).cloned().ok_or_else(|| "Unknown or destroyed session".into())
}

fn owned(text: impl Into<String>) -> *mut c_char {
    CString::new(text.into().replace('\0', "\\0")).unwrap().into_raw()
}
fn panic_text(payload: Box<dyn std::any::Any + Send>) -> String {
    if let Some(abort) = payload.downcast_ref::<HostAbort>() { return abort.0.clone(); }
    "Digital processor panicked; session was reset".into()
}
fn boundary(body: impl FnOnce() -> Result<(), String>) -> *mut c_char {
    match catch_unwind(AssertUnwindSafe(body)) {
        Ok(Ok(())) => std::ptr::null_mut(),
        Ok(Err(e)) => owned(e),
        Err(e) => owned(panic_text(e)),
    }
}

fn settings_from_json(bytes: &[u8]) -> Result<DigitalSettings, String> {
    if bytes.len() > MAX_JSON { return Err("Settings JSON exceeds 64 KiB".into()); }
    let value: Value = serde_json::from_slice(bytes).map_err(|e| format!("Invalid settings JSON: {e}"))?;
    let object = value.as_object().ok_or("Settings must be an object of numeric upstream parameters")?;
    let mut settings = DigitalSettings::default();
    for (key, raw) in object {
        let param = DigitalParam::by_id(key).ok_or_else(|| format!("Unknown setting: {key}"))?;
        let number = raw.as_f64().filter(|v| v.is_finite()).ok_or_else(|| format!("Setting {key} must be finite and numeric"))?;
        match param.kind {
            DigitalParamKind::Slider { min, max, .. } if number < min || number > max => return Err(format!("Setting {key} must be in [{min}, {max}]")),
            DigitalParamKind::Choice(options) if number.fract() != 0.0 || number < 0.0 || number >= options.len() as f64 => return Err(format!("Invalid choice for {key}")),
            _ => {},
        }
        if let Some(step) = param.step {
            if (number / step - (number / step).round()).abs() > 1e-9 { return Err(format!("Setting {key} requires steps of {step}")); }
        }
        settings.set(key, number);
    }
    Ok(settings)
}

fn frame_bytes(width: u32, height: u32, stride: usize, length: usize) -> Result<usize, String> {
    if width == 0 || height == 0 || width > 16_384 || height > 16_384 { return Err("Invalid dimensions".into()); }
    let pixels = (width as usize).checked_mul(height as usize).ok_or("Dimensions overflow")?;
    if pixels > MAX_PIXELS { return Err("Frame exceeds 16 megapixels".into()); }
    let row = (width as usize).checked_mul(4).ok_or("Row overflow")?;
    if stride < row { return Err("RGBA8 stride is shorter than a row".into()); }
    let required = stride.checked_mul(height as usize).ok_or("Buffer size overflow")?;
    if required > MAX_BYTES || length < required || length > isize::MAX as usize { return Err("Invalid or oversized pixel buffer length".into()); }
    Ok(required)
}

fn validate_context(context: &FrameContext, length: usize) -> Result<usize, String> {
    if context.struct_size as usize != std::mem::size_of::<FrameContext>() { return Err("Unsupported frame context size".into()); }
    if context.format != RGBA8 { return Err("Only top-down straight RGBA8 is supported".into()); }
    if context.flags & !STILL != 0 || context.reserved != 0 { return Err("Unknown frame flags or reserved fields".into()); }
    if context.frame_index < 0 || context.frame_index > 10_000_000 { return Err("Frame index must be in [0, 10000000]".into()); }
    if context.fps_num == 0 || context.fps_den == 0 { return Err("Frame rate must be positive".into()); }
    let fps = context.fps_num as f64 / context.fps_den as f64;
    if !(1.0..=240.0).contains(&fps) { return Err("Frame rate must be in [1, 240]".into()); }
    let cw = context.capture_width as usize;
    let ch = context.capture_height as usize;
    if cw < 16 || ch < 16 || cw > context.width as usize || ch > context.height as usize || cw.checked_mul(ch).is_none_or(|n| n > MAX_CAPTURE_PIXELS) {
        return Err("Capture must fit the source, be at least 16x16 and at most one megapixel".into());
    }
    frame_bytes(context.width, context.height, context.stride, length)
}

struct CollectedFrame {
    expected_width: u32, expected_height: u32,
    calls: usize,
    result: Option<Result<(Vec<u8>, usize), String>>,
}

/// Copy history while the provider's storage is valid; do not retain foreign pointers.
unsafe extern "C" fn collect_frame(user: *mut c_void, view: *const FrameView) -> i32 {
    let result = catch_unwind(AssertUnwindSafe(|| -> Result<(), String> {
        if user.is_null() { return Err("Null frame sink context".into()); }
        let collected = unsafe { &mut *(user as *mut CollectedFrame) };
        collected.calls += 1;
        let data = (|| {
            if collected.calls != 1 { return Err("Frame provider called sink more than once".into()); }
            if view.is_null() { return Err("Frame provider returned null descriptor".into()); }
            let view = unsafe { view.read() };
            if view.struct_size as usize != std::mem::size_of::<FrameView>() || view.format != RGBA8 || view.width != collected.expected_width || view.height != collected.expected_height {
                return Err("History frame format/dimensions differ from current source".into());
            }
            let required = frame_bytes(view.width, view.height, view.stride, view.length)?;
            if view.pixels.is_null() { return Err("Frame provider returned null pixels".into()); }
            let pixels = unsafe { std::slice::from_raw_parts(view.pixels, required) }.to_vec();
            Ok((pixels, view.stride))
        })();
        let good = data.is_ok();
        collected.result = Some(data);
        if good { Ok(()) } else { Err("History frame rejected".into()) }
    }));
    i32::from(matches!(result, Ok(Ok(()))))
}

#[derive(Debug)]
struct HostAbort(String);

#[unsafe(no_mangle)]
pub extern "C" fn tt_digital_host_api_version() -> u32 { API_VERSION }

#[unsafe(no_mangle)]
pub extern "C" fn tt_digital_metadata_json() -> *mut c_char {
    catch_unwind(|| {
        let parameters: Vec<_> = DigitalParam::all().iter().map(|p| {
            let mut value = json!({"id":p.id,"label":p.label,"group":p.group.label(),"default":p.default_value,"help":p.help,"step":p.step});
            match p.kind {
                DigitalParamKind::Slider { min, max, percent, unit } => {value["kind"]=json!("number");value["min"]=json!(min);value["max"]=json!(max);value["percent"]=json!(percent);value["unit"]=json!(unit);},
                DigitalParamKind::Choice(options) => {value["kind"]=json!("choice");value["options"]=json!(options);},
            }
            value
        }).collect();
        owned(json!({"hostApi":API_VERSION,"settingsSchema":1,"processor":"NTSCRT ntscrt-codec","upstreamRevision":"0ab60bf952b09a0c41ea4290979f23dadd963614","upstreamLicense":"MIT","hostLicense":"GPL-3.0-only","pixelFormat":"RGBA8","alpha":"output opaque","settingsHistory":false,"seed":"upstream deterministic capture-index seed; no independent seed override","parameters":parameters}).to_string())
    }).unwrap_or(std::ptr::null_mut())
}

#[unsafe(no_mangle)]
pub extern "C" fn tt_digital_defaults_json() -> *mut c_char {
    catch_unwind(|| owned(serde_json::to_string(&DigitalSettings::default().values).unwrap())).unwrap_or(std::ptr::null_mut())
}

/// # Safety
/// JSON and out_handle must be valid for the stated lengths; no concurrent access.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn tt_digital_create(json: *const u8, length: usize, out_handle: *mut u64) -> *mut c_char {
    boundary(|| {
        if out_handle.is_null() { return Err("Null output handle".into()); }
        unsafe { *out_handle = 0; }
        if json.is_null() || length == 0 || length > MAX_JSON { return Err("Invalid settings buffer".into()); }
        let settings = settings_from_json(unsafe { std::slice::from_raw_parts(json, length) })?;
        let handle = NEXT_HANDLE.try_update(Ordering::Relaxed, Ordering::Relaxed, |h| h.checked_add(1)).map_err(|_| "Session handles exhausted")?;
        sessions().lock().map_err(|_| "Session registry poisoned")?.insert(handle, Arc::new(Mutex::new(Session {settings,renderer:DigitalVideoRenderer::new(),stream:None})));
        unsafe { *out_handle = handle; }
        Ok(())
    })
}

/// # Safety
/// Descriptors/buffers and callbacks must obey the header's lifetime contract.
/// In particular a provider must call its sink synchronously on this thread.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn tt_digital_process(handle: u64, context: *const FrameContext, pixels: *mut u8, length: usize, provider: Option<FrameProvider>, user: *mut c_void) -> *mut c_char {
    boundary(|| {
        if context.is_null() || pixels.is_null() { return Err("Null frame context or pixels".into()); }
        let context = unsafe { context.read() };
        let required = validate_context(&context, length)?;
        let session = session(handle)?;
        let mut session = match session.try_lock() {
            Ok(value) => value,
            Err(TryLockError::WouldBlock) => return Err("Session busy: concurrent or reentrant calls are unsupported".into()),
            Err(TryLockError::Poisoned(_)) => return Err("Session poisoned; destroy and recreate it".into()),
        };
        let identity = StreamIdentity {width:context.width,height:context.height,capture_width:context.capture_width,capture_height:context.capture_height,fps_num:context.fps_num,fps_den:context.fps_den,still:context.flags & STILL != 0};
        if session.stream != Some(identity) { session.reset(); session.stream = Some(identity); }
        let mut working = unsafe { std::slice::from_raw_parts(pixels, required) }.to_vec();
        // Preserve the original still independently of the in-place output.
        // Its replay uses the same counted visitor as video, so a cold
        // datamoshed seek cannot bypass the per-call history work budget.
        let still_source = identity.still.then(|| working.clone());
        let mut requests = 0usize;
        let mut visitor = |index: i64, body: &mut dyn FnMut(&[u8], usize, usize, usize)| -> bool {
            requests += 1;
            if requests > MAX_REPLAY_REQUESTS { panic_any(HostAbort("History replay exceeds per-call limit; decode sequentially first".into())); }
            if let Some(source) = still_source.as_deref() {
                body(source,context.width as usize,context.height as usize,context.stride);
                return true;
            }
            let Some(provider) = provider else { panic_any(HostAbort(format!("Real source frame {index} is required; video history provider is missing"))); };
            let mut collected = CollectedFrame {expected_width:context.width,expected_height:context.height,calls:0,result:None};
            let status = unsafe { provider(user,index,collect_frame,(&mut collected as *mut CollectedFrame).cast()) };
            if status != 1 || collected.calls != 1 { panic_any(HostAbort(format!("History provider failed for source frame {index} (status {status}, sink calls {})",collected.calls))); }
            let (data,stride) = collected.result.unwrap().unwrap_or_else(|error| panic_any(HostAbort(error)));
            body(&data,context.width as usize,context.height as usize,stride);
            true
        };
        let settings = session.settings.clone();
        let run = catch_unwind(AssertUnwindSafe(|| {
            session.renderer.process(context.frame_index,&mut working,context.width as usize,context.height as usize,context.stride,(context.capture_width as usize,context.capture_height as usize),context.fps_num as f64 / context.fps_den as f64,&settings,None,Some(&mut visitor));
        }));
        if let Err(error) = run { session.reset(); return Err(panic_text(error)); }
        unsafe { std::ptr::copy_nonoverlapping(working.as_ptr(),pixels,required); }
        Ok(())
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn tt_digital_reset(handle: u64) -> *mut c_char {
    boundary(|| {
        let session = session(handle)?;
        let mut guard = session.try_lock().map_err(|_| "Session busy or poisoned")?;
        guard.reset();
        Ok(())
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn tt_digital_destroy(handle: u64) -> *mut c_char {
    boundary(|| {
        let mut sessions = sessions().lock().map_err(|_| "Session registry poisoned")?;
        let session = sessions.get(&handle).ok_or("Unknown or destroyed session")?;
        let guard = session.try_lock().map_err(|_| "Session busy or poisoned")?;
        drop(guard);
        sessions.remove(&handle);
        Ok(())
    })
}

/// # Safety
/// Only accepts a non-null allocation returned by this library, exactly once.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn tt_digital_string_free(value: *mut c_char) {
    if !value.is_null() { unsafe { drop(CString::from_raw(value)); } }
}

#[cfg(test)]
mod tests;
