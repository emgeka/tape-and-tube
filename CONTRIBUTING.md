# Contributing

Tape & Tube is an independent Windows frontend for upstream video processors. Please keep responsibilities and attribution clear.

- Build from the pinned dependencies described in README.md.
- Keep media, credentials, local configuration, downloaded tools and build outputs out of commits.
- Describe the user-visible change and how you checked it. Run the suites relevant to the change; rendering/video tests need the documented external components.
- UI text belongs in `src/Ntscrt.Windows/Translations.json`. Preserve formatting placeholders, filenames, license identifiers and upstream parameter names. Run `scripts/test-language.ps1` after changing translations.
- Fixes to upstream effect implementations should be proposed to their respective projects when appropriate. Preserve original licenses and authorship.
- Include the applicable SPDX identifier in new own-code files. Contributions to our own code are under GPL-3.0-only; third-party content requires its original licensing information.

Bug reports should include the app version, Windows/GPU details, shader, reproduction steps and a non-sensitive test case. Avoid posting personal file paths, private media or complete diagnostic dumps without reviewing them first.
