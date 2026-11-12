# Shader redistribution review — retain all seven presets

Review date: 2026-11-11. Pinned snapshot: `cb01f2f238700eed7ec1c859be1737a9fbcdcb7c`.

**Owner requirement: keep all seven existing presets.** Do not remove, rename, silently replace or disable presets as a shortcut to publication.

## Release policy adopted by the owner

On 2026-11-11 the owner chose to follow the upstream collection's practical handling of incomplete licensing information. Preserve the pinned upstream files, existing licenses, attribution and available source. Document the unresolved scopes openly. Missing per-file notices or an unanswered scope question are **not automatic release blockers** and do not require individual retrospective permission requests for every port contribution.

This accepts documented uncertainty; it does not create a blanket GPL/MIT grant or change third-party licenses. Concrete incompatible restrictions, rights-holder objections or takedown requests must be assessed when identified. None is established for the selected presets by this review. The license inventory remains incomplete in the evidentiary sense, while these recorded shader uncertainties are accepted under the owner's release policy. Separate native dependency packaging and runtime checks remain open.

## Evidence collected

`shader-license-audit.json` records all 71 collected files, SHA-256 hashes, dependent presets, visible declarations, family references and open scope questions. Regenerate it with `node scripts/audit-shaders.mjs`. Curated findings and hash-bound comparisons are preserved in `shader-license-evidence.json`; regeneration rejects mismatched hashes. This report records evidence and review assessments, not a new license grant.

| Preset | Evidence found | Remaining question |
| --- | --- | --- |
| crt-easymode | EasyMode's shader explicitly declares GPL; unspecified version is addressed by GPL's version-choice clause | Preserve notice, comply under GPLv3; final preset/port scope check |
| crt-aperture | Same explicit GPL declaration and version-choice rule | Same preset/port scope check |
| crt-royale | GPL-2.0-or-later headers, MIT helpers, identical author-family LICENSE.TXT; all six masks match author pixels | Original mask provenance established; finish later wrappers/settings/preset contribution scope and notices |
| crt-hyllian | MIT-style grant in the core shader; Hyllian's history identifies LUT origins | Confirm support shader and LUT/preset license scope, including Glow-derived contributions |
| crtglow_gauss | Original Cg implementation and Slang port traced | Explicit license grant for original code, port, later contributions and preset is not yet evidenced |
| crtglow_lanczos | Same traced Glow family | Same open license-scope question; preserve both variants |
| crtsim | Author's whole implementation has CC0 dedication; original crtbase.fx has notice; both texture conversions match author pixels | Original code/texture grant evidenced; check new Slang-port and preset contributions |

Of the 20 source/header files flagged by the notice scanner, five have a CRT-Royale family-license reference, and original CRTSim base-code scope now has author-source evidence. Scanner counts are not counts of missing licenses. A file can be covered by a family grant without repeating a header; later contributions still require assessment.

## Author-source comparisons completed

- **CRTSim:** [author COPYING.txt](https://github.com/MinorKeyGames/CRTSim/blob/dbbe9d1bc2512288f5b1747e6be35ff44a7baae1/CRTSim/COPYING.txt) dedicates this implementation under CC0. The author [reference source](https://github.com/MinorKeyGames/CRTSim/blob/dbbe9d1bc2512288f5b1747e6be35ff44a7baae1/CRTSim/bin/crtbase.fx) contains the notice missing from the ported header. The author's `artifacts.bmp` (256×224) and `mask.bmp` (64×32) match the selected PNGs in dimensions and every decoded BGRA pixel. Preserve the original dedication in `licenses/upstream/CRTSim-COPYING.txt`.
- **CRT-Royale:** the [author's family directory](https://github.com/TroggleMonkey/common-shaders/tree/53329b74a3f9b5af1a48e56473fabd370bdbd8ca/crt/shaders/crt-royale) has the same GPL family license (Git blob `d8cf7d463e2a4f064a157fa994bb394d3623b9cc`). All six selected mask PNGs match decoded author pixels, despite differing PNG byte encodings. The author's preset references these masks. This supports their origin within the licensed family; it is not evidence for every later port edit.
- **EasyMode/aperture:** [GPLv2 section 9](https://github.com/spdx/license-list-data/blob/main/text/GPL-2.0-or-later.txt) permits a choice of FSF-published versions if none is specified. Our compliance choice is GPLv3; the original `License: GPL` notice remains unchanged. Missing version digits are not themselves a missing grant.

The machine-readable evidence records source commits, blob IDs, pinned-file hashes, pixel dimensions and pixel hashes. Local author-source copies and comparison results remain under the excluded `artifacts/license-evidence/` directory. `scripts/compare-license-textures.ps1` repeats the six Royale comparisons when those local source copies are available; it does not download them.

## Glow provenance traced

1. [Original bloom addition by Themaister, 2014](https://github.com/libretro/common-shaders/commit/c8c5f5ab3d0a5c47c3e246efa134c4467ec5d1d4).
2. [Move from bloom to crt/glow by Themaister](https://github.com/libretro/common-shaders/commit/a7c364862d7aa93dd5c8a66806900b36e41f5901).
3. [Cg tree refactor by Hyllian, 2015](https://github.com/libretro/common-shaders/commit/6c2ca06be42a3678a4b92f27b8a34ade92bf79bd).
4. [Slang translation by rz5, 2016](https://github.com/libretro/slang-shaders/commit/efd79721cb517b1affe366b3094115992e04dc50).
5. [Curvature/moire additions by hunterk, 2018](https://github.com/libretro/slang-shaders/commit/8f26d985e683c499c92327c629ca92cd1eeee9d3). The pinned resolve shader also credits Timothy Lottes.

The inspected original bloom and Slang introduction patches do not contain a grant resolving the whole family. Historical authorship helps target a question; it is not permission to relicense their work.

The maintainer's [existing license discussion](https://github.com/libretro/slang-shaders/issues/150#issuecomment-670993746) confirms that this collection uses mixed licenses, including files without a listed license. It supplies no blanket GPL/MIT grant for the whole collection. Comments about boilerplate or code size are not used as our redistribution clearance.

## Optional upstream clarification

A focused upstream request is preserved in [SHADER_LICENSE_REQUEST.md](SHADER_LICENSE_REQUEST.md) as an optional draft. It is not a release prerequisite and is not scheduled to be sent. No message has been posted. Explicit owner authorization would be needed before representing the project externally.

If clarification becomes useful, ask for existing references rather than demanding that maintainers retroactively establish every contribution's legal history. Keep the declarations as supplied; our GPL-3.0-only declaration does not replace them.
