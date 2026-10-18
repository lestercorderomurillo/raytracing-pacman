# Raytracing Pac-Man

C#/.NET 8 game consumer of the sibling `../radiant-game-engine` library. `RadiantRoot` overrides that path. The engine's `eng/Radiant.Desktop.props` pins MonoGame 3.8.6 DesktopVK. Keep the local MGCB tool manifest at the same version.

- Keep engine code, shared shaders, and engine test scenes in Radiant; this repo owns Pac-Man scenes, maze code, player/ghost logic, HUD, Ghost/Eyes textures, and PressStart2P.ttf.
- Use public `Scene`, `ECS`, `Renderer`, and `GameClient` APIs. Do not create GPU resources directly in gameplay systems.
- Startup: `new GameClient().Run(new PacmanMazeLevelScene(), new() { Title = "Raytracing Pac-Man" });`.
- `dotnet build -c Release` builds both repositories and content. `dotnet run -c Release` needs a graphical desktop with Vulkan.
- Preserve the six existing levels and gameplay when changing repository boundaries.

Tests: `dotnet run --project tests/Pacman.Tests -c Release` is headless. Add `-- --graphics` for real Vulkan rendering, resize, all six levels, GI/upscaler switching, restart, and pause checks. The test project links the engine's SmokeWindow helper; production builds include only Program.cs and src/**/*.cs. CI checks out both repositories and supports an alternate engine_ref on manual runs.

VS Code uses the checked-in `raytracing-pacman.sln`, including the sibling engine library and all five engine/game projects, instead of generating a solution that omits external references. `raytracing-pacman.code-workspace` opens both repositories. Keep `.vscode` build/debug settings and solution paths in sync. `.fx` means HLSL. For a custom RadiantRoot outside the sibling layout, target the game csproj explicitly or update solution/workspace paths.

The maze calls `Renderer.FitWorld(center, size)` after building each level to preserve its proportions and keep it centered on resize. The HUD uses projected maze bounds and display-pixel font sizes. Native UDR quality is the default; the inspector retains Balanced and Performance options.

`python3 tests/window_layout.py` checks actual presented game-window captures at startup and six resizes, including square and portrait windows. Run after a Release build in X11/XWayland with Python/Pillow, xdotool, and ImageMagick installed. Captures go to `artifacts/window-layout`; the test terminates only its own game process. The Vulkan test also checks world/screen round-trips, square cells, and maze centering across level changes.

README screenshots live in `docs/screenshots/gameplay-maximized.png` (2880×1521) and `gameplay-windowed.png` (1280×720). They are unedited captures of the real game window on the second Linux monitor, at native rendering resolution. Retain real game captures when refreshing them.
