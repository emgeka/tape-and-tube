# Publication — 0.1.0-beta.2

The owner explicitly authorized publishing beta.2 on 2026-11-16, following successful Windows 11 VM installation and export feedback. Publish source and Windows installer as a public GitHub prerelease. Do not declare 0.1.0 stable.

## Destination and authorization

Existing repository: https://github.com/emgeka/tape-and-tube, owner emgeka, public, main branch. Verify the authenticated account and repository push/admin permission before external writes. Preserve main's history and any upstream changes; never bypass branch protection or required review. No X post or maintainer message is authorized by this publication request.

## Contents and provenance

Upload only the allowlisted source tree. Exclude developer toolchains, personal media, private configuration, build caches and diagnostics. Preserve prominent NTSCRT attribution and every original third-party license and notice. All seven CRT presets remain included.

Release assets:

- TapeTube-0.1.0-beta.2-win-x64-setup.exe
- TapeTube-0.1.0-beta.2-source.zip, matching the release build
- SHA256SUMS.txt with exactly those two hashes

The installer contains matching own source, shaders/notices and upstream source references. FFmpeg and Microsoft redistributables are not embedded or mirrored; optional downloads go directly to their providers with pinned hashes. Existing FFmpeg is referenced in place. The portable REVIEW ZIP remains local.

## Procedure

1. Update release documentation and build the installer/source companion. Compare runtime and installer logic with the owner-tested candidate; metadata/documentation updates are not an untested application feature change.
2. Validate the release package, selected installer modes and final hashes. Record actual VM feedback and remaining uncertainty without converting a successful overall test into proof of every missing-runtime/cancel branch.
3. Prepare and scan the allowlisted source tree. Commit it on the current remote main history; tag that exact commit v0.1.0-beta.2. Push without force and respect any protection requirements.
4. Create a draft prerelease with prerelease=true and make_latest=false, upload the installer, source and public checksum file. Verify the server digests against local files before publishing.
5. Publish under the owner's authorization. Verify anonymous release feed, public download links and tag/commit correspondence. Save a local publication receipt. Keep beta.1 intact.

## Limits and channels

The latest owner VM export had no flicker or black frames; the earlier VirtualBox CRTSim failure is not permanently resolved by that observation. Native notice gaps and accepted upstream shader-scope uncertainties remain documented. Separate fresh-machine cancellation branches and broad hardware coverage are not claimed.

Beta includes prereleases and stable versions; Release includes stable versions only. Installing updates remains user-controlled. No Linux release or additional effect family is included in beta.2.
