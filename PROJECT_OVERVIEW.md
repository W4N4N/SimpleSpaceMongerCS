# SimpleSpaceMongerCS — Project Overview

This document explains the repository layout, design decisions and the functional specification so another developer or an automated agent can quickly understand and extend the project.

## Goal
A simple WinForms treemap viewer that visualizes folder sizes for a selected root folder and lets the user inspect, open and navigate (zoom) into folders. The UI also provides a drives overview on startup so users can quickly pick a disk to scan.

## Project structure (key files)
- `MainForm.cs` — Primary UI: treemap rendering, interaction (hover, click, context menu), menu bar, drives overview and application glue.
- `Program.cs` — Standard WinForms program entrypoint.
- `Services/FileScanner.cs` — Scans a folder tree and returns aggregated sizes per directory. Key API: `FileScanner.ScanPath(string root, IProgress<int> progress)`.
- `Controls/BufferedPanel.cs` — Small derived `Panel` enabling flicker-free double buffering for the treemap area.
- `Utils/GraphicsHelpers.cs` — Visual helper functions (human-readable byte formatting, color generation, text brush selection, HSL conversion).
- `SimpleSpaceMongerCS.csproj` — SDK-style .NET project file (WinForms). Uses implicit file includes; no manual csproj edits required when adding files to subfolders.
- `.gitignore` — Project-level ignores.
- `PROJECT_OVERVIEW.md` — This file (updated to reflect recent UI/feature changes).

Notes:
- New code files are placed under `Services/`, `Controls/`, and `Utils/` to keep concerns separated.

## High-level design
- MVC-lite single-window application. `MainForm` owns the UI and interacts with a single pure/static service `FileScanner` for filesystem work.
- Scanning runs on a background thread (`Task.Run`) and reports progress via `IProgress<int>` back to the UI.
- The treemap is a recursive binary partition treemap that aims for reasonable aspect ratios; drawing is immediate and tiles are stored in `tileHitTest` for hit-testing.
- On startup the UI shows a drives overview (grid of disk icons). Clicking a disk icon scans that disk.

Interaction model (current):
- Drives overview: shows each mounted drive as an icon + label (drive letter and volume name). Left-click a drive to start scanning it.
- Treemap view (after scanning a folder/disk):
  - Hover: shows a tooltip (name/size/path) after a small debounce.
  - Parent highlight: when hovering over a nested tile the parent tile (second-innermost) is highlighted to make selecting the parent easier.
  - Left click: for normal folders shows tooltip; for drives left-click starts scan; for other tiles you can also use the context menu.
  - Right click: context menu with Open in Explorer, Details, Zoom In/Zoom Out (for drive tiles the menu includes "Scan this disk").
  - Menu bar: File (Browse, Refresh, Exit), View (Zoom In, Zoom Out), Help (About).

## Free-space handling
- When viewing a drive root (e.g., `D:\`) the UI now queries `DriveInfo` and adds a special white "free-space" tile sized to the drive's available free space — the white area is blank (no text) so it visually indicates unused disk area and its area is proportional to the free bytes on the disk.
- When viewing non-drive folders, the UI falls back to showing unused bytes under the current root (if any) as a white tile (previous semantics).

## Important data and flows
- `sizes: Dictionary<string,long>` — Aggregated directory sizes with keys as absolute paths.
- `total: long` — The aggregated size for `rootPath` (used internally when scanning folders).
- `tileHitTest: List<(RectangleF rect, string? path, long size, string name)>` — Populated each paint pass; used for hover/click detection. Note: some paths are synthetic markers (e.g. `rootPath + "|FREE|"` for the free-space tile, or `DRIVE:C:\` for drives overview items).

Rendering flow (simplified):
1. On startup the UI shows drives overview (no scanning yet).
2. User picks a root (drive or folder), `ScanAndInvalidateAsync(rootPath)` runs scanner on a background thread and sets `sizes` and `total` when done.
3. `DrawPanel_Paint` builds `items` (immediate children) and adds a white free-space tile for drives (drive free bytes) or remaining bytes for folders.
4. `DrawTreemap` recursively partitions the area and calls `DrawTile`, which appends entries into `tileHitTest` used for hit-testing. Parent highlight overlay is drawn after tiles.

Hit-testing detail: when finding a tile under the mouse we select the innermost matching tile (the last match in `tileHitTest`) to prefer specific children over parents.

## Visual / UX notes
- Tile borders are softened (lighter gray) and the border width slightly increases with nesting depth to provide visual hierarchy without being visually aggressive.
- Parent tiles are given extra spacing from their children (padding increases with depth) so parent edges are easier to click.
- Free-space tiles are intentionally rendered as blank white regions to make them visually distinct from content tiles.

## Extension points / where to change behavior
- Scanning algorithm: `Services/FileScanner.cs` — change to include/exclude file types, parallel enumeration, or to skip content under certain folders.
- Treemap algorithm: `MainForm.DrawTreemap` — replace with squarified treemap or other layout algorithm.
- Hit testing / selection: `tileHitTest` is populated in `DrawTile`; selection model can be extended to maintain a persistent selected tile or a larger click target for parents.
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

If you want I can also add a CONTRIBUTING.md, a short annotated TODO list, or live runtime controls to tune padding/highlight/border preferences.
