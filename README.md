# Kuma Engine + Kearu Pixel Studio

Kuma Engine is a custom game-creation environment currently centered on Kearu Pixel Studio, a desktop pixel art and animation editor built in C# on top of Silk.NET and OpenGL.

This repository is the active development home for the editor, runtime, and supporting tools. The public branding is now Kuma Engine + Kearu Pixel Studio, while some internal code and project names still use the earlier `ProjectSPlus` naming and will be cleaned up over time.

## Current Status

The current milestone is focused on making the 2D editor feel stable, understandable, and shippable. The editor currently includes:

- Pixel drawing and erasing tools
- Selection, transform, mirror, and rotation workflows
- Shape drawing with fill and outline modes
- Frame-based animation editing with onion skin and playback controls
- Layer management with grouping, locking, alpha lock, and opacity controls
- Palette workflows with saved palettes, working palettes, recent colors, and custom themes
- Autosave, recovery, and crash-report support

## Tech Stack

- C#
- .NET 8
- Silk.NET.Windowing
- Silk.NET.OpenGL
- Silk.NET.Input
- SixLabors.ImageSharp

## Running The App

Prerequisites:

- Windows
- .NET SDK `8.0.419` or a compatible .NET 8 SDK

Build:

```powershell
dotnet restore ProjectSPlus.sln --configfile NuGet.Config
dotnet build ProjectSPlus.sln
```

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Run-ProjectSPlus.ps1
```

## Project Layout

- `src/` application, editor, runtime, and shared code
- `assets/` bundled editor assets
- `docs/` notes, plans, and blueprints
- `tests/` automated test projects
- `tools/` helper utilities and scripts

## Data And Storage

- App-managed settings, logs, autosaves, and recovery files are stored in `%LocalAppData%\Kuma Engine`
- User artwork and exported files are saved wherever the user chooses
- The repository is not intended to be the place where normal user project saves live

## Version History

Tagged milestone versions are published in git and on GitHub:

- `kuma-engine-0.0.01`
- `kuma-engine-0.0.05`
- `kuma-engine-0.0.10`
- `kuma-engine-0.0.15`
- `kuma-engine-0.0.20`
- `kuma-engine-0.0.25`
- `kuma-engine-0.0.30`
- `kuma-engine-0.0.35`

## Roadmap Direction

The near-term goal is to finish the 2D workflow first:

- transform precision and drawing workflow polish
- stronger layer and animation control
- reliable export and project handling
- packaging and shipping readiness

After that, the longer-term plan is to use Kuma as a bridge into game-development workflows rather than rushing into a full 3D editor too early.

## Documentation

- [Blueprint](docs/blueprint.md)

## License Status

No open-source license has been added yet. Until a license is chosen and committed, the project should be treated as all rights reserved.
