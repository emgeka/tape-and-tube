# Release and Beta channels

Open **Updates** at the top right of the app. Select **Release** for stable versions only, or **Beta** for stable versions and prereleases. The initial default follows the installed build: this beta starts on Beta; a stable build starts on Release. Your selection is saved independently of language/effect settings under `%LOCALAPPDATA%/TapeTube/update-settings.json`.

Automatic checking is enabled by default and can be disabled in the dialog. When enabled, one check runs shortly after opening the app. A newer eligible version changes the Updates button into an update notice. No installer is fetched or run automatically. **Download öffnen / Open download** opens the project's GitHub release page; the user selects a download and starts installation.

The app makes an anonymous HTTPS request to `https://api.github.com/repos/emgeka/tape-and-tube/releases?per_page=100`, without account credentials, media paths, effect settings or telemetry payloads. GitHub receives the normal network request and a TapeTube/version user-agent. Connection failures and rate limits do not block editing; manual checks show a failure message. The current implementation examines the latest 100 release entries and has no periodic background polling within a session.

## Selection rules

- Release excludes both GitHub prereleases and semantically prerelease-tagged versions, even if their flag is wrong.
- Beta accepts stable releases and prereleases. Semantic version precedence decides the newest eligible version, rather than publication order; beta.10 sorts after beta.2.
- Drafts, malformed version tags and download pages outside this repository's HTTPS GitHub release pages are excluded.
- Switching channels never installs or suggests a downgrade. Build metadata alone does not trigger an update.
- Tags use `vMAJOR.MINOR.PATCH` for stable, or suffixes such as `-beta.1` and `-rc.1` for previews. Stable release publishing must set `prerelease=false`; Beta must set `prerelease=true` and must not become GitHub's latest stable release.

The first publication is `v0.1.0-beta.1`. There is no stable release yet. Providing two channels does not claim the beta is stable.

## Verification

`./scripts/test-updates.ps1` checks version precedence, channel filtering, downgrade exclusion, URL restrictions, missing/corrupt preferences and persistence. It uses synthetic release lists and performs no network requests. Package tests also run this suite. Live GitHub feed availability is verified separately after publication.
