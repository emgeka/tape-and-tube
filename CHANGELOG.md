# Changelog

## 0.1.0-beta.3 — Windows test candidate

- Added digital video, VCR, TV reception, Screen Loop, and import of animated NTSCRT looks.
- Added source crop, preview zoom, first and last frame controls, and a direct shortcut to the output settings.
- Added HEVC MP4 and animated GIF export, still-image animation, and HDR-to-SDR conversion for supported inputs.
- Localized the built-in effect controls, choices and tooltips in ten languages.
- Localized the German setup overview. When selected, FFmpeg is prepared on the component page before installation. The CRT icon appears in the Windows app and Setup.

## 0.1.0-beta.2 — 2026-11-16

- Existing FFmpeg installations are listed by version and folder. Setup checks matching, responsive ffmpeg/ffprobe pairs with a bounded native helper and saves the selected absolute paths. External tools are used in place and never copied or uninstalled by Tape & Tube.
- One component page combines C++/DirectX status and actions with optional FFmpeg installation or existing-tool selection. Detected app/PATH tools are reused by default; missing required components block Next.
- Setup's Install DirectX action downloads the pinned original Microsoft package, automatically extracts it and launches interactive elevated DXSETUP; the actual runtime is rechecked afterwards. No manual extraction path or executable search is needed.
- Setup can optionally download and install a checksum-pinned FFmpeg/ffprobe build directly from its upstream distributor, retaining the original license, build information and documentation. Existing tools and skipping remain supported. No system PATH changes.
- Effect expansion and a Linux edition are under evaluation; neither is implemented by this installer change.

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
