# Agent instructions

Build setup and validation commands are in [BUILD.md](BUILD.md).

## Repository boundaries

- This repository owns Pac-Man scenes, maze code, player and ghost logic, HUD, Ghost/Eyes textures, and PressStart2P.ttf. Keep engine code, shared shaders, and engine test scenes in the sibling `radiant-game-engine` repository.
- Use public `Scene`, `ECS`, `Renderer`, and `GameClient` APIs. Do not create GPU resources directly in gameplay systems.
- The game targets .NET 8. Radiant's `eng/Radiant.Desktop.props` pins MonoGame 3.8.6 DesktopVK; keep the local MGCB tool manifest at the same version.
- Production builds include only `Program.cs` and `src/**/*.cs`. The test project links Radiant's `SmokeWindow` helper; keep test scenes and harnesses out of production assemblies.

## Behavior and documentation

- Preserve the six existing levels and gameplay when changing repository boundaries.
- Keep the maze centered and proportional through `Renderer.FitWorld(center, size)`. The HUD uses projected maze bounds and display-pixel font sizes. Native UDR quality is the default; retain Balanced and Performance options in the inspector.
- Keep `.vscode` build/debug settings, `raytracing-pacman.sln`, and `raytracing-pacman.code-workspace` in sync with the sibling engine layout. The solution includes the engine library and all five engine/game projects.
- Keep the README focused on the game. Put source setup, build, and test documentation in `BUILD.md`, and keep agent instructions in `AGENTS.md`.
- Use unedited captures of the actual game for screenshots. The README uses `docs/screenshots/gameplay-maximized.png` (2880×1521); `gameplay-windowed.png` (1280×720) is an additional capture in the same directory. Preserve native resolution when refreshing them.
- Select validation appropriate to the change from [BUILD.md](BUILD.md#checks). Graphical checks cover all six levels, lighting/upscaler switching, restart, pause, resize, maze proportions, centering, and coordinate mapping.

## Coding style

- Separate logical chunks with one blank line: setup, processing, and commit/cleanup. Keep closely related statements together; do not add a blank line after every statement.
- Leave a blank line before and after a loop when it sits between other steps, and between independent loops. Keep the loop header attached to its body.
- Use four spaces per indentation level, including single-statement loop bodies. Keep the existing brace style and readable fluent calls on one line.
- Use descriptive camelCase names for locals and parameters, such as `horizontalScale`, `cellCenter`, `eyeRadius`, and `distanceSquared`. Spell out ambiguous abbreviations (`sx`, `pdx`, `dist`, `tmp`); give coordinates and loop indices a subject (`cellX`, `directionIndex`). Keep established PascalCase member names, conventional tuple coordinate labels, and framework API names.
- Remove unused locals, parameters, fields, and assignments, including state that is only copied to itself. Preserve required side effects, `ref`/`out` outputs, and framework overrides. Use `_` for intentionally ignored values when a binding is required. The build enforces unused-code diagnostics from `.editorconfig`.
- Add a short `//` comment above a major phase when it helps explain its purpose. Usually zero to two comments per method are enough; do not narrate each statement, add decorative banners, or remove necessary explanations just to meet a count.
- For formatting requests, preserve behavior, names, statement order, and control flow. Change whitespace and concise comments only; avoid unrelated refactors or formatting untouched files.
- Before finishing, review the changed code for logical grouping and comment noise. `.editorconfig` handles mechanical whitespace; choosing meaningful chunks and comments requires this review.

Example:

```csharp
        // Draw emissive wall borders.
        Renderer.Reset().Configure(BlendState.Opaque).SetTarget(MazeEmissiveTarget).Clear(Color.Transparent);

        for (int borderIndex = 0; borderIndex < wallBorders.Count; borderIndex++)
            Renderer.DrawTexture(solidTexture, wallBorders[borderIndex].rect, wallBorders[borderIndex].color);

        Renderer.Commit();
```
