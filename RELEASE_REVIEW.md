# Release review — 0.1.0-beta.1

The owner approved a public source-and-installer beta under emgeka/tape-and-tube on 2026-11-12. Own application and host code remain GPL-3.0-only; external projects retain their licenses. This review records evidence and limitations, not a blanket legal clearance.

## Origin and scope

Tape & Tube is a Windows adaptation of NTSCRT by Finn McKenty. Concept, core signal chain and workflow originate there. Windows application/host code is newly implemented; Swift/Metal application code is not copied. ntsc-rs supplies signal processing, RetroArch authors supply CRT effects, librashader executes shaders and external FFmpeg supplies media processing.

All seven selected CRT presets are retained unchanged. The owner adopted upstream's pragmatic handling of incompletely documented shader scopes: preserve files, notices, attribution and source; disclose uncertainty without demanding retrospective grants for every headerless file. Concrete restrictions are not overridden. The optional maintainer request remains unsent.

## Validation evidence

- Existing functional suites: 182 checks. New update-channel suite: 17 checks; total 199.
- Previous installer candidate: local install/reinstall/uninstall verified 605 payload files, matching own source and preservation of user files. Owner's Windows 11 VirtualBox test confirmed installation, app/Start menu launch, test picture, video opening and Windows Settings uninstallation with VC/DirectX already installed.
- The channel-enabled release build must have its own package/installer validation report. Prior candidate results do not replace those checks.
- The missing-component flow on a fresh Windows VM is still unverified. A second physical Windows computer/GPU has not been certified.

## Known runtime limits

- VirtualBox-generated CRTSim exports contain black/flickering frames from around 16 / 17.56 seconds. The supplied original and saved preset exported all 744 frames correctly on the physical host. Root cause is not established. See docs/EXPORT_FLICKER_INVESTIGATION.md.
- The reference DLL requires Visual C++ v14 x64 and D3DX9_43.dll. Setup probes system DLL loadability and offers official Microsoft download links; the user runs those installers separately. A D3D11-only rebuild remains a possible future improvement.
- FFmpeg is external, not bundled. No silent system-component installation, signing certificate, automatic installer execution or HDR workflow is provided.
- Release/Beta update checks use public GitHub data anonymously. Download/installation are manual, and automatic checking can be disabled. See docs/UPDATE_CHANNELS.md.

## Licensing evidence and open inventory items

Original CRTSim CC0 scope and both texture conversions were evidenced. All six Royale masks match author pixels. Original exact-package Vortice/SharpGen notices, ntsc-rs-macros notices, WindowsDesktop notices and MIT C header are collected. librashader binary provenance matches the official release asset digest. Upstream shader sources/notices and the librashader tag source archive accompany the installer.

Complete native transitive notices and the hifijson standalone notice/provenance remain documented gaps; no copyright statements or grants are invented. See docs/DEPENDENCY_LICENSE_REVIEW.md, docs/SHADER_LICENSE_REVIEW.md and their machine-readable evidence. Product-name checks were preliminary, not comprehensive trademark clearance.

## Publication

Publish as v0.1.0-beta.1, a prerelease rather than latest stable. Attach the Windows installer, matching application source and checksums. Exclude personal paths/configuration, development tools, original user media and local diagnostics. Record the exact commit/tag, asset hashes and final public URLs in the local publication receipt. The GitHub authorization does not include posts or messages on X.
