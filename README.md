# Raytracing Pac-Man

Pac-Man gameplay built on [Radiant](https://github.com/lestercorderomurillo/radiant-game-engine), with radiance-cascade lighting, procedural mazes, ghost AI, pellets, a HUD, and a runtime inspector. Runs natively on Linux with MonoGame **3.8.6 DesktopVK**.

The entire application entry point is:

```csharp
using com.radiant.engine.runtime;
using RaytracingPacman;

new GameClient().Run(new PacmanMazeLevelScene(), new() { Title = "Raytracing Pac-Man" });
```

## Screenshots

Actual game-window captures from a second monitor on Linux, running MonoGame DesktopVK at native rendering resolution. Click an image to view the original PNG.

**Maximized, 2880 × 1521** — Pac-Man, ghosts, and pellets illuminate the maze.

[![Maximized game with a centered purple maze, glowing pellets, Pac-Man, and four colored ghosts](docs/screenshots/gameplay-maximized.png)](docs/screenshots/gameplay-maximized.png)

**Windowed, 1280 × 720** — the maze stays centered and proportional, with a readable HUD.

[![Windowed game with the complete maze centered and its original proportions preserved](docs/screenshots/gameplay-windowed.png)](docs/screenshots/gameplay-windowed.png)

## Run

Install the .NET 8 SDK or newer and Vulkan graphics drivers. Clone the two repositories next to each other, using the corresponding engine/game split revisions:

```sh
git clone https://github.com/lestercorderomurillo/radiant-game-engine.git
git clone https://github.com/lestercorderomurillo/raytracing-pacman.git
cd raytracing-pacman
dotnet run -c Release
```

The build restores the pinned MGCB tool and builds both projects and their content. Linux uses the native Vulkan runtime and shader compiler; no Wine or Windows build is needed. Run in a graphical desktop session. MonoGame's native runtime packages also include Windows and macOS backends; those platforms require their own runtime verification.

If the engine checkout is elsewhere:

```sh
dotnet run -c Release -p:RadiantRoot=/absolute/path/to/radiant-game-engine
```

Arrow keys move, F1 toggles the inspector, and Escape exits. The inspector's Scene window provides level switching, restarting, and pause controls. Lighting and upscaler controls remain available in the rendering pipeline inspector.

The game starts at native rendering resolution for sharp lighting and characters. Choose Balanced or Performance in the inspector to reduce GPU cost. The maze automatically fits the window without stretching, including square and portrait windows.

## Cross-repository dependency

`raytracing-pacman.csproj` imports `Radiant.Desktop.props` and references `radiant.csproj` from the sibling checkout. `RadiantRoot` overrides that location. This is a source project reference: engine edits rebuild automatically, with no duplicated engine files or checked-in binaries.

This repository owns:

- `src/Scenes/PacmanMazeLevelScene.cs`: level definitions and scene composition.
- `src/Maze`: maze generation, rendering, pellets, and level configuration.
- `src/Gameplay`: player movement, ghost AI, and HUD.
- `Content`: ghost/eye textures and the Press Start 2P font.

Radiant owns the ECS, renderer, GI/upscaling shaders, window, inspector, and shared fonts/icons. Its lighting and tileset test scenes remain in the engine repository. Compiled engine content is copied through the project reference, including when publishing.

## Publish for Linux

```sh
dotnet publish raytracing-pacman.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/raytracing-pacman
```

Content paths are relative to the executable, so launching from another directory works too. Keep both repositories on compatible revisions when changing the engine API.

## Validation

```sh
dotnet run --project tests/Pacman.Tests -c Release
dotnet run --project tests/Pacman.Tests -c Release -- --graphics
```

Headless checks verify that engine/game assemblies stay separate, engine test scenes stay out of the library, and 64 maze seeds remain deterministic and symmetric. `--graphics` also tests real Vulkan rendering, resizing, all six levels, restart/previous-level controls, pause/resume, both GI modes, and all three upscalers using the same callbacks as the inspector. Screenshots are saved under `artifacts/smoke`. The graphical harness is shared from Radiant's test support; it is excluded from both production assemblies.

To check the actual presented Linux window, install Python with Pillow, `xdotool`, and ImageMagick, then run in an X11/XWayland desktop session:

```sh
dotnet build -c Release
python3 tests/window_layout.py
```

This captures startup and six resizes, checks that the visible maze is centered, proportional, and unclipped, and saves the captures in `artifacts/window-layout`. The Vulkan tests also check centering and coordinate mapping across all six levels.

Linux CI checks out both repositories side by side and builds, tests, and publishes the game. Publish the compatible engine changes before the game changes; a manual workflow run can select an alternate `engine_ref` to verify a paired branch.

## VS Code

Open this repository as a folder, or open `raytracing-pacman.code-workspace` to edit both repositories together. The checked-in `raytracing-pacman.sln` includes the game, the engine, and all test projects; C# Dev Kit uses it automatically. This makes engine namespaces and linked test helpers resolve before a command-line build. F5 launches Pac-Man after the Debug build; Ctrl+Shift+B builds the solution. MonoGame `.fx` files are associated with HLSL, and `dotnet build` performs their shader validation.

If VS Code was open during the repository split, run **Developer: Reload Window** once to replace its old generated solution. The solution/workspace use sibling checkout paths. For a non-sibling engine checkout, build the project explicitly with `dotnet build raytracing-pacman.csproj -p:RadiantRoot=/path/to/radiant-game-engine`, or adjust the solution/workspace paths to match.
