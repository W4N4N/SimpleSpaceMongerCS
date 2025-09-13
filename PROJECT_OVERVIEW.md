# SimpleSpaceMongerCS — Project Overview

This document explains the repository layout, design decisions and the functional specification so another developer or an automated agent can quickly understand and extend the project.

## Goal
A simple WinForms treemap viewer that visualizes folder sizes for a selected root folder and lets the user inspect, open and navigate (zoom) into folders.

## Project structure (key files)
- `MainForm.cs` — Primary UI: treemap rendering, interaction (hover, click, context menu), menus and application glue.
- `Program.cs` — Standard WinForms program entrypoint.
- `Services/FileScanner.cs` — Scans a folder tree and returns aggregated sizes per directory. Key API: `FileScanner.ScanPath(string root, IProgress<int> progress)`.
- `Controls/BufferedPanel.cs` — Small derived `Panel` enabling flicker-free double buffering for the treemap area.
- `Utils/GraphicsHelpers.cs` — Visual helper functions (human-readable byte formatting, color generation, text brush selection, HSL conversion).
- `SimpleSpaceMongerCS.csproj` — SDK-style .NET project file (WinForms). Uses implicit file includes; no manual csproj edits required when adding files to subfolders.
- `.gitignore` — Project-level ignores.
- `PROJECT_OVERVIEW.md` — This file.

Notes:
- New code files are placed under `Services/`, `Controls/`, and `Utils/` to keep concerns separated.

## High-level design
- MVC-lite single-window application. `MainForm` owns the UI and interacts with a single pure/static service `FileScanner` for filesystem work.
- Scanning runs on a background thread (`Task.Run`) and reports progress via `IProgress<int>` back to the UI.
- The treemap is a recursive binary partition treemap that aims for reasonable aspect ratios; drawing is immediate and tiles are stored in `tileHitTest` for hit-testing.
- Interaction model:
  - Hover: shows a tooltip with name/size/path after brief debounce.
  - Left click: shows the same (instant) tooltip for the clicked tile.
  - Right click: context menu with Open, Details, Zoom In, Zoom Out (Zoom In/Out also available in the View menu).
  - Menu bar: Browse, Refresh, Zoom In/Out, About.

## Important data and flows
- `sizes: Dictionary<string,long>` — Aggregated directory sizes with keys as absolute paths.
- `total: long` — The aggregated size for `rootPath` (used to compute free space tile).
- `tileHitTest: List<(RectangleF rect, string? path, long size, string name)>` — Filled each paint pass; used for hover/click detection. Note: path may be a synthetic marker (e.g. `rootPath + "|FREE|"`) for a free-space tile.

Rendering flow (simplified):
1. User selects rootPath (Browse menu or initial current directory).
2. `ScanAndInvalidateAsync(rootPath)` runs scanner on background thread and sets `sizes` and `total` when done.
3. `DrawPanel_Paint` builds `items` (immediate children) and adds a white "Free space" tile if `total > sum(children)`.
4. `DrawTreemap` recursively partitions the area and calls `DrawTile`, which appends entries into `tileHitTest` used for hit-testing.

Hit-testing detail: when finding a tile under the mouse we select the last matching tile in `tileHitTest` (the innermost tile) to prefer more specific children over their parent tiles.

## Functional specification (current behavior)
- Select root folder: File → Browse... (also initial directory is current working directory).
- Visualization: shows immediate subfolders as tiles sized by their aggregated size.
- Free space: if the root's total size is larger than the sum of displayed children, the remainder is shown as a white tile labeled "Free space".
- Hover: shows tooltip with folder name, size and path (free tile shows name & size only).
- Left click: shows tooltip immediately (same info as hover).
- Right click: shows a context menu for the clicked tile (Open in Explorer, Details, Zoom In, Zoom Out) — free tile only has Details and Zoom Out.
- Menu: File (Browse, Refresh, Exit), View (Zoom In, Zoom Out), Help (About).

## Extension points / where to change behavior
- Scanning algorithm: `Services/FileScanner.cs` — change to include/exclude file types, parallel enumeration, or to skip content under certain folders.
- Treemap algorithm: `MainForm.DrawTreemap` — replace with squarified treemap or other layout algorithm.
- Hit testing / selection: `tileHitTest` is populated in `DrawTile`; selection model can be extended to maintain a persistent selected tile.
- Context actions: `MainForm.DrawPanel_MouseClick` and the ContextMenuStrip population.

## Tests and validation
- There are no automated tests in this repo now. Recommended unit tests:
  - `FileScanner.ScanPath` correctness with synthetic directory trees.
  - UI-level integration tests (manual) for zoom, hover, context menu actions.

## Developer tips
- The project uses nullable reference types. Match event-handler signatures (e.g., `object? sender`) to avoid nullability warnings.
- Add new files under appropriate folders; the SDK-style csproj will pick them up automatically.
- For performance with very large trees, consider streaming scan results (progressive rendering) instead of waiting for full scan completion.

## Quick file map
- Main UI: `MainForm.cs` (logic, drawing, interaction)
- Background work: `Services/FileScanner.cs`
- Visual helpers: `Utils/GraphicsHelpers.cs`
- Small control: `Controls/BufferedPanel.cs`

If you want I can also add a CONTRIBUTING.md or a short annotated TODO list describing next work items (unit tests, squarified layout, performance profiling).
