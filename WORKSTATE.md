# Tweek Pro — current work checkpoint

Updated: 2026-09-21 19:58 (Asia/Bangkok).
Status: PAUSED by owner — "finish current work, report, do not continue the roadmap yet".
Last editor: Cursor.

## Branch switch (20:05)

PR #7 merged by fast-forwarding `main` to `f8ce05c`; `main` is the only working branch from now on (AGENTS.md updated). `cursor/integration-all-features-e772` is frozen history. `release.ps1` runs from `main`.

## Shipped today (pushed; now all on `main`)

- `30d7a30` taskbar icon; `74f2717` AI tab last.
- `b1b5962` PUP/bloatware detector (`Pup/`), Flag column, 7th Health area.
- `fdd902a` Old Downloads tab (`Stale/`), vault `Kind=Stale`, settings defaults.
- `6e0fd92` Update check v1 (`Update/`): opt-in button on Overview + Tools, reads GitHub releases/latest (fallback tags `v*`), only notifies + opens page.
- `fdf8658` Startup Insight (`Startup/`): StartupApproved decode, impact/signature/size columns, Health `AutorunBroken`; **also contains** Windows Tools system icons (`Presentation.ShellIcon`, `WindowsTools.Icon`, `toolIcons` ImageList, expanded catalog).
- Live install `C:\Program Files\Tweek Pro\TweekPro-0.7.exe` = Release build of `fdf8658` (SHA256 verified by agent).
- `eb53c98` `release.ps1`: one-command release — validates git state, bumps version in csproj/App.cs/build.ps1/iss (UTF-8/BOM preserved), `build.ps1 -SelfTest`, commit `Release vX`, annotated tag, push branch + tag; Actions `release.yml` publishes the Release.
- `a00b21c` **Release v0.7.1** cut with the script (self-test PASS, elevated via UAC). Tag `v0.7.1` pushed separately after fixing a script bug (git progress on stderr aborted the tag push). Actions run for the tag builds Setup + zip and publishes https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.1.
- `3167d64` script fix: stderr-tolerant `Run-Git`; `-Version` optional → auto next patch from max(project version, highest `v*` tag), so each release is 0.7.2, 0.7.3, …
- `0a60809` Update auto-check at startup: silent probe after Reload, Overview banner (Success tint) with **Tải bản mới** / **Bỏ qua bản này**, settings `updateAutoCheck` (default true) + `updateSkipVersion`; `UpdateCheck.ShouldNotify` + tests; `--preview health` shows the banner.
- `a5ad0b4` installer `[InstallDelete]` removes old `TweekPro-*.exe` before copying.
- `d2ed04c` **Release v0.7.2** cut with `release.ps1` (auto version); Actions success; https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.2. Installed on the owner PC via `TweekPro-0.7.2-Setup.exe /VERYSILENT /CLOSEAPPLICATIONS` → `C:\Program Files\Tweek Pro\TweekPro-0.7.2.exe` running.

## Verification

Release build 0 W / 0 E; `--self-test` PASS at `fdf8658`. `release.ps1 -DryRun` exit 0; bad version and dirty tree refused.

## Next (when owner resumes)

1. Future releases: `release.ps1 -Notes "…"` from elevated PowerShell (auto 0.7.3, 0.7.4 …), then install the Setup from the Release (silent: `/VERYSILENT /CLOSEAPPLICATIONS`).
2. Roadmap: broken Start Menu/Desktop shortcuts → vault; localization completion (Health/Junk/Services/Store/AI runtime strings, `LangTests` source scan); optional Health stale-downloads area; AI tools `list_unwanted`/`list_stale`.

## Notes for agents

Cursor prompts Allow per shell command; pass `required_permissions:["all"]` from the first call and batch commands. Never `Set-Content` on Vietnamese sources.

## Git

`main` (local checkout tracks `origin/main`). Push after each verified commit.
