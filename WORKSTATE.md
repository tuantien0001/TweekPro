# Tweek Pro — current work checkpoint

Updated: 2026-09-21 19:40 (Asia/Bangkok).
Status: PAUSED by owner — "finish current work, report, do not continue the roadmap yet".
Last editor: Cursor.

## Shipped today (all on `cursor/integration-all-features-e772`, pushed)

- `30d7a30` taskbar icon; `74f2717` AI tab last.
- `b1b5962` PUP/bloatware detector (`Pup/`), Flag column, 7th Health area.
- `fdd902a` Old Downloads tab (`Stale/`), vault `Kind=Stale`, settings defaults.
- `6e0fd92` Update check v1 (`Update/`): opt-in button on Overview + Tools, reads GitHub releases/latest (fallback tags `v*`), only notifies + opens page.
- `fdf8658` Startup Insight (`Startup/`): StartupApproved decode, impact/signature/size columns, Health `AutorunBroken`; **also contains** Windows Tools system icons (`Presentation.ShellIcon`, `WindowsTools.Icon`, `toolIcons` ImageList, expanded catalog).
- Live install `C:\Program Files\Tweek Pro\TweekPro-0.7.exe` = Release build of `fdf8658` (SHA256 verified by agent).
- `release.ps1` (this commit): one-command release from terminal — validates git state, bumps version in csproj/App.cs/build.ps1/iss (UTF-8/BOM preserved, verified byte-diff), `build.ps1 -SelfTest`, commit `Release vX`, annotated tag, push branch + tag; Actions `release.yml` then publishes the Release. `-DryRun` tested OK (0.7 → 0.7.1). No real release cut yet — owner decides the version.

## Verification

Release build 0 W / 0 E; `--self-test` PASS at `fdf8658`. `release.ps1 -DryRun` exit 0; bad version and dirty tree refused.

## Next (when owner resumes)

1. Owner: `release.ps1 -Version 0.7.1 -Notes "…"` from elevated PowerShell to create the first GitHub Release (needed for in-app update check to report anything).
2. Roadmap: broken Start Menu/Desktop shortcuts → vault; localization completion (Health/Junk/Services/Store/AI runtime strings, `LangTests` source scan); optional Health stale-downloads area; AI tools `list_unwanted`/`list_stale`.

## Notes for agents

Cursor prompts Allow per shell command; pass `required_permissions:["all"]` from the first call and batch commands. Never `Set-Content` on Vietnamese sources.
