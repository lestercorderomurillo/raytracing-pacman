using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using RaytracingPacman;
using Radiant.Testing;
using Microsoft.Xna.Framework;

var engineAssembly = typeof(Scene).Assembly;
var gameAssembly = typeof(PacmanMazeLevelScene).Assembly;

if (engineAssembly == gameAssembly || engineAssembly.GetTypes().Any(type => type.Name.Contains("Pacman") || type.Name.EndsWith("LevelScene")))
    throw new Exception("Engine contains game implementation types.");
if (engineAssembly.GetTypes().Any(type => type.Name is "SimpleLightScene" or "TilesetScene"))
    throw new Exception("Engine library contains test scenes.");
if (engineAssembly.GetReferencedAssemblies().Any(assemblyName => assemblyName.Name == gameAssembly.GetName().Name))
    throw new Exception("Engine depends on game assembly.");

for (int seed = 0; seed < 64; seed++)
{
    var maze = PacmanMazeGenerator.Generate(seed);
    if (maze.Length != 31 || maze.Any(row => row.Length != 28))
        throw new Exception($"Invalid maze dimensions for seed {seed}.");
    if (!maze.SequenceEqual(PacmanMazeGenerator.Generate(seed)))
        throw new Exception($"Maze seed {seed} is not reproducible.");
    if (maze.Any(row => !row.SequenceEqual(row.Reverse())))
        throw new Exception($"Maze seed {seed} is not symmetric.");
}

Console.WriteLine("PASS: independent engine/game assemblies, engine test isolation, 64 deterministic symmetric mazes.");

if (args.Contains("--graphics"))
{
    using var window = new SmokeWindow(new PacmanMazeLevelScene(), "artifacts/smoke", ExerciseGame);
    window.Run();
}
else if (args.Contains("--rendering"))
{
    using var window = new RenderingWindow();
    window.Run();
}

static void ExerciseGame(Scene scene, int frameNumber)
{
    var maze = scene.ECS.GetSystem<PacmanMazeBuilder>();
    var renderer = scene.Renderer;
    var mazeCenter = new Vector2(maze.OffsetX + maze.Columns * maze.CellSize / 2f,
        maze.OffsetY + maze.Rows * maze.CellSize / 2f);
    if (Vector2.Distance(renderer.WorldToScreen(mazeCenter), renderer.ScreenSize / 2f) > 0.01f)
        throw new Exception("Maze is not centered after resize or level transition.");

    var horizontalCellOffset = renderer.WorldToScreen(mazeCenter + new Vector2(maze.CellSize, 0)) - renderer.WorldToScreen(mazeCenter);
    var verticalCellOffset = renderer.WorldToScreen(mazeCenter + new Vector2(0, maze.CellSize)) - renderer.WorldToScreen(mazeCenter);
    if (MathF.Abs(horizontalCellOffset.Length() - verticalCellOffset.Length()) > 0.01f)
        throw new Exception("Window aspect ratio stretches maze cells.");
    if (Vector2.Distance(renderer.ScreenToWorld(renderer.WorldToScreen(mazeCenter)), mazeCenter) > 0.01f)
        throw new Exception("World-to-screen input mapping does not round-trip.");

    // Invoke the actual inspector callbacks, independent of window placement/DPI.
    var inspectorSystem = scene.ECS.GetSystem<Inspector>();
    var windowsField = typeof(Inspector).GetField("Windows", BindingFlags.Instance | BindingFlags.NonPublic);
    var windows = (Dictionary<string, WindowData>)windowsField.GetValue(inspectorSystem);

    Widget GetWidget(string windowId, string widgetId)
    {
        var window = windows[windowId];
        return window.Widgets[window.WidgetIndex[widgetId]];
    }

    if (frameNumber >= 20 && frameNumber <= 60 && frameNumber % 10 == 0)
    {
        GetWidget("scene", "nextLevel").ButtonCallback();
        var player = scene.ECS.GetSystem<PacmanPlayerController>();
        if (!player.IsTracked || player.CoinsTotal <= 0)
            throw new Exception("Level transition did not create the player and coins.");

        Console.WriteLine($"Level {player.LevelTag}: {player.CoinsTotal} coins, {scene.ECS.EntityCount} entities.");
    }

    if (frameNumber == 25) GetWidget("pipeline", "lighting").DropdownCallback(1);
    if (frameNumber == 35) GetWidget("pipeline", "upscaler").DropdownCallback(0);
    if (frameNumber == 45) GetWidget("pipeline", "upscaler").DropdownCallback(1);
    if (frameNumber == 55) GetWidget("pipeline", "upscaler").DropdownCallback(2);
    if (frameNumber == 65) GetWidget("pipeline", "lighting").DropdownCallback(0);
    if (frameNumber == 70) GetWidget("scene", "restartLevel").ButtonCallback();

    if (frameNumber == 75)
    {
        GetWidget("scene", "pause").ToggleCallback(true);
        if (!scene.ECS.GameplayPaused || !scene.ECS.AnimationPaused)
            throw new Exception("Pause callback did not pause gameplay and animation.");
    }

    if (frameNumber == 80)
    {
        GetWidget("scene", "pause").ToggleCallback(false);
        GetWidget("scene", "prevLevel").ButtonCallback();
    }
}
