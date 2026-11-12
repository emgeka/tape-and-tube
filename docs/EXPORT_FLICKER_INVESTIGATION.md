# Export flicker investigation — CRTSim

Reported during Windows 11 VirtualBox testing on 2026-11-11.

## Observed by the tester

- Preset: `crtsim`.
- Reported dimensions: 960 × 1707 (whether input or chosen output still to establish).
- Exported video starts flickering at approximately 16 seconds; original video does not.
- Audio appears correct. Preview and export computation are slow in the VM.
- Test picture works after Visual C++ x64 and June 2010 DirectX runtime installation.
- Tested review package: `20261111-095644-543`, version 0.1.0-beta.1.
- The exact flickering export also flickers on the physical host, ruling out an issue confined to VM playback.
- After disabling `Animate NTSC Artifacts`, the tester reports that flicker is replaced by black output from the affected point. Preview appeared normal. This is a second failure symptom, not a successful workaround. The new export must be inspected directly to establish onset and image data.

## Local diagnostic added

`--crt-temporal-diagnostic` records CRTSim parameters, decoded image means and PNG samples at 960 × 1707, with NTSC both on/off and artifact animation both on/off. Initial frames and later frame-index samples are nonblack on the development host. Jumping to frame 480 alone does not reproduce elapsed history and is not treated as a 16-second playback test.

The diagnostic also performs a continuous synthetic 20-second, 24 fps export with default NTSC, CRTSim, animation disabled and requested 960 × 1707 output (MP4 padded to 960 × 1708). Its decoded sample report is written separately as `artifacts/crt-temporal-diagnostic/export-report.json`. This fixture is distinct from the tester's original material; a passing result would not clear the reported defect.

## Supplied-file findings and physical-host reproduction

The owner supplied the original, both VM exports and a saved preset. The preset has NTSC and CRT enabled, `crtsim`, retro width 320, scale 3, automatic output geometry, `NTSCLerp=1`, and `animate_artifacts=0`. The original is a rotated 1920 × 1080 H.264 recording, 744 frames, approximately 24.797 seconds. Both exports are 960 × 1708; the calculated 1707-pixel height is padded as documented.

- First VM export: 141 fully black decoded frames, starting at frame 481 (16.031617 s), in 105 separate ranges. The first range lasts roughly 1.2 seconds; later ranges mostly alternate with visible frames.
- Second VM export: 217 consecutive fully black decoded frames from frame 527 (17.564786 s) to the end. Thus disabling artifact animation did not establish the cause or fix the failure.
- Black means the decoded limited-range luma is uniformly 16, not just a dark preview. These images are present in the encoded VM files.
- Physical host: a new export of the supplied original using the supplied preset completed all 744 frames. Pre-encoder sampled statistics show no all-black frames. The encoded output is decoded independently as an additional check; results are recorded in `artifacts/export-investigation/host-comparison.json`.
- The owner also reports correct behaviour on the physical host. The same failure has not been reproduced there. This supports a VM-dependent processing problem, potentially virtual GPU/driver behaviour during export; the precise cause is not established.
- The 23 existing export regression checks pass after adding diagnostic observation. No upstream shader, default or rendering algorithm has been changed as a supposed fix.

The source clips, exports, frame images and diagnostic data remain excluded local artifacts. Do not include the owner's media in source/release archives.

## Implementation inspection

- Export passes complete BGRA frames through a sequential awaited write. Decoder uses exact-size reads. No time-based frame dropping is used in export.
- MP4 encoding pads an odd requested height to the next even height (1707 to 1708). That alone does not explain a delayed flicker onset.
- Upstream CRTSim `composite.slang` alternates its artifact phase each frame when `animate_artifacts` is enabled (default 1), and reads `PassFeedback0` for persistence. This is a candidate mechanism for content-dependent flicker, not an established cause.
- Playback preview skips frames when rendering is slow; therefore preview can show temporal shader effects differently from the full-frame export.
- Existing automated export fixtures are short. Their successful results do not establish temporal correctness beyond 16 seconds or reproduce this clip/preset/VM combination.

## Next diagnostic steps

1. Supplied-file analysis and physical-host replay are complete; retain these as the reproduction reference.
2. If investigating VirtualBox further, capture pre-encoder diagnostics inside the affected VM and compare with host output. Establish guest additions, VirtualBox version and graphics driver details before a driver-specific fix.
3. Disabling artifact animation has changed the symptom to black output; do not suggest it as a fix or change defaults based on it.
4. Compare raw rendered frames with decoded encoded frames to isolate source decoding, shader feedback/frame phases and H.264/player behaviour. Reproduce past the reported onset before implementing a fix.

Status: failure confirmed in VM-generated encoded files; not reproduced on the physical host with the supplied original/preset. Precise VM-dependent cause unresolved. No effect settings or upstream shaders changed. Slow rendering alone is not an explanation for corrupted/flickering exported frames.
