# Assisted DirectX installation

Tape & Tube needs the legacy x64 `D3DX9_43.dll` through its current librashader build. The prerequisite probe checks the actual system library's loadability; it does not rely on the Windows DirectX version number.

The **Install DirectX / DirectX installieren** button on setup's component page now performs these steps only when that component is missing:

1. Download the [original Microsoft June 2010 redistributable](https://www.microsoft.com/download/details.aspx?id=8109), version 9.29.1974.1, directly from `download.microsoft.com` (100,275,120 bytes, approximately 96 MB).
2. Verify SHA-256 `053f76dcbb28802e23341b6a787e3b0791c0fa5c8d4d011b1044172dbf89c73b` before executing the extractor. The reviewed file has a valid Microsoft Corporation Authenticode signature.
3. Run the redistributable with `/Q /T:"<setup temporary directory>\directx-runtime"`. This extracts files without asking for a path; it does not install the runtime. The extraction command was tested against this exact package, producing 157 files.
4. Check expected setup files and the x64 D3DX CAB. Verify the extracted `DXSETUP.exe` SHA-256 `8f47d7121ef6532ad9ad9901e44e237f5c30448b752028c58a9d19521414e40d`. The reviewed executable also has a valid Microsoft signature.
5. Launch DXSETUP using the Windows `runas` verb and wait for it to close. No `/silent` switch is passed: the user responds to UAC and Microsoft's license/wizard. Tape & Tube does not accept Microsoft's license on the user's behalf.
6. Run the native component probe again. Missing components still block installation, regardless of whether the Microsoft dialog closed normally. A canceled elevation request, failed extraction/download or still-missing runtime gets an actionable message. Setup does not restart Windows itself.

The manifest is `installer/directx-download.json`; build generates constants from it. Download and extracted files live in the setup temporary directory, with normal setup cleanup after the child process exits. The redistributable is neither embedded in our installer nor mirrored in our release; Microsoft components retain their own terms. Download requests send ordinary network connection data to Microsoft, not user media or app settings. There is no new system-component action in silent setup.

The extraction/preparation helper is separated in `installer/DirectX.iss`. `scripts/test-directx-preparation.ps1` compiles a small harness that includes this exact helper and exercises the real download/extraction code without launching DXSETUP or changing system components. `-RejectHash` checks that a deliberately wrong digest prevents extraction. The harness has no app payload or uninstall registration. Keep missing-prerequisite success/cancel tests on a VM rather than deleting system DLLs on the development machine. Download verification does not by itself prove that the runtime installed successfully.

Microsoft's [deployment guidance](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/directx-setup-for-game-developers) explains the role of DXSETUP. The new flow remains an installer change; it does not fix the documented VirtualBox CRTSim export issue, whose investigation is currently deferred.
