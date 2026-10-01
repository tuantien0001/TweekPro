# Tweek Pro - current work checkpoint

Updated: 2026-10-01 (Asia/Bangkok).
Objective: install the verified header/Hunter changes locally and overwrite existing GitHub release downloads, explicitly requested by the owner.
Status: complete. Updated 0.8.1 installed locally and existing v0.8.1 Setup/portable assets replaced; tag unchanged (14be977).

- Installed exe: C:/Program Files/Tweek Pro/TweekPro-0.8.1.exe, SHA256 613039FF3F1553D4801BF1D91925294936656C7548259B9444E9D2F9B7E327A6, matches verified build. Old 0.8.0 exe removed by installer; desktop shortcut targets 0.8.1. Installed full self-test exit 0, PASS 2026-10-01T23:46:54. Installed app opened successfully, PID 51412 intentionally left running for review.
- Source 3c376f2: build 0 warnings/errors and full tests passed; VI/EN UI checks passed. Clean packages contain runtime files only. Setup SHA256 82717B96F300CDA20EED6C78FA07C8480CFE148E9CBFC551C968681CAE45952E; portable SHA256 FE88C5E9BEF333B5BD8268D190CF110A4D3A40AA9C61EE1674774EACEFC16784. Both public GitHub downloads fetched and byte-for-byte verified against local packages.
- Backups: %LOCALAPPDATA%/TweekPro-install-backup-20261001-234632/installation and %LOCALAPPDATA%/TweekPro-release-backup-20261001 (old GitHub assets, digest verified). Release note and README identify the refreshed 01/10 downloads; no new version/tag.
- Preserve untracked .bridge/ and .cursor/. No verification commands remain running. Next: owner review; no implementation, installation or upload steps remain.
