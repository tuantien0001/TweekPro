# Tweek Pro — current work checkpoint

Updated: 2026-09-21 ~19:00 (Asia/Bangkok).
Status: Update check v1 DONE (this agent). Startup/Tools still WIP in the working tree (other agents).
Last editor: Cursor (Update agent).

## Owner objective (this job)

In-app update detection from GitHub — honest about no live-patch without download; poll Releases (fallback tags); opt-in UI; `Update/` folder; build+SelfTest; commit+push.

## Done (Update)

- `Update/UpdateCheck.cs`: GitHub Releases/latest vs `MainForm.Version`; 404 → newest `v*` tag; asset prefer `.exe`/setup then `.zip`; no silent overwrite.
- `Update/UpdateUI.cs`: `CheckForUpdates()` (opt-in MessageBox → open download/release URL).
- `Update/UpdateLang.cs` + `Update/UpdateTests.cs` (no network); wired in `Core/L` and `Tests07`.
- Overview button in `Health/HealthUI.cs`; Tools tab may call the same method (thin hook in local AdvancedUI — may be uncommitted with Tools WIP).
- README: limits + publish note + changelog bullet.
- Verified: Release build 0/0; `--self-test` PASS.

## Not this job (leave for other agents)

- `Startup/*`, Advanced autorun columns, Health AutorunBroken — do not own.
- Tools icons / catalog expansion in `AdvancedUI` / `Advanced.cs`.

## Next concrete action (owner / other agents)

1. Publish a GitHub Release (tag `v*`) so in-app check sees a real installer URL.
2. Startup impact + Tools icons agents finish and push their files.
3. Localization completion (HANDOFF §5).

## Git

`cursor/integration-all-features-e772`; PR #7. Push Update commit after green SelfTest.
