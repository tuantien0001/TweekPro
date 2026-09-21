# TweekPro icon handoff — approved sample 2

Status: ASSETS ONLY. The owner selected sample 2 (AI PC Manager) and explicitly requested a new folder without editing existing project files. No application, installer, build configuration, or existing icon has been changed by this asset delivery.

## Files

- `TweekPro-TPC-AI.ico`: Windows icon containing 16, 20, 24, 32, 40, 48, 64, 96, 128, 192 and 256 pixel frames.
- `png/`: individual transparent RGBA PNGs at 16, 20, 24, 28, 32, 40, 48, 64, 96, 128, 192, 256, 512 and 1024 pixels.
- `source/TweekPro-TPC-AI-master.png`: byte-for-byte copy of the approved generated master.
- `manifest.json`: dimensions, sizes and SHA-256 checksums.
- `DESIGN-PROMPT.md`: design provenance and original prompt.

The approved image has been preserved; smaller sizes are Lanczos resamples, not redesigned variants. Its fine PC lettering may become indistinct at small sizes. The monitor silhouette and application tiles remain the primary visual identity. Transparency outside the rounded tile is preserved.

## Integration instructions for the next agent

Perform integration only as part of the owner's subsequent application work. This folder does not activate the icon automatically.

1. `TweekPro.csproj` currently uses `<ApplicationIcon>TweekPro.ico</ApplicationIcon>`. Use the provided multi-frame ICO for the executable icon, either by referencing it or replacing the root icon during authorized integration.
2. `Branding.cs` currently draws the logo in `DrawLogo`, creates window icons through `AppIcon`, and exports a generated icon through `WriteIconFile`. Update these paths consistently to use the approved assets. Include resources explicitly if loading embedded assets; avoid relying on the working directory. Retain correct ownership and disposal of images, Icon objects, streams and native handles.
3. `App.cs` obtains the main window icon through `Branding.AppIcon(32)` and disposes it when the form closes. Its `--export-icon` command calls `Branding.WriteIconFile`; ensure this command will not regenerate the previous logo.
4. `installer/TweekPro.iss` uses `SetupIconFile=..\TweekPro.ico`; the uninstall entry and shortcuts obtain the installed executable's icon. Keep the installer, executable and runtime branding consistent.
5. Review all `Branding.DrawLogo` call sites, including headers, for the approved new artwork.
6. After integration, build and inspect title bar, taskbar, Explorer, shortcuts and installer at normal and high DPI (125%, 150%, 200%). Verify small icon readability and image-resource lifetime. This assets-only delivery did not build or launch the app.

The above integration points were read from the project at handoff time. Recheck current code before editing because another agent is actively developing the application.
