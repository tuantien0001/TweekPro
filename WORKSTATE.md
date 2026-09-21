# Tweek Pro — current work checkpoint

Updated: 2026-09-22 00:10 (Asia/Bangkok).
Status: DONE (Cursor). Owner request 00:06: the header line about backed-up deletions is covered by the language/admin buttons and looks bad — shorten it.
Last editor: Cursor.

## Current task (00:10)

G. DONE Header subtitle shortened to "Gỡ ứng dụng, dọn rác, theo dõi mạng • Mọi thao tác xóa đều được sao lưu và hoàn tác" (EN pair in LangEn). `BuildHeaderActions` no longer AutoSize+Dock (that overlay let the label run under the buttons); the right cluster gets an explicit `PreferredSize` width so the subtitle stops before VI / Quyền quản trị viên. `PreviewHealth` sample update is current patch + 1 (was hardcoded 0.7.2, which rendered "0.7.2 is newer than 0.7.3"). Screenshots regenerated. Verification below.

## Current task (23:35)

D. DONE README screenshots: `--preview all [dir] [--en]` (`App.PreviewModes`, `ApplyPreview`, `MainForm.PreviewTab` for empty/dupes/analyzer/vault/logs, `Snapshot(form,file,Size?)` resizes after Show because `Load` restores the saved window size; language forced to VI unless `--en`) writes 18 PNGs to `docs/screenshots/` (health, apps, store, remnants, junk, empty, dupes, stale, analyzer, vault, autorun, explorer, tweaks, network, services, tools, logs, ai). README: hero image under the badges + `### Ảnh các tab` gallery (2-column HTML table) + `--preview all` row + changelog bullet; AGENTS.md notes to regenerate the PNGs whenever a tab layout changes.
   Explorer tab redesigned while capturing (the stacked split left ~1 visible row per list at 1240×820): a switcher row (`ShowExplorerView`, buttons "Duyệt tệp và chi tiết tệp" / "Tùy chỉnh File Explorer") shows one full-height pane; nav buttons ▲ Lên / This PC / Người dùng / Chọn tệp… + hidden-filter checkbox sit on the path row; file action buttons + `fileStage` moved into the details pane; browser split keeps a ratio (SplitterMoved ignored until first placement). Bug fixed: `DisguisedAs` returns "" (never null) so every file row was red — now `!String.IsNullOrEmpty`. `PreviewExplorer(bool tweaks)`; new preview mode `tweaks`.
Verification: build 0 W / 0 E; full `--self-test` PASS 23:31; `docs/screenshots/*.png` reviewed (explorer/tweaks/health/vault/network). Commit `03f0317` on `origin/main`.
E. DONE Fixed-order tab header (owner 23:37: "tabs run jumbled"): Windows multiline TabControl moves the selected row down, so 17 tabs in 3 rows shuffled on every click. `Presentation.TabStrip` (fixed grid, hover, glyphs via `Branding.DrawTabGlyph`, DPI via `ScaleControl`, repaints on TabPage.TextChanged) + `TabStrip.HideNativeHeaders(tabs)`; `BuildTabs` now only mounts it; `Controls.Add(tabs);Controls.Add(tabStrip);Controls.Add(header)`. Self-test PASS 23:43, screenshots regenerated (18), README changelog bullet. Commit `450c06b`.
F. DONE Release v0.7.3 (owner chose "cut now" 23:52): `release.ps1` → commit `449e120`, tag `v0.7.3`, Actions run 35628588712 success, Release published 23:56 with `TweekPro-0.7.3-Setup.exe` + portable zip. Installed via the downloaded Setup (/VERYSILENT), `C:\Program Files\Tweek Pro\TweekPro-0.7.3.exe` running. Policy stays: releases only when the owner asks (agent may run release.ps1 on request).

## Previous task (22:50)

A. DONE Explorer folder browser: `FileInspector.ListFolder` (hidden/system always listed, extension, shell type via SHGetFileInfo, sizes, attributes, 20 000-entry cap, drives list for ""), `ParentOf`, `ShellIcon(FolderEntry)`; `ExplorerUI` bottom = vertical split (browser left: Tên/Đuôi/Mô tả/Dung lượng/Sửa/Thuộc tính, hidden = italic muted, system = amber, disguised = red; details right, auto-inspect on select without hashes, "Xem chi tiết + băm" hashes). Buttons Lên một cấp / This PC / Thư mục người dùng / Chọn tệp / Mở trong Explorer / Thuộc tính Windows, filter "Chỉ hiện mục ẩn / hệ thống". Opens the user profile the first time the tab is selected. Test `ExplorerTests.Listing`.
B. DONE Network block: `Network/FirewallBlock.cs` (netsh advfirewall paired in+out Block rules, ASCII rule name `TweekPro Block - <exe> [hash8]`, rules read from `FirewallPolicy\FirewallRules` registry, `BlockReason` refuses core processes/System32/Windows root/own exe/non-.exe/UNC, vault `Kind=Firewall`, `Unblock`/`Restore` delete by exact name only), `FirewallLang`, `FirewallBlockTests` (parse, naming, refusals, fake-netsh round-trip via `Runner`/`RuleSource`). UI: "Chặn mạng" column (red rows), context menu Chặn/Bỏ chặn, toolbar "Đang chặn mạng…" dialog (unblock selected/all), note text updated (old LangEn key removed). Wired `Engine.Restore`, `KindLabel`, `Core.L`, `Tests07`.
C. DONE `Remnants/TraceHunter.cs` called at the end of `Advanced.DeepScan` (shared 25 s budget, every stage bounded): `NameVariants` (strip version/year/arch/parenthesis, publisher prefix, collapsed, exe base names; `Strong` = ≥8 chars or multi-word → cleanable, else Review), deletable values via `Advanced.ValidateValue` ∪ `TraceHunter.ValueKeys` (MUICache, AppCompat Persisted, FeatureUsage counters, FirewallRules `App=`, SharedDLLs, StartupApproved mirrors of found Run/Startup items), review-only: Classes `Applications\exe`, CLSID Inproc/LocalServer32, ProgID shell\open\command, RegisteredApplications, PATH (user+system), EventLog EventMessageFile, Installer UserData/Products by DisplayName/ProductName, Microsoft\Tracing, CrashDumps, WER ReportArchive/Queue, Prefetch, Recent .lnk targets, Start Menu folders, Documents/Saved Games/Pictures/Videos/Music/`.name` folders, `SOFTWARE\variant` keys. Deep-scan folder walk: strong variants join `names`; weak variants → Review rows. Tests `TraceHunterTests` (variants, parsers, matchers, allow-list, live bounded smoke on Windows).
Verification: build 0 W / 0 E; `--self-test` PASS 23:00 (A + B + C); `--preview explorer` and `--preview network` PNGs checked. README tab table + `### 0.7.n` changelog updated. Commits A+B `1295d40`, C + README `40b6a60`, both on `origin/main`. Installed exe `C:\Program Files\Tweek Pro\TweekPro-0.7.2.exe` replaced with this build (SHA-256 67BADD65…) and relaunched 23:04.
NEXT: owner may cut 0.7.3 with `release.ps1 -Notes "…"`; roadmap items below still open.

## Previous task (22:25)

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
