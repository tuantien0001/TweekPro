# Tweek Pro — current work checkpoint

Updated: 2026-09-21 18:40 (Asia/Bangkok).
Status: IN PROGRESS — Cursor working through the roadmap (HANDOFF.md §5). Sole writer since the localization session stopped at `e7c842b`.
Last editor: Cursor.

## Owner objective (2026-09-21 18:04)

"Icon fixed. Move the AI tab to the end, continue the project, self-improve, upgrade into a deep Windows management super-app." Commit + push each verified part to `cursor/integration-all-features-e772`; never merge main.

## Done this session

- Taskbar icon `30d7a30`; AI tab last `74f2717` (owner confirmed both).
- **PUP / bloatware detector v1** (this commit): `Pup/PupDetector.cs` (rules, `PupSafety` trusted publishers/names, Appx protected/caution never flagged, rule-file validation refuses broad globs), `Pup/pup-rules.json` (embedded, 11 rules), `Pup/PupLang.cs` (English table merged by `Core.L`, duplicate keys throw), `Pup/PupTests.cs` wired in `Tests07.Run()`. UI: Flag column + «Chỉ hiện mục cảnh báo» filter + note in Applications; Flag column in Windows Apps; 7th Health area "Ứng dụng không mong muốn" (`HealthInputs.PupCount/PupHigh`). `--preview apps`. README + AGENTS updated.
- Verified: Release build 0/0; `--self-test` PASS (includes PupTests + updated HealthTests); previews `apps`/`health` rendered with correct Vietnamese; live inventory (101 apps) flags 2 (Adobe Genuine Service → bundled).

## Incident (resolved, lesson in AGENTS.md)

A PowerShell `Set-Content` rename corrupted App.cs Vietnamese to `?`; restored via `git checkout -- App.cs` and re-applied the PUP edits with an explicit UTF-8 script. All edited files scanned: no `?`/mojibake damage.

## Next concrete action

3. Stale large files (Downloads/Desktop installers older than N days, vault `Kind=Stale`) — engine + safety + tests + UI + README.
4. Startup impact + broken shortcuts.
1. Localization completion: Health/Junk/Services/Store/AI runtime strings, `LangTests` source scan for unwrapped `Log("…")`.
Optional follow-up for PUP: AI tool `list_unwanted`, CSV export of flags, one-click "select all flagged".

## Git

`cursor/integration-all-features-e772`; PR #7. Push after each verified commit.
