# Tweek Pro - current work checkpoint

Updated: 2026-09-27 (Asia/Bangkok).
Objective: owner explicitly authorized installing over the local executable and publishing GitHub version 0.8.0.
Status: release commit 86f0e26 and annotated v0.8.0 pushed; remote main and peeled tag identity verified. GitHub run 36308801882 building assets; installation pending.

- Prior UI work verified: build 0 warnings/errors; full Windows self-test PASS 2026-09-27T16:10:33; VI/EN navigation and result filters PASS.
- Release self-test PASS 2026-09-27T16:15:17. Next: await GitHub assets, install 0.8.0 over Program Files and verify version/hash. Local installation backed up at %LOCALAPPDATA%/TweekPro-install-backup-20260927-161501.
- Release preview command returned before PNG capture finished; fixed release.ps1 to wait and check process exit status. All 22 tab captures now complete; committing refreshed gallery separately without moving the published tag. PowerShell parse and git diff --check pass.
- Owner authorization supersedes previous hold on push/executable upload and installed-exe replacement. Do not skip tests or force-push.
- Existing Tweek Pro PID 15408 may need closing for replacement. Preserve user data/vault and untracked .bridge/ and .cursor/.
