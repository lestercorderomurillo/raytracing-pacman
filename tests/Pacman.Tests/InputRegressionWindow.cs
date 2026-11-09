using System;
using com.radiant.engine.runtime;
using com.radiant.engine.input;
using Microsoft.Xna.Framework;
using RaytracingPacman;

/// <summary>Exercises gameplay through Radiant input snapshots on an isolated window.</summary>
internal sealed class InputRegressionWindow : Window
{
    private sealed class ScriptedInput : IInputSource
    {
        public InputSnapshot Snapshot;
        public InputSnapshot Capture() => Snapshot;
    }
    private readonly ScriptedInput input = new();
    private readonly PacmanMazeLevelScene scene = new();
    private int step;
    private Vector2 start;
    private Key moveKey;

    public InputRegressionWindow() : base(new GameLoop(), new() { Title = "Game input regression", Width = 640, Height = 480 })
    {
        GameLoop.AddScene(scene);
        GameLoop.InputSource = input;
        GameLoop.ThrottleUnfocused = false;
    }

    protected override void Update(GameTime gameTime)
    {
        var player = scene.ECS?.GetSystem<PacmanPlayerController>();
        if (step == 1)
        {
            var maze = scene.ECS.GetSystem<PacmanMazeBuilder>();
            start = player.WorldPosition;

            foreach (var direction in new[] { (Key.Right, 1, 0), (Key.Left, -1, 0), (Key.Up, 0, -1), (Key.Down, 0, 1) })
                if (maze.CanMove(player.Cell.x, player.Cell.y, direction.Item2, direction.Item3)) { moveKey = direction.Item1; break; }

            if (moveKey == Key.None) throw new Exception("No walkable starting direction.");
        }
        input.Snapshot = new(new KeyboardState(step >= 1 ? new[] { moveKey } : Array.Empty<Key>()), default);
        base.Update(gameTime);

        if (++step == 8)
        {
            player = scene.ECS.GetSystem<PacmanPlayerController>();
            if (!player.HasMoved || Vector2.Distance(start, player.WorldPosition) < 0.01f)
                throw new Exception("Radiant arrow input did not move Pacman.");
            Console.WriteLine("PASS: Pacman moves using Radiant keyboard snapshots and frame timing.");
            Exit();
        }
    }
}
