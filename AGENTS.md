# Coding style

Repository context and validation commands are in [CLAUDE.md](CLAUDE.md).

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
