using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using RaytracingPacman;
using Radiant.Testing;

var Engine = typeof(Scene).Assembly;
var Game = typeof(PacmanMazeLevelScene).Assembly;
if (Engine == Game || Engine.GetTypes().Any(Type => Type.Name.Contains("Pacman") || Type.Name.EndsWith("LevelScene")))
    throw new Exception("Engine contains game implementation types.");
if (Engine.GetTypes().Any(Type => Type.Name is "SimpleLightScene" or "TilesetScene"))
    throw new Exception("Engine library contains test scenes.");
if (Engine.GetReferencedAssemblies().Any(Name => Name.Name == Game.GetName().Name))
    throw new Exception("Engine depends on game assembly.");
for (int Seed = 0; Seed < 64; Seed++)
{
    var Maze = PacmanMazeGenerator.Generate(Seed);
    if (Maze.Length != 31 || Maze.Any(Row => Row.Length != 28))
        throw new Exception($"Invalid maze dimensions for seed {Seed}.");
    if (!Maze.SequenceEqual(PacmanMazeGenerator.Generate(Seed)))
        throw new Exception($"Maze seed {Seed} is not reproducible.");
    if (Maze.Any(Row => !Row.SequenceEqual(Row.Reverse())))
        throw new Exception($"Maze seed {Seed} is not symmetric.");
}
Console.WriteLine("PASS: independent engine/game assemblies, engine test isolation, 64 deterministic symmetric mazes.");
if (args.Contains("--graphics"))
{
    using var Window = new SmokeWindow(new PacmanMazeLevelScene(), "artifacts/smoke", ExerciseGame);
    Window.Run();
}

static void ExerciseGame(Scene Scene, int Frame)
{
    // Invoke the actual inspector callbacks, independent of window placement/DPI.
    var InspectorSystem = Scene.ECS.GetSystem<Inspector>();
    var WindowsField = typeof(Inspector).GetField("Windows", BindingFlags.Instance | BindingFlags.NonPublic);
    var Windows = (Dictionary<string, WindowData>)WindowsField.GetValue(InspectorSystem);
    Widget GetWidget(string WindowId, string WidgetId)
    {
        var Window = Windows[WindowId];
        return Window.Widgets[Window.WidgetIndex[WidgetId]];
    }
    if (Frame >= 20 && Frame <= 60 && Frame % 10 == 0)
    {
        GetWidget("scene", "nextLevel").ButtonCallback();
        var Player = Scene.ECS.GetSystem<PacmanPlayerController>();
        if (!Player.IsTracked || Player.CoinsTotal <= 0)
            throw new Exception("Level transition did not create the player and coins.");
        Console.WriteLine($"Level {Player.LevelTag}: {Player.CoinsTotal} coins, {Scene.ECS.EntityCount} entities.");
    }
    if (Frame == 25) GetWidget("pipeline", "lighting").DropdownCallback(1);
    if (Frame == 35) GetWidget("pipeline", "upscaler").DropdownCallback(0);
    if (Frame == 45) GetWidget("pipeline", "upscaler").DropdownCallback(1);
    if (Frame == 55) GetWidget("pipeline", "upscaler").DropdownCallback(2);
    if (Frame == 65) GetWidget("pipeline", "lighting").DropdownCallback(0);
    if (Frame == 70) GetWidget("scene", "restartLevel").ButtonCallback();
    if (Frame == 75)
    {
        GetWidget("scene", "pause").ToggleCallback(true);
        if (!Scene.ECS.GameplayPaused || !Scene.ECS.AnimationPaused)
            throw new Exception("Pause callback did not pause gameplay and animation.");
    }
    if (Frame == 80)
    {
        GetWidget("scene", "pause").ToggleCallback(false);
        GetWidget("scene", "prevLevel").ButtonCallback();
    }
}
