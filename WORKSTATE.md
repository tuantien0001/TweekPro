# Tweek Pro — current work checkpoint

Updated: 2026-09-21 18:05 (Asia/Bangkok).
Status: IN PROGRESS — forcing Win11 taskbar to use 256px TPC icon (shell was caching old T.).
Last editor: Cursor.

## Active task

Owner: taskbar still shows old "T." while title bar / header show TPC.
PE resource already has TPC; Win11 ignores 32px Form.Icon for the taskbar and keeps a stale shell cache for the exe path.
Code change: `Branding.ApplyWindowIcons` sends WM_SETICON with a 256px ICON_SMALL + ICON_BIG; `RegisterAppUserModelId` (`TweekPro.App.0.7.tpc`) before `Application.Run`. Then rebuild, refresh Program Files, clear icon cache.

Preserved uncommitted localization edits from the other pass — do not discard (`Cleaner/EmptyFolderUI.cs` and any other open UI files).

## Next

1. build.ps1 -SelfTest
2. Update Program Files exe; clear Explorer icon cache
3. Owner relaunches and confirms taskbar
4. Commit + push
