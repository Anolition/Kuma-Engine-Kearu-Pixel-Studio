# Kuma Engine + Kearu Pixel Studio

## Competitive Feature Gap Analysis

Date: 2026-04-21

This document compares the current Kuma / Kearu Pixel Studio workflow against a small set of strong reference tools:

- Aseprite
- Pixelorama
- Piskel
- Krita animation
- Godot 2D pipeline
- Unity 2D pipeline

The goal is not to copy everything those tools do. The goal is to identify the highest-value missing features for Kuma's current phase work, especially the features that make a pixel editor feel production-ready for real art and game workflows.

## Local Reality Check

Before comparing against outside tools, the current repo already appears to cover a meaningful amount of the "basic editor" layer:

- transform with pivot and rotation snap
- mirror drawing with axis controls
- grouped layers
- autosave and crash recovery
- hidden-layer edit warnings
- palette libraries and palette locking
- onion skin controls for previous/next
- frame loop in / loop out controls
- forward and bounce playback modes
- GIF / PNG export
- shape tools

That matters because the next gains are no longer about adding random buttons. The next gains are about workflow leverage, export reliability, and reducing friction between creation and game use.

## What Comparable Tools Add That Kuma Still Lacks

### 1. Animation Structure Beyond Raw Frames

This is the biggest remaining gap.

Reference tools:

- Aseprite supports tags for named animation ranges and lets each tag define playback direction such as forward, reverse, or ping-pong.
- Aseprite and Pixelorama both support linked cels so repeated content can be shared across frames instead of duplicated.
- Krita exposes playback range directly in the timeline workflow.

What Kuma is still missing:

- named animation tags or clips
- reverse playback mode as a first-class mode
- per-tag export targeting
- linked cels / reused frame content
- stronger playback range editing and visualization
- cel-level properties such as opacity or offset

Why this matters:

- Once users build more than one animation in the same file, raw loop in / out stops being enough.
- Tags are the bridge between "drawing frames" and "shipping an animation set."
- Linked cels are a huge quality-of-life feature for static parts, held poses, and repeating content.

### 2. Layer System Depth

Kuma's layer ordering, grouping, visibility, and warnings are in much better shape now, but comparable tools go further.

Reference tools:

- Pixelorama exposes blend modes, clipping masks, and non-destructive layer effects.
- Pixelorama also supports audio layers and tilemap layers as layer types.

What Kuma is still missing:

- layer blend modes
- clipping masks
- reference layers
- "ignore in onion skin" layer property
- per-layer non-destructive effects
- audio layers for animation timing

Why this matters:

- Blend modes and clipping masks dramatically increase how much art can be built without destructive edits.
- Reference and onion-ignore states help animators avoid visual noise.
- Audio layers become important the moment animation goes beyond silent loops.

### 3. Game-Ready Export Metadata

This is where game engines and mature sprite tools start to pull ahead.

Reference tools:

- Aseprite can export sprite sheets with JSON data, slices, tags, layer splits, and batch-oriented CLI workflows.
- Unity's Sprite Editor supports automatic slicing and configurable pivots.
- Unity Sprite Atlas and Aseprite CLI both point toward a stronger atlas/export pipeline than Kuma currently has.

What Kuma is still missing:

- JSON metadata export for sprite sheets
- named slice regions
- pivot metadata per exported sprite or slice
- trim / padding / extrude controls
- texture atlas packing options
- export presets for engine targets
- batch or command-line export

Why this matters:

- For real game development, the image file alone is rarely enough.
- Engines and pipelines usually need frame rectangles, pivots, tags, slices, padding rules, and naming stability.
- This is one of the highest-value "ship readiness" upgrades Kuma can make.

### 4. Tile and Level-Art Workflow

Comparable tools increasingly treat tiles as a first-class workflow rather than a side export.

Reference tools:

- Aseprite supports tilemap layers with tilesets.
- Pixelorama supports rectangular, isometric, and hexagonal tilemaps.
- Godot's TileSet workflow emphasizes painting, large-scale placement, and tile metadata such as collision and navigation.
- Unity has Tile Palette and tile asset workflows.

What Kuma is still missing:

- tileset management
- tilemap layers
- tile palette panel
- smart import of tilesheets
- tile metadata or export metadata hooks
- optional collision / marker metadata planning for future engine use

Why this matters:

- Tile workflows are one of the strongest bridges between pixel art and engine building.
- If Kuma eventually feeds directly into Kuma Engine, this is a high-value long-term investment.

### 5. Import Intelligence

Right now Kuma has file operations and export coverage, but not much "smart import" behavior.

Reference tools:

- Aseprite and Unity both support sprite slicing workflows.
- Pixelorama supports spritesheet import with manual grid settings and smart slicing behavior.

What Kuma is still missing:

- smart sprite sheet import
- grid by cell size / cell count import
- automatic transparency-based slice detection
- editable import preview before commit
- import as frames vs import as layer vs import as tileset

Why this matters:

- Import friction is a hidden productivity killer.
- Good import tools save artists from doing repetitive manual setup work.

### 6. Deliberate Color Modes

This one needs to be handled carefully because Kuma recently fixed a major palette architecture issue.

Reference tools:

- Pixelorama supports both RGBA and Indexed projects.
- Indexed mode intentionally ties pixels to palette entries.

What Kuma should not do:

- reintroduce implicit palette-to-canvas coupling in normal RGBA editing

What Kuma could add later, safely:

- explicit project-level Indexed mode
- palette remap as a separate deliberate action
- palette reduction / quantization tools
- color replace by exact match or tolerance

Why this matters:

- Indexed workflows are powerful for retro art, but only if they are a clearly separate mode.
- Kuma's current RGBA workflow should stay decoupled from palette switching.

### 7. Brush and Pixel-Tool Depth

Kuma's core brush work is improving, but comparable tools usually go further here too.

Reference tools:

- Pixelorama highlights specialized rotation and scaling algorithms tuned for pixel art, plus brush systems and shading tools.

What Kuma is still missing:

- image/stamp brushes
- custom brush library
- shading or material-aware pixel tools
- line / curve / polyline tools if not already added later
- fill variants such as contiguous / global / tolerance options
- more pixel-art-specific transform sampling controls

Why this matters:

- Once the base editor is stable, tools that reduce repetitive drawing work become very noticeable quality multipliers.

## Highest-Value Missing Features By Current Phase

### Finish Next In Phase 4

These are the strongest animation/control additions to do before jumping further:

- named animation tags
- reverse playback mode
- better playback range UI and clarity
- clearer per-frame duration editing
- linked cels
- optional "ignore in onion skin" layer flag

Reason:

- This completes the jump from "frames exist" to "animation workflow exists."

### Best Additions For Phase 5

- palette remap tool as an explicit action
- replace color / replace ramp tools
- optional indexed mode planning, but only as a separate document mode
- export-friendly palette presets

Reason:

- These build on the recent palette redesign without undoing it.

### Best Additions For Phase 6 and 7

- export presets
- sprite sheet JSON metadata export
- slices and pivot metadata
- smart import slicing
- batch / CLI export
- recent exports / target profiles

Reason:

- These features make Kuma useful in a real external production pipeline, not just as a standalone editor.

### Strong Bridge Features Before Any Serious 3D Expansion

- tilesets
- tilemap layers
- game-engine-friendly atlas export
- metadata for pivots, slices, and named clips

Reason:

- These are better 2D-to-game-dev bridge features than jumping directly into a 3D editor.

## Recommended Next Execution Order

If the goal is to keep moving forward without destabilizing the editor, this is the strongest order:

1. Animation tags and playback range polish
2. Linked cels
3. Layer blend modes and clipping masks
4. Sprite sheet metadata export
5. Smart sprite sheet import
6. Tile / tileset / tilemap foundations
7. Audio layers
8. Optional indexed mode as a separate project mode

## Features To Avoid Adding Too Early

These are attractive, but they should not come before the items above:

- full 3D scene editing
- node graph systems
- complicated shader effect stacks
- advanced packaging UI
- plugin ecosystems

Why:

- Kuma still gets more value from tightening the 2D production path than from expanding sideways.

## Summary

Kuma is no longer missing the basic "can I draw and animate?" layer.

The largest remaining gaps compared with strong pixel tools are:

- animation organization
- linked content reuse
- deeper layer compositing
- game-ready export metadata
- smart import
- tileset/tilemap workflows

If we solve those well, Kuma stops feeling like a promising editor and starts feeling like a practical production tool.

## Sources

- Aseprite linked cels: https://www.aseprite.org/docs/linked-cels/
- Aseprite tags: https://www.aseprite.org/docs/tags/
- Aseprite tilemap: https://www.aseprite.org/docs/tilemap/
- Aseprite CLI: https://www.aseprite.org/docs/cli/
- Aseprite sprite sheets: https://www.aseprite.org/docs/sprite-sheet/
- Pixelorama homepage: https://pixelorama.org/
- Pixelorama layers: https://pixelorama.org/concepts/layer/
- Pixelorama tilemaps: https://pixelorama.org/user_manual/tilemaps/
- Pixelorama CLI: https://pixelorama.org/user_manual/cli/
- Pixelorama color mode: https://pixelorama.org/concepts/color_mode/
- Piskel homepage: https://www.piskelapp.com/
- Krita timeline docker: https://docs.krita.org/en/reference_manual/dockers/animation_timeline.html
- Krita audio for animation: https://docs.krita.org/en/reference_manual/audio_for_animation.html
- Godot TileSet workflow: https://docs.godotengine.org/en/stable/tutorials/2d/using_tilesets.html
- Unity Sprite Editor: https://docs.unity3d.com/ru/2021.1/Manual/SpriteEditor.html
- Unity Sprite Atlas: https://docs.unity3d.com/ru/2019.4/Manual/class-SpriteAtlas.html
