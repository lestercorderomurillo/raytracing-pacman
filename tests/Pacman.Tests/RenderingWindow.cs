using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using com.radiant.engine.bundle;
using com.radiant.engine.runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RaytracingPacman;

sealed class RenderingWindow : Window
{
    private readonly PacmanMazeLevelScene Scene = new();
    private readonly (int width, int height, int quality, int upscaler, int lighting)[] Cases =
    [
        (1280, 720, 1, 2, 0),
        (1024, 640, 2, 2, 0),
        (800, 1000, 0, 2, 0),
        (1440, 600, 1, 0, 0),
        (960, 960, 2, 0, 1),
        (1280, 720, 0, 0, 1),
        (1024, 640, 1, 1, 1),
        (800, 1000, 2, 1, 0),
        (1440, 600, 0, 1, 0),
        (1024, 640, 1, 2, 0)
    ];
    private readonly List<Vector2> CharacterCenters = [];
    private Dictionary<string, WindowData> InspectorWindows;
    private int Frame;
    private int CaseIndex = -1;
    private int CaseStart;
    private float BodyRadius;

    public RenderingWindow()
        : base(new GameLoop(), new() { Title = "Pac-Man rendering regression checks", Width = 1280, Height = 720 })
    {
        GameLoop.AddScene(Scene);
        GameLoop.ThrottleUnfocused = false;
        GameLoop.SetTargetFps(0);
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (Frame != 3) return;

        if (UDRQuality.Index != 1 || Scene.Renderer.RenderScale != 0.5f)
            throw new Exception("Default gameplay must use Balanced upscaling.");

        var inspector = Scene.ECS.GetSystem<Inspector>();
        InspectorWindows = (Dictionary<string, WindowData>)typeof(Inspector)
            .GetField("Windows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inspector);
        inspector.Enabled = false;
        StageCharacters();
        NextCase();
    }

    private Widget GetWidget(string windowId, string widgetId)
    {
        var window = InspectorWindows[windowId];
        return window.Widgets[window.WidgetIndex[widgetId]];
    }

    private void StageCharacters()
    {
        var maze = Scene.ECS.GetSystem<PacmanMazeBuilder>();
        var ghosts = Scene.ECS.GetSystem<PacmanGhostAI>();
        var player = Scene.ECS.GetSystem<PacmanPlayerController>();
        ghosts.Clear();
        ghosts.ClearRainbow();
        BodyRadius = ghosts.BodyRadius;
        var bodyTexture = Scene.Renderer.GetTexture("Ghost");
        (int x, int y)[] cells = [(1, 5), (26, 5)];
        GhostEntry[] entries = [new() { Type = PacmanGhostType.Blinky }, new() { Type = PacmanGhostType.Pinky }];
        var colors = entries.Select(entry => PacmanGhostAI.PersonalityColor(entry.Type)).ToArray();
        var ghostIds = maze.SpawnAtCells(cells, colors, BodyRadius, 65530f, bodyTexture);
        ghosts.Track(ghostIds, cells, entries);
        CharacterCenters.AddRange(cells.Select(cell => maze.CellCenter(cell.x, cell.y)));

        var rainbowCenter = maze.CellCenter(26, 23);
        int rainbowId = LightFactory.CreateLight(Scene.ECS, rainbowCenter, BodyRadius,
            Color.Transparent, Color.Red, 65530f, bodyTexture);
        ghosts.TrackRainbow(rainbowId, (26, 23));
        CharacterCenters.Add(rainbowCenter);

        player.Track(player.EntityId, (1, 23), 65530f);
        var playerCenter = maze.CellCenter(1, 23);
        Scene.ECS.GetComponent<Transform>(player.EntityId).Position = new Vector3(playerCenter, 65530f);
        CharacterCenters.Add(playerCenter);
        Scene.ECS.GameplayPaused = true;
        Scene.ECS.AnimationPaused = true;
    }

    private void NextCase()
    {
        CaseIndex++;
        if (CaseIndex == Cases.Length)
        {
            Console.WriteLine("PASS: fitted ghost/Rainbow/player eyes, all UDR qualities and upscalers, both lighting modes, resize, Native bypass, and disabled-upscaler isolation.");
            Exit();
            return;
        }

        var testCase = Cases[CaseIndex];
        GraphicsDeviceManager.PreferredBackBufferWidth = testCase.width;
        GraphicsDeviceManager.PreferredBackBufferHeight = testCase.height;
        GraphicsDeviceManager.ApplyChanges();
        GetWidget("pipeline", "upscaler").DropdownCallback(testCase.upscaler);
        GetWidget("pipeline", "lighting").DropdownCallback(testCase.lighting);
        UDRQuality.Set(testCase.quality);
        CaseStart = Frame;
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        Frame++;
        if (CaseIndex < 0 || CaseIndex >= Cases.Length || Frame - CaseStart < 12) return;

        CheckPipeline();
        CheckEyes();
        CheckDisabledUpscaler();
        Console.WriteLine($"PASS rendering case {CaseIndex + 1}: {Cases[CaseIndex]}");
        NextCase();
        Scene.Renderer.PresentToBackBuffer();
    }

    private void CheckPipeline()
    {
        var renderer = Scene.Renderer;
        var testCase = Cases[CaseIndex];
        if (renderer.ScreenWidth != testCase.width || renderer.ScreenHeight != testCase.height)
            throw new Exception("Renderer did not follow the window resize.");
        if (renderer.RenderScale != UDRQuality.ScaleNormalized)
            throw new Exception("Selected quality did not reach the renderer.");

        Texture2D lighting = testCase.lighting == 0
            ? Scene.ECS.GetSystem<HRCGI>().GetOutput() : Scene.ECS.GetSystem<RCGI>().GetOutput();
        var expectedSize = testCase.lighting == 0
            ? new Vector2(renderer.ScaledHigherPowerOfTwo) : renderer.ScaledSize;
        if (lighting.Width != expectedSize.X || lighting.Height != expectedSize.Y)
            throw new Exception("Lighting buffers did not use the selected render scale.");
        var color = Scene.ECS.GetSystem<ColorManagement>().GetOutput();
        if (color.Width != lighting.Width || color.Height != lighting.Height)
            throw new Exception("Tonemapping did not follow lighting resolution.");

        com.radiant.engine.core.System[] upscalers =
        [Scene.ECS.GetSystem<UDR1>(), Scene.ECS.GetSystem<UDR2>(), Scene.ECS.GetSystem<UDR3>()];
        if (upscalers.Count(upscaler => upscaler.Enabled) != 1 || !upscalers[testCase.upscaler].Enabled)
            throw new Exception("Exactly the selected upscaler must be active.");
        if (testCase.quality == 2 && testCase.upscaler > 0)
        {
            var active = upscalers[testCase.upscaler];
            int accumulatedFrames = (int)active.GetType().GetField("FrameIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(active);
            if (accumulatedFrames != 0)
                throw new Exception("Native mode still runs unused temporal upscaling passes.");
        }
    }

    private void CheckEyes()
    {
        var renderer = Scene.Renderer;
        var target = renderer.SceneRT;
        var withEyes = new Color[target.Width * target.Height];
        target.GetData(withEyes);
        Directory.CreateDirectory("artifacts/rendering");
        using (var output = File.Create($"artifacts/rendering/case-{CaseIndex}.png"))
            target.SaveAsPng(output, target.Width, target.Height);

        // Redraw the same GPU buffers without the eye overlays to isolate their actual pixels.
        var ghosts = Scene.ECS.GetSystem<PacmanGhostAI>();
        var player = Scene.ECS.GetSystem<PacmanPlayerController>();
        ghosts.Enabled = false;
        player.Enabled = false;
        GameLoop.Render();
        ghosts.Enabled = true;
        player.Enabled = true;
        var withoutEyes = new Color[withEyes.Length];
        target.GetData(withoutEyes);
        var bounds = CharacterCenters.Select(center => renderer.VirtualToScreenRect(
            center.X - BodyRadius, center.Y - BodyRadius, BodyRadius * 2, BodyRadius * 2)).ToArray();
        var eyePixels = new int[bounds.Length];
        int outsideBody = 0;

        for (int pixelIndex = 0; pixelIndex < withEyes.Length; pixelIndex++)
        {
            var first = withEyes[pixelIndex];
            var second = withoutEyes[pixelIndex];
            if (Math.Abs(first.R - second.R) + Math.Abs(first.G - second.G) + Math.Abs(first.B - second.B) < 12) continue;

            int pixelX = pixelIndex % target.Width;
            int pixelY = pixelIndex / target.Width;
            int characterIndex = Array.FindIndex(bounds, rectangle => rectangle.Contains(pixelX, pixelY));
            if (characterIndex >= 0)
                eyePixels[characterIndex]++;
            else
                outsideBody++;
        }

        if (outsideBody != 0 || eyePixels.Any(count => count < 2))
            throw new Exception($"Eye overlays are detached: {outsideBody} pixels outside bodies; eye pixels per body: {string.Join(", ", eyePixels)}.");
    }

    private void CheckDisabledUpscaler()
    {
        GetWidget("pipeline", "upscalerToggle").ToggleCallback(false);
        UDRQuality.Set(UDRQuality.Index == 0 ? 1 : 0);
        if (Scene.Renderer.RenderScale != 1f)
            throw new Exception("A disposed upscaler still reacts to quality changes.");
        GetWidget("pipeline", "upscalerToggle").ToggleCallback(true);
        if (Scene.Renderer.RenderScale != UDRQuality.ScaleNormalized)
            throw new Exception("Re-enabling upscaling did not restore the selected quality.");
    }
}
