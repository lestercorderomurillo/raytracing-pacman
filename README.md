<h1 align="center">Raytracing Pac-Man</h1>

<p align="center">
  <a href="https://github.com/lestercorderomurillo/raytracing-pacman/releases/tag/v0.1.1"><img src="https://img.shields.io/badge/release-v0.1.1-E73C00?style=flat-square" alt="Release v0.1.1"></a>
  <img src="https://img.shields.io/badge/Linux-Vulkan-8B5CF6?style=flat-square" alt="Linux with Vulkan">
</p>

[![Pac-Man and four ghosts illuminate a purple maze with glowing pellets](docs/screenshots/gameplay-maximized.png)](docs/screenshots/gameplay-maximized.png)

Collect every coin to advance. Power pellets let you eat frightened ghosts; getting caught restarts the level.

- **Six levels:** a classic maze and five seeded layouts with different colors and ghost lineups.
- **Seven ghost types:** Blinky, Pinky, Inky, Clyde, Dinky, Shadow, and Rainbow.
- **Dynamic lighting:** glowing walls, ghosts, coins, and power pellets.

## Play

**[Download v0.1.1 for Linux x64](https://github.com/lestercorderomurillo/raytracing-pacman/releases/download/v0.1.1/raytracing-pacman-v0.1.1-linux-x64.tar.gz)** · [Release notes](https://github.com/lestercorderomurillo/raytracing-pacman/releases/tag/v0.1.1)

```sh
tar -xzf raytracing-pacman-v0.1.1-linux-x64.tar.gz
cd raytracing-pacman-v0.1.1-linux-x64
./raytracing-pacman
```

Requires Linux x64, Vulkan graphics drivers, and a graphical desktop. Keep the bundled runtime and assets with the executable.

## Controls

[![Controls: arrow keys move Pac-Man; F1 toggles the inspector; Esc exits the game.](docs/controls.png)](docs/controls.png)

## Ghosts

| Ghost | In game |
| --- | --- |
| **Blinky** — Direct pursuit; retreats to the upper-right corner. | ![Red Blinky in game](docs/screenshots/ghosts/blinky.png) |
| **Pinky** — Direct pursuit; retreats to the upper-left corner. | ![Pink Pinky in game](docs/screenshots/ghosts/pinky.png) |
| **Inky** — Direct pursuit; retreats to the lower-right corner. | ![Cyan Inky in game](docs/screenshots/ghosts/inky.png) |
| **Clyde** — Chases from afar; retreats when Pac-Man gets close. | ![Orange Clyde in game](docs/screenshots/ghosts/clyde.png) |
| **Dinky** — Heads for random corners when Pac-Man gets close. | ![Green Dinky in game](docs/screenshots/ghosts/dinky.png) |
| **Shadow** — Dark body; moves up to 50% faster. | ![Dark purple Shadow with white eyes in game](docs/screenshots/ghosts/shadow.png) |
| **Rainbow** — Splits into three color-changing ghosts, then merges back into one. | ![Rainbow changing colors, splitting into three ghosts, and merging in game](docs/screenshots/ghosts/rainbow.gif) |

## Levels

| Level | In game |
| --- | --- |
| **1-1** | [![Level 1-1 in game](docs/screenshots/level-1.png)](docs/screenshots/level-1.png) |
| **1-2** | [![Level 1-2 in game](docs/screenshots/level-2.png)](docs/screenshots/level-2.png) |
| **1-3** | [![Level 1-3 in game](docs/screenshots/level-3.png)](docs/screenshots/level-3.png) |
| **1-4** | [![Level 1-4 in game](docs/screenshots/level-4.png)](docs/screenshots/level-4.png) |
| **2-1** | [![Level 2-1 in game](docs/screenshots/level-5.png)](docs/screenshots/level-5.png) |
| **2-2** | [![Level 2-2 in game](docs/screenshots/level-6.png)](docs/screenshots/level-6.png) |

## Editor

Press **F1**, then choose **View → Show All** to display every editor panel.

[![Editor with all panels visible](docs/screenshots/editor-show-all.png)](docs/screenshots/editor-show-all.png)

[Build and test instructions](BUILD.md). Built with [Radiant](https://github.com/lestercorderomurillo/radiant-game-engine) and MonoGame DesktopVK.
