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

Current sources require Radiant v0.4.0, including the portable graphics, renderer and window contracts. Use the matching engine tag for reproducible release builds.

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

**Vulkan smoke tests** exercise all six levels, resizing, maze centering, coordinate mapping, scene controls, HRC lighting, and upscalers. Captures go to `artifacts/smoke`:

```sh
dotnet run --project tests/Pacman.Tests -c Release -- --graphics
```

**Rendering regressions** check eye alignment from actual rendered pixels, all four UDR upscalers and quality levels, HRC lighting, resizing, and disabling/re-enabling upscaling. Maze emission and light blocking are checked on every frame, including startup and immediately after resizing:

```sh
dotnet run --project tests/Pacman.Tests -c Release -- --rendering
```

Gameplay starts with UDR4 in Balanced quality (50% lighting render scale). F1 opens the inspector; use Pipeline to select the upscaler and its quality settings to choose Performance (25%), Balanced (50%), or Native (100%).

**Presented-window checks** require an X11/XWayland desktop, Python with Pillow, `xdotool`, and ImageMagick:

```sh
dotnet build -c Release
python3 tests/window_layout.py
```

Set `PACMAN_EXECUTABLE` to the absolute path of a published executable to run the same checks on a release package. The script checks startup and six resizes, including square and portrait windows, and saves captures to `artifacts/window-layout`. It stops only the game process it started.

[Linux CI](.github/workflows/build.yml) checks out both repositories, checks formatting, builds, runs headless tests, and runs the Linux publish command. Manual runs accept an `engine_ref` to select a compatible engine revision.

## Publish for Linux

```sh
dotnet publish raytracing-pacman.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/raytracing-pacman
```

The output includes the .NET runtime, engine, and game content. Keep the published files together. Content paths resolve relative to the executable, so it can launch from another working directory.

UDR4 requires the matching engine checkout with `UDR4` and its shader. It reconstructs jittered HRC lighting with floating-point motion and conservative temporal history rejection. UDR1–3 remain available for comparisons. See [engine UDR4 notes](../radiant-game-engine/docs/UDR4.md) for limitations and validation.

To refresh the unedited 2880×1521 gameplay capture, run the test harness with `--capture` on an isolated display. It validates the resolution and visible content before saving `artifacts/captures/gameplay-maximized.png`.

## Shared camera controller

With Radiant v0.2.1, open F1 > ECS > Open Camera Controls, or use the Camera controls button in the game settings. The single window searches camera IDs/types and selects instances independently of the entity inspector. This game uses the fitted 2D World view: enable Navigate camera and hold RMB with WASD to pan, or use Zoom in / Zoom out. Adjust movement speed for world units. Closing the controller stops navigation. Changing levels restores the game framing where the scene calls FitWorld.

## Radiant API migration checks

Gameplay uses `Time.DeltaSeconds` and `Controls.Keyboard`. `src/RadiantTypes.cs`
selects public .NET/Radiant types and is linked into the test project; it does not
reference the engine's internal compilation aliases. Native texture readback stays
inside the graphics test harness.

Run the input regression on an isolated display:

```sh
xvfb-run -a -s '-screen 0 1600x1200x24 -extension MIT-SHM' dotnet tests/Pacman.Tests/bin/Release/net8.0/Pacman.Tests.dll --input
```

This checks actual player movement from Radiant keyboard snapshots. Run `--graphics`
and `--rendering` on the same isolated display setup. If the hardware Vulkan driver
cannot present to Xvfb, set `VK_ICD_FILENAMES` to an installed software Vulkan ICD;
those checks establish correctness, not hardware performance.

## Radiant graphics resources

Gameplay uses Radiant textures and render targets through `src/RadiantTypes.cs`;
resource creation stays in `Renderer`. Region readback uses Radiant rectangles.
The engine owns native conversion and cached resources. See the engine's
[resource contracts](../radiant-game-engine/docs/GraphicsResources.md) for ownership
and effect lifetime rules. Run `python ../radiant-game-engine/eng/check-consumers.py`
to build the engine and both games together before the isolated graphical checks.

## Renderer and window contracts

Current sources also require Radiant's `IRenderer` and `IWindow` migration. Scene/ECS
renderers and game drawing helpers consume that interface; native MonoGame window
subclasses remain confined to graphical test harnesses. Fluent calls still work, and
window closure is `Renderer.Window.RequestClose()`. See the engine's
[renderer contracts](../radiant-game-engine/docs/RendererContracts.md).

The engine now delegates fitting, world/screen mapping and scaled metrics to its
backend-independent `RenderView`. Game calls still use `IRenderer`. The `--graphics`
check additionally compares sprite transforms with input coordinates and verifies
scaled metrics after resizing; asset caches are tested through Radiant's injected
`IGraphicsAssetLoader`.

Fonts now use Radiant `FontFace`/`IFontLoader` contracts, and shape accumulation
uses the shared CPU `ShapeCollection` before backend submission. Gameplay keeps
using named `DrawString` and `IRenderer` shape commands. The graphics checks verify
the game's HUD font metrics and repeated font lookup identity as well as rendering.

Radiant now routes mesh and colored-triangle submission through `IGeometryBackend`.
Game code continues to use `IRenderer.DrawMesh` / `DrawTriangles`; GPU buffer ownership
and upload caching stay in the engine backend. Use the matching sibling engine checkout.
