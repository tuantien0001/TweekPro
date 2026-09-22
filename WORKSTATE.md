# Tweek Pro — current work checkpoint

Updated: 2026-09-22 17:30 (Asia/Bangkok).
Status: DONE (Cursor), not released. Owner (17:08): Khởi động tab must load when clicked; Win10/Win7 weak-PC support; make the app lighter.
Last editor: Cursor.

## Current task (17:30)

R. DONE Lighter startup + lazy tabs + old-Windows manifest.
- Root cause of "Khởi động tab shows nothing": `tabs.SelectedIndexChanged` skipped the load while `busy` (startup Reload/health check hold Guard). New `MainForm.WhenIdle(action)` waits for `busy` to clear then Guards; autorun handler shows "Đang đọc mục khởi động…" overlay meanwhile (`autorunLoading` flag), Explorer tab uses the same helper.
- Startup no longer holds Guard for the slow parts: `Reload` returns right after the inventory renders; `MeasureSizes()` (fire-and-forget, `LowPriority`) fills sizes and re-filters only if `inventory` is still the same list; `AutoHealthCheck` runs outside Guard with `RunHealthCheck` re-entrancy check (`healthCancellation!=null` → log + return) and low-priority probes.
- `Advanced.Autoruns(bool includeServices=true)` → `StartupSources.Append(items,includeServices)`; health probe passes false (was WMI 300+ services + signature per third-party service, all discarded). Test in `AdvancedTests`.
- `Sizing/SizeCache.cs`: folder → bytes/partial/measuredAt, XML at `%LOCALAPPDATA%\TweekPro\install-sizes.xml`, MaxAge 24 h, Keep 30 d, `FileOverride`/`Reset` for tests; used by `InstallSize.Apply` and `WindowsAppsUI.MeasureStoreSizes`. Tests in `InstallSizeTests` (zero-budget hit, reload from disk, expiry, case-insensitive exact match, re-measure).
- Measured (this PC, 100 apps, 40 measured folders): baseline WS 94 MB / private 52 MB / CPU 7.8 s in 30 s, buttons disabled ~8 s. Now: cold cache 8.5 s CPU (writes cache), warm 3.7 s CPU, WS 91 MB, buttons usable after ~1 s, health done at +3 s. ngen trial: install/uninstall exit 0, CPU 3.4 s (small here, bigger on slow CPUs) — uninstalled again, nothing left in the NIC.
- `app.manifest`: supportedOS for Windows 8 and 7 added (app is x64; Win7 SP1 needs .NET 4.8). `installer/TweekPro.iss`: `[Run]` `{dotnet4064}\ngen.exe install` (runhidden waituntilterminated skipifdoesntexist) + `[UninstallRun]` uninstall. NOT compiled locally (no ISCC here); syntax checked by hand, CI compiles it on the next tag — if the release workflow fails, look here first.
- README: requirements (Win7/8.1 caveats, no 32-bit, untested on Win7), "Máy yếu" bullet, data folder lists `install-sizes.xml`, `### 0.7.7` bullets. Build 0 W / 0 E; elevated `--self-test` PASS 17:22.
- 17:32 Installed exe `C:\Program Files\Tweek Pro\TweekPro-0.7.6.exe` replaced with this build (SHA-256 24D898A8…, = bin of `bd29579`) and relaunched (PID 45624). Version string still 0.7.6.
- NEXT: owner tests, then says "push 0.7.7" → `release.ps1 -Version 0.7.7` elevated; watch the Actions run for the ISCC step (ngen lines in TweekPro.iss are untested locally).

## Previous task (17:10)

Q. DONE Overview: drive rings + auto check. `Health/HealthCheck.cs`: `DriveGauge` (Name/Free/Total, `UsedFraction` clamped, `Level`), `HealthCheck.DriveLevel` (shared with the Disk finding), `ReadDrives` (fixed + ready, letter order, never throws), `DriveLabel`. `Health/HealthGauge.cs`: `HealthRenderer.Draw(..., drives)` puts one 72 px ring per drive on the right of the card (`DrivesThatFit` keeps ≥360 px for the text block), two-line caption "{0} trống" / "/ total"; `HealthGaugePanel.Drives`. `Health/HealthUI.cs`: `RefreshDriveRings`, `AutoHealthCheck` (rings, then `RunHealthCheck` if `settings.HealthAutoCheck`), rings re-read inside every check, "Tự kiểm tra khi mở" checkbox saves `Settings.HealthAutoCheck` (default true, restored in `Defaults`), list overlay says "Đang kiểm tra…" during the first run and reverts if stopped. `App.cs` Shown: Reload → `Guard(AutoHealthCheck)` → `AutoCheckForUpdates`. LangEn pairs added; gauge idle text now says the check runs on launch. Tests in `HealthTests` (levels, fraction clamps, label, live drives, slot fitting). README tab row + `### 0.7.7 (chưa phát hành)`; screenshots regenerated 17:05 (health.png shows C:/D:/E: rings).
- Verification: build 0 W / 0 E; elevated `--self-test` PASS 17:08. Version string still 0.7.6.
- NEXT: install this build over `C:\Program Files\Tweek Pro\TweekPro-0.7.6.exe` (owner's habit) or cut 0.7.7 on request.

## Previous task (12:30)

P. DONE Release v0.7.6. `release.ps1 -Version 0.7.6` elevated → build 0 W / 0 E, self-test PASS 12:18, commit `b8c2235` "Release v0.7.6", tag `v0.7.6` (`5b9640d`), both on origin. Actions run 35690206135 success; Release published 12:19 UTC+7 with `TweekPro-0.7.6-Setup.exe` (5.5 MB) + portable zip: https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.6 . Setup installed (`/VERYSILENT /CLOSEAPPLICATIONS /NORESTART`, exit 0) → `C:\Program Files\Tweek Pro\TweekPro-0.7.6.exe` 0.7.6.0 running since 12:29 (PID 30868). README `### 0.7.6` finalized by the script; next changelog heading will be `### 0.7.7 (chưa phát hành)`. Note: this shell has no git identity/`gh`; commit with `git -c user.name='Cursor Agent' -c user.email=cursoragent@cursor.com`, poll Actions via api.github.com.

## Previous task (12:20)

O. DONE Three upgrades (shipped in 0.7.6).
- Tiện ích trình duyệt → actionable. New `Extensions/ExtensionRemoval.cs` (vault `Kind=Extension`): `RefuseReason` (component locations 5/10, Firefox registry/system add-ons, anything outside the profile folder), `Running`/`CloseBrowser` (WM_CLOSE, 15 s), `Remove` (folder → vault; `CutFromPrefs` removes `extensions.settings.<id>`, `protection.macs.extensions.settings.<id>`, pinned/toolbar refs from `Preferences` and `Secure Preferences` with `File.Replace`; install-source key `Software\<vendor>\Extensions\<id>` and matching Forcelist values saved as payload then deleted; Firefox `.xpi` moved), `MergeIntoPrefs` on restore (only when missing), `ForcelistEntries`/`RemoveForcelist` (HKLM+HKCU, both views, empty key deleted). Test hooks `RunningOverride`, `PolicyKeyOverride`. `BrowserExtension.ProfileFolder` added. UI: "Gỡ tiện ích đã chọn (vào Kho)", "Dọn Forcelist (ép cài)…", `EnsureBrowserClosed` prompt.
- Dấu vết: catalog 13 → 26 rules (PSReadLine, RDP, mapped drives, RecentApps, FeatureUsage, ShellBags off by default, Regedit LastKey via new `TrackRule.Values`, Office File/Place MRU with multi-`*` patterns, Office Recent shortcuts, Acrobat cRecentFiles, Chromium sessions off by default, Firefox history keeping bookmarks via SQL, Firefox cookies). New `Core/WinSqlite.cs` (P/Invoke `winsqlite3.dll`, Win10+; `Available` false elsewhere → whole-file fallback). `TrackRule.SqlFile/SqlTable/SqlHostColumn/SqlStatements/SqlCountTable`; `CleanSqlite` copies the DB to the vault, deletes rows (cookie whitelist: `Settings.TracksCookieKeep`, `ParseKeep` → host + `.host` + subdomains), `VACUUM`; restore copies the file back when the browser is closed (`BlockerOverride` hook). `TracksSafety.ValidateKey` matches `*` per segment. UI: "Cookie giữ lại…" (`CookieKeepForm`), "Dọn khi thoát Tweek Pro" (`Settings.TracksCleanOnExit`, `TracksExitRules`; `RunTracksOnExit` from `MainForm.FormClosing`, skipped in preview). `tracksRendering`/`tracksMeasured` guard so rendering does not overwrite the saved rule set.
- Dung lượng hệ thống: `SpaceItem.Reclaim/ReclaimNote/Vaulted/Paths`; `AnalyzeComponentStore` parses `DISM /AnalyzeComponentStore` (ordinal line parsing, `ParseAnalyze` handles "8,02 GB"); new item `installer-orphans` (`ReferencedPackages` from `Installer\UserData\*\Products\*\InstallProperties` + `Patches\*` `LocalPackage`; `Orphans` = unreferenced top-level `.msi/.msp` older than `InstallerMinAge` 1 day; skipped when no product could be read); `MoveOrphans` → vault `Kind=Installer` (`InstallerBackupKind`), `RestoreInstaller`. `Execute` streams stdout via `ReadLines` (splits on `\r` too, DISM %). UI: "Ước tính thu hồi" column, per-item incremental render, "Dừng" button (`spaceCancellation`), `MoveInstallerOrphans` flow with confirm + live progress.
- Wiring: `Engine.Restore` (Extension, Installer), `App.KindLabel`, `Core.L` tables (removed duplicate "Dừng"), `Tests07` unchanged (suites already registered). README tab table (Dấu vết, Dung lượng hệ thống, Tiện ích trình duyệt), limitations, `### 0.7.6` changelog; `docs/screenshots/` regenerated (elevated, absolute path).
- Verification: Release build 0 W / 0 E; elevated `--self-test` PASS 12:13 (`bin/Release/net48/test-results.txt`). Not yet installed over `C:\Program Files\Tweek Pro\TweekPro-0.7.5.exe`.
- NEXT: owner decides on `release.ps1 -Version 0.7.6` (or auto). Open roadmap: 2 install monitor, 3 broken shortcuts, 4 localization completion; Phase 3 placement (owner asked where to put it when tabs are full — answered: group tabs / sub-tabs, no decision yet).

## Previous task (11:30)

N. DONE Release v0.7.5 (owner 11:03). `release.ps1 -Version 0.7.5` elevated → self-test PASS 11:16, commit `9414a9a` "Release v0.7.5", annotated tag `v0.7.5` (`3da8d3f`), both on origin. Actions run 35686306291 success; Release published with `TweekPro-0.7.5-Setup.exe` (5.2 MB) + portable zip: https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.5 . Setup installed (`/VERYSILENT /CLOSEAPPLICATIONS`, exit 0) → `C:\Program Files\Tweek Pro\TweekPro-0.7.5.exe` 0.7.5.0 running since 11:29. Tab order commit `8554821` (new tabs sit after Nhật ký, before Trợ lý AI). README `### 0.7.5` finalized by the script; next changelog heading will be `### 0.7.6 (chưa phát hành)`.

## Current task (10:55)

M. DONE Roadmap 1/5/6/7/8/9, one commit per feature on `main`:
- 1 `Remnants/ForcedUninstall*.cs` + `RemnantsLang`: "Gỡ cưỡng bức…" button on Ứng dụng builds a synthetic `AppEntry` (name, optional publisher/folder/exe) and feeds `ReviewLeftovers`/deep scan; `ValidatePickedFolder` refuses Windows/Program Files roots/profile/drive roots; folders outside cleanable roots become review-only. Commit `5ad8000`.
- 5 `Tracks/` tab Dấu vết: file rules (Recent, Jump Lists, ActivitiesCache) + HKCU registry rules (RunMRU, TypedPaths, RecentDocs, ComDlg32, WordWheelQuery, UserAssist) + browser history/cookies/form data; `TracksSafety` allow-list; vault `Kind=Tracks` (files moved; registry saved as `RegNode` tree, key emptied but parent kept, restore merges without overwriting newer values). Browser rules lock while the browser runs. Commit `1630746`.
- 6 `RegClean/` tab Registry: orphan Uninstall keys, App Paths, Applications, MUICache, SharedDLLs, Shell Extensions Approved whose target file is verifiably absent; `RegCleanSafety` exact parent allow-list; vault `Kind=Registry`, restore only re-creates missing entries; "Mở trong Regedit". Commit `1fcaa4b`.
- 7 `SysSpace/` tab Dung lượng hệ thống: measures Windows.old, WinSxS, Delivery Optimization, duplicate DriverStore packages (`Get-WindowsDriver`), hiberfil, pagefile, shadow storage, Recycle Bin; each reclaimed only via its Windows tool (cleanmgr sageset, DISM StartComponentCleanup, pnputil without /force, powercfg, vssadmin, cmdlets); no vault, confirm + elevation, tool output logged. `SystemSpace.Runner` injectable for tests. Commit `3200db8`.
- 8 `Extensions/` tab Tiện ích trình duyệt (read-only): Chrome/Edge/Brave/Vivaldi/Opera/Firefox across profiles; Preferences/Secure Preferences/extensions.json read via copy; provenance (store / policy forcelist / unpacked / sideloaded), broad permissions, install time; CSV; open folder / manager page. Commit `cf04290`.
- 9 `Shredder/`: "Xóa không phục hồi…" (Danger button) in the Explorer file pane; `FileShredder.Plan` (no reparse traversal, 20k cap) → `ShredConfirmForm` (ack checkbox unlocks; 1 or 3 passes; medium note) → `Shred` (WriteThrough overwrite, truncate, random rename, delete; links unlinked, folders removed bottom-up). `ShredSafety.Validate` refuses drive roots, system files at drive root, Windows, Program Files, ProgramData\Microsoft, profile/Users, vault, Tweek Pro folder, UNC, reparse points. Commit `c775448` (also removed duplicate "Chưa đo" key that broke `Core.L` at startup).
- Wiring: `Core.L` merges Remnants/Tracks/RegClean/SystemSpace/Extensions/Shredder tables; `Engine.Restore` handles Tracks/Registry kinds; `Branding` glyphs footprints/registry/drive/puzzle; `LangTests` tab list; `App.PreviewModes` + `--preview all` (22 PNGs); `Tests07` runs all six test suites.
- Verification: Release build 0 W / 0 E; elevated `--self-test` PASS 10:48 (`bin/Release/net48/test-results.txt`); `docs/screenshots/` regenerated 10:52 and tracks/registry/space/extensions/explorer PNGs reviewed. README tab table, gallery and 0.7.5 changelog updated.
- Pushed: `59b5b78` (README/screenshots/WORKSTATE) on `origin/main` 10:55; local == remote. Installed exe `C:\Program Files\Tweek Pro\TweekPro-0.7.4.exe` replaced with this build (SHA-256 B4FC0D8B…B063) and relaunched 10:56; the log shows Dung lượng hệ thống (64 GB reclaimable), Dấu vết (1,469 items) and Registry (182 orphans) scanning without errors. Two instances were accidentally started (PIDs 31136, 29544); close the spare one.
- NEXT: owner decides when to cut 0.7.5 (`release.ps1`). Open roadmap items: 2 install monitor, 3 broken shortcuts, 4 localization completion.

## Previous task (09:22)

L. DONE Vault tab crash. Cause: the vault list is filled at startup (`LoadBackups` in `Reload`) before its tab is ever shown; when the tab is first selected the ListView handle is created, Windows sends LVN_ITEMCHANGED for every row (state image 0 → 1), WinForms raises `ItemChecked`, `UpdateBackupSummary` enumerates `backups.Items` and `Items[i]` returns null for rows not yet inserted natively → NRE inside `TabControl.SelectedIndex`, so the new page never became visible. Only bites with ≥2 backups (the owner now has autorun backups). Reproduced in a stand-alone WinForms harness (5 events, 10 null items) and confirmed the fix there (0 events during handle creation, user check still raises 1).
- Fix: `Presentation.SmoothListView` overrides `OnHandleCreated`/`OnItemCheck`/`OnItemChecked` and drops both events while the handle is being created. Every list in the app is a `SmoothListView`, so remnants/junk/dupes/store/stale lists filled from another tab (AI tools) are covered too.
- `Application.ThreadException` now logs `e.Exception.ToString()` (stack trace) instead of type + message.
- README `### 0.7.5 (chưa phát hành)` bullet. Build 0 W / 0 E; elevated `--self-test` PASS 09:20. Commit `dca9a9f` on `origin/main`. Installed exe `C:\Program Files\Tweek Pro\TweekPro-0.7.4.exe` replaced (SHA-256 93AA77DD…8D2D) and relaunched 09:22; the session logged no errors (owner closed it 09:23). Not released: version string is still 0.7.4.

## Previous task (09:08)

K. DONE Sizes like Revo (`Sizing/InstallSize.cs`, `Sizing/InstallSizeTests.cs`, wired in `Tests07`, `SizingLang` in `Core.L`). Not released; version stays 0.7.4, README has `### 0.7.5 (chưa phát hành)`.
- `SteamLibrary`: appid from the Uninstall key name `Steam App <id>` or `steam://uninstall/<id>`; reads `<steam>\steamapps\libraryfolders.vdf` (also `config\`) and `appmanifest_<id>.acf`; returns library, installdir, SizeOnDisk. On this machine PUBG/CS2/AoE IV live in `D:\SteamLibrary` while the registry still says `C:\…\Steam\steamapps\common\…`; `Apply` swaps `Location` to the real folder only when the registry folder does not exist.
- `FolderSize.Measure`: bounded walk (6 s per folder, 250k files), skips nested reparse points but follows the root (WindowsApps package folders are junctions to `D:\WindowsApps\…` for Xbox games; GTA V Enhanced measures 103 GB).
- `InstallSize.FolderOf`: InstallLocation, else the uninstaller's folder, else DisplayIcon's folder (never Temp). `Measurable` refuses drive roots, Program Files, ProgramData, user profile, AppData, Windows tree, `Users\<name>`. Live result: 41 of 42 size-less desktop apps filled in 3 s; only the MSI AppCompat Fix Database stays unknown.
- UI: Ứng dụng tab measures after the first render (20 s budget) and re-filters; tooltip says where the size came from; `+` suffix = partial. Ứng dụng Windows tab has a new Dung lượng column measured after render (25 s budget). Preview: store sample gets synthetic bytes, apps preview runs Apply.
- Verification: build 0 W / 0 E; elevated `--self-test` PASS 09:05; screenshots regenerated 09:05 (apps.png, store.png show the column).

## Previous task (01:48)

J. DONE Release v0.7.4 (owner 01:41). `release.ps1 -Version 0.7.4` → commit `0e15ded`, annotated tag `v0.7.4`. Self-test PASS 01:42. Actions run 35640168638 success; branch run skipped because the message starts with "Release v". Release published 01:43 UTC+7 with Setup + portable zip: https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.4 . Setup installed (`/VERYSILENT /CLOSEAPPLICATIONS`, exit 0); `C:\Program Files\Tweek Pro\TweekPro-0.7.4.exe` file version 0.7.4.0 and relaunched.

## Previous task (01:35)

Autorun expansion, released as 0.7.4.
- `Startup/StartupSources.cs`: packaged startup tasks (HKCU AppModel SystemAppData State) and automatic Win32 services (no kernel/FS drivers). Core services stay listed and `DisableAutostart` refuses them. Disable does not stop a running service. Vault `Kind=StartupTask` / `Kind=Service` Purpose=Autorun.
- Checkbox on the Khởi động list. ItemCheck ignores a change that already matches the row, so creating the list handle no longer tries to "enable" every checked item (that produced `LỖI: Mục này không có công tắc…` in the status bar).
- Groups in order: Run / Startup, Ứng dụng Windows, Dịch vụ tự chạy, Kho Tweek Pro.
- A Run/Startup entry Windows already disabled is turned back on by rewriting StartupApproved only (`StartupInspector.SetEnabled`, vault Purpose=`StartupApproved`). RunOnce still has no on/off flag.
- Health count excludes services. README `### 0.7.4 (chưa phát hành)`. `docs/screenshots/` regenerated 01:35 (`autorun.png` shows the checkboxes; Windows-apps group sits under the Run group and is on screen at the owner's taller window).
- Verification: Release build 0 warnings / 0 errors. Elevated `--self-test` PASS 01:34. `--preview autorun` status bar stays "Sẵn sàng" (the false enable error is gone) and the list shows Run / Startup then Ứng dụng Windows (1Password, Intel Graphics, Claude). Commit `61c126a` on `origin/main`. Installed exe replaced (SHA-256 B5C1F549…7B61) and relaunched 01:39. Do not move tag v0.7.3.

## Previous task (00:16)

H. DONE Language control is a flag (`Branding.DrawLanguageGlyph`): Vietnam (red field, gold star) while the UI is Vietnamese, Union Jack while English. The button background matches the header so only the flag shows; tooltip and accessible name still say which language a click switches to. Verified by drawing both flags and by `--preview health`.
I. DONE Republished v0.7.3 (owner 00:17, same version). Changelog bullets folded into `### 0.7.3` in `70cf586`; tag `v0.7.3` moved there (annotated object `6579557`). Actions run 35631213030 success; Release assets replaced 00:19 UTC+7: https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.3 . In-app update check does not nag machines already on 0.7.3.

## Previous task (00:10)

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
