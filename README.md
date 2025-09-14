# SimpleSpaceMongerCS

SimpleSpaceMongerCS is a compact WinForms treemap viewer for Windows that helps you visualize folder and drive disk usage and quickly find large folders and free space.

## At a glance
- Interactive treemap visualization of folder sizes.
- Drives overview on startup: click a drive to scan.
- Hover tooltips, parent highlight, selection, and context menu actions (Open, Details, Zoom In/Out).
- Free-space tile shown for drive roots.
- Background scanning, layout caching and pre-rendered bitmap for responsive UI.

---

## Features
- Drives grid for quick disk selection.
- Adaptive rendering: automatically expands child tiles where visible (configurable depth and minimum tile size via View → Rendering).
- Persistent selection highlight and non-flickering rendering via cached bitmap.
- Context actions: Open in Explorer, Details (size and percentage), Zoom In/Out.

---

## Quick start (Windows / PowerShell)
1. Build
   ```
   dotnet build -c Debug
   ```

2. Run from build output
   ```
   Start-Process -FilePath "bin\Debug\net8.0-windows\SimpleSpaceMongerCS.exe"
   ```

3. Or run from Visual Studio
   - Open `SimpleSpaceMongerCS.sln` and run (target: .NET 8.0 / Windows).

---

## Project structure (key files)
- `MainForm.cs` (+ partials) — UI, rendering orchestration, menus, and interaction handlers.
- `Program.cs` — application entry point.
- `Services/FileScanner.cs` — background scanner that aggregates sizes per-directory.
- `Services/TreemapLayout.cs` — algorithm to compute treemap rectangles (TileLayout entries).
- `Controls/TreemapRenderer.cs` — centralized drawing helper for tiles.
- `Controls/BufferedPanel.cs` — small double-buffered panel used for the drawing area.
- `Utils/GraphicsHelpers.cs` — human-readable formatting and color/brush helpers.
- `Models/ColorSchemes.cs` — color schemes and palettes definitions.

Files are picked up automatically by the SDK-style csproj; add files under appropriate folders.

---

## Design & runtime flow
1. User chooses a root (drive click or File → Browse).
2. `ScanAndInvalidateAsync(root)` runs `FileScanner` on a background thread and updates `sizes` and `total`.
3. `MainForm` builds a list of immediate child items and adds a synthetic free-space item when applicable.
4. `TreemapLayout.BuildLayout` computes top-level tile rectangles.
5. Adaptive expansion optionally computes nested child tile layouts where visible, up to a global depth cap.
6. The full `cachedLayout` is rendered into `cachedBitmap` (a pre-rendered bitmap matching the draw panel) for fast Paint operations.
7. During Paint, the bitmap is drawn and `tileHitTest` is repopulated from `cachedLayout` so hover/click remain accurate.

Important runtime data structures
- `sizes: Dictionary<string,long>` — aggregated sizes per absolute path.
- `total: long` — total size under the current root.
- `cachedLayout: List<TreemapLayout.TileLayout>` — flattened layout (may include nested tiles).
- `cachedBitmap: Bitmap?` — pre-rendered image used for fast repaint.
- `tileHitTest: List<TileHit>` — hit-test list of rectangles and metadata; search from end to get innermost tile.

---

## Rendering & UX notes
- Tile coloring: by size, monochrome, pastel, or by-path palettes.
- Borders and padding: border width increases subtly with depth; nested padding prevents tight parent-child overlap for easier clicking.
- Free-space tiles: rendered as blank white regions for visual clarity.
- Selection: persistent highlight drawn over cached bitmap to avoid flicker.

---

## Interaction model
- Hover shows tooltip (name, size, path) after a short debounce.
- Parent highlight (second-innermost under mouse) helps selecting parent folders.
- Left-click: select/pin tile (drive left-click starts a scan).
- Right-click: context menu with Open in Explorer, Details, Zoom In/Out.

---

## Extension points
- Swap the layout algorithm (e.g., squarified treemap) in `Services/TreemapLayout`.
- Modify scanning behavior, filters, or concurrency in `Services/FileScanner`.
- Persist rendering preferences (max depth, min tile size) in user settings.
- Add a preferences dialog or in-app sliders for live tuning of rendering parameters.

---

## Developer tips & gotchas
- Avoid transient CreateGraphics overlays; use cached bitmap rendering for consistency.
- Keep expensive work off the UI thread; scanners use `Task.Run` with `IProgress<int>` for updates.
- Maintain `tileHitTest` when changing draw order or rendering strategy so hit-testing remains accurate.
- Use `Path.GetRelativePath` to determine immediate children reliably.
- Targeting `net8.0-windows`: ensure SDK/tooling support when building.

---

## Contributing
- Fork, implement changes in a feature branch and open a pull request against `main`.
- Keep changes small and add unit tests for behavior-critical logic (scanner, layout) where practical.

---

## Next steps & suggestions
- Persist rendering preferences across runs.
- Add unit tests for `FileScanner` and `TreemapLayout`.
- Add `CONTRIBUTING.md` and `CHANGELOG.md`.
- Consider progressive rendering to surface largest items earlier during long scans.

---


---

Repository: https://github.com/W4N4N/SimpleSpaceMongerCS
