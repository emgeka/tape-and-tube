# Changelog

## 0.1.0-beta.1 — Windows beta

First Windows adaptation of [NTSCRT](https://github.com/finnmckenty/NTSCRT) by Finn McKenty, reimplementing the original project’s core workflow with a new WPF interface and Rust host integration.

- NTSC/VHS processing through ntsc-rs and CRT rendering through librashader with seven upstream presets.
- Dark editor UI, large preview and separate effects/output settings.
- Still-image and video loading, frame seeking/stepping, playback and loop control.
- Draggable original/processed comparison for images and video.
- PNG and H.264 MP4 exports, synchronized first original audio track, progress and cancellation.
- Automatic, 720p, 1080p, 4K and custom output sizes; fit or centered crop.
- Effect presets and configurable third-party paths/preset catalogs.
- Ten UI languages with automatic detection and persistent choice.
- Components & credits with explicit upstream responsibilities, sources and licenses.
- Windows x64 installer, per-user installation, Start menu integration and uninstaller.
- Native x64 prerequisite checks with official Microsoft runtime download links; optional selection of existing FFmpeg tools.
- Matching application source, original licenses, shader inventory and documented upstream uncertainties shipped with the installer.
- Separate Release/Beta update channels with persisted choice, optional anonymous startup checks and a manual download/installation flow.

Known beta limits: missing-runtime handling on a fresh VM is still unverified; VirtualBox CRTSim exports may contain black/flickering frames. The same reported original/preset exported correctly on physical hardware. Native transitive notice inventory gaps remain documented. The owner approved publication of source and installer as a beta on 2026-11-12; this is not a claim of complete platform or licensing verification.
