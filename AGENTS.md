# Tweek Pro – agent instructions

Tweek Pro 0.7 is a .NET Framework 4.8 WinForms application (C# 7.3, SDK-style `TweekPro.csproj`) that manages Windows: uninstall + leftovers, junk cleaner, duplicates, empty folders, disk analyzer, network view, health check, Windows (Appx) apps, recovery vault. The UI language is Vietnamese by default with an English dictionary.

Read `HANDOFF.md` first for the current state, roadmap and Windows-only verification checklist.

## Build and test

- Windows: `powershell -ExecutionPolicy Bypass -File .\build.ps1 -SelfTest` (needs .NET SDK 6+; the built exe runs on the .NET Framework 4.8 that ships with Windows). The exe has a `requireAdministrator` manifest, so run `--self-test` from an elevated PowerShell.
- Linux/macOS (CI or cloud agents): `dotnet build -c Release -p:EnableWindowsTargeting=true` then `env -u DISPLAY mono bin/Release/net48/TweekPro-0.7.exe --self-test junk`. The `junk` argument runs every platform-neutral suite (`CoreTests`, `Tests07` and everything it calls) and skips registry/UI tests. Exit code 0 and `bin/Release/net48/test-results.txt` starting with `PASS` mean success.
- Layout preview without a real Windows session: `mono bin/Release/net48/TweekPro-0.7.exe --preview <remnants|autorun|tools|junk|network|health|store|services|ai> [--show]` (`--show` opens the live window instead of writing `TweekPro-preview.png`). Under Mono set `MONO_WINFORMS_XIM_STYLE=disabled` and avoid keyboard input.
- Build must stay at 0 errors / 0 warnings. Every engine change needs a test in the matching `*Tests.cs`, wired into `Tests07.Run()`.

## Conventions

- One folder per feature: `Xyz/XyzEngine.cs` (pure logic, no WinForms), `Xyz/XyzSafety` or explicit allow/forbid lists, `Xyz/XyzUI.cs` (`partial class MainForm`, `BuildXyzTab()`), `Xyz/XyzTests.cs`. Register the tab in `App.cs` (`Build...Tab()` call and the `ordered` array) and add a glyph key in `Branding.GlyphKey` + `DrawTabGlyph`.
- Every destructive action goes through the vault: create a `Backup` (`Kind`, `State` = `Pending` → `BackedUp`/`NeedsReview`/`Restored`/`PendingReboot`), save with `Engine.SaveBackup`, dispatch restore in `Engine.Restore`. Nothing is deleted without a user confirmation dialog.
- Safety boundaries live in code, never only in JSON: `JunkSafety.ForbiddenRoots`/`WindowsCoreFolders`, `DupeSafety`, `EmptyFolders.ForbiddenRoots`, `WindowsApps.Classify`. Never relax them to make a feature "work"; add tests that prove refusals.
- Strings: write Vietnamese in code and wrap with `Core.L.T("…")` / `Core.L.F("…{0}", x)`; add the English pair to `Core/LangEn.cs` (duplicate keys throw at startup; `Core/LangTests` checks tab titles, junk rules and placeholders). `Theme.Button/Note/HeaderBand/SetOverlay`, `SetupList` columns and `TabPage` titles translate automatically.
- Comments and documentation in English; only method-level `/// <summary>` docstrings, no comments narrating logic inside methods. Code style is compact (single-line statements, few blank lines) – match the surrounding file.
- Windows-only APIs (registry HKLM, ACL/ownership, `MoveFileEx`, PowerShell Appx cmdlets, ETW) must be guarded so the Linux self-test still passes (`StubbornFiles.IsWindows`, `PowerShell.Executable == null`, `Environment.OSVersion`).
- README.md is the user-facing manual (Vietnamese): update the tab table and the "Cập nhật 0.7" changelog for every feature.

## Git

- Work on `cursor/integration-all-features-e772` (PR #7 collects everything; PRs #1–#6 are its ancestors). Small logical commits with descriptive messages; never force-push.
