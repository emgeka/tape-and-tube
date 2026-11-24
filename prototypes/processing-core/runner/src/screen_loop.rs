// SPDX-License-Identifier: GPL-3.0-only
use anyhow::{Context, Result, ensure};
use ntscrt_engine::{Engine, HowlRender, HowlSettings, Image, Settings};
use ntscrt_engine::howl::{Kind, PARAMS, Param, feedback::feedback_size};
use serde_json::{Value, json};
use std::collections::BTreeMap;

fn details(kind: Kind) -> (&'static str, f64, f64, bool, &'static str, Option<f64>) {
    match kind {
        Kind::Slider { min, max, percent, unit, step } => ("slider", min, max, percent, unit, step),
        Kind::Toggle => ("toggle", 0.0, 1.0, false, "", Some(1.0)),
        Kind::Position { min, max } => ("slider", min, max, true, "", None),
    }
}

pub fn describe() -> Value {
    let parameters: Vec<_> = PARAMS.iter().map(|p| {
        let (kind, min, max, percent, unit, step) = details(p.kind);
        json!({ "id": p.id, "label": p.label, "group": p.group.label(), "kind": kind,
            "min": min, "max": max, "percent": percent, "unit": unit, "step": step,
            "default": p.default_value, "help": p.help })
    }).collect();
    json!({ "parameters": parameters })
}

pub fn validate(values: &BTreeMap<String, f64>) -> Result<HowlSettings> {
    for (key, &value) in values {
        let param = Param::by_id(key).with_context(|| format!("Unknown Screen Loop parameter: {key}"))?;
        let (_, min, max, _, _, step) = details(param.kind);
        ensure!(value.is_finite() && (min..=max).contains(&value)
            && (step != Some(1.0) || value.fract() == 0.0), "Invalid Screen Loop value: {key}");
    }
    Ok(HowlSettings::new(values))
}

pub fn begin(engine: &mut Engine, options: &HowlSettings, frame: &Image, settings: &Settings,
    frames: u32, fps: f64) -> Result<()> {
    let input_size = settings.downscale.map_or((frame.width, frame.height), |spec| (spec.width, spec.height));
    let (w, h) = feedback_size(input_size);
    let ring = u64::from(w) * u64::from(h) * 4 * (options.delay() as u64 + 1);
    ensure!(w <= 8192 && h <= 8192 && ring <= 512 * 1024 * 1024,
        "Screen Loop feedback is too large. Reduce the internal width or delay.");
    let render = HowlRender { settings: options.clone(), length: f64::from(frames) / fps };
    engine.howl_begin(render, settings.shader.as_ref(), input_size)?;
    // The Windows host can cancel even during warm-up by stopping this worker.
    engine.howl_run_up(frame, settings, fps, &|| false)
}
