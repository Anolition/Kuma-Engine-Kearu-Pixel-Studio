# Milestone 1: Editor Shell

## Target Outcome

Produce the first runnable Project S+ executable with a stable editor shell layout.

## Success Criteria

- Launches into a native window on Windows
- Window size can be configured
- Basic shell regions render consistently
- App closes cleanly without crashing
- Settings can be saved and loaded
- Theme mode can be switched and persisted
- Text is rendered in-shell with configurable font settings

## Current Shortcuts

- `F6` toggle dark/light theme
- `F7` cycle text size: `Small`, `Medium`, `Large`
- `F8` cycle readable font families
- `F9` open or close the preferences view

## Preferences View

- The preferences workspace shows current theme and typography settings
- Shortcut actions can be selected with `Up` and `Down`
- Press `Enter` to rebind the selected shortcut
- Press `Escape` while rebinding to cancel

## Home Surface

- The home tab acts as the first start page for the editor
- Starter cards expose actions like creating a project slot, opening projects, and opening preferences
- Recent projects are listed in the workspace and in the left panel
- Tabs and menu dropdowns are clickable with the mouse
- Scratch tabs can be opened and closed from the workspace tab strip

## Project Flow

- The projects page now includes a basic project creation form
- Project name and project library path fields can be edited in-app
- A simple folder browser can be opened to choose the current project library
- Recent projects can be reopened from the home page and projects page

## Shell Regions

- Menu bar
- Left panel
- Center workspace tabs
- Right panel
- Bottom status bar

## Early UX Notes

- The first version should prioritize clarity and responsiveness
- Panels can use placeholder content while the framework is built
- The center workspace should be treated as the long-term home for specialized tools

## Risks To Avoid

- Building the UI directly into one giant class
- Tying editor layout to runtime code
- Hard-coding panel behavior in a way that blocks future tools
- Overdesigning the renderer before the app shell exists

## Follow-Up Milestones

- Milestone 2: project browser and layout persistence
- Milestone 3: node editor prototype
- Milestone 4: sprite and animation workflow
- Milestone 5: pixel art tool foundation
