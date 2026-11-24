// SPDX-License-Identifier: GPL-3.0-only
use anyhow::{Context, Result, ensure};
use ntscrt_engine::{
    CRT_SHADERS, DigitalHistoryRef, Look, NtscSettings, Settings,
    codec::{DigitalHistory, DigitalSettings}, glitch::{GlitchFrame, GlitchSettings},
    timeline::{Evaluator, Keyframe, ShaderMeta},
};
use std::{collections::BTreeMap, path::Path, sync::Arc};

pub struct ImportedLook {
    base: Settings,
    timeline: EvaluatedTimeline,
    reception: bool,
    analog: GlitchSettings,
}

pub struct EvaluatedTimeline {
    evaluator: Option<Arc<Evaluator>>,
    digital_history: Option<DigitalHistoryRef>,
}

fn digital_at(base: &DigitalSettings, evaluator: &Evaluator, t: f64) -> DigitalSettings {
    let mut values = base.values.clone();
    values.extend(evaluator.digital(t).into_iter().filter_map(|(key, value)| Some((key, value.as_f64()?))));
    DigitalSettings::new(values)
}

fn time_at(frame: i64, count: u32) -> f64 {
    frame.clamp(0, i64::from(count) - 1) as f64 / f64::from(count.saturating_sub(1).max(1))
}

impl ImportedLook {
    pub fn new(json: &str, picture: (u32, u32), shaders: &Path, frames: u32) -> Result<Self> {
        let mut look = Look::parse(json)?;
        ensure!((1..=3).contains(&look.version) && look.raw.contains_key("downscale"), "Unsupported NTSCRT look format");
        ensure!(!look.raw.contains_key("crop"), "This NTSCRT look requires source cropping");
        ensure!(look.unported().is_empty(), "Unknown NTSCRT downscale method");
        ensure!(look.keys.len() <= 1000 && look.keys.iter().all(|k| (0.0..=1.0).contains(&k.t)), "Invalid NTSCRT timeline");
        let width = *look.downscale_width.get_or_insert(320);
        ensure!((16..=1280).contains(&width), "NTSCRT look downscale width must be 16–1280");
        if look.shader_enabled.unwrap_or(true) {
            let id = look.shader_preset.as_deref().unwrap_or("glow_gauss");
            let (_, _, path) = CRT_SHADERS.iter().find(|s| s.0 == id).context("Unknown NTSCRT CRT preset")?;
            let root = shaders.canonicalize()?;
            ensure!(root.join(path).canonicalize()?.starts_with(&root), "NTSCRT CRT preset is outside the shader folder");
        }
        // render_settings is also used for thumbnails upstream, where it bakes
        // the first key. Here the evaluator must have the unmodified base.
        let keys = std::mem::take(&mut look.keys);
        let base = look.render_settings(picture, shaders, 0)?;
        if let Some(d) = base.downscale {
            ensure!(d.picture_height() <= 8192 && u64::from(d.width) * u64::from(d.picture_height()) <= 24_000_000,
                "NTSCRT look downscale exceeds supported dimensions");
        }
        let keys = if look.timeline_enabled.unwrap_or(true) { keys } else { Vec::new() };
        let timeline = EvaluatedTimeline::new(&keys, base.shader.as_ref(), base.digital.as_ref().map(|d| &d.settings), frames)?;
        Ok(Self { base, timeline, reception: look.glitch_enabled, analog: look.glitch_values })
    }

    pub fn frame(&self, index: u32, frames: u32, fps: f64, still: bool) -> Settings {
        let mut settings = self.base.clone();
        settings.frame = index as usize + usize::from(!still || frames > 1);
        let mut analog = self.analog.clone();
        self.timeline.apply(&mut settings, Some(&mut analog), index, frames);
        settings.glitch = GlitchSettings::for_stage(self.reception, &analog).map(|settings|
            GlitchFrame { settings, time: f64::from(index) / fps.max(1.0), history: None });
        if let Some(digital) = settings.digital.as_mut() {
            digital.frame = i64::from(index);
            digital.source_fps = fps.max(1.0);
        }
        settings
    }
}

impl EvaluatedTimeline {
    pub fn new(keys: &[Keyframe], shader: Option<&ntscrt_engine::ShaderSpec>,
        digital: Option<&DigitalSettings>, frames: u32) -> Result<Self> {
        ensure!(keys.len() <= 1000 && keys.iter().all(|key| (0.0..=1.0).contains(&key.t)), "Invalid effect timeline");
        if keys.is_empty() {
            return Ok(Self { evaluator: None, digital_history: None });
        }
        let shader_meta = match shader {
            Some(shader) => ntscrt_engine::preset_parameters(&shader.preset)?.into_iter().map(|p| (
                p.name, ShaderMeta { minimum: p.minimum, maximum: p.maximum, step: p.step }
            )).collect(),
            None => Default::default(),
        };
        let evaluator = Evaluator::new(keys, shader_meta, &ntscrt_engine::ntsc_settings()).map(Arc::new);
        let digital_history = match (digital, &evaluator) {
            (Some(digital), Some(eval)) => {
                let eval = Arc::clone(eval);
                let base = digital.clone();
                // A changed configuration resets the engine and its codec checkpoints.
                Some(DigitalHistoryRef(Arc::new(DigitalHistory::new(1, move |n| digital_at(&base, &eval, time_at(n, frames))))))
            }
            _ => None,
        };
        Ok(Self { evaluator, digital_history })
    }

    pub fn apply(&self, settings: &mut Settings, analog: Option<&mut GlitchSettings>, index: u32, frames: u32) {
        let t = time_at(i64::from(index), frames);
        if let Some(eval) = &self.evaluator {
            if let Some(shader) = settings.shader.as_mut() {
                let mut values: BTreeMap<_, _> = shader.params.iter().cloned().collect();
                values.extend(eval.shader(t));
                shader.params = values.into_iter().collect();
            }
            if settings.ntsc.is_some() {
                let values = eval.ntsc(t);
                if !values.is_empty() {
                    settings.ntsc = Some(NtscSettings::Json(ntscrt_engine::ntsc_values_json(&values)));
                }
            }
            if let Some(analog) = analog {
                analog.values.extend(eval.glitch(t).into_iter().filter_map(|(key, value)| Some((key, value.as_f64()?))));
            }
        }
        if let Some(digital) = settings.digital.as_mut() {
            digital.history = self.digital_history.clone();
        }
    }
}
