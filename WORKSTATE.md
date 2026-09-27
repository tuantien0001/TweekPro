# Tweek Pro - current work checkpoint

Updated: 2026-09-27 (Asia/Bangkok).
Objective: simplify sidebar for nontechnical users, keep Applications/Cleanup fully expanded and make uninstall easy to find.
Status: owner approved the flat sidebar (2026-09-27 16:48). Installed exe overwritten with the Release build (hash ED6B264E…, shortcut points at TweekPro-0.8.0.exe). Pushing the four sidebar commits plus this checkpoint; screenshots in docs/screenshots are from this UI.

- One flat vertical list of 21 feature links with no group headings or nested panels. Uninstall apps remains bold blue directly below Overview; same order as previous review.
- VI/EN translations and README pending 0.8.1 notes updated. All 22 tab screenshots regenerated locally in both languages; Vietnamese docs/screenshots; English in %TEMP%/TweekProUIReview-c31ab63717ac4c8ba8853d5aed663cbd/en.
- Build: 0 warnings/errors. Elevated `--self-test` PASS 2026-09-27T16:46:08. VI/EN UI smoke PASS: 21 clickable/keyboard-focusable links, flat list contains exactly 21 buttons with no captions; no scrollbar or clipped links at 1120x700 and 1280x820 in VI/EN, six cleanup filters correct. git diff --check passed.
- Review app launched from workspace bin/FitReview/TweekPro-0.8.0.exe, PID 46648. Existing installed 0.8.0 remains unchanged. User should review the workspace window.
- Next: nothing pending for this iteration. No new tag; v0.8.0 release assets stay as published. README gallery updates from the pushed screenshots.
- Previous v0.8.0 Release and installed build verified in prior checkpoint; installation backup remains %LOCALAPPDATA%/TweekPro-install-backup-20260927-161501.
- Preserve untracked .bridge/ and .cursor/. No verification commands still running; review app intentionally remains open. Older review PIDs 49596, 46340 and 51232 were not closed. This local checkpoint is not a GitHub backup.
