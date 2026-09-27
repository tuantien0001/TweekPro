# Tweek Pro - current work checkpoint

Updated: 2026-09-27 (Asia/Bangkok).
Objective: simplify sidebar for nontechnical users, keep Applications/Cleanup fully expanded and make uninstall easy to find.
Status: implemented locally and opened for owner review. DO NOT PUSH or replace installed build until owner approves this iteration (explicit latest instruction). Base main/origin 2ee0c71.

- All three sidebar groups permanently expanded; headings are labels with no collapse action. Compact rows; prominent bold blue Uninstall apps link (VI: Go ung dung with native Vietnamese text). Tab switches no longer auto-scroll sidebar away from top.
- VI/EN translations and README pending 0.8.1 notes updated. All 22 tab screenshots regenerated locally in both languages; Vietnamese docs/screenshots; English in %TEMP%/TweekProUIReview-d929723cda3c4a848c31de0c485f5ef9/en.
- Build: 0 warnings/errors. Full self-test attempt did not run because Windows elevation was cancelled; do not claim a new full PASS. VI/EN UI smoke PASS: 21 clickable/keyboard-focusable links, groups stay expanded through every selection, six cleanup filters correct. git diff --check passed.
- Review app launched from workspace bin/Release/net48/TweekPro-0.8.0.exe, PID 49596, title confirmed. Existing installed 0.8.0 remains unchanged. User should review the workspace window.
- Next: collect owner feedback/approval, rerun full elevated self-test before distribution, then update GitHub and latest screenshots only after approval. No new tag/release created.
- Previous v0.8.0 Release and installed build verified in prior checkpoint; installation backup remains %LOCALAPPDATA%/TweekPro-install-backup-20260927-161501.
- Preserve untracked .bridge/ and .cursor/. No verification commands still running; review app intentionally remains open. This local checkpoint is not a GitHub backup.
