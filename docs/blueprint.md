# Project S+ Blueprint

## Purpose

Project S+ is an open-source, cross-platform game engine and editor designed around a serious visual workflow. The system should feel approachable for creators while staying modular enough for long-term technical growth.

The core design rule is:

`Keep the runtime stable and keep advanced tooling layered on top of it.`

That rule helps us add new systems without ripping out old ones.

## Product Vision

Project S+ should eventually support:
- A custom editor application
- Dockable panels and tabbed workspaces
- Node-based logic authoring
- Scene editing
- Sprite, animation, and tile workflows
- Built-in pixel art tooling
- Import of images, audio, fonts, and later 3D assets
- Extensible modules and plugins for contributors

## Technical Direction

### Chosen Stack

- Language: `C#`
- Runtime: `.NET`
- Target platforms: `Windows` and `Linux`
- Windowing and input: `Silk.NET.Windowing`
- Rendering backend for first phase: `OpenGL`

### Why This Stack

- `C#` gives us a much safer and faster development loop than `C++`
- `.NET` is a practical choice for a cross-platform open-source desktop app
- `Silk.NET.Windowing` gives us a clean cross-platform native window layer from C#
- `OpenGL` is a practical first renderer and keeps the first milestone achievable

## Architecture

Project S+ should be built in layers.

### Layer 1: Core

Shared low-level systems:
- Logging
- Timing
- File system access
- Math types and helpers
- Serialization
- Events and messaging
- Configuration loading and saving

### Layer 2: Runtime

Engine-facing systems used by both the editor and future shipped games:
- Application host
- Window abstraction
- Renderer abstraction
- Input system
- Asset system
- Scene model
- Entity/component model
- Project model

### Layer 3: Editor

Desktop tooling and workflow systems:
- Main editor shell
- Docking layout management
- Tabs and panel registry
- Menu and command system
- Theme and UI settings
- Property inspector foundation
- Project browser foundation
- Status and notifications

### Layer 4: Tools

Feature-specific creator tools that plug into the editor:
- Node graph editor
- Pixel art editor
- Animation editor
- Tilemap editor
- Asset import tools

### Layer 5: Plugin API

Contributor-facing extension surface:
- Register panels
- Register importers
- Register commands
- Register editor tools

## Dependency Rules

To keep the project safe to grow:
- `Core` must not depend on editor code
- `Runtime` may depend on `Core`
- `Editor` may depend on `Runtime` and `Core`
- `Tools` may depend on `Editor`, `Runtime`, and `Core`
- Plugins should use public APIs and avoid internal engine details

This separation is what protects us from destructive rewrites later.

## Milestone 1

### Goal

Build the first editor shell that launches cleanly as a desktop executable and proves the layout foundation.

### Milestone 1 Deliverables

- Application opens into a desktop window
- Window title, width, height, and startup mode are configurable
- Basic top menu bar
- Left and right side panels
- Central tabbed workspace
- Bottom status bar
- Simple default theme
- Dark/light theme support with persistent preference
- Readable text rendering for menu, tabs, panels, and status areas
- Typography settings for preferred font family and size preset
- A visual preferences workspace for core editor settings and shortcut configuration
- A home/start page with starter actions, recent projects, and clickable navigation
- Settings file for window and UI preferences
- Clean startup and shutdown flow
- A code structure that future tools can plug into

### What Milestone 1 Will Not Include

To keep the first slice healthy, Milestone 1 should not try to include:
- Full node graph logic
- Full scene editing
- Pixel art editing features
- Import pipeline
- Physics
- Audio tooling
- 3D rendering

Those come after the shell is stable.

## Initial UI Layout

The first launch layout should feel like a real editor, even if the content is minimal.

### Default Layout

- Top: application menu bar
- Left: project or explorer panel
- Center: welcome tab and empty workspace tabs
- Right: inspector panel
- Bottom: status bar and message area

### UI Design Priorities

- Stable layout behavior over flashy visuals
- Clear panel boundaries
- Readable typography
- Consistent spacing and alignment
- Persistent user preferences
- Layout system designed for future docking and detachable tools

## Milestone 1 Module Plan

We should start with these modules:

### `ProjectSPlus.Core`

Responsibilities:
- Logging
- Config files
- Shared utility types

### `ProjectSPlus.Runtime`

Responsibilities:
- App bootstrap
- Window settings
- SDL host
- Render loop

### `ProjectSPlus.Editor`

Responsibilities:
- Main editor shell
- Panels
- Tabs
- Menu model
- Theme model
- Status bar

### `ProjectSPlus.App`

Responsibilities:
- Entry point
- Composition root
- Startup wiring

## Suggested Folder Structure

```text
Project S+/
  assets/
    icons/
    themes/
  docs/
    blueprint.md
  src/
    ProjectSPlus.App/
    ProjectSPlus.Core/
    ProjectSPlus.Runtime/
    ProjectSPlus.Editor/
  tests/
    ProjectSPlus.Core.Tests/
  tools/
```

## First Implementation Order

The safest first coding sequence is:

1. Create the solution and projects
2. Build the app entry point
3. Add window configuration and settings loading
4. Open the SDL window
5. Add the editor shell layout regions
6. Add menu bar and status bar foundations
7. Add simple panel registration
8. Save and reload window settings on close

## Settings To Support Early

The first settings file should support:
- Window width
- Window height
- Window start position
- Window maximized state
- Theme name
- Last opened project path

## Open Source Readiness

We should prepare for contributors from the start:
- Keep module boundaries obvious
- Use readable naming
- Document public systems
- Prefer small, composable classes over giant managers
- Add a contributor guide early
- Avoid hidden coupling between runtime and editor systems

## Immediate Next Work

After this blueprint, the next practical work item should be:

`Scaffold the C# solution and implement the first app window shell.`
