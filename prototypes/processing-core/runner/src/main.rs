// SPDX-License-Identifier: GPL-3.0-only
// The upstream engine and effect implementations retain their original licenses.
use std::collections::BTreeMap;
use std::fs::{self, File, OpenOptions};
use std::io::{Read, Seek, SeekFrom, Write};
use std::path::{Path, PathBuf};
use std::time::{Instant, SystemTime, UNIX_EPOCH};

use anyhow::{Context, Result, bail, ensure};
use ntscrt_engine::codec::{DigitalFormat, DigitalParam, DigitalParamKind, DigitalSettings};
use ntscrt_engine::{DigitalFrame, DownscaleMethod, DownscaleSpec, Engine, Image, NtscSettings, Settings, ShaderSpec};
use serde::Deserialize;
use serde_json::{Value, json};
mod worker;
mod analog;
mod screen_loop;
mod imported_look;

const API_VERSION: u32 = 1;
const MAX_DIMENSION: u32 = 8192;
const MAX_PIXELS: u64 = 16_777_216;
const MAX_FRAMES: usize = 100_000;
const MAX_RAW_BYTES: u64 = 128 * 1024 * 1024 * 1024;
const MAX_JOB_BYTES: u64 = 1024 * 1024;

#[derive(Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct Job {
    api_version: u32,
    input_path: PathBuf,
    output_path: PathBuf,
    width: u32,
    height: u32,
    output_width: u32,
    output_height: u32,
    frames: usize,
    fps_num: u32,
    fps_den: u32,
    retro_width: u32,
    shader_root: PathBuf,
    #[serde(default)]
    shader_preset: Option<PathBuf>,
    #[serde(default)]
    ntsc_json: Option<String>,
    #[serde(default)]
    digital_format: Option<String>,
    #[serde(default)]
    digital_settings: Option<BTreeMap<String, f64>>,
    #[serde(default)]
    indices: Option<Vec<usize>>,
}

fn sources() -> Value {
    json!({
        "ntscrt": "889256dde173a89e475919be3d1bba34e6cc7355",
        "ntscRs": "add90f5bf1bf7e3c573e4e945a16f44a82941b51",
        "slangShaders": "cb01f2f238700eed7ec1c859be1737a9fbcdcb7c",
        "librashader": "76462c030b75c4f2d56e5386c3d4d7d1128318b8",
        "librashaderPatch": "NTSCRT/port/librashader-wgpu-push-constants.patch",
        "hostLicense": "GPL-3.0-only",
        "effectLicense": "NTSCRT MIT; dependencies retain their own licenses"
    })
}

fn describe() -> Value {
    let formats: Vec<_> = DigitalFormat::all().iter().map(|f| json!({
        "id": f.id, "name": f.name, "suggestedRetroWidth": f.width,
        "values": f.values_map()
    })).collect();
    let parameters: Vec<_> = DigitalParam::all().iter().map(|p| {
        let kind = match p.kind {
            DigitalParamKind::Slider { min, max, percent, unit } =>
                json!({"type":"number", "minimum":min, "maximum":max, "percent":percent, "unit":unit, "step":p.step}),
            DigitalParamKind::Choice(choices) =>
                json!({"type":"choice", "choices":choices}),
        };
        json!({"id":p.id, "label":p.label, "default":p.default_value, "kind":kind})
    }).collect();
    json!({
        "apiVersion":API_VERSION, "ok":true, "name":"tapetube-core-probe",
        "sources":sources(),
        "stages":["digital", "ntsc", "areaDownscale", "crt", "output"],
        "implementedInThisProbe":["rawRgba8", "sequentialFrames", "seekWithCodecHistory", "originalFrameIndices"],
        "notExposedInThisProbe":["vcr", "glitch", "screenLoop", "timeline", "mediaDecode", "audio"],
        "digitalFormats":formats, "digitalParameters":parameters,
        "analog":analog::describe(),
        "screenLoop":screen_loop::describe(),
        "crtPresets":ntscrt_engine::CRT_SHADERS.iter().map(|(id,name,path)|
            json!({"id":id,"name":name,"path":path})).collect::<Vec<_>>(),
        "schema":{
            "required":["apiVersion","inputPath","outputPath","width","height","outputWidth","outputHeight","frames","fpsNum","fpsDen","retroWidth","shaderRoot"],
            "optional":["shaderPreset","ntscJson","digitalFormat","digitalSettings","indices"],
            "pixelFormat":"RGBA8, straight alpha, packed rows, width * 4 bytes per row",
            "paths":"Relative paths resolve against the job file; output must not already exist.",
            "inputLength":"Exactly width * height * 4 * frames bytes; no header.",
            "indices":"Requested original frame numbers in output order; defaults to every source frame.",
            "digital":"null format and null settings bypass; a format selects upstream values, settings override them; retroWidth is always explicit.",
            "ntsc":"ntscJson is the upstream settings JSON encoded as a string; null bypasses.",
            "crt":"shaderPreset is a relative .slangp path within shaderRoot; null bypasses.",
            "output":"Concatenated raw frames at outputWidth * outputHeight; atomically committed only after all renders succeed."
        },
        "limits":{"maxDimension":MAX_DIMENSION,"maxPixelsPerFrame":MAX_PIXELS,"maxSourceFrames":MAX_FRAMES,
            "maxRequestedFrames":MAX_FRAMES,"maxRawBytes":MAX_RAW_BYTES,"maxJobBytes":MAX_JOB_BYTES,
            "minRetroWidth":16,"maxRetroWidth":1280,"minFps":1.0,"maxFps":240.0}
    })
}

fn rooted(base: &Path, path: &Path) -> PathBuf {
    if path.is_absolute() { path.to_path_buf() } else { base.join(path) }
}

fn frame_bytes(width: u32, height: u32, name: &str) -> Result<u64> {
    ensure!(width > 0 && height > 0 && width <= MAX_DIMENSION && height <= MAX_DIMENSION,
        "{name} dimensions must be between 1 and {MAX_DIMENSION}");
    let pixels = u64::from(width) * u64::from(height);
    ensure!(pixels <= MAX_PIXELS, "{name} exceeds the {MAX_PIXELS} pixel limit");
    Ok(pixels * 4)
}

fn digital_settings(job: &Job) -> Result<Option<DigitalSettings>> {
    if job.digital_format.is_none() && job.digital_settings.is_none() { return Ok(None); }
    let mut values = match job.digital_format.as_deref() {
        Some(id) => DigitalFormat::by_id(id).with_context(|| format!("Unknown digitalFormat {id:?}"))?.values_map(),
        None => BTreeMap::new(),
    };
    if let Some(overrides) = &job.digital_settings {
        for (id, value) in overrides {
            let p = DigitalParam::by_id(id).with_context(|| format!("Unknown digital setting {id:?}"))?;
            ensure!(value.is_finite(), "Digital setting {id} must be finite");
            match p.kind {
                DigitalParamKind::Slider { min, max, .. } => {
                    ensure!(*value >= min && *value <= max, "Digital setting {id} is outside {min}..{max}");
                    if let Some(step) = p.step {
                        let ticks = (value - min) / step;
                        ensure!((ticks - ticks.round()).abs() < 1e-7, "Digital setting {id} requires steps of {step}");
                    }
                }
                DigitalParamKind::Choice(choices) => {
                    ensure!(*value >= 0.0 && *value < choices.len() as f64 && value.fract() == 0.0,
                        "Digital setting {id} requires a valid integer choice index");
                }
            }
            values.insert(id.clone(), *value);
        }
    }
    Ok(Some(DigitalSettings::new(values)))
}

/// RAII removes incomplete output on ordinary errors and unwinding panics.
struct PendingOutput {
    path: PathBuf,
    file: Option<File>,
    committed: bool,
}

impl PendingOutput {
    fn create(destination: &Path) -> Result<Self> {
        let parent = destination.parent().context("Output has no parent directory")?;
        let stamp = SystemTime::now().duration_since(UNIX_EPOCH)?.as_nanos();
        for attempt in 0..100 {
            let path = parent.join(format!(".tapetube-core-{}-{stamp}-{attempt}.partial", std::process::id()));
            match OpenOptions::new().write(true).create_new(true).open(&path) {
                Ok(file) => return Ok(Self { path, file:Some(file), committed:false }),
                Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => continue,
                Err(e) => return Err(e).context("Cannot create temporary output"),
            }
        }
        bail!("Cannot reserve temporary output name")
    }

    fn commit(mut self, destination: &Path) -> Result<()> {
        let mut file = self.file.take().context("Output file was already closed")?;
        file.flush()?;
        file.sync_all()?;
        drop(file);
        ensure!(fs::symlink_metadata(destination).is_err_and(|e| e.kind() == std::io::ErrorKind::NotFound),
            "Output appeared while rendering; refusing to replace it");
        publish_without_replacement(&self.path, destination)
            .context("Cannot commit output without replacing an existing file")?;
        self.committed = true;
        Ok(())
    }
}

impl Drop for PendingOutput {
    fn drop(&mut self) {
        self.file.take();
        if !self.committed { let _ = fs::remove_file(&self.path); }
    }
}

#[cfg(windows)]
fn publish_without_replacement(source: &Path, destination: &Path) -> std::io::Result<()> {
    use std::os::windows::ffi::OsStrExt;
    #[link(name = "kernel32")]
    unsafe extern "system" {
        fn MoveFileW(source: *const u16, destination: *const u16) -> i32;
    }
    let source: Vec<u16> = source.as_os_str().encode_wide().chain(Some(0)).collect();
    let destination: Vec<u16> = destination.as_os_str().encode_wide().chain(Some(0)).collect();
    // Unlike replace-capable rename APIs, MoveFileW fails if destination exists.
    if unsafe { MoveFileW(source.as_ptr(), destination.as_ptr()) } == 0 {
        Err(std::io::Error::last_os_error())
    } else { Ok(()) }
}

#[cfg(not(windows))]
fn publish_without_replacement(source: &Path, destination: &Path) -> std::io::Result<()> {
    // Same-directory link is atomic and never replaces an existing Unix file.
    fs::hard_link(source, destination)?;
    if let Err(error) = fs::remove_file(source) {
        eprintln!("Completed output is saved; temporary file cleanup failed: {error}");
    }
    Ok(())
}

fn read_frame(file: &mut File, index: usize, frame_len: u64, pixels: &mut [u8]) -> Result<()> {
    let offset = frame_len.checked_mul(index as u64).context("Frame offset overflow")?;
    file.seek(SeekFrom::Start(offset))?;
    file.read_exact(pixels).with_context(|| format!("Cannot read source frame {index}"))
}

fn run_job(job_path: &Path) -> Result<Value> {
    let started = Instant::now();
    let job_path = fs::canonicalize(job_path).context("Cannot find job file")?;
    let base = job_path.parent().context("Job has no parent directory")?;
    let job_bytes = fs::metadata(&job_path)?.len();
    ensure!(job_bytes <= MAX_JOB_BYTES, "Job exceeds {MAX_JOB_BYTES} bytes");
    let job: Job = serde_json::from_slice(&fs::read(&job_path)?).context("Invalid job JSON")?;
    ensure!(job.api_version == API_VERSION, "Unsupported apiVersion {}; expected {API_VERSION}", job.api_version);
    ensure!(job.frames > 0 && job.frames <= MAX_FRAMES, "frames must be between 1 and {MAX_FRAMES}");
    let input_len = frame_bytes(job.width, job.height, "Input")?;
    let output_len = frame_bytes(job.output_width, job.output_height, "Output")?;
    ensure!((16..=1280).contains(&job.retro_width), "retroWidth must be between 16 and 1280");
    ensure!(job.fps_num > 0 && job.fps_num <= 1_000_000 && job.fps_den > 0 && job.fps_den <= 1_000_000,
        "fpsNum and fpsDen must be between 1 and 1000000");
    let fps = job.fps_num as f64 / job.fps_den as f64;
    ensure!((1.0..=240.0).contains(&fps), "Frame rate must be between 1 and 240 fps");
    let indices: Vec<usize> = match &job.indices {
        Some(indices) => indices.clone(),
        None => (0..job.frames).collect(),
    };
    ensure!(!indices.is_empty() && indices.len() <= MAX_FRAMES, "indices must contain 1..{MAX_FRAMES} entries");
    ensure!(indices.iter().all(|&i| i < job.frames), "A requested index is outside the source frame range");
    let expected_input = input_len.checked_mul(job.frames as u64).context("Input length overflow")?;
    let expected_output = output_len.checked_mul(indices.len() as u64).context("Output length overflow")?;
    ensure!(expected_input <= MAX_RAW_BYTES && expected_output <= MAX_RAW_BYTES, "Raw input/output exceeds {MAX_RAW_BYTES} bytes");
    let input_path = fs::canonicalize(rooted(base, &job.input_path)).context("Cannot find inputPath")?;
    ensure!(fs::metadata(&input_path)?.is_file(), "inputPath must be a regular file");
    ensure!(fs::metadata(&input_path)?.len() == expected_input,
        "Input size does not match width * height * 4 * frames ({expected_input} bytes)");
    let output_requested = rooted(base, &job.output_path);
    ensure!(fs::symlink_metadata(&output_requested).is_err_and(|e| e.kind() == std::io::ErrorKind::NotFound),
        "outputPath already exists or cannot be checked; source/output replacement is forbidden");
    let output_parent = fs::canonicalize(output_requested.parent().context("outputPath has no parent")?)
        .context("Output directory must already exist")?;
    ensure!(output_parent.is_dir(), "Output parent must be a directory");
    let output_path = output_parent.join(output_requested.file_name().context("outputPath has no filename")?);
    ensure!(output_path != input_path && output_path != job_path, "Output cannot replace the input or job file");
    let shader = match &job.shader_preset {
        Some(relative) => {
            ensure!(relative.is_relative(), "shaderPreset must be relative to shaderRoot");
            let shader_root = fs::canonicalize(rooted(base, &job.shader_root)).context("Cannot find shaderRoot")?;
            let preset = fs::canonicalize(shader_root.join(relative)).context("Cannot find shaderPreset")?;
            ensure!(preset.starts_with(&shader_root) && preset.is_file(), "shaderPreset must be a file within shaderRoot");
            ensure!(preset.extension().and_then(|e| e.to_str()).is_some_and(|e| e.eq_ignore_ascii_case("slangp")),
                "shaderPreset must be a .slangp file");
            Some(ShaderSpec { preset, params:vec![] })
        }
        None => None,
    };
    if let Some(json) = &job.ntsc_json {
        ensure!(json.len() <= 65_536, "ntscJson exceeds 65536 bytes");
        serde_json::from_str::<Value>(json).context("ntscJson is not valid JSON")?;
    }
    let digital = digital_settings(&job)?;
    let mut downscale = DownscaleSpec::for_width(job.retro_width, (job.width, job.height), DownscaleMethod::Area);
    if digital.as_ref().is_some_and(|s| s.fields() == ntscrt_engine::codec::FieldMode::OneField) { downscale = downscale.one_field(); }
    frame_bytes(downscale.width, downscale.height, "Downscale")?;
    let capture = DigitalFrame::capture_size((job.width, job.height), Some((downscale.width, downscale.picture_height())));
    let mut input = File::open(&input_path).context("Cannot open inputPath")?;
    let mut earlier_input = File::open(&input_path).context("Cannot open codec history source")?;
    let mut earlier_pixels = vec![0u8; input_len as usize];
    let init_started = Instant::now();
    let mut engine = Engine::new().context("GPU engine initialization failed; no rendering tests were skipped")?;
    let initialization_ms = init_started.elapsed().as_secs_f64() * 1000.0;
    let info = engine.adapter_info();
    let adapter = json!({"name":info.name,"vendor":info.vendor,"device":info.device,
        "backend":format!("{:?}",info.backend),"deviceType":format!("{:?}",info.device_type),
        "driver":info.driver,"driverInfo":info.driver_info});
    eprintln!("Rendering {} requested frames on {} ({:?})", indices.len(), info.name, info.backend);
    let mut pending = PendingOutput::create(&output_path)?;
    let mut render_ms = 0.0;
    let mut history_reads = 0usize;
    for &index in &indices {
        let mut pixels = vec![0u8; input_len as usize];
        read_frame(&mut input, index, input_len, &mut pixels)?;
        engine.set_source(Image::new(job.width, job.height, pixels));
        let settings = Settings {
            ntsc: job.ntsc_json.as_ref().map(|s| NtscSettings::Json(s.clone())),
            downscale:Some(downscale), shader:shader.clone(), frame:index + 1, glitch:None,
            digital: digital.as_ref().map(|s| DigitalFrame {
                settings:s.clone(), frame:index as i64, source_fps:fps, capture, history:None,
            }),
        };
        let mut history_error = None;
        let mut visitor = |n: i64, body: &mut dyn FnMut(&[u8], usize, usize, usize)| -> bool {
            let prior = match usize::try_from(n) {
                Ok(prior) if prior < job.frames => prior,
                _ => { history_error = Some(format!("Codec requested invalid source frame {n}")); return false; }
            };
            if let Err(e) = read_frame(&mut earlier_input, prior, input_len, &mut earlier_pixels) {
                history_error = Some(format!("{e:#}"));
                return false;
            }
            history_reads += 1;
            body(&earlier_pixels, job.width as usize, job.height as usize, job.width as usize * 4);
            true
        };
        let frame_started = Instant::now();
        let rendered = engine.render_export(&settings, (job.output_width, job.output_height), Some(&mut visitor));
        render_ms += frame_started.elapsed().as_secs_f64() * 1000.0;
        if let Some(error) = history_error { bail!("Cannot restore digital history: {error}"); }
        let rendered = rendered.with_context(|| format!("Rendering source frame {index} failed"))?;
        ensure!(rendered.width == job.output_width && rendered.height == job.output_height && rendered.rgba.len() as u64 == output_len,
            "Engine returned an unexpected output shape for frame {index}");
        pending.file.as_mut().context("Temporary output is closed")?.write_all(&rendered.rgba)?;
    }
    pending.commit(&output_path)?;
    Ok(json!({
        "apiVersion":API_VERSION,"ok":true,"framesWritten":indices.len(),"sourceFrames":job.frames,
        "bytesWritten":expected_output,"width":job.output_width,"height":job.output_height,
        "fpsNum":job.fps_num,"fpsDen":job.fps_den,"indices":indices,"historyReads":history_reads,
        "inputPath":input_path,"outputPath":output_path,"adapter":adapter,"sources":sources(),
        "timingsMs":{"initialization":initialization_ms,"render":render_ms,"total":started.elapsed().as_secs_f64()*1000.0},
        "stages":{"digital":digital.is_some(),"ntsc":job.ntsc_json.is_some(),"downscale":"area","crt":shader.is_some()},
        "digitalSettings":digital.as_ref().map(|s| &s.values)
    }))
}

fn main() {
    if std::env::args().nth(1).as_deref()==Some("--worker") {
        let result=std::panic::catch_unwind(worker::run);
        match result { Ok(Ok(()))=>{}, Ok(Err(e))=>{eprintln!("{e:#}");std::process::exit(1);},Err(_)=>{eprintln!("Core worker graphics failure; process state discarded");std::process::exit(1);} }
        return;
    }
    let result = std::panic::catch_unwind(|| -> Result<Value> {
        let args: Vec<_> = std::env::args_os().skip(1).collect();
        match args.as_slice() {
            [arg] if arg == "--describe" => Ok(describe()),
            [arg, path] if arg == "--job" => run_job(Path::new(path)),
            _ => bail!("Usage: tapetube-core-probe --describe | --job <job.json>"),
        }
    });
    let (value, success) = match result {
        Ok(Ok(value)) => (value, true),
        Ok(Err(error)) => {
            eprintln!("{error:#}");
            (json!({"apiVersion":API_VERSION,"ok":false,"error":format!("{error:#}")}), false)
        }
        Err(payload) => {
            let message = payload.downcast_ref::<String>().map(String::as_str)
                .or_else(|| payload.downcast_ref::<&str>().copied()).unwrap_or("unknown panic");
            (json!({"apiVersion":API_VERSION,"ok":false,"error":format!("Rendering process panicked: {message}")}), false)
        }
    };
    println!("{}", value);
    if !success { std::process::exit(1); }
}
