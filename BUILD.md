# Building Raytracing Pac-Man

## Requirements

- Git and the .NET 8 SDK or newer.
- Vulkan graphics drivers and a graphical desktop to run the game or graphical tests.
- Linux is the verified platform; Windows and macOS still need runtime verification.

## Source setup

Clone the game and its Radiant dependency into sibling directories:

```sh
git clone https://github.com/lestercorderomurillo/radiant-game-engine.git
git clone https://github.com/lestercorderomurillo/raytracing-pacman.git
cd raytracing-pacman
```

Keep both checkouts on compatible revisions. The [v0.1.0 release notes](https://github.com/lestercorderomurillo/raytracing-pacman/releases/tag/v0.1.0) record the exact revisions used for that release.

## Build and run

Run commands from the game repository root:

```sh
dotnet build -c Release
dotnet run -c Release
```

The first build restores the pinned MonoGame content tool and builds the game, its engine dependency, and their content. Shader compilation is part of the build.

For a Radiant checkout elsewhere, target the game project and set `RadiantRoot`:

```sh
dotnet run --project raytracing-pacman.csproj -c Release -p:RadiantRoot=/absolute/path/to/radiant-game-engine
```

The solution and workspace use sibling paths. Target the game `.csproj` for a custom dependency location, or update those paths to match.

## VS Code

Open the [workspace](raytracing-pacman.code-workspace) to work with both repositories. The checked-in [solution](raytracing-pacman.sln) includes the game, engine, and test projects. **F5** builds Debug and launches Pac-Man; **Ctrl + Shift + B** builds the solution. Shader `.fx` files use HLSL highlighting.

## Checks

**C# formatting:**

```sh
git ls-files -z '*.cs' | xargs -0 dotnet format whitespace . --folder --verify-no-changes --include
```

**Headless tests** check assembly boundaries and 64 deterministic, symmetric maze seeds:

```sh
dotnet run --project tests/Pacman.Tests -c Release
```

**Vulkan smoke tests** exercise all six levels, resizing, maze centering, coordinate mapping, scene controls, lighting modes, and upscalers. Captures go to `artifacts/smoke`:

```sh
dotnet run --project tests/Pacman.Tests -c Release -- --graphics
```

**Rendering regressions** check eye alignment from actual rendered pixels, all three UDR upscalers and quality levels, both lighting modes, resizing, and disabling/re-enabling upscaling:

```sh
dotnet run --project tests/Pacman.Tests -c Release -- --rendering
```

Gameplay starts with UDR3 in Balanced quality (50% lighting render scale). F1 opens the inspector; use Pipeline to select the upscaler and its quality settings to choose Performance (25%), Balanced (50%), or Native (100%).

**Presented-window checks** require an X11/XWayland desktop, Python with Pillow, `xdotool`, and ImageMagick:

```sh
dotnet build -c Release
python3 tests/window_layout.py
```

The script checks startup and six resizes, including square and portrait windows, and saves captures to `artifacts/window-layout`. It stops only the game process it started.

[Linux CI](.github/workflows/build.yml) checks out both repositories, checks formatting, builds, runs headless tests, and runs the Linux publish command. Manual runs accept an `engine_ref` to select a compatible engine revision.

## Publish for Linux

```sh
dotnet publish raytracing-pacman.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/raytracing-pacman
```

The output includes the .NET runtime, engine, and game content. Keep the published files together. Content paths resolve relative to the executable, so it can launch from another working directory.
