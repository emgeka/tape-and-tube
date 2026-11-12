# Upstream license clarification — draft, not sent

**Optional reference draft only.** Following the owner's decision on 2026-11-11, unanswered shader-scope questions are documented accepted uncertainties, not automatic release blockers. This draft is not scheduled for sending and no reply is required before proceeding with packaging.

Proposed destination: a focused follow-up in https://github.com/libretro/slang-shaders/issues/150, subject to approval. The public handles in the history are attribution/provenance, not a claim that one person can license every contribution.

## Draft text

Hi, we are preparing Tape & Tube, a Windows adaptation of Finn McKenty's NTSCRT workflow. Our own Windows frontend and host integration are newly implemented and GPL-3.0-only. We would like to keep all seven original CRT presets while preserving their upstream licenses and attribution.

Our reference snapshot is `cb01f2f238700eed7ec1c859be1737a9fbcdcb7c`. We have not publicly distributed our Windows binary bundle. Could you point us to authoritative licensing information for these specific scopes?

1. **Glow** (`crtglow_gauss.slangp`, `crtglow_lanczos.slangp`, and the nine files in `crt/shaders/glow/`): we traced the Cg predecessor through Themaister's bloom addition, the 2015 tree refactor, rz5's 2016 Slang port (`efd79721cb517b1affe366b3094115992e04dc50`), and later hunterk contributions. The current resolve shader also credits Timothy Lottes. We could not locate an explicit grant covering the original implementation, the port, later contributions and presets. Is there an existing license notice we should preserve, or which rights holders should confirm that scope?
2. **Hyllian support files and LUTs**: does the MIT-style grant in `crt-hyllian-base.slang` also cover `support/multiLUT-linear-fast.slang`, `support/glow/{threshold,blur_horiz,blur-glow-mask-geom}.slang`, the two Sony Wega LUT PNGs referenced by `crt-hyllian.slangp`, and that preset? If some support code derives from Glow or other authors, which notices apply?
3. **CRT-Royale port additions**: we found TroggleMonkey's author family directory, an identical GPL family license and pixel-identical originals for all six selected masks. Do the same GPL terms cover the later Slang wrappers/settings and preset contributions, with MIT helper notices preserved? Please point us to the applicable scope statement if separate.
4. **CRTsim port additions**: MinorKeyGames/CRTSim has a whole-implementation CC0 dedication and an original `crtbase.fx` notice. Both selected PNGs match the author's BMPs pixel-for-pixel. We therefore have evidence for the original implementation/assets. Are the new Slang-port and `crtsim.slangp` contributions also under CC0, as the port shader notices suggest?
5. **Shared/preset details**: what notice applies to `include/compat_macros.inc`, and do the EasyMode/aperture preset and port contributions follow their shader GPL grants? We retain the original unspecified-version GPL notices and use GPLv3 under the GPL version-choice clause; we are not requesting a new version grant on that basis.

We are looking for existing grants or explicit confirmation from the relevant rights holders, not asking maintainers to relicense contributions they do not own. A short file/family mapping or source reference would help us preserve the complete preset set and provide accurate notices and source alongside our app.

Thank you for the shader collection and for helping clarify attribution and redistribution.
