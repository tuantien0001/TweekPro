# Tweek Pro – handoff for the next agent (Codex or any other)

For the latest active task and Cursor/Codex transfer, read `WORKSTATE.md` first. The rest of this file is architecture and recovery history.

Last updated: 2026-09-21, branch `main` (PR #7 merged 2026-09-21; `cursor/integration-all-features-e772` is frozen history), commit after `f8ce05c`.
Local working copy on the owner's PC: `C:\Users\ADMIN\TweekPro`. Refresh it with `powershell -ExecutionPolicy Bypass -File .\get-latest.ps1 -Run` from an elevated PowerShell.

## Recovery update — 2026-09-21

Recovered seven files from the owner's Cloud session and integrated them on the existing integration branch. LM Studio/Ollama support, model discovery, optional local key, Confirm/ReadOnly/explicit Auto modes and leftover/junk tools are restored. Fixed stale configuration reuse after validation failure, provider key carryover, ambiguous/traversal paths and tool-free fallback history. Default and unknown action modes require confirmation. The approved sample-2 T + PC icon is now embedded in the executable and used at runtime and by icon export.

Validation on the owner's Windows PC: Release build 0 warnings/errors; full `--self-test` PASS (including recovered and extended AI tests); isolated UI + real localhost HTTP mock PASS for both local providers, model listing, connection checks, chat, tool rejection and invalid configuration. Preview rendered successfully. No real applications/services were removed or stopped. Real model inference and a fresh installer install were not tested.

The original rescue sources remain untouched in `file đang dang dở chưa push`, excluded from compilation and Git; integrated versions are in `AI/` and `Core/Settings.cs`. Backup of the original rescue files and previous icon: `C:\Users\ADMIN\TweekPro-recovery-backup-20260921-171137`. That folder also contains the isolated UI/HTTP test scripts, results and preview. Do not copy the rescue files back over the integrated versions: the integrated files include fixes.

The sections below describe the earlier baseline; their untested-on-Windows statement predates this recovery validation. Destructive live-app checks still require dedicated test applications or a VM.

## 1. Where things stand

Everything below is implemented, builds with 0 warnings and passes the headless self-test on Linux (`--self-test junk`). It has been exercised as a live Mono WinForms window in a virtual display, but **not yet on a real Windows machine** since the last big changes. Section 4 lists what must be verified there first.

| Area | Files | State |
|---|---|---|
| App shell, tabs, header, vault, logs | `App.cs`, `Presentation.cs`, `Branding.cs`, `Engine.cs` | Stable. 13 tabs, canonical order in `App.cs` `ordered[]`. |
| Health check (Tổng quan) | `Health/` | Done, read-only, tests. |
| Junk cleaner incl. system junk with hard-blocked core folders | `Cleaner/Junk*`, `Cleaner/junk-rules.json` | Done, tests prove System32/WinSxS/Program Files refusals. |
| Duplicates, empty folders, disk analyzer | `Dupes/`, `Cleaner/Empty*`, `Analyzer/` | Done, tests. |
| Network view with per-app packet icons and realtime packet animation | `Network/` | Done; bandwidth via ETW needs admin (now default). |
| Localization VI/EN | `Core/L.cs`, `Core/LangEn.cs`, `Core/LangTests.cs` | Done; toggle is the globe icon button in the header. Log messages in some older tabs are still Vietnamese only. |
| Run as administrator by default | `app.manifest`, `App.Windows.config`, `TweekPro.csproj` | Done (`requireAdministrator`, PerMonitorV2 in manifest + WinForms `DpiAwareness` config that ships from Windows builds only, `AutoScaleDimensions` 96 on both forms). "Restart as administrator" button removed. |
| Stubborn files (attributes → take ownership → retry → schedule delete at reboot) | `Core/StubbornFiles.cs`, `Core/StubbornTests.cs`, hooks in `Engine.Quarantine`, `Advanced.Store`, `Engine.ScheduleOnReboot`, Leftovers tab in `App.cs` | Done; Windows branches (`TakeOwnership`, `MoveFileEx`) untested on real Windows. |
| Windows apps (Appx/MSIX) list + uninstall with code-level protection | `Store/WindowsApps.cs`, `Store/WindowsAppsUI.cs`, `Store/WindowsAppsTests.cs`, `Core/PowerShell.cs` | Done; real `Get-AppxPackage`/`Remove-AppxPackage` untested on real Windows (parser was written against real cmdlet output). |

Open PRs: #7 is the integration PR and supersedes #1–#6 (they can be closed once #7 merges to `main`).

## 2. Non-negotiable product rules (from the owner)

1. The app always runs elevated, but **never deletes anything inside `System32`, `SysWOW64`, `WinSxS`, `Program Files` or the other `JunkSafety.WindowsCoreFolders`**. Elevation is for Windows-owned junk, HKLM leftovers, ETW, Appx removal for all users and ownership takeover of leftovers of uninstalled apps.
2. Never delete files or folders unrelated to the application being cleaned. Leftover cleanup refuses when the app is still registered (`Engine.Installed`) or the folder overlaps an installed app's location.
3. Every deletion is either backed up in the vault or clearly labelled as direct/irreversible with a red button and an explicit confirmation. Delete-at-reboot items are flagged `PendingReboot` and have no backup by design.
4. Core Windows packages (`WindowsApps.CoreProtected`, frameworks, `NonRemovable`, `Microsoft.Windows.*`/`MicrosoftWindows.*`/`Windows.*` prefixes minus `SafeInbox`) can never be selected or scripted for removal.
5. The UI must look custom (logo, vector tab glyphs, grouped lists, notes) and be usable by non-technical users; Vietnamese first, English available.

## 3. How to work

See `AGENTS.md` for build/test commands and code conventions. Short version:

```powershell
# Windows, elevated PowerShell
cd C:\Users\ADMIN\TweekPro
.\get-latest.ps1            # fetch + build + self-test
.\build.ps1 -SelfTest       # rebuild after edits
.\bin\Release\net48\TweekPro-0.7.exe
```

```bash
# Linux (cloud agents / CI)
dotnet build -c Release -p:EnableWindowsTargeting=true
env -u DISPLAY mono bin/Release/net48/TweekPro-0.7.exe --self-test junk
```

Add a feature = engine + safety list + tests wired into `Tests07.Run()` + `partial class MainForm` tab + `Core/LangEn.cs` pairs + README tab table/changelog + small commits.

## 4. First thing to do on a real Windows PC (verification checklist)

Run from `C:\Users\ADMIN\TweekPro` after `get-latest.ps1`. Fix anything that fails before adding features.

1. Launch `TweekPro-0.7.exe`: UAC prompt appears once; header badge reads "Quyền quản trị viên"; no "Khởi động lại với quyền quản trị" button.
2. `--self-test` (elevated): exit 0; `test-results.txt` starts with `PASS` and includes the registry suites that Linux skips.
3. Globe button: click → confirm → app restarts in English; click again → Vietnamese. Setting persists in `%LOCALAPPDATA%\TweekPro\settings.json` (`language`).
4. Tab **Ứng dụng Windows** → "Tải danh sách": list loads with real package logos; Start/Search/Settings/Windows Security/Edge/frameworks appear under "Được bảo vệ" only when "Hiện thành phần được bảo vệ" is on and cannot be checked. Remove a harmless third-party package (e.g. a game or `Microsoft.BingWeather`) with both checkboxes off; verify it disappears, a `Kind=Store` entry appears in Kho khôi phục, and **Khôi phục** re-registers it (or opens its Store page). Then test "Gỡ cho mọi tài khoản" once.
5. Stubborn files. Prepare a leftover that Windows refuses to move:
   ```powershell
   $d="$env:LOCALAPPDATA\StubbornTest"; mkdir $d | Out-Null; 'x' > "$d\a.txt"
   attrib +r +h +s "$d\a.txt"; icacls $d /deny "$env:USERNAME:(D,DC)"
   ```
   Quarantine that folder from the Leftovers tab (scan a fake app id or add it via the scan window). Expect success and a log line "Tệp cứng đầu tại …: bỏ thuộc tính …; chiếm quyền sở hữu …", and the folder restorable from the vault. Then lock a file and repeat:
   ```powershell
   $f=[IO.File]::Open("$d\a.txt",'Open','Read','None')   # keep this PowerShell open
   ```
   Expect the "hẹn xóa khi khởi động lại" prompt; accept; verify the path in `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations`, the `PendingReboot` row in Kho khôi phục, and that the folder is gone after a reboot. Cleanup: `$f.Dispose()`.
6. Junk cleaner "Rác hệ thống" group is now enabled (admin): Preview then Clean into vault; confirm nothing under `C:\Windows\System32` is listed (the safety test guarantees it, but look).
7. Network tab: bandwidth (ETW) toggles on without an elevation prompt; packet animation reacts to traffic.
8. High-DPI: on a 125–150% display check text is crisp, controls (toolbar, notes, list icons) are scaled and nothing is clipped; resize the window, close and reopen twice and confirm the window size stays the same (sizes are persisted in logical pixels and restored in `Load`).

9. **Services tab** ("Dịch vụ hệ thống"): "Tải danh sách" reads ~250 services via WMI in under 3 s; every row has an explanation (no empty "What it does" cells except truly unknown Windows services, which show the Windows description); right-click a third-party service (e.g. AdobeARMservice) → Stop → confirm → row turns "Đã dừng"; right-click → "Dừng và vô hiệu hóa" → confirm → a `Kind=Service` row appears in the Recovery Vault and restoring it sets the start mode back; right-click RpcSs → every stop/disable item and "Kết thúc tiến trình" are disabled with a reason. Per-user services (`cbdhsvc_xxxxx`) must resolve to their base entry.
10. **Right-click menus** on Applications (uninstall / scan leftovers / open folder / copy), Windows Apps, Startup and Recovery Vault lists: verify "Mở thư mục cài" opens Explorer selecting the folder and "Gỡ ứng dụng này" runs the same queue as the toolbar button for just that row.
11. **AI Assistant** ("Trợ lý AI"): paste a real Anthropic or OpenAI key → "Lưu khóa" → `settings.json` contains `aiKeyProtected: "dpapi:…"` (never the clear key) → "Kiểm tra kết nối" shows the model's "OK" with token counts. Ask "Dịch vụ nào của bên thứ ba dừng được?" → the transcript shows a `list_services` tool line and a grounded answer with real service names; ask it to stop one → the confirmation dialog names the tool and arguments → after Yes the Services tab refreshes. Untick "Cho phép AI thực hiện thao tác thay đổi" and ask again → no dialog, the model explains it was denied. Wrong key → status shows "HTTP 401 … API key không hợp lệ". Off-line tests cover request/response shapes for both providers (`AI/AiTests.cs`).

## 5. Roadmap (next features, in priority order)

Each item follows the standard pattern (engine + safety + tests + tab/finding + i18n + README).

1. **Localization completion** – wrap remaining Vietnamese-only log messages and dialogs in Applications, Leftovers, Vault, Startup, Network tabs with `Core.L.T`; extend `LangTests` to scan `*.cs` for `Log("...")` calls with Vietnamese diacritics that are not wrapped.
2. **PUP / bloatware detector** – rules JSON (publisher/name/path patterns, e.g. toolbars, bundled updaters, trial AV, OEM bloat) + read-only classification of `Engine.Inventory()` and of Windows apps; show as a Health finding and a badge column in Ứng dụng / Ứng dụng Windows. No automatic removal; link to the existing uninstall flows.
3. **Stale large files** – old installers/ISOs/archives in Downloads and Desktop older than N days; vault `Kind=Stale`; Health finding with reclaimable size.
4. **Startup impact** – measure autorun entries (file size, signature, publisher, last-write, whether the target still exists) and show an impact/health column in Khởi động; "broken shortcut" detection for Start Menu/Desktop.
5. **Browser extension hygiene** – read-only listing of Chrome/Edge/Brave/Firefox extensions with publisher/permissions; flag known-bad IDs; no removal in v1.
6. **Registry cleaner (conservative)** – only orphaned uninstall keys, missing MUI cache and invalid `App Paths`; always through vault `Kind=Registry`; heavy tests.
7. **Scheduler / one-click maintenance** – run Health → Junk preview → apply selected groups from a single "Dọn nhanh" button with a summary dialog.
8. ~~Installer / distribution~~ – **done**: `publish.ps1` (zip + Inno Setup via `installer\TweekPro.iss`), `TweekPro.ico` embedded in the exe (`--export-icon`), GitHub Actions `release.yml` builds on `windows-latest` and attaches both files to a Release on `v*` tags. Still open: version bump to 0.8 in `TweekPro.csproj`, `MainForm.Version`, `build.ps1`, `app.manifest`, `installer\TweekPro.iss` default.

## 6. Known limitations / gotchas

- `ImageList.Images.Add` keeps a reference to the source bitmap until the ImageList handle exists (WinForms copies lazily). Always touch `imageList.Handle` before adding bitmaps you dispose afterwards — the Network tab crashed on Windows with "Parameter is not valid" for exactly this reason (invisible on Mono, which copies eagerly).

- Mono on Linux: keyboard input crashes WinForms (`X11Keyboard.Xutf8LookupString`); use `MONO_WINFORMS_XIM_STYLE=disabled` and mouse only. `MessageBox` buttons render without labels under Mono (left = Yes, right = No). Neither affects Windows.
- `Get-AppxPackage -AllUsers` needs elevation; the tab falls back to the current user when not elevated (should not happen now).
- `Directory.Move` of a folder containing an open file fails with ERROR_ACCESS_DENIED, not a sharing violation, so the escalation ladder runs once before the reboot prompt is offered; that is intended.
- `Theme.Note` grows with wrapped text; other fixed-height labels (`Height=30/34`) still truncate with `AutoEllipsis`.
- Independent review round (2026-09-21) fixed: `TOKEN_PRIVILEGES` interop layout (privileges were never enabled), Deny ACE removal during ownership takeover, validation now runs inside the escalation ladder, `PendingReboot` entries purgeable and `Restore` refuses them, null-safe `Get-AppxPackage` fields for Windows 8.1/old 10, UTF-8 PowerShell output, `build.ps1` waits for the WinExe self-test, logo cache swapped atomically on the UI thread. The layout is now asserted by `StubbornTests`; the ACL path still needs the Windows checklist.
- Line endings are mixed: `Engine.cs`/`Advanced.cs` are LF, `Presentation.cs`/`Core/L.cs`/`Core/StubbornFiles.cs` are CRLF. Keep each file's existing ending (do not renormalize in a feature commit; if desired add `.gitattributes` with `*.cs text=auto` in its own commit).
- Tests create fixtures under `%TEMP%\tweekpro-*`, `%LOCALAPPDATA%\TweekProTest*` and `HKCU\Software\TweekProTest*` and clean them up.
