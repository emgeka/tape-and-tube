# Dependency licensing review — 2026-11-11

Status: **native dependency packaging checks remain open**. This review covers the current local package and pinned sources. All seven CRT presets remain selected. The owner accepts documented shader licensing uncertainties following upstream practice; these are not automatic release blockers. See [shader findings and policy](SHADER_LICENSE_REVIEW.md).

| Component | Evidence / packaging action | Remaining work |
| --- | --- | --- |
| Own frontend and Rust host | GPL-3.0-only in root LICENSE, manifests and source; own source companion prepared | Publish matching complete source with binaries and final repository URL |
| NTSCRT origin | App concept/workflow attribution to Finn McKenty retained; no Swift/Metal app code bundled | Keep prominent origin credit in release |
| ntsc-rs and macros | MIT OR ISC OR Apache-2.0, pinned source and root notices present | Collector now also copies root notices for macro crate; verify final source/notice set |
| Other host Rust crates | 28 package metadata entries in previous inventory, mostly permissive; Unicode AND clause retained | `hifijson` 0.5.0 declares MIT but ships no standalone notice. Recorded VCS commit could not be found upstream on review date; do not invent a copyright notice. Locate authoritative version notice / include appropriate attribution after verification |
| Vortice.Windows 3.8.3 | MIT text retrieved at exact NuGet repository commit `9e609cb9439c9872aa1b339f177e40ec96f77239` | Supplementary original license now copied for Direct3D11, DirectX and DXGI; final binary check |
| Vortice.Mathematics 2.1.0 | MIT text retrieved at exact package commit `fa05ec6dcba48f3f7331791da6dc7f3d866b2ad6` | Supplementary original license now copied |
| SharpGen.Runtime / COM 2.4.2-beta | MIT text retrieved at package commit `6990bcafe124a4c22515ad19cee5a081da8db67b` | Supplementary original license now copied; retain nuspec attribution as well as original license |
| .NET 10.0.14 self-contained runtimes | Runtime license texts and .NET third-party notices collected; WindowsDesktop WPF notices retrieved from the exact NuGet repository commit `0427b9abbead3fa64c6655eb4290c537397131fa` | Package validation checks inclusion of these texts; a clean-machine runtime test remains separate |
| Rust runtime | Toolchain notices and license directory collected | Match runtime portions actually linked into host DLL |
| librashader 0.12.0 | Official Windows ZIP digest verified; extracted and packaged DLL hashes match. Tag resolves to `87e8a97b50516d997defeaa168173dcd185d4022`. Original MPL/GPL texts, MIT C header and tag source archive included | Upstream multi-runtime binary has no complete transitive notice bundle. Provenance is now established; complete transitive notices/source inventory remains open. See `librashader-release-evidence.json` |
| Microsoft VC v14 / D3DX9_43 | Detected imports; these system DLLs are not copied into the package | Use official prerequisites or assess redistribution terms before installer bundling. D3D11-only rebuild could remove legacy dependency; not done in this audit |
| FFmpeg | External user installation, not bundled | If bundled later, audit that exact build, enabled codecs/libraries, notices and source obligations first |

## Evidence and concrete corrections

The previous notice inventory had empty notice lists for six NuGet packages and ntsc-rs-macros. NuGet metadata's `MIT` label is useful evidence but does not replace the complete MIT copyright and permission notice. Exact-version upstream texts are now vendored in `licenses/upstream/`, with source URLs and Git blob IDs in `sources.json`. The collector includes them automatically. This does not alter any upstream license or source file.

The collector inventory includes build-time/procedural-macro crates as a conservative superset. It is not a claim that each such crate is linked into the executable. Similarly, librashader's workspace lockfile includes components not necessarily shipped in the selected DLL; a feature-aware native build inventory is still needed.

## Follow-through

1. Package the shader inventory, available notices and source with transparent scope notes. Upstream clarification is optional; the unsent [request](SHADER_LICENSE_REQUEST.md) is not a release prerequisite.
2. Establish librashader DLL build provenance/features and collect its native dependency notices/source. Prefer a controlled D3D11-only build if the existing release cannot provide this evidence.
3. Resolve hifijson notice/provenance and finish the runtime file-level check.
4. Generate a fresh review package from these corrected collectors; verify notice files and matching source before approving public binaries or an installer.

No maintainer was contacted and no repository/release was published by this review. Fresh review packages include the corrected collection, readable license index, original package metadata for the hifijson notice gap, and explicit librashader provenance/remaining-uncertainty records. The running app is not replaced by packaging. The package-validation report records actual verification results; it does not claim a clean-machine test or complete native transitive inventory.
