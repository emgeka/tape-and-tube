# Windows installer — local beta draft

The installer uses Inno Setup 6.7.4 and installs Tape & Tube for the current user, normally under `%LOCALAPPDATA%/Programs/TapeTube`. It adds a Start menu entry and a Windows uninstall entry. A desktop shortcut is optional. Setup is in German or English; the app retains its ten languages.

The native x64 prerequisite helper checks whether the actual system copies of `MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll` and `D3DX9_43.dll` load. It is statically linked and does not depend on .NET or the VC runtime being checked. A 64-bit helper is necessary because the setup process's architecture must not accidentally test the 32-bit system DLLs.

If a required runtime is missing, setup offers official Microsoft download links and a recheck button. The user runs the Microsoft installers separately, including their license/admin prompts; setup never silently installs system components. For DirectX, extract the Microsoft download and run DXSETUP.exe. Installation cannot proceed until the checks succeed, including in silent mode. This checks loadability, not GPU compatibility or all possible VC version mismatches.

Optional FFmpeg page: select an existing folder containing both tools to copy them into the installed app, or leave it empty to use PATH/add the files later. No FFmpeg build is downloaded or embedded in the installer. Selected external files are installed locally and tracked for removal with the app. Existing PATH installations are not changed.

All seven presets and original notices are included, plus the matching own-source companion ZIP under the installed `sources` directory. Native transitive license inventory gaps remain documented; building an installer does not resolve them. No signing certificate or public publication is implied.

## Build

Install the official Inno Setup 6.7.4 compiler in portable mode under `.tools/innosetup`. Official installer: https://github.com/jrsoftware/issrc/releases/download/is-6_7_4/innosetup-6.7.4.exe

Verified SHA-256: `a549929319f9120716677c685535ca4671e4b937b5fdc87e2585f32d8eb30e8c`.

Portable arguments: `/PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR="<project>/.tools/innosetup"`. Official portable-mode documentation: https://jrsoftware.org/ishelp/topic_technotes.htm

Run `./scripts/build-installer.ps1 -Offline` to build a fresh review package and installer. `-UseCurrentPackage` compiles the current package instead. Generated EXE, source companion, checksums and installer manifest are in the timestamped release directory. Native helper source and installer recipe are included in the source companion. The compiler and downloaded build tools are excluded.

## Validation

- Locally validate per-user installation into an isolated test directory, installed payload/notice hashes, and uninstallation. Do not target an existing real installation or delete user-created files.
- On a fresh VM: before installing Microsoft components, setup should identify them as missing, open the right official pages and proceed only after rechecking successfully. Test cancel and a restart of setup. Missing-component branches need this clean environment; the developer machine cannot prove them.
- Test without FFmpeg first (images), then choose an existing folder containing both tools (video). Check preservation of existing overrides/user media during upgrade/uninstall. The uninstaller removes installed files, not user settings under `%LOCALAPPDATA%/TapeTube`.
- Run graphics/export validation on physical hardware. VirtualBox-generated CRTSim exports have a documented black-frame issue; prerequisite checks do not fix or certify the virtual GPU.

The current installer is unsigned and remains a local draft. Verify logs and manifests for the actual checks run; a local install/uninstall test is not a fresh-machine certification.

## Owner VM result — 2026-11-11

The owner confirms successful installation, installed-app launch, Start menu launch, test picture, video opening and uninstallation through Windows Settings in Windows 11 / VirtualBox using the installer with SHA-256 `4b4372377c1cc7a339b3385d87809af7a04c2de5b1e3e83c8ac8e31f1f86ae09`. Visual C++ and the legacy DirectX runtimes had already been installed during earlier testing. Missing-component handling remains untested. These checks do not resolve the previously confirmed VirtualBox CRTSim export issue or certify post-install video export.
