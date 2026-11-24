# Experimental digital-stage host

This is prototype host code, GPL-3.0-only. It wraps the **unchanged MIT-licensed
NTSCRT `port/codec`** from commit
`0ab60bf952b09a0c41ea4290979f23dadd963614`. Preserve Finn McKenty's original
copyright/license and the upstream dependency notices when packaging. It does not
change the released Windows app, its presets, or its installer.

`include/tapetube_digital.h` defines version **1** of the C ABI. Cargo builds a
`cdylib` for a foreign caller and an `rlib` for Rust-based prototype integration.
No WPF, Direct3D, Media Foundation or platform-specific media decoder is used.
An actual native Linux build/test is still required before claiming support.

Create a session with a UTF-8 JSON object of **numeric upstream digital settings**,
for example `{"codec":0,"frame_rate":15,"quality":0.55,"keyframe_interval":1}`.
Missing controls take upstream parameter defaults, not the app's starting look.
Unknown controls, unsupported choices, out-of-range/nonfinite values and malformed
JSON fail. Settings stay fixed during a session; create another session for new
settings. This prototype does **not** implement animated settings history.

Frames are top-down **straight RGBA8**, with explicit dimensions and byte stride.
The caller retains ownership. Upstream produces opaque alpha. Padding bytes are
preserved. The frame context has an exact `struct_size` (use `sizeof`), format 1,
the source frame index and rational source FPS, plus a capture resolution. The
current bounds are 16 megapixels/source, one megapixel/capture, positive FPS from
1 to 240, capture at least 16x16 fitting inside the source, and 256 MiB per frame
buffer. Length must include **all rows including the last row's padding**.
These are prototype resource bounds, not a claim of universal media support.

`frame_index` is a **source-frame index**. The upstream codec derives its internal
capture index from source FPS and its `frame_rate` setting. Grain, loss and
datamoshing use upstream's capture-index random sequence. There is **no extra
free seed parameter**; changing indices to imitate a seed would change timing and
is deliberately not provided.

Video callers provide real earlier source frames using the synchronous C visitor
callback. The host copies pixels during each sink call and never retains foreign
storage. A callback must invoke its sink exactly once, on the calling thread,
before returning 1. Errors, absent frames, multiple calls, changing dimensions,
invalid buffers or wrong formats fail the render; **the current frame is never
silently substituted for missing video history**. Requests may repeat or move
backwards between calls, because the codec can rerun from an earlier keyframe.
Use a bounded decoded-source cache or seekable decoder, not processed frames.

For a still image only, set `TT_DIGITAL_STILL`; then the current image is explicitly
used as all historical source frames. History is needed for predictive frames,
data-loss repair, capture-FPS repetition, cold seeks and datamoshing. Even intra
coding can need a previous frame for data-loss repair. The host bounds history
requests to 16,384 per call for both video and still images. Still history uses
an independent read-only copy of the original input and never calls an external
provider. Callers should warm long runs sequentially rather
than cold-seek across an entire datamoshed clip.

Each session has its own renderer and upstream checkpoint cache (upstream budget
96 MiB). Preview/export need separate sessions with identical settings and source
frames. Presentation may skip frames, but processing must use the real timeline.
Call reset on source replacement or a deliberate new run. Seek and loop are
handled by upstream reruns with the provider; tests compare them to sequential
processing. Dimensions/capture size/FPS/still-mode changes automatically reset
the session. Do not change source content at the same index without resetting.

Every processing call works on private pixels and only commits on success. On
processor panic/provider failure the renderer is reset and caller pixels remain
unchanged. Rust panics are caught before crossing the C ABI; a **foreign callback
must never unwind**. Out-of-memory aborts and invalid foreign pointers cannot be
recovered by a C ABI. Returned UTF-8 strings belong to the library and must be
freed once with its matching `tt_digital_string_free`, including metadata/defaults.

Handles are opaque, nonzero `uint64_t` IDs. Unknown/destroyed handles fail without
dereferencing arbitrary addresses. Calls on one session must be serial;
concurrent/reentrant processing returns an error. Independent sessions can run
in parallel. Caller must not race destruction against another use of that handle.

Run `cargo test --manifest-path prototypes/processing-core/digital-host/Cargo.toml`.
Tests exercise changing source content, DCT intra and predictive coding, Video 1,
data loss/datamosh, reduced camera FPS, cold forward/backwards seek, loop, dropped
presentation frames, independent sessions, RGBA channel order/alpha/row padding,
invalid arguments, history failures/recovery and strict settings. These are host
correctness tests; they do not prove macOS pixel parity, Linux availability,
GPU compatibility or end-to-end audio/video synchronization.
