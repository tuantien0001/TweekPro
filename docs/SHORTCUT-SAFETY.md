# Broken shortcut cleanup

Open **Tools > Broken shortcuts** to scan the current account and all-users Desktop, Start Menu and Startup roots. Scanning is read-only; all findings start unchecked. Select individual links or Select all, then confirm their movement to the Recovery Vault. Restore them from the existing Vault tab.

## Detection boundaries

- Only valid `.lnk` headers with a local, fully qualified DOS target on a ready fixed disk are considered. MSI advertised links (`HasDarwinID`) are excluded because a missing target can be intentional.
- Empty targets, shell namespace objects, URLs, UNC paths, relative paths, unresolved environment variables and removable/network drives are excluded.
- Each target path component is inspected. Only file/directory-not-found is treated as absence. Access denied and other I/O failures are uncertainty. Reparse points and offline/recall attributes stop classification.
- Scanning skips linked/cloud-placeholder directories, deduplicates overlapping roots, supports cancellation and is bounded to 20,000 entries, depth 16 and 30 seconds between filesystem calls. Hitting a bound is shown as incomplete. An individual operating-system call may take longer.

## Cleanup and recovery

Before each move, the engine enforces the real shortcut allowlist, checks the link SHA-256 against the preview, reads the target again and verifies that it is still missing. A pending vault manifest is written before a same-volume `File.Move`; success becomes BackedUp, failure NeedsReview. A shortcut on a different volume from the vault is left untouched. Only the link moves; the target is never executed or deleted. Existing `Kind=File` restoration refuses to overwrite a new file at the original path.

Detection is conservative and cannot guarantee that every broken shortcut is found. Findings are candidates for user review. A repaired or replaced shortcut is refused on revalidation. Filesystem operations are not a transaction with other applications; users should avoid editing shortcuts while cleanup is running.

## Verification

`ShortcutsTests.Run`, called by the normal self-test, covers normal/malformed/advertised headers; access denial, device failure, offline/reparse ancestors and unsupported targets; duplicate roots; live Windows shell links to existing/missing files and folders; target reappearance; changed shortcut contents; out-of-scope cleanup refusal; real vault round trip and restore collision; cancellation. All mutation fixtures live in a uniquely named test directory, not existing user shortcuts.

Synthetic UI preview: `TweekPro-0.7.9.exe --preview shortcuts` (add `--en` for English).

## References

- [Microsoft Shell Link LinkFlags specification](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-shllink/ae350202-3ba9-4790-9e9e-98935f4ee5af): advertised-link metadata.
- [Microsoft File.GetAttributes documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.getattributes): distinguish absence from access and I/O errors.
