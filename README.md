# Project S+

Project S+ is the working title for an open-source game engine and editor focused on a deep, node-based creation workflow.

The long-term vision is a cross-platform tool for building games with:
- A modular 2D-first runtime
- A desktop editor with dockable panels and tabbed workspaces
- Visual node-based game logic
- Built-in pixel art and animation tools
- Future support for 3D asset import and rendering

## Initial Direction

- Language: `C#`
- Platforms: `Windows` and `Linux`
- License target: open source
- Early focus: a stable editor shell that launches as a desktop executable
- Current native window layer: `Silk.NET.Windowing + OpenGL`

## Milestone 1 Goal

Create the first usable editor shell:
- Opens as a desktop app
- Has a configurable main window
- Includes a menu bar, side panels, tabs, and status bar
- Stores baseline settings like window size and theme preferences
- Establishes an architecture we can safely build on
- Supports dark and light editor themes with in-app switching
- Supports readable UI typography with configurable font family and size presets
- Includes a visual preferences view for editor settings and shortcut editing
- Includes a home/start surface with recent projects, starter actions, clickable tabs, and menu dropdowns
- Includes a basic project creation form, simple folder browser, and closable scratch tabs

## Project Layout

- `docs/` design notes, blueprints, and milestone plans
- `src/` application and engine source code
- `assets/` built-in editor assets such as icons, fonts, and themes
- `tests/` automated tests
- `tools/` helper scripts and developer utilities

## Next Step

See [docs/blueprint.md](/C:/Users/mrflu/OneDrive/Documents/Codex%20Project/docs/blueprint.md) for the full Milestone 1 blueprint.
