// SPDX-License-Identifier: GPL-3.0-only
use anyhow::{Context, Result, ensure};
use ntscrt_engine::glitch::{
    DECK_KEYS, DECKS, GlitchSettings, Kind, PARAMS, TAPE_KEYS, TAPES, VCR_ID, VcrCondition,
    params::param,
};
use serde::Deserialize;
use serde_json::{Value, json};
use std::collections::BTreeMap;

const MAX_SEED: f64 = 2_147_483_647.0;

#[derive(Clone, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AnalogConfiguration {
    vcr: bool,
    reception: bool,
    values: BTreeMap<String, f64>,
}

impl AnalogConfiguration {
    pub fn reception_enabled(&self) -> bool { self.reception }

    pub fn for_stage(&self) -> Result<Option<GlitchSettings>> {
        for (id, &value) in &self.values {
            let p = param(id).filter(|p| p.id != VCR_ID)
                .with_context(|| format!("Unknown VCR / reception setting: {id}"))?;
            let (min, max, discrete) = limits(p.kind);
            ensure!(value.is_finite() && (min..=max).contains(&value)
                && (!discrete || value.fract() == 0.0), "Invalid VCR / reception value: {id}");
        }
        let mut settings = GlitchSettings::from_values(self.values.iter().map(|(id, &v)| (id, v)));
        settings.values.insert(VCR_ID.to_owned(), if self.vcr { 1.0 } else { 0.0 });
        Ok(GlitchSettings::for_stage(self.reception, &settings))
    }
}

fn limits(kind: Kind) -> (f64, f64, bool) {
    match kind {
        Kind::Slider { min, max, .. } => (min, max, false),
        Kind::Toggle => (0.0, 1.0, true),
        Kind::Seed => (0.0, MAX_SEED, true),
        Kind::Choice(choices) => (0.0, (choices.len() - 1) as f64, true),
    }
}

fn conditions(list: &[VcrCondition], keys: &[&str]) -> Vec<Value> {
    list.iter().map(|condition| {
        let mut settings = GlitchSettings::default();
        condition.apply(keys, &mut settings);
        let values: BTreeMap<_, _> = keys.iter().map(|&key| (key, settings.get(key))).collect();
        json!({ "name": condition.name, "help": condition.help, "values": values })
    }).collect()
}

pub fn describe() -> Value {
    let parameters: Vec<_> = PARAMS.iter().filter(|p| p.id != VCR_ID).map(|p| {
        let (min, max, _) = limits(p.kind);
        let (kind, choices, percent, unit) = match p.kind {
            Kind::Slider { percent, unit, .. } => ("slider", &[][..], percent, unit),
            Kind::Toggle => ("toggle", &[][..], false, ""),
            Kind::Choice(choices) => ("choice", choices, false, ""),
            Kind::Seed => ("seed", &[][..], false, ""),
        };
        json!({ "id": p.id, "label": p.label, "group": p.group.label(), "section": p.section,
            "kind": kind, "min": min, "max": max, "default": p.default, "choices": choices,
            "percent": percent, "unit": unit, "help": p.help })
    }).collect();
    json!({ "parameters": parameters, "tapes": conditions(&TAPES, &TAPE_KEYS),
        "decks": conditions(&DECKS, &DECK_KEYS) })
}
