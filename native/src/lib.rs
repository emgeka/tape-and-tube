//! Windows host boundary for the unmodified upstream ntsc-rs processor.
use std::ffi::{c_char, CStr, CString};
use std::panic::{catch_unwind, AssertUnwindSafe};
use ntsc_rs::{NtscEffect, settings::{standard::NtscEffectFullSettings, SettingsList, SettingDescriptor, SettingKind},
    yiq_fielding::{BlitInfo, Bgrx, DeinterlaceMode, YiqView}};
use serde_json::{json, Value};

#[unsafe(no_mangle)]
pub extern "C" fn tapetube_host_api_version() -> u32 { 1 }

fn string(value: String) -> *mut c_char { CString::new(value).map(CString::into_raw).unwrap_or(std::ptr::null_mut()) }

fn describe(d: &SettingDescriptor<NtscEffectFullSettings>) -> Value {
    let mut v = json!({"id":d.id.name,"label":d.label,"description":d.description});
    match &d.kind {
        SettingKind::Boolean => v["kind"] = json!("bool"),
        SettingKind::Percentage { .. } => { v["kind"]=json!("number"); v["min"]=json!(0); v["max"]=json!(1); },
        SettingKind::IntRange { range } => { v["kind"]=json!("int"); v["min"]=json!(range.start()); v["max"]=json!(range.end()); },
        SettingKind::FloatRange { range, .. } => { v["kind"]=json!("number"); v["min"]=json!(range.start()); v["max"]=json!(range.end()); },
        SettingKind::Enumeration { options } => { v["kind"]=json!("enum"); v["options"]=json!(options.iter().map(|o|json!({"label":o.label,"value":o.index})).collect::<Vec<_>>()); },
        SettingKind::Group { children } => { v["kind"]=json!("group"); v["children"]=json!(children.iter().map(describe).collect::<Vec<_>>()); },
    }
    v
}

#[unsafe(no_mangle)]
pub extern "C" fn ntscrt_schema() -> *mut c_char {
    catch_unwind(|| string(json!(SettingsList::<NtscEffectFullSettings>::new().setting_descriptors.iter().map(describe).collect::<Vec<_>>()).to_string())).unwrap_or(std::ptr::null_mut())
}
#[unsafe(no_mangle)]
pub extern "C" fn ntscrt_defaults() -> *mut c_char {
    catch_unwind(|| string(SettingsList::<NtscEffectFullSettings>::new().to_json_string(&NtscEffectFullSettings::default()).unwrap())).unwrap_or(std::ptr::null_mut())
}
/// Caller owns the returned allocation until this function is called, exactly once.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ntscrt_string_free(value: *mut c_char) {
    if !value.is_null() { unsafe { drop(CString::from_raw(value)); } }
}
/// All pointers must reference valid buffers for the documented lengths.
/// Returns an allocated error message, or null on success. Never unwinds into .NET.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ntscrt_process(settings: *const c_char, pixels: *mut u8, length: usize, width: u32, height: u32, frame: u32) -> *mut c_char {
    let result = catch_unwind(AssertUnwindSafe(|| -> Result<(), String> {
        let required = (width as usize).checked_mul(height as usize).and_then(|n|n.checked_mul(4)).ok_or("Bild zu groß")?;
        if settings.is_null() || pixels.is_null() || width == 0 || height == 0 || length != required { return Err("Ungültiger Bildpuffer".into()); }
        let text = unsafe { CStr::from_ptr(settings) }.to_str().map_err(|e|e.to_string())?;
        let settings = SettingsList::<NtscEffectFullSettings>::new().from_json(text).map_err(|e|e.to_string())?;
        let effect: NtscEffect = (&settings).into();
        let dims = (width as usize, height as usize);
        let field = effect.use_field.to_yiq_field(frame as usize);
        let mut planar = vec![0f32; YiqView::buf_length_for(dims, field)];
        let mut yiq = YiqView::from_parts(&mut planar, dims, field);
        let data = unsafe { std::slice::from_raw_parts_mut(pixels, length) };
        let blit = BlitInfo::from_full_frame(dims.0, dims.1, dims.0 * 4);
        yiq.set_from_strided_buffer::<Bgrx, u8, _>(data, blit, ());
        effect.apply_effect_to_yiq(&mut yiq, frame as usize, [1.0, 1.0]);
        yiq.write_to_strided_buffer::<Bgrx, u8, _>(data, blit, DeinterlaceMode::Bob, ());
        Ok(())
    }));
    match result { Ok(Ok(())) => std::ptr::null_mut(), Ok(Err(e)) => string(e), Err(_) => string("ntsc-rs konnte das Bild nicht verarbeiten".into()) }
}
