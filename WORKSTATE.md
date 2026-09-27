# Tweek Pro - current work checkpoint

Updated: 2026-09-27 (Asia/Bangkok).
Objective: install over local executable and publish GitHub version 0.8.0. Owner explicitly authorized both.
Status: complete. Release v0.8.0 published; local installation updated successfully.

- Release commit 86f0e26; remote annotated v0.8.0 peeled to that commit. GitHub Actions run 36308801882 completed SUCCESS (build, full tests, installer/portable and Release upload).
- Release: https://github.com/tuantien0001/TweekPro/releases/tag/v0.8.0 with Setup.exe and portable.zip. Main also includes 870e2b3: wait for screenshot process in release.ps1 and complete 0.8.0 gallery; no tag moved.
- Full local release self-test PASS 2026-09-27T16:15:17. Installed executable full self-test PASS 2026-09-27T16:20:30. Installed/build SHA256 both 5E5A9DA31DF9AE948439C09DED02B4BE25B9BABB92F3B99F3CCD1E9017C8D684.
- Installed via locally compiled Inno Setup from the tested 0.8.0 output (GitHub downloads were slow and cancelled). Setup exit 0, no restart required. Program Files now has only TweekPro-0.8.0.exe; uninstall registry DisplayVersion 0.8.0; Start Menu shortcut points to it. Local installer: dist/TweekPro-0.8.0-Setup.exe.
- Previous installation backup: %LOCALAPPDATA%/TweekPro-install-backup-20260927-161501. User data/vault retained. Old app closed gracefully; new app launched as PID 50976. No verification/download commands remain running.
- Preserve untracked .bridge/ and .cursor/. Logs: .bridge/release-080.log, install-080.log, setup-080.log. All project changes committed and pushed; next action is owner's normal use/feedback.
