# Tweek Pro — current work checkpoint

Updated: 2026-09-26 16:09 (Asia/Bangkok).
Status: READY FOR HANDOFF — broken shortcut cleanup verified locally and on GitHub.
Last editor: Codex. Owner confirmed Cursor stopped and asked to resume from this file.

## Current objective and result

Continue the first outstanding roadmap item: broken Desktop / Start Menu shortcuts with vault-backed cleanup.

- Added `Shortcuts/`: bounded read-only scan of Desktop, Start Menu and Startup; skips advertised, unknown, network/removable, offline and reparse targets. Access denied does not mean missing.
- Added **Tools > Broken shortcuts…**: preview, Select all / Deselect all, cancel, explicit cleanup confirmation, per-item revalidation. Moves only `.lnk` files into the existing File vault; restore never overwrites a new destination. Different-volume shortcuts are kept.
- VI/EN translations; README pending 0.7.10 section, `docs/SHORTCUT-SAFETY.md`, synthetic dialog preview at `docs/screenshots/shortcuts.png`.
- Existing tabs recaptured with `--preview all`; Tools and dialog previews inspected in VI, dialog also in EN.

## Verification

- Release build: 0 warnings / 0 errors.
- Full Windows `--self-test`: PASS, exit 0, 2026-09-26T16:02:56 (`bin/Release/net48/test-results.txt`). Includes new target uncertainty, header, changed-link/target, out-of-scope refusal, actual shell-link vault round trip, restore collision and cancellation tests.
- `--preview all`: exit 0; dialog VI/EN: exit 0. No real user shortcut was cleaned.
- Diff whitespace check uses `git -c core.whitespace=cr-at-eol diff --check` because some existing tracked C# files store CRLF.

## Git and releases

- Feature commit `fd6e1d1a0945fd96af5844b51ee4c2d5f647e1b9` pushed to `origin/main`; local and remote hashes verified equal. This checkpoint-only commit follows it. Push each verified milestone, never force-push.
- GitHub Actions 36231657945 SUCCESS: Windows build, self-test, installer/portable packaging and artifact upload. https://github.com/tuantien0001/TweekPro/actions/runs/36231657945 (artifact `TweekPro-dist`).
- v0.7.9 is already published with Setup + portable assets at https://github.com/tuantien0001/TweekPro/releases/tag/v0.7.9 (tag commit `6aeb122`, Actions success). Old release IN PROGRESS state was stale.
- Keep application version 0.7.9 until the owner asks for a release. The new shortcut feature is source/build work, not yet in that published release.
- Preserve local untracked `.cursor/permissions.json`; do not include it in a commit.
- Keep commits, README and release notes free of other product names. Prior detailed checkpoints remain in Git history (WORKSTATE at `57c96d5`); HANDOFF.md holds architectural background.

## Local installation

Owner requested overwriting the installed copy on 2026-09-26. Replaced only `C:\Program Files\Tweek Pro\TweekPro-0.7.9.exe` at 16:08:39; dependencies/config already matched. Installed SHA-256 `3C1934198D1E11837B79888E91FBB748468D87A888E7B924DC61B17227C008A9` matches the tested build. Desktop/Start Menu shortcuts already target this exe. Previous executable and updater result are in `C:\Users\ADMIN\TweekPro-install-backup-20260926-160836`. No user data/settings/vault changed; no app process was running or started. The older 0.7.8 exe/uninstall registration were left in place; this was an executable replacement, not an installer upgrade.

## Next concrete actions

1. Owner can test the local build or the `TweekPro-dist` Actions artifact. Resume by fetching and reading this checkpoint; there is no unfinished code in this milestone.
2. Remaining roadmap: complete runtime VI/EN localization (Health/Junk/Services/Store/AI); add AI tools `list_unwanted` / `list_stale`; optional old-downloads Health finding.
3. Cut a release only when requested, using `release.ps1`; do not move the existing 0.7.9 tag.

No background test or preview process remains from this milestone. Only one agent should write to this checkout; owner controls handoff.
