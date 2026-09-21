# Tweek Pro — current work checkpoint

Updated: 2026-09-21 21:50 (Asia/Bangkok).
Status: IN PROGRESS (Cursor). Owner resumed the roadmap: "automate README changelog on release; continue upgrading; add Explorer tab (file details, show extensions, show hidden files)".
Last editor: Cursor.

## Current task (22:25)

1. DONE `40d0764` `release.ps1` finalizes the README changelog (pending `### 0.7.n (chưa phát hành)` → `### x.y.z (dd/MM/yyyy)`, `-Notes` first bullet, new section when none pending; section = tag message); `release.yml` extracts that section into the Release body (`body_path`) + auto notes. Script now saved with UTF-8 BOM (contains Vietnamese regexes). Bug found/fixed: `$` in .NET multiline mode does not match before `\r` → anchors dropped.
2. DONE (this commit) `Explorer/` tab between Khởi động and Mạng: `ExplorerTweaks` (17-entry catalog, `Interpret`, `Read`, `Apply`→vault `Kind=Explorer` payload `absent|dword:n|…`, `Restore`, `RefreshExplorer` via SHChangeNotify + WM_SETTINGCHANGE, `RestartExplorer` via `runas /trustlevel:0x20000`), `ExplorerSafety` (exact HKCU key allow-list + catalog identity check; fixture key `Software\TweekProTest\Explorer`), `ExplorerShell` (Properties dialog, reveal), `FileInspector` (PE machine, Zone.Identifier ADS, signer via `ProcessResolver.PublisherOf`, owner, version, SHA-256/MD5 ≤ 2 GB, `Warn` rules, `Rows`/`Report`), `ExplorerUI` (SplitContainer; drag-drop; preview sample), `ExplorerLang`, `ExplorerTests` (catalog, interpretation, payload round-trip, refusals, HKCU fixture apply/restore/purge, disguise, PE header, zone, temp-file inspection incl. ADS + hidden). Wired: `Core.L`, `Engine.Restore`, `App.cs` (build, order, KindLabel, `--preview explorer`), `Branding` glyph `explorer`, `LangTests` tab list, `Tests07`. csproj `FileVersion` fixed (`$(TweekVersion).0`, was 5-part → CS7035).
3. Verification: Release build 0 W / 0 E; `--self-test` (elevated, UAC) PASS 22:25 on the committed tree; `--preview explorer` renders (PNG checked, live registry read: 8/17 on).
4. DONE `36ef315` pushed to `origin/main`. Release exe copied over `C:\Program Files\Tweek Pro\TweekPro-0.7.2.exe` (SHA-256 55D0B123…126E matches bin) and relaunched; owner sees the Explorer tab.
5. NEXT (roadmap): cut a release when the owner wants (`release.ps1 -Notes "…"` → 0.7.3, changelog auto-finalized); broken Start Menu/Desktop shortcuts → vault; localization completion; AI tools `list_unwanted`/`list_stale`/`inspect_file`; Health hint when Explorer hides extensions (`ExplorerTweaks.Read()` show-ext off).

Lessons: WinForms `ListView.ItemChecked` fires during handle creation and `Items` may yield null then; `SplitContainer` needs a `Size` before `Panel*MinSize`; Windows PowerShell reads BOM-less `.ps1` as ANSI — keep Vietnamese scripts BOM'd.

## Branch switch (20:05)

PR #7 merged by fast-forwarding `main` to `f8ce05c`; `main` is the only working branch from now on (AGENTS.md updated). `release.ps1` runs from `main`.
Branch cleanup (21:30, owner request): all seven `cursor/*-e772` branches deleted locally and on GitHub; PRs #1–#6 auto-closed (their content was already integrated into `main`). Only `main` remains. Do not look for the old branches; use `git log main` for history.

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
