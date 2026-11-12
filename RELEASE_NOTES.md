# v0.1.0-beta.1 — Tape & Tube for Windows

**Tape & Tube is a Windows adaptation of [NTSCRT](https://github.com/finnmckenty/NTSCRT) by Finn McKenty.** NTSCRT is the original macOS application and the starting point for the concept, signal chain and workflow. This release reimplements its core NTSC/VHS → CRT workflow for Windows using ntsc-rs and RetroArch community shaders through librashader.

The C#/WPF interface and Rust host integration are newly written. This preview covers a subset of the original project’s features; NTSCRT’s Swift/Metal application code is not included.

This first beta includes image/video playback, looping, a draggable original/output comparison, PNG export, H.264 MP4 export with synchronized original audio, custom output sizes and ten UI languages. All seven CRT presets are retained. The Windows UI uses a dark layout with a large preview and visible save controls.

## Download and install

Download **TapeTube-0.1.0-beta.1-win-x64-setup.exe**, run setup and start Tape & Tube from the Start menu. Windows x64 and a Direct3D 11-capable GPU are required. .NET is included. Setup checks Visual C++ v14 x64 and legacy DirectX (`D3DX9_43.dll`) and opens official Microsoft downloads for missing components. You install those components separately and click **Check again**. Setup is available in German and English; the app supports ten languages.

For video, provide **ffmpeg.exe** and **ffprobe.exe** via an existing PATH installation, select their folder during setup, or add them under `runtime/media`. FFmpeg is not bundled or automatically downloaded. Images work without it.

The matching own-source ZIP and **SHA256SUMS.txt** are attached. The installer also includes that source ZIP, original notices and source references. To build, follow README.md. This unsigned installer may trigger Windows reputation prompts; this beta does not have a signing certificate.

## Limitations

- **VirtualBox:** CRTSim exports produced black/flickering frames in the tested VM. The same original and saved preset exported all 744 frames correctly on the physical host. Treat virtual-GPU export compatibility as unverified; changing artifact animation did not resolve the failure.
- **Fresh prerequisite test pending:** installation, Start menu launch, test picture, video opening and uninstallation succeeded in Windows 11 / VirtualBox with Microsoft runtimes already present. The missing-component workflow still needs a fresh-machine test.
- Preview is silent; exports include the first original audio track as AAC. SDR workflow, variable frame rates normalized to the preview grid, and slower-than-real-time processing are possible. Separate Release/Beta update channels can notify you at startup; downloading and installation remain manual. Automatic checking can be disabled under Updates.
- Upstream shader-scope uncertainties and incomplete native transitive notice inventory are documented in `docs/SHADER_LICENSE_REVIEW.md` and `docs/DEPENDENCY_LICENSE_REVIEW.md`, also shipped with the app. Existing upstream licenses and notices are preserved; no blanket relicensing is claimed.

## Validation

The test suites contain 199 checks, including 17 checks for update-channel selection and preference persistence. Local install/reinstall/uninstall verification and candidate VM validation are recorded separately; the new channel-enabled build still needs the fresh-VM missing-prerequisite test. Release-package validation reports record checks for the actual attached build. These results do not establish compatibility with every Windows computer.

## Credits and license

Our application and host code: GPL-3.0-only. ntsc-rs, librashader, RetroArch shaders, FFmpeg and other third-party components retain their own licenses. Original application concept and workflow: Finn McKenty / NTSCRT. Tape & Tube is a separately maintained Windows adaptation.
