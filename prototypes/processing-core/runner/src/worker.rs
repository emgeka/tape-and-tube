// SPDX-License-Identifier: GPL-3.0-only
// Persistent, framed pipe transport; effect algorithms remain upstream's.
use std::{collections::BTreeMap, io::{Read, Write}, path::PathBuf};
use anyhow::{Result, Context, ensure, bail};
use serde::Deserialize;
use serde_json::{Value,json};
use ntscrt_engine::{Engine, Image, Settings, DigitalFrame, DownscaleMethod, DownscaleSpec, NtscSettings, ShaderSpec};
use ntscrt_engine::codec::{DigitalParam,DigitalParamKind,DigitalSettings};
use ntscrt_engine::glitch::GlitchFrame;
use crate::analog::AnalogConfiguration;
const MAX_PIXELS:u64=24_000_000;
const MAX_HEADER:usize=1_048_576;
const MAX_DATA:usize=96_000_000;

fn receive(input:&mut impl Read)->Result<(Value,Vec<u8>)>{
    let mut prefix=[0u8;8]; input.read_exact(&mut prefix).context("Pipe closed while reading header")?;
    let h=u32::from_le_bytes(prefix[..4].try_into()?) as usize;
    let d=u32::from_le_bytes(prefix[4..].try_into()?) as usize;
    ensure!(h>0&&h<=MAX_HEADER&&d<=MAX_DATA,"Invalid packet length");
    let mut header=vec![0;h];input.read_exact(&mut header)?;
    let mut data=vec![0;d]; input.read_exact(&mut data)?;
    Ok((serde_json::from_slice(&header)?,data))
}
fn send(output:&mut impl Write,header:Value,data:&[u8])->Result<()>{
    let h=serde_json::to_vec(&header)?;ensure!(h.len()<=MAX_HEADER&&data.len()<=MAX_DATA,"Output too large");
    output.write_all(&(h.len() as u32).to_le_bytes())?;output.write_all(&(data.len() as u32).to_le_bytes())?;
    output.write_all(&h)?;output.write_all(data)?;output.flush()?;Ok(())
}
#[derive(Clone,Deserialize,PartialEq)]
#[serde(rename_all="camelCase",deny_unknown_fields)]
struct Configuration{
    source_id:String,width:u32,height:u32,frames:u32,fps_num:u32,fps_den:u32,still:bool,
    ntsc_json:Option<String>,shader:Option<PathBuf>,shader_root:PathBuf,
    shader_values:BTreeMap<String,f32>,retro_width:u32,output_width:u32,output_height:u32,
    digital:Option<BTreeMap<String,f64>>,
    analog:Option<AnalogConfiguration>,
    screen_loop:Option<BTreeMap<String, f64>>,
    imported_look:Option<String>,
    effect_timeline:Option<Value>,
    downscale_method: Option<String>,
    downscale_enabled: Option<bool>,
}
impl Configuration {
    fn uses_downscale(&self) -> bool {
        self.downscale_enabled.unwrap_or_else(|| self.shader.is_some() || self.digital.is_some()
            || self.analog.is_some() || self.screen_loop.is_some())
    }
}
fn dimensions(w:u32,h:u32)->Result<usize>{
    ensure!(w>0&&h>0&&w<=16384&&h<=16384&&u64::from(w)*u64::from(h)<=MAX_PIXELS,"Invalid image dimensions");
    Ok(w as usize*h as usize*4)
}

fn source_frame(c: &Configuration, frame: u32, id: u64, input: &mut impl Read, output: &mut impl Write) -> Result<Vec<u8>> {
    send(output, json!({"op":"needFrame", "id":id, "sourceId":c.source_id, "frame":frame}), &[])?;
    let (reply, pixels) = receive(input)?;
    ensure!(reply["op"]=="sourceFrame" && reply["id"]==id && reply["frame"]==frame &&
        pixels.len()==dimensions(c.width, c.height)?, "Missing genuine source history");
    Ok(pixels)
}
fn validate(c:&Configuration, method: DownscaleMethod)->Result<(Option<DigitalSettings>,Option<ShaderSpec>,Value)>{
    dimensions(c.width,c.height)?;dimensions(c.output_width,c.output_height)?;
    ensure!(c.source_id.len()<=1024&&c.frames>0&&c.frames<=10_000_000,"Invalid source descriptor");
    // FFprobe can report an average rate with large terms for ordinary video.
    // Check the actual rate against VideoSource's range, regardless of its fraction.
    ensure!(c.fps_num > 0 && c.fps_den > 0, "Invalid frame rate");
    let fps = f64::from(c.fps_num) / f64::from(c.fps_den);
    ensure!((0.1..=240.0).contains(&fps), "Frame rate must be 0.1–240 fps");
    ensure!((16..=1280).contains(&c.retro_width),"Invalid retro width");
    if c.uses_downscale() {
        let size=DownscaleSpec::for_width(c.retro_width,(c.width,c.height),method);
        dimensions(size.width,size.picture_height())?;
    }
    let digital=if let Some(values)=&c.digital{
        for(id,value)in values{
            let p=DigitalParam::by_id(id).with_context(||format!("Unknown digital setting {id}"))?;
            ensure!(value.is_finite(),"Nonfinite digital setting");
            match p.kind{
                DigitalParamKind::Slider{min,max,..}=>{
                    ensure!(*value>=min&&*value<=max,"Digital setting out of range: {id}");
                    if let Some(step)=p.step{ensure!(((value-min)/step-((value-min)/step).round()).abs()<1e-6,"Invalid digital step");}
                },
                DigitalParamKind::Choice(choices)=>ensure!(*value>=0.0&&*value<choices.len() as f64&&value.fract()==0.0,"Invalid digital choice"),
            }
        }
        Some(DigitalSettings::new(values.clone()))
    }else{None};
    let mut parameters=vec![];
    let shader=if let Some(path)=&c.shader{
        let root=c.shader_root.canonicalize()?;let path=path.canonicalize()?;
        ensure!(path.starts_with(root)&&path.extension().and_then(|x|x.to_str())==Some("slangp"),"Shader outside configured root");
        let ps=ntscrt_engine::preset_parameters(&path)?;
        for(id,value)in &c.shader_values{
            let p=ps.iter().find(|p|p.name==*id).with_context(||format!("Unknown shader parameter {id}"))?;
            ensure!(value.is_finite()&&*value>=p.minimum&&*value<=p.maximum,"Shader parameter out of range: {id}");
        }
        parameters=ps.iter().map(|p|json!({"name":p.name,"label":p.description,"initial":p.initial,"minimum":p.minimum,"maximum":p.maximum,"step":p.step})).collect();
        Some(ShaderSpec{preset:path,params:c.shader_values.iter().map(|(k,v)|(k.clone(),*v)).collect()})
    }else{None};
    Ok((digital,shader,json!(parameters)))
}

pub fn run()->Result<()>{
    let mut input=std::io::stdin().lock();let mut output=std::io::stdout().lock();
    send(&mut output,json!({"op":"hello","apiVersion":1,"rendererProfile":"ntscrt-wgpu-v2","pixelFormat":"RGBA8","description":crate::describe(),"maxPixels":MAX_PIXELS}),&[])?;
    let mut config:Option<Configuration>=None;let mut engine:Option<Engine>=None;
    let mut digital=None;let mut shader=None;let mut parameters=json!([]);
    let mut analog = None;
    let mut screen_loop = None;
    let mut imported_look = None;
    let mut effect_timeline = None;
    let mut downscale_method = DownscaleMethod::Area;
    let mut previous_source: Option<(u32, Vec<u8>)> = None;
    let mut last:Option<u32>=None;let mut cached=vec![];
    loop{
        let(header,data)=receive(&mut input)?;
        let id=header["id"].as_u64().context("Missing request id")?;
        let op=header["op"].as_str().context("Missing operation")?;
        if op=="quit"{send(&mut output,json!({"op":"bye","id":id}),&[])?;return Ok(());}
        let result=(||->Result<()>{
            match op{
                "configure"=>{
                    ensure!(data.is_empty(),"Unexpected configure payload");
                    let next:Configuration=serde_json::from_value(header["configuration"].clone())?;
                    if config.as_ref()!=Some(&next){
                        let method = next.downscale_method.as_deref()
                            .map(|name| DownscaleMethod::from_raw(name).context("Unknown downscale method"))
                            .transpose()?.unwrap_or(DownscaleMethod::Area);
                        let(d,s,p)=validate(&next, method)?;
                        analog = next.analog.as_ref().map(AnalogConfiguration::for_stage).transpose()?.flatten();
                        screen_loop = next.screen_loop.as_ref().map(crate::screen_loop::validate).transpose()?;
                        imported_look = next.imported_look.as_ref().map(|json| crate::imported_look::ImportedLook::new(
                            json, (next.width, next.height), &next.shader_root, next.frames)).transpose()?;
                        ensure!(screen_loop.is_none() || imported_look.is_none(), "Open Screen Loop separately from an imported look");
                        ensure!(next.effect_timeline.is_none() || (screen_loop.is_none() && imported_look.is_none()),
                            "Use one animation mode at a time");
                        effect_timeline = next.effect_timeline.as_ref().map(|timeline| -> Result<_> {
                            let mut look = ntscrt_engine::Look::parse(&json!({"version": 3, "timeline": timeline}).to_string())?;
                            for key in &mut look.keys {
                                key.glitch.remove(ntscrt_engine::glitch::VCR_ID);
                            }
                            crate::imported_look::EvaluatedTimeline::new(&look.keys, s.as_ref(), d.as_ref(), next.frames)
                        }).transpose()?;
                        ensure!(screen_loop.is_none() || d.is_none(), "Screen Loop bypasses digital video");
                        last=None;cached.clear();previous_source=None;
                        if let Some(e)=engine.as_mut(){e.reset_host_state();}
                        else{engine=Some(Engine::new().context("Graphics adapter cannot initialize the shared core; choose a supported backend")?);}
                        let e=engine.as_ref().unwrap();
                        let info=e.adapter_info();
                        let adapter=json!({"name":info.name,"backend":format!("{:?}",info.backend),"deviceType":format!("{:?}",info.device_type)});
                        downscale_method = method;
                        digital=d;shader=s;parameters=p;config=Some(next);
                        send(&mut output,json!({"op":"configured","id":id,"adapter":adapter,"parameters":parameters}),&[])?;
                    }else{send(&mut output,json!({"op":"configured","id":id,"parameters":parameters}),&[])?;}
                },
                "reset"=>{engine=None;last=None;cached.clear();previous_source=None;config=None;send(&mut output,json!({"op":"reset","id":id}),&[])?;},
                "render" | "previewFrame"=>{
                    let single_frame = op == "previewFrame";
                    ensure!(!single_frame || (screen_loop.is_none() && imported_look.is_none() && effect_timeline.is_none()),
                        "This look needs all preceding frames");
                    let c=config.as_ref().context("Configure source first")?;
                    let index=u32::try_from(header["frame"].as_u64().context("Missing frame")?)?;
                    ensure!(index<c.frames&&data.len()==dimensions(c.width,c.height)?,"Invalid source frame");
                    if !single_frame && last==Some(index){send(&mut output,json!({"op":"frame","id":id,"sourceId":c.source_id,"frame":index,"width":c.output_width,"height":c.output_height}),&cached)?;return Ok(());}
                    if single_frame || last.is_some_and(|n|index<n){engine.as_mut().context("Missing engine")?.reset_host_state();last=None;previous_source=None;}
                    let e=engine.as_mut().context("Worker requires reconfiguration")?;
                    let first=if single_frame {index}else{last.map_or(0,|n|n+1)};
                    let fps = f64::from(c.fps_num) / f64::from(c.fps_den);
                    for n in first..=index{
                        let pixels=if n==index{data.clone()}else if c.still{data.clone()}else{
                            source_frame(c, n, id, &mut input, &mut output)?
                        };
                        let mut downscale=DownscaleSpec::for_width(c.retro_width,(c.width,c.height),downscale_method);
                        if digital.as_ref().is_some_and(|d|d.fields()==ntscrt_engine::codec::FieldMode::OneField){downscale=downscale.one_field();}
                        let downscale = c.uses_downscale().then_some(downscale);
                        let mut settings=Settings{
                            ntsc:c.ntsc_json.as_ref().map(|s|NtscSettings::Json(s.clone())),
                            downscale, shader:shader.clone(),frame: n as usize + usize::from(!c.still || screen_loop.is_none()),
                            glitch: analog.as_ref().map(|settings| GlitchFrame { settings: settings.clone(), time: f64::from(n) / fps.max(1.0), history: None }),
                            digital:digital.as_ref().map(|d|DigitalFrame{settings:d.clone(),frame:n as i64,source_fps:fps,capture:DigitalFrame::capture_size((c.width,c.height),downscale.map(|spec| (spec.width, spec.picture_height()))),history:None}),
                        };
                        if let Some(look) = &imported_look {
                            settings = look.frame(n, c.frames, fps, c.still);
                        }
                        if let Some(timeline) = &effect_timeline {
                            let mut glitch = settings.glitch.take();
                            timeline.apply(&mut settings, glitch.as_mut().map(|g| &mut g.settings), n, c.frames);
                            if let Some(glitch) = &mut glitch {
                                if !c.analog.as_ref().is_some_and(AnalogConfiguration::reception_enabled) {
                                    glitch.settings = glitch.settings.reception_healthy();
                                }
                            }
                            settings.glitch = glitch;
                        }
                        let visit_earlier = !c.still && !single_frame &&
                            settings.digital.as_ref().is_some_and(|d| d.settings.fields().is_interlaced());
                        let remember_source = visit_earlier.then(|| (n, pixels.clone()));
                        let scene = Image::new(c.width, c.height, pixels);
                        if let Some(options) = &screen_loop {
                            if n == 0 {
                                crate::screen_loop::begin(e, options, &scene, &settings, c.frames, fps)?;
                            }
                            let camera = e.howl_camera(&scene, f64::from(n) / fps)?;
                            e.set_source(camera);
                        } else {
                            e.set_source(scene);
                        }
                        let size = (c.output_width,c.output_height);
                        let rendered = if visit_earlier {
                            let mut read_error = None;
                            let mut visit = |frame: i64, use_pixels: &mut dyn FnMut(&[u8], usize, usize, usize)| {
                                let Ok(frame) = u32::try_from(frame) else { return false };
                                if frame >= n { return false }
                                let (w,h) = (c.width as usize,c.height as usize);
                                if let Some((index, pixels)) = &previous_source {
                                    if *index == frame {
                                        use_pixels(pixels,w,h,w*4);
                                        return true;
                                    }
                                }
                                match source_frame(c,frame,id,&mut input,&mut output) {
                                    Ok(pixels) => { use_pixels(&pixels,w,h,w*4); true }
                                    Err(error) => { read_error=Some(error); false }
                                }
                            };
                            let rendered = e.render_export(&settings,size,Some(&mut visit));
                            if let Some(error) = read_error { return Err(error) }
                            rendered?
                        } else {
                            e.render_export(&settings,size,None)?
                        };
                        previous_source = remember_source;
                        if screen_loop.is_some() { e.howl_advance(); }
                        last=Some(n);cached=rendered.rgba;
                        if n<index{send(&mut output,json!({"op":"progress","id":id,"frame":n,"target":index}),&[])?;}
                    }
                    send(&mut output,json!({"op":"frame","id":id,"sourceId":c.source_id,"frame":index,"width":c.output_width,"height":c.output_height}),&cached)?;
                    if single_frame {
                        e.reset_host_state();
                        last = None;
                        cached = Vec::new();
                    }
                },
                _=>bail!("Unknown worker operation"),
            }
            Ok(())
        })();
        if let Err(error)=result{
            send(&mut output,json!({"op":"error","id":id,"message":format!("{error:#}")}),&[])?;
            // A failed operation invalidates the entire temporal state.
            engine=None;config=None;last=None;cached.clear();previous_source=None;
        }
    }
}
