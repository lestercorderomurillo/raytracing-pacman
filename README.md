<h1 align="center">Raytracing Pac-Man</h1>

<p align="center">
  <strong>Classic maze chasing with glowing walls and colorful ghosts.</strong>
</p>

<p align="center">
  <a href="https://github.com/lestercorderomurillo/raytracing-pacman/releases/tag/v0.1.0"><img src="https://img.shields.io/badge/release-v0.1.0-E73C00?style=flat-square" alt="Release v0.1.0"></a>
  <img src="https://img.shields.io/badge/Linux-Vulkan-8B5CF6?style=flat-square" alt="Linux with Vulkan">
</p>

<p align="center">
  <a href="#the-game">The game</a> ·
  <a href="#play">Play</a> ·
  <a href="#controls">Controls</a>
</p>

[![Pac-Man and four ghosts illuminate a purple maze with glowing pellets](docs/screenshots/gameplay-maximized.png)](docs/screenshots/gameplay-maximized.png)

## The game

Collect every coin to advance to the next maze. Dodge the ghosts, grab power pellets to eat them while they're frightened, and watch the maze light up as you move. Getting caught restarts the current level.

| In the game | What to explore |
| --- | --- |
| **Six levels** | A classic maze and five seeded layouts, each with its own colors and ghost lineup. |
| **Seven ghost types** | Blinky, Pinky, Inky, Clyde, Dinky, Shadow, and a Rainbow ghost that splits and merges. |
| **Dynamic lighting** | Walls, ghosts, coins, and power pellets cast colorful light through the corridors. |

## Play

**[Download v0.1.0 for Linux x64](https://github.com/lestercorderomurillo/raytracing-pacman/releases/download/v0.1.0/raytracing-pacman-v0.1.0-linux-x64.tar.gz)** · [Release notes](https://github.com/lestercorderomurillo/raytracing-pacman/releases/tag/v0.1.0)

Extract the archive and launch the game:

```sh
tar -xzf raytracing-pacman-v0.1.0-linux-x64.tar.gz
cd raytracing-pacman-v0.1.0-linux-x64
./raytracing-pacman
```

Requires Linux x64, Vulkan graphics drivers, and a graphical desktop. The game runtime and assets are included; keep the extracted files together.

## Controls

| Input | Action |
| --- | --- |
| <kbd>↑</kbd> <kbd>←</kbd> <kbd>↓</kbd> <kbd>→</kbd> | Move Pac-Man |
| <kbd>F1</kbd> | Open or close the inspector |
| <kbd>Esc</kbd> | Exit the game |
| Inspector → **Scene** | Next/previous level, restart, and pause |
| Inspector → **Pipeline** | Lighting and upscaler settings |

For smoother performance, choose **Balanced** or **Performance** in the upscaler's **Quality** menu.

## Development

Game code lives in [`src`](src). See the [repository notes](CLAUDE.md) for build and test commands, and the [coding rules](AGENTS.md) for contributions.

Built with [Radiant](https://github.com/lestercorderomurillo/radiant-game-engine) and MonoGame DesktopVK.
