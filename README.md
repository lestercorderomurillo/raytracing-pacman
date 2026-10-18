<h1 align="center">Raytracing Pac-Man</h1>

<p align="center">
  <strong>Classic maze chasing, lit by radiance cascades.</strong><br>
  Built with <a href="https://github.com/lestercorderomurillo/radiant-game-engine">Radiant</a> and MonoGame DesktopVK.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=flat-square" alt=".NET 8">
  <img src="https://img.shields.io/badge/MonoGame-3.8.6-E73C00?style=flat-square" alt="MonoGame 3.8.6">
  <img src="https://img.shields.io/badge/Linux-Vulkan-8B5CF6?style=flat-square" alt="Linux with Vulkan">
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> ·
  <a href="#controls">Controls</a> ·
  <a href="#development">Development</a>
</p>

[![Pac-Man and four ghosts illuminate a purple maze with glowing pellets](docs/screenshots/gameplay-maximized.png)](docs/screenshots/gameplay-maximized.png)

| In the game | What to explore |
| --- | --- |
| **Six levels** | A classic maze and seeded procedural layouts, with different palettes and ghost lineups. |
| **Seven ghost types** | Blinky, Pinky, Inky, Clyde, Dinky, Shadow, and a Rainbow ghost that splits and merges. |
| **Dynamic lighting** | Glowing walls, ghosts, coins, and power pellets, with two global illumination modes and three upscalers. |
| **Live controls** | Switch levels, restart, pause, and tune rendering from the runtime inspector. |
| **Flexible windows** | A centered, proportional maze and readable HUD across wide, square, and portrait windows. |

## Quick start

**Requirements:** .NET 8 SDK or newer, Vulkan graphics drivers, and a graphical desktop session.

Clone the game and its engine next to each other, then run:

```sh
git clone https://github.com/lestercorderomurillo/radiant-game-engine.git
git clone https://github.com/lestercorderomurillo/raytracing-pacman.git
cd raytracing-pacman
dotnet run -c Release
```

The first build restores the pinned MGCB tool and builds both projects and their content. Linux uses the native Vulkan runtime and shader compiler.

```text
Repositories/
├── radiant-game-engine/
└── raytracing-pacman/
```

Keep both checkouts on compatible revisions when changing the engine API. MonoGame also includes Windows and macOS backends; those platforms still need runtime verification.

<details>
<summary><strong>Use an engine checkout in another location</strong></summary>

Set `RadiantRoot` when running or building the game project:

```sh
dotnet run --project raytracing-pacman.csproj -c Release -p:RadiantRoot=/absolute/path/to/radiant-game-engine
```

The solution and VS Code workspace use sibling checkout paths. For a different layout, target the game `.csproj` explicitly or update those paths to match.

</details>

## Controls

| Input | Action |
| --- | --- |
| <kbd>↑</kbd> <kbd>←</kbd> <kbd>↓</kbd> <kbd>→</kbd> | Move Pac-Man |
| <kbd>F1</kbd> | Toggle the runtime inspector |
| <kbd>Esc</kbd> | Exit the game |
| Inspector → **Scene** | Next/previous level, restart, and pause |
| Inspector → **Pipeline** | Lighting and upscaler settings |

Rendering starts at **Native** quality. Choose **Balanced** or **Performance** in the inspector to reduce GPU cost. The maze fits the window while preserving its proportions.

## Development

Open the [VS Code workspace](raytracing-pacman.code-workspace) to edit the game and Radiant together. **F5** runs the game; **Ctrl + Shift + B** builds the solution.

Game code lives in [`src`](src), assets in [`Content`](Content), and tests in [`tests`](tests). Radiant provides the engine and rendering through a source reference, so engine edits rebuild automatically.

Build and run headless tests from the repository root:

```sh
dotnet build -c Release
dotnet run --project tests/Pacman.Tests -c Release
```

Add `-- --graphics` to the test command for Vulkan checks across all six levels, resizing, and rendering modes.

Publish a self-contained Linux build:

```sh
dotnet publish raytracing-pacman.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/raytracing-pacman
```

[Linux CI](.github/workflows/build.yml) checks formatting, builds, tests, and publishes. See the [coding rules](AGENTS.md) and [repository notes](CLAUDE.md) for details.
