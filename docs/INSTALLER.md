# Windows installer — local beta draft

The installer uses Inno Setup 6.7.4 and installs Tape & Tube for the current user, normally under `%LOCALAPPDATA%/Programs/TapeTube`. It adds a Start menu entry and a Windows uninstall entry. A desktop shortcut is optional. Setup is in German or English; the app retains its ten languages.

The native x64 prerequisite helper checks whether the actual system copies of `MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll` and `D3DX9_43.dll` load. It is statically linked and does not depend on .NET or the VC runtime being checked. A 64-bit helper is necessary because the setup process's architecture must not accidentally test the 32-bit system DLLs.

If a required runtime is missing, setup offers the official Visual C++ download, an **Install DirectX / DirectX installieren** action and a recheck button. DirectX is downloaded directly from Microsoft with a pinned SHA-256, extracted automatically into setup's temporary directory and started as interactive elevated DXSETUP. The user handles Microsoft's license and administrator prompts; setup waits for the installer to close and then rechecks the actual runtime. No manual folder entry or executable search is needed. Setup never silently installs system components. If the runtime is already loadable, the DirectX action does not reinstall it. In silent mode there is no automatic DirectX download, installation or elevation; prerequisites must be provisioned first. Installation cannot proceed until the checks succeed. This checks loadability, not GPU compatibility or all possible VC version mismatches. See [DirectX download details](DIRECTX_INSTALLATION.md).

The common component page (development beta.2) combines C++, DirectX and optional FFmpeg; there is no separate FFmpeg page. Missing required components disable Next; available C++/DirectX actions are disabled. FFmpeg candidates from the destination app/PATH and user-added folders are shown by version/path. Matching versions and bounded startup are checked by a native helper. Existing tools are referenced in place; an app-local Unicode selection file makes the choice independent of later PATH order. Explicit environment overrides retain priority. For FFmpeg: choose the checksum-pinned upstream download, a listed existing installation (or add a folder containing both tools), or skip. Existing app/PATH tools are reused by default; otherwise download is the interactive default. The download button can prepare verified files immediately; copying occurs during installation. unattended setup defaults to skip unless `/MEDIA=download` is specified. `/MEDIA=existing /MEDIAFOLDER="<folder>"` selects existing tools without copying them; `/MEDIA=skip` does not add them. All modes preserve system PATH and external installations. The downloaded tools and original notices are installed only inside the app and tracked by the uninstaller. See [FFmpeg provenance and failure handling](FFMPEG_INSTALLATION.md). Published beta.1 only supports selecting existing tools.

In the revised beta.3 installer, selecting the FFmpeg download and clicking **Next** prepares the verified tools on the component page. The optional **Prepare FFmpeg** button starts the same step immediately. Installation copies the prepared files into Tape & Tube's folder. The German wizard displays a German overview and introduces the English GPL original on the agreement page.

All seven presets and original notices are included, plus the matching own-source companion ZIP under the installed `sources` directory. Native transitive license inventory gaps remain documented; building an installer does not resolve them. No signing certificate or public publication is implied.

## Build

Install the official Inno Setup 6.7.4 compiler in portable mode under `.tools/innosetup`. Official installer: https://github.com/jrsoftware/issrc/releases/download/is-6_7_4/innosetup-6.7.4.exe

Verified SHA-256: `a549929319f9120716677c685535ca4671e4b937b5fdc87e2585f32d8eb30e8c`.

Portable arguments: `/PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR="<project>/.tools/innosetup"`. Official portable-mode documentation: https://jrsoftware.org/ishelp/topic_technotes.htm

Run `./scripts/build-installer.ps1 -Offline` to build a fresh review package and installer. `-UseCurrentPackage` compiles the current package instead. Generated EXE, source companion, checksums and installer manifest are in the timestamped release directory. Native helper source and installer recipe are included in the source companion. The compiler and downloaded build tools are excluded.

## Validation

- Locally validate per-user installation into an isolated test directory, installed payload/notice hashes, and uninstallation. Do not target an existing real installation or delete user-created files.
- On a fresh VM: before installing Microsoft components, setup should identify them as missing, open the right official pages and proceed only after rechecking successfully. Test cancel and a restart of setup. Missing-component branches need this clean environment; the developer machine cannot prove them.
- DirectX flow: verify that clicking Install DirectX needs no extraction-path entry, opens Microsoft DXSETUP, waits for it and changes the status to Available only after a successful native check. Cancel the UAC prompt and Microsoft wizard in separate runs; setup must remain usable and keep blocking Next while the runtime is missing. Do not remove or rename system DLLs to simulate a clean VM.
- Test without FFmpeg first (images), then choose an existing folder containing both tools (video). Check preservation of existing overrides/user media during upgrade/uninstall. The uninstaller removes installed files, not user settings under `%LOCALAPPDATA%/TapeTube`.
- Run graphics/export validation on physical hardware. VirtualBox-generated CRTSim exports have a documented black-frame issue; prerequisite checks do not fix or certify the virtual GPU.

The current installer is unsigned and remains a local draft. Verify logs and manifests for the actual checks run; a local install/uninstall test is not a fresh-machine certification.

## Owner VM result — 2026-11-11

The owner confirms successful installation, installed-app launch, Start menu launch, test picture, video opening and uninstallation through Windows Settings in Windows 11 / VirtualBox using the installer with SHA-256 `4b4372377c1cc7a339b3385d87809af7a04c2de5b1e3e83c8ac8e31f1f86ae09`. Visual C++ and the legacy DirectX runtimes had already been installed during earlier testing. Missing-component handling remains untested. These checks do not resolve the previously confirmed VirtualBox CRTSim export issue or certify post-install video export.

## Owner VM result — 2026-11-16

The owner explicitly confirmed that beta.2's FFmpeg installation worked well and completed a video export. CRTSim flickered only in the exported VM video; preview remained normal. Treat this as the existing VirtualBox limitation for now, with root cause still unconfirmed. Its investigation is deferred by the owner, not resolved or removed from the release notes. The owner also confirmed Visual C++ installation and reported the manual DirectX extraction/DXSETUP friction. The new assisted DirectX flow requires another VM check; the earlier result does not validate this change.
