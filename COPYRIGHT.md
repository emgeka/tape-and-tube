# Copyright and licensing

Copyright (C) 2026 Tape & Tube contributors.

The original application code, native host wrapper, build scripts and project documentation in this repository are licensed under **GNU GPL version 3 only**, SPDX `GPL-3.0-only`. See `LICENSE`.

Tape & Tube is distributed in the hope that it will be useful, but without any warranty, including the implied warranties of merchantability or fitness for a particular purpose.

Third-party libraries, runtime components, shaders, textures and license texts retain their own copyrights and licenses. The GPL declaration for our own code does not relicense those files. See `THIRD_PARTY.md` and the notices accompanying the release package.

The processing worker directly uses NTSCRT's MIT-licensed Rust engine and digital-video codec, Copyright (c) 2026 Finn McKenty, revision `889256dde173a89e475919be3d1bba34e6cc7355`. These modules, the derived digital-format catalog and the original presets retain their original attribution and MIT license. See `licenses/upstream/NTSCRT-MIT.txt`. Our host lifecycle patch and process transport are GPL-3.0-only.

Tape & Tube is a Windows adaptation of NTSCRT by Finn McKenty (https://github.com/finnmckenty/NTSCRT). NTSCRT is the original application and the starting point for the concept, signal-chain organization and workflow. Credit for that original work belongs to Finn McKenty / NTSCRT. The Windows application and host integration are newly implemented; NTSCRT’s Swift/Metal application code is not included. Tape & Tube is not an official NTSCRT, ntsc-rs, RetroArch or librashader release.
