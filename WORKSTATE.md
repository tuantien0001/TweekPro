# Tweek Pro - current work checkpoint

Updated: 2026-09-27 (Asia/Bangkok).
Objective: owner explicitly authorized installing over the local executable and publishing GitHub version 0.8.0.
Status: releasing from main, local UI commit d288e51; origin/main 5e0e93e, fetch confirmed no incoming commits.

- Prior UI work verified: build 0 warnings/errors; full Windows self-test PASS 2026-09-27T16:10:33; VI/EN navigation and result filters PASS.
- Next: run release.ps1 -Version 0.8.0 elevated with full tests, verify remote main/tag and GitHub Actions assets, install 0.8.0 over existing Program Files installation and verify executable version/hash.
- Owner authorization supersedes previous hold on push/executable upload and installed-exe replacement. Do not skip tests or force-push.
- Existing Tweek Pro PID 15408 may need closing for replacement. Preserve user data/vault and untracked .bridge/ and .cursor/.
