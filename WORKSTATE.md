# Tweek Pro — current work checkpoint

Updated: 2026-09-21 19:05 (Asia/Bangkok).
Status: IN PROGRESS — Cursor working through the roadmap (HANDOFF.md §5). Sole writer since the localization session stopped at `e7c842b`.
Last editor: Cursor.

## Owner objective (2026-09-21 18:04)

"Icon fixed. Move the AI tab to the end, continue the project, self-improve, upgrade into a deep Windows management super-app." Commit + push each verified part to `cursor/integration-all-features-e772`; never merge main.

## Done this session (all pushed unless noted)

- Taskbar icon `30d7a30`; AI tab last `74f2717` (owner confirmed both).
- PUP / bloatware detector v1 `b1b5962`: `Pup/` (rules JSON, `PupSafety`, `PupLang`, tests), Flag column in Applications + Windows Apps, 7th Health area, `--preview apps`.
- **Old Downloads (stale files) v1** (this commit): `Stale/StaleFinder.cs` (scan Downloads/Desktop for installers, archives, disk images, partial downloads, large files older than N days; `StaleSafety` allow = Downloads/Desktop only, deny = Documents/media/cloud/system/vault; quarantine `Kind=Stale` with `files.xml` layout; restore never overwrites), `Stale/StaleUI.cs` tab "Tệp tải về cũ" (options: Downloads/Desktop, age days, large MB; vault/direct modes; open location), `Stale/StaleLang.cs`, `Stale/StaleTests.cs` wired in `Tests07`. Settings `staleMinAgeDays=30`, `staleLargeMB=200`, `staleIncludeDesktop` with `[OnDeserializing]` defaults for old files. `Engine.Restore` dispatch, vault kind label, Branding glyph `download`, `LangTests` tab list, README, AGENTS preview list, `--preview stale`.
- Verified: Release build 0/0; `--self-test` PASS; previews `apps`/`health`/`stale` render correct Vietnamese.

## Lessons (also in AGENTS.md)

Never rewrite sources with PowerShell `Set-Content` (ANSI). The editor's string-replace tool now writes non-ASCII into `App.cs` as `?` (encoding cache after the incident) — for `App.cs` keep new text ASCII (move Vietnamese literals into feature classes, e.g. `StaleFinder.BackupKindLabel()`), or use an explicit UTF-8 script. Always scan edited files for `?`/mojibake before committing.

## Next concrete action

4. Startup impact + broken shortcuts (Autorun tab: measure exe size/signature/last-run, flag missing targets; Start Menu/Desktop `.lnk` with missing targets → vault `Kind=File`).
1. Localization completion: Health/Junk/Services/Store/AI runtime strings; `LangTests` source scan for unwrapped `Log("…")`.
Optional: Health area for stale downloads (reuse `StaleFinder.Scan` on Downloads, read-only); AI tools `list_unwanted` / `list_stale`.

## Git

`cursor/integration-all-features-e772`; PR #7. Push after each verified commit.
