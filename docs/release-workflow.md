# Release Workflow

This repository now includes a repeatable Windows publish script for Kuma Engine + Kearu Pixel Studio.

## Current Goal

The script is meant to create a stable release folder before a future installer or richer packaging step is added.

## Publish A Windows Build

From the non-OneDrive repository root. On the primary dev machine this is:

```text
C:\Dev\Kuma-Engine-Kearu-Pixel-Studio
```

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Publish-KumaEngine.ps1 -Version 0.0.40 -RuntimeIdentifier win-x64 -Archive
```

This publishes the desktop app to:

```text
artifacts\releases\kuma-engine-<version>-<runtime>
```

If `-Archive` is included, it also creates a `.zip` beside that folder.

## Useful Options

- `-Version 0.0.40`
- `-Configuration Release`
- `-RuntimeIdentifier win-x64`
- `-SelfContained`
- `-SingleFile`
- `-Archive`

Example with a self-contained release:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Publish-KumaEngine.ps1 -Version 0.0.40 -RuntimeIdentifier win-x64 -SelfContained -Archive
```

## Ship-Readiness Check

Before publishing or tagging:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-KumaShipReadiness.ps1
```

This check refuses OneDrive-backed source/output paths, scans for stale `Frog 1` / `Frog 2` labels and old OneDrive-specific workflow text, builds the app, and verifies the required sound assets are present in the output.

## Notes

- The publish script performs a runtime-specific restore before publish.
- The run, publish, and ship-readiness scripts refuse to operate from a OneDrive-backed checkout.
- The publish output includes the app assets and a `release-manifest.txt`.
- The script also copies the repository `README.md` into the release folder for reference.
- Current internal assembly names still use `ProjectSPlus`, but published metadata now identifies the product as Kuma Engine + Kearu Pixel Studio.

## Recommended Release Checklist

- Build and publish from the `C:\Dev\Kuma-Engine-Kearu-Pixel-Studio` repository only
- Run `tools\Test-KumaShipReadiness.ps1`
- Launch the published app once before shipping it
- Verify settings, logs, and recovery files land in `%LocalAppData%\Kuma Engine`
- Test open, save, import, export, autosave, and recovery on the published build
- Keep the version tag and release artifact version aligned
