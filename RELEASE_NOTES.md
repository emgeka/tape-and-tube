# v0.1.0-beta.5 — Tape & Tube for Windows

Tape & Tube is a Windows adaptation of [NTSCRT](https://github.com/finnmckenty/NTSCRT) by Finn McKenty. This beta adds side-by-side comparison, optional preview audio and more ways to recreate early television.

- **Side-by-side comparison.** View the full source beside the processed image or video. Save both views as one clip with the original audio track in MP4, MOV or WebM.
- **Preview audio and playback.** Enable audio while editing a video. Changing a look or effect during playback resumes from the current position after the preview refreshes.
- **Early television and film.** Try Baird 30-line, British 405-line and 16 mm kinescope looks. Expert mode lets you combine the historical display and film stages with other effects.
- **NTSCRT 0.29.2 effects engine.** The update fixes black or striped output with CRT Aperture on large inputs when internal downscaling is disabled. All 44 original looks and five Screen Loop presets remain available in the bundled preset folders.

The shared effects engine requires a graphics adapter with compute shader support. Video export and WebP use FFmpeg with the corresponding encoders.

Own application and host code are GPL-3.0-only. NTSCRT, ntsc-rs, librashader, the RetroArch shader community and other third-party projects retain their original licenses and credits.
