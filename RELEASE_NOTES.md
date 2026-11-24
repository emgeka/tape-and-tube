# v0.1.0-beta.3 — Tape & Tube for Windows

Tape & Tube 0.1.0-beta.3 is a Windows x64 prerelease and an adaptation of [NTSCRT](https://github.com/finnmckenty/NTSCRT) by Finn McKenty.

- Digital camera formats, VCR, TV reception and Screen Loop join NTSC/VHS and CRT. Import NTSCRT looks, including animated looks.
- Source crop, preview zoom, first and last frame controls, and direct access to output settings from the save buttons.
- PNG, H.264 and HEVC MP4, and animated GIF. Supported HDR inputs are tone-mapped to SDR for editing and export.
- Effect labels, choices and tooltips are available in all ten interface languages.
- New CRT icon in the app and setup. The German setup overview is localized, and the optional FFmpeg download can be prepared and verified on the component page.

Windows 11 in VirtualBox was tested with a short MP4 export with audio and all effects disabled. Digitalvideo, VCR, TV reception, Screen Loop and imported NTSCRT looks use the shared renderer and require a graphics adapter that supports compute shaders. The virtual graphics adapter selected in the test VM could not initialize that renderer. A CRTsim export from the reference clip was verified on the physical Windows host.

## Previous release — v0.1.0-beta.2

**Tape & Tube is a Windows adaptation of [NTSCRT](https://github.com/finnmckenty/NTSCRT) by Finn McKenty.** NTSCRT supplies the original concept, signal chain and workflow. Our C#/WPF application and Rust host integration are independently implemented around ntsc-rs, librashader and RetroArch community shaders. This beta covers a subset of NTSCRT's features; it is not an official NTSCRT release.

## What's new since beta.1

- **One component page** for Visual C++ Runtime, legacy DirectX and optional FFmpeg, with clear Available / Missing / Ready for installation status.
- **Assisted DirectX setup:** download the pinned original Microsoft package, verify it, extract to a temporary folder and start DXSETUP automatically. No manual extraction path or executable search. You confirm Microsoft's license and administrator prompts; setup waits and checks the actual runtime again.
- **Optional FFmpeg installation:** download a checksum-pinned FFmpeg/ffprobe pair directly from Gyan Doshi / gyan.dev, use an existing installation, or skip video tools.
- **Choose between existing FFmpeg installations:** show version and folder for working pairs found in the app folder and PATH; add other folders manually. The selected absolute paths are saved, so a changed PATH order does not silently select another installation. External tools are used in place and are never copied or removed by our uninstaller.
- Reject mismatched ffmpeg/ffprobe versions and unresponsive tools. Explicit environment overrides remain available.

The core editor remains: images and video, playback/looping, original/output comparison, seven CRT presets, PNG and H.264 MP4 export with synchronized first original audio track, custom output sizes and ten UI languages. New effect families and Linux support are planning topics, not features of this release.

## Download and install

Download **TapeTube-0.1.0-beta.2-win-x64-setup.exe**. Windows x64 and a Direct3D 11-capable GPU are required. .NET is included; no development tools are needed. Setup installs per user and adds Start menu and uninstall entries. Visual C++ uses the official Microsoft download. DirectX assistance and the FFmpeg download need internet access; system PATH is not changed.

FFmpeg's reference download is Gyan Essentials 9.0.2 (GPL v3 per distributor), about 34 MiB. Downloaded tools retain their original license, build documentation and source reference under `licenses/ffmpeg`. FFmpeg and Microsoft installers are downloaded directly from their providers, not embedded in our installer or mirrored here. Images work without FFmpeg.

The attached **TapeTube-0.1.0-beta.2-source.zip** contains matching application/host and installer sources. **SHA256SUMS.txt** covers the installer and source ZIP. The installer includes that source ZIP, original notices, shader sources and upstream source references. This unsigned beta has no signing certificate.

## Validation and known limits

- All 199 existing functional checks passed. Installer download/existing/skip modes, actual selected-tool use, Unicode paths, saved selection on upgrade, missing-selection errors, video/audio export, preservation of external/user files and uninstallation were tested locally. Native tool probes reject version mismatches and time out unresponsive tools.
- The owner reports successful Windows 11 / VirtualBox installation of the latest candidate and an export without flicker or black frames. **Earlier CRTSim exports did exhibit those faults. They were not reproduced in the latest test; their root cause has not been established, and no permanent fix is claimed.**
- DirectX download, checksum rejection and automatic extraction were tested without changing system components on the development machine. The owner's overall VM test was successful, but separate results for every fresh-machine missing-runtime/UAC-cancel branch were not recorded. These results do not certify every Windows/GPU configuration.
- Preview is silent; exports include the first original audio track as AAC. SDR workflow; variable frame rates are normalized to the preview grid. Processing can be slower than real time.
- Original upstream licenses and notices are preserved. Shader-scope uncertainties and native transitive notice inventory gaps remain documented in `docs/SHADER_LICENSE_REVIEW.md` and `docs/DEPENDENCY_LICENSE_REVIEW.md`. No blanket relicensing or complete legal clearance is claimed.

## Updates and credits

This is a **Beta-channel prerelease**, not a stable Release-channel version. The app can notify you about updates; downloading and installation remain manual. Automatic checking can be disabled under Updates.

Own application and host code: **GPL-3.0-only**. Finn McKenty / NTSCRT, ntsc-rs, librashader, RetroArch shader authors, FFmpeg and other third-party components retain their own attribution and licenses. See [third-party credits](https://github.com/emgeka/tape-and-tube/blob/main/THIRD_PARTY.md).
