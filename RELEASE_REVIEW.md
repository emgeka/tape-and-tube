# Release review — 0.1.0-beta.5 test candidate

Local Windows installer for testing. Candidate installation and public release approval are pending.

## Scope and validation

The development builds verified preview audio and playback during effect changes, side-by-side video export, and the pinned NTSCRT 0.29.2 effects engine. The candidate package has its own checksums and validation results. The shared effects engine requires compute-shader support.

Windows setup remains per-user. Existing FFmpeg installations are referenced in place. Microsoft runtimes and optional FFmpeg are supplied by their respective providers. The application is unsigned.

## Origin and licenses

Tape & Tube is a Windows adaptation of NTSCRT by Finn McKenty. Own application and host code are GPL-3.0-only. Original third-party notices, all seven CRT presets, and the selected NTSCRT source and preset files retain their attribution and licenses. The matching source package includes the pinned build sources and patches used for NTSCRT.

The owner’s adopted shader review approach preserves upstream files and notices and records known scope uncertainties. Native transitive notice gaps remain documented. See `docs/DEPENDENCY_LICENSE_REVIEW.md`, `docs/SHADER_LICENSE_REVIEW.md` and the packaged evidence.
