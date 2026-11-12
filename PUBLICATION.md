# Publication plan — 0.1.0-beta.1

The owner explicitly authorized publishing source and Windows installer as a public GitHub beta on 2026-11-12. The separate Release/Beta channel request is included in this version. No stable version is being declared.

## Destination and rights

- Owner: emgeka; authenticated connector and GitHub CLI identities verified as the same account.
- Repository: https://github.com/emgeka/tape-and-tube (public), main branch.
- Before creating it, confirm the destination does not exist. After creation, verify owner/admin rights before pushing. Never overwrite an unrelated repository or bypass branch protections.
- Only an allowlisted source tree is uploaded. Toolchains, personal media, local configuration, build caches and diagnostic images are excluded.

## Release assets

- v0.1.0-beta.1, marked prerelease=true and make_latest=false.
- TapeTube-0.1.0-beta.1-win-x64-setup.exe.
- Matching TapeTube-0.1.0-beta.1-source.zip.
- SHA256SUMS.txt containing exactly those public artifact hashes.
- The portable win-x64-REVIEW ZIP remains a local validation artifact.

The installer contains the matching own-source ZIP, shader sources, notices and upstream source references. FFmpeg and Microsoft runtime installers are not embedded. Installer-source recipes and prerequisite helper source are in the repository and source companion.

## Publication procedure

1. Update source and release documentation; preserve the NTSCRT origin prominently.
2. Build the channel-enabled installer and run package and installer tests. Record the actual hashes/results. Fresh-VM missing-component testing is explicitly pending.
3. Prepare and validate the allowlisted source tree, including private-path/credential checks. Use this tree for the repository commit; keep a matching commit SHA and tag.
4. Create the owner's repository, confirm rights, then push main and the exact reviewed tag. Create a draft GitHub prerelease, attach installer/source/checksums and verify uploaded hashes and metadata.
5. Publish the draft as a prerelease using the owner's authorization. Verify public URLs and the update feed. Record the actual publication receipt locally. No X announcements or maintainer messages are authorized by this GitHub publication request.

## Accepted uncertainty and remaining validation

All seven CRT presets remain unchanged. Upstream-scope uncertainties are transparently documented under the owner's adopted policy; no blanket relicensing is inferred. Native notice inventory gaps remain recorded in docs/DEPENDENCY_LICENSE_REVIEW.md. A fresh-machine missing-prerequisite test is pending. VirtualBox CRTSim exports can contain black/flickering frames; the supplied original/preset exported correctly on the physical host. These are beta limitations, not claims of stable validation.

## Update channels

Release accepts stable versions only. Beta accepts stable versions and prereleases. Automatic checks may show a notice, but installation is always started by the user. See docs/UPDATE_CHANNELS.md. Stable builds and releases must use a non-prerelease semantic version and prerelease=false. The first beta does not create a stable channel release.
