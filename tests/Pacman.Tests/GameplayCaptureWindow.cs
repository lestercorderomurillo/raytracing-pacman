using Window = com.radiant.engine.backends.monogame.MonoGameWindow;
using System;
using System.IO;
using System.Linq;
using com.radiant.engine.runtime;
using Microsoft.Xna.Framework;
using RaytracingPacman;

/// <summary>Captures a validated game frame at the requested display resolution.</summary>
sealed class GameplayCaptureWindow : Window
{
    private readonly PacmanMazeLevelScene scene = new();
    private int frame;

    /// <summary>Creates the test fixture with the supplied scene or window configuration.</summary>
    public GameplayCaptureWindow() : base(new GameLoop(), new() { ShowLoader = false, Title = "Pac-Man gameplay capture", Width = 2880, Height = 1521 })
    {
        GameLoop.AddScene(scene);
        GameLoop.ThrottleUnfocused = false;
        GameLoop.SetTargetFps(0);
    }

    /// <inheritdoc/>
    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        if (++frame != 12) return;

        var target = scene.Renderer.SceneRT;
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        if (target.Width != 2880 || target.Height != 1521 || pixels.Count(pixel => pixel.R > 12 || pixel.G > 12 || pixel.B > 12) < 5000)
            throw new Exception("Gameplay capture is empty or has the wrong resolution.");

        Directory.CreateDirectory("artifacts/captures");
        using var output = File.Create("artifacts/captures/gameplay-maximized.png");
        target.SaveAsPng(output, target.Width, target.Height);
        Console.WriteLine("CAPTURE: unedited 2880×1521 gameplay frame.");
        Exit();
    }
}
