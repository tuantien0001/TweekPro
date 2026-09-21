# Tweek Pro – agent instructions

Tweek Pro 0.7 is a .NET Framework 4.8 WinForms application (C# 7.3, SDK-style `TweekPro.csproj`) that manages Windows: uninstall + leftovers, junk cleaner, duplicates, empty folders, disk analyzer, network view, health check, Windows (Appx) apps, recovery vault. The UI language is Vietnamese by default with an English dictionary.

Read `WORKSTATE.md` for the latest task checkpoint, then `HANDOFF.md` for architecture/history and `README.md` for product behavior. Verify notes against the actual Git state; notes are not proof that a push or test succeeded.

## Build and test

- Windows: `powershell -ExecutionPolicy Bypass -File .\build.ps1 -SelfTest` (needs .NET SDK 6+; the built exe runs on the .NET Framework 4.8 that ships with Windows). The exe has a `requireAdministrator` manifest, so run `--self-test` from an elevated PowerShell.
- Linux/macOS (CI or cloud agents): `dotnet build -c Release -p:EnableWindowsTargeting=true` then `env -u DISPLAY mono bin/Release/net48/TweekPro-0.7.exe --self-test junk`. The `junk` argument runs every platform-neutral suite (`CoreTests`, `Tests07` and everything it calls) and skips registry/UI tests. Exit code 0 and `bin/Release/net48/test-results.txt` starting with `PASS` mean success.
- Layout preview without a real Windows session: `mono bin/Release/net48/TweekPro-0.7.exe --preview <remnants|apps|autorun|tools|junk|network|health|store|services|ai> [--show]` (`--show` opens the live window instead of writing `TweekPro-preview.png`). Under Mono set `MONO_WINFORMS_XIM_STYLE=disabled` and avoid keyboard input.
- Build must stay at 0 errors / 0 warnings. Every engine change needs a test in the matching `*Tests.cs`, wired into `Tests07.Run()`.

## Conventions

- One folder per feature: `Xyz/XyzEngine.cs` (pure logic, no WinForms), `Xyz/XyzSafety` or explicit allow/forbid lists, `Xyz/XyzUI.cs` (`partial class MainForm`, `BuildXyzTab()`), `Xyz/XyzTests.cs`. Register the tab in `App.cs` (`Build...Tab()` call and the `ordered` array) and add a glyph key in `Branding.GlyphKey` + `DrawTabGlyph`.
- Every destructive action goes through the vault: create a `Backup` (`Kind`, `State` = `Pending` → `BackedUp`/`NeedsReview`/`Restored`/`PendingReboot`), save with `Engine.SaveBackup`, dispatch restore in `Engine.Restore`. Manual deletion requires a confirmation dialog. AI defaults to Confirm mode; explicitly selected Auto mode runs tool actions without per-action dialogs, still through the same vault and hard safety boundaries. Unknown action modes fall back to Confirm.
- Safety boundaries live in code, never only in JSON: `JunkSafety.ForbiddenRoots`/`WindowsCoreFolders`, `DupeSafety`, `EmptyFolders.ForbiddenRoots`, `WindowsApps.Classify`. Never relax them to make a feature "work"; add tests that prove refusals.
- Strings: write Vietnamese in code and wrap with `Core.L.T("…")` / `Core.L.F("…{0}", x)`; add the English pair to `Core/LangEn.cs` or to a feature-local table registered in `Core.L` (e.g. `Pup/PupLang.cs`); a key present in two tables throws at startup (`Core/LangTests` checks tab titles, junk rules and placeholders).
- Never rewrite source files with PowerShell `Set-Content`/`Out-File` (Windows PowerShell 5.1 defaults to ANSI and destroys Vietnamese). Use the editor tools or an explicit UTF-8 read/write; files are UTF-8 (most with BOM) with CRLF. `Theme.Button/Note/HeaderBand/SetOverlay`, `SetupList` columns and `TabPage` titles translate automatically.
- Comments and documentation in English; only method-level `/// <summary>` docstrings, no comments narrating logic inside methods. Code style is compact (single-line statements, few blank lines) – match the surrounding file.
- Windows-only APIs (registry HKLM, ACL/ownership, `MoveFileEx`, PowerShell Appx cmdlets, ETW) must be guarded so the Linux self-test still passes (`StubbornFiles.IsWindows`, `PowerShell.Executable == null`, `Environment.OSVersion`).
- README.md is the user-facing manual (Vietnamese): update the tab table and the "Cập nhật 0.7" changelog for every feature.

## Git

- Work on `cursor/integration-all-features-e772` (PR #7 collects everything; PRs #1–#6 are its ancestors). Small logical commits with descriptive messages; never force-push.

## Continuity between Cursor and Codex

- Use the owner's existing checkout `C:\Users\ADMIN\TweekPro` when working locally, on `cursor/integration-all-features-e772`. Only one agent may write to this checkout at a time. Do not infer that a session stopped from an old note alone; the owner controls handoff.
- At task start, inspect branch, status and latest commits. Fetch the remote and compare before making changes. Fast-forward only when the working tree is clean and it is safe; preserve uncommitted work, and never reset/clean/stash it away automatically. A dirty tree may be the previous agent's unfinished work.
- Update `WORKSTATE.md` at the start of substantive work, after meaningful milestones, before long verification, and before handing off. Record the user's objective, completed work, pending files/steps, exact test results, blockers, and the next concrete action. Keep it short; replace stale status rather than appending an endless diary.
- Commit coherent, validated changes and push them to the existing development branch as part of the owner's established GitHub workflow. Verify remote commit identity before saying work is backed up. Do not wait until the very end of a long task to checkpoint. Do not force-push, merge main, create a release, or include secrets merely to hand off.
- If work is incomplete, preserve it and label the checkpoint clearly as incomplete, including failing tests. Local uncommitted files can be resumed in the same checkout; they are not a GitHub backup. For a remote handoff, use a clearly identified work-in-progress branch if needed rather than knowingly breaking the integration branch, and tell the next agent the exact branch and base.
- Treat repository files as the shared memory. Do not assume access to the other assistant's chat, cloud VM, credentials, model configuration or unsaved buffers. Do not rely on a guaranteed warning before a usage limit; a sudden interruption may occur before the next checkpoint.
- On handoff, finish or stop pending commands, report any still-running process, save files, update state, and report commit/branch/push status. Once handed off, do not keep editing the shared checkout until the owner returns the task.
