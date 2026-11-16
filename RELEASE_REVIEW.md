# Release review — 0.1.0-beta.2

The owner authorized publication on 2026-11-16 after reporting a successful Windows 11 VM installer test and export without flicker or black frames. Own application/host code remains GPL-3.0-only; third parties retain their licenses. This is a prerelease, not a blanket platform or licensing clearance.

## Origin and scope

Tape & Tube is a Windows adaptation of NTSCRT by Finn McKenty. The concept, signal chain and workflow originate there. The WPF application and Rust host boundary are newly implemented; original Swift/Metal application code is not included. ntsc-rs supplies signal processing, RetroArch authors supply CRT shaders, librashader executes them, and FFmpeg handles media.

All seven selected CRT presets are retained. Follow the owner's adopted upstream-oriented license policy: preserve original files, source, notices and attribution; disclose uncertainty without inventing retrospective grants. Concrete restrictions still apply.

## Beta.2 changes

One installer component page, assisted pinned Microsoft DirectX download/extraction with interactive DXSETUP/recheck, optional pinned FFmpeg download, version/path-based selection of existing matched tools, persistent app-local selection and clearer status wording. External FFmpeg files are referenced in place and never removed by our uninstaller. No system PATH change, silent Microsoft license acceptance, Linux build or new effect family is included.

## Validation evidence

- All 199 existing functional checks passed for the tested application payload.
- Local installer tests verified 605 package files, source companion, install/reinstall/uninstall, user-file preservation, download/existing/skip modes, Unicode selection paths, use of the chosen external tools, retained choice on upgrade and a missing-selection error instead of PATH fallback. Video decode/export checks include synchronized original audio.
- Native pair probing rejects different versions and limits unresponsive processes. DirectX download/extraction and checksum rejection were exercised without running the system installer on the developer host.
- The owner reports the latest installer works in the VM and the latest export had no flicker/black frames. Prior CRTSim failures remain historical evidence: latest success is a non-reproduction, not a proven permanent repair.
- Release documentation/approval metadata are updated for publication. The release build must retain the tested application and installer logic and have its own final hashes/source checks. A successful overall VM test does not establish separately recorded coverage of every missing-component, UAC-cancel or restart branch, or every GPU.

## Remaining limits

Preview is silent, SDR only, VFR normalized to the preview grid; processing may be slow. The reference DLL still requires Visual C++ v14 x64 and D3DX9_43.dll. Existing explicit environment overrides take priority over setup's saved media selection. Update checks are anonymous; download/install remain manual.

Original CRTSim CC0 and texture evidence, Royale mask comparisons, exact-package Vortice/SharpGen/ntsc-rs-macros/WindowsDesktop notices and official librashader provenance remain included. Complete native transitive notices and the hifijson standalone notice/provenance remain documented gaps. See docs/DEPENDENCY_LICENSE_REVIEW.md, docs/SHADER_LICENSE_REVIEW.md and machine-readable evidence. Name checks were preliminary, not comprehensive trademark clearance.

Publish v0.1.0-beta.2 as prerelease=true and make_latest=false with installer, matching own source and checksums. Record exact commit/tag, server asset digests and public URLs. GitHub publication authorization does not include X messages or posts.
