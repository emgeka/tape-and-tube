# Optional FFmpeg installation

Development build 0.1.0-beta.2 adds an installer choice: download FFmpeg for this app, use existing tools, or skip. The published beta.1 is unchanged.

## Provenance

- Project: [FFmpeg](https://ffmpeg.org/), [Windows download providers](https://ffmpeg.org/download.html#build-windows).
- Distributor: [Gyan Doshi / gyan.dev](https://www.gyan.dev/ffmpeg/builds/), a provider linked by FFmpeg.
- Exact asset: [9.0.2 Essentials 7z](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2), 35,430,500 bytes (about 34 MiB). Provider SHA-256: `4705843ccaaf54257c16ad90f3e952ece33c17df964ecf7bfdbb0f49c7171077`.
- Original archive README declares GPL v3 and references [FFmpeg commit 946fcce07b](https://github.com/FFmpeg/FFmpeg/commit/946fcce07b). Its full build configuration and external library list are retained; this is not an LGPL-only or nonfree build. The original LICENSE is retained without relabeling.
- Machine-readable pin: `installer/ffmpeg-download.json`. Build generates the installer constants from it and validates the version, provider URL and digest format.

The installer obtains the unmodified archive directly from the distributor over HTTPS. It does not contain or mirror FFmpeg binaries. No user media, settings or credentials are uploaded. The distributor/CDN receives the ordinary download request. The application runs ffmpeg and ffprobe as separate processes; no FFmpeg code is linked into the WPF application or Rust host.

Setup preserves `LICENSE`, `README.txt`, the upstream documentation tree and the download manifest under `licenses/ffmpeg`. The two tools go in `runtime/media`. The README identifies build options, external components and the FFmpeg source revision. It is **not** a complete corresponding-source archive for all linked libraries. Before embedding or hosting the binaries ourselves, collect exact corresponding sources/build inputs and notices for the full build; a source hyperlink alone must not be treated as proof that this obligation is satisfied. See [FFmpeg's license guidance](https://ffmpeg.org/legal.html). The direct-download path does not claim a broader licensing clearance.

## User choice and failure behavior

The common component page shows FFmpeg alongside C++ and DirectX, with its distributor, GPL label and approximate download size. It lists matching installations from the destination app and PATH by version and folder, plus folders added by the user. A native helper runs each tool with -version, requires equal version tokens, bounds output and terminates unresponsive processes after three seconds per tool. This checks tool startup and pairing, not every codec. Saved selection and valid paired environment overrides are preferred. The download button prepares the verified package now; file installation happens with the app. Users can select their own pair or skip. Silent mode skips by default; `/MEDIA=download` explicitly opts in. `/MEDIA=existing /MEDIAFOLDER="<folder>"` selects existing tools. Existing tools are used in place, never copied. The selected absolute paths are stored as UTF-16 in runtime/media-selection.ini. The app uses this selection ahead of older JSON/component paths and PATH. Explicit TAPETUBE_/NTSCRT_ environment overrides still take priority; they are intended developer/user overrides. If a selected executable disappears, the app reports the missing selection instead of silently using another PATH installation. No PATH or machine-wide installation is modified.

Download and extraction happen before application files are installed. Inno Setup checks the pinned SHA-256 before extraction. Network, cancellation, mismatch and missing expected files fail preparation with a retry/back/skip explanation; unverified tools are not executed or installed. Files are extracted into setup's temporary directory and deleted by setup's normal cleanup. The fixed version must be deliberately updated and rechecked when fixing upstream security bugs; setup never substitutes a moving latest build after a hash failure.

The installer owns media-selection.ini and may replace it when an installation is chosen; custom configuration should use the documented overrides instead. Uninstall removes this app-local selection file, never the external executables. An upgrade that skips FFmpeg leaves an earlier selection and installed copy available; skip does not mean remove. Uninstall removes tools installed by setup, preserving external tool directories and user-created files. Other existing tool builds remain the user's selection, with their own license terms; their separate notices are not inferred from the pinned build.

## Validation

Run `scripts/test-installer.ps1 -MediaMode download` for the actual upstream download, extracted notices, video decode/export, repeat install and uninstall. Use `-MediaMode skip` and `-MediaMode existing -MediaFolder <folder>` for the other branches. The script refuses to run while a real installation owns the product's uninstall registration. Fresh-machine Microsoft prerequisites and VirtualBox graphics remain separate checks. Installer language/layout, cancellation and failure paths require targeted checks; report only the cases actually exercised.
