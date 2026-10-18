using System;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace RaytracingPacman;

[RunAfter(typeof(PacmanPlayerController))]
[SystemTag("Pacman")]
public class PacmanHUD : com.radiant.engine.core.System
{
    public override RenderLayer RenderLayer => RenderLayer.Gameplay;

    private PacmanPlayerController Player;
    private PacmanMazeBuilder Maze;
    private Geometry Geometry;

    const string HudFontName = "PressStart2P";

    public override void Initialize()
    {
        Player = Scene.ECS.GetSystem<PacmanPlayerController>();
        Maze = Scene.ECS.GetSystem<PacmanMazeBuilder>();
        Geometry = Scene.ECS.GetSystem<Geometry>();
    }

    public override void LateRender()
    {
        if (Player == null || !Player.IsTracked || Geometry.IsDebugHidingGameplay) return;

        // HUD text is measured in display pixels so it stays readable in a small window.
        float HudFontSize = MathF.Round(Math.Clamp(Renderer.ScreenHeight / 720f * 14f, 10f, 24f));
        float padding = 8f;
        float iconSize = HudFontSize + 4f;
        float gap = 8f;

        var MazeTopLeft = Renderer.WorldToScreen(new Vector2(Maze.OffsetX + 40f, Maze.OffsetY));
        float mazeLeft = MazeTopLeft.X;
        float mazeRight = Renderer.WorldToScreen(new Vector2(Maze.OffsetX + Maze.Cols * Maze.CellSize - 40f, Maze.OffsetY)).X;
        float y = MathF.Max(8f, MazeTopLeft.Y - HudFontSize - 8f);

        string LevelLabel = $"LEVEL {Player.LevelTag}";
        var tagSize = Renderer.MeasureString(HudFontName, HudFontSize, LevelLabel);
        float tagBlockWidth = tagSize.X;
        float textHeight = tagSize.Y;

        string collected = Player.CoinsCollected.ToString();
        string separator = " / ";
        string total = Player.CoinsTotal.ToString();
        var collectedSize = Renderer.MeasureString(HudFontName, HudFontSize, collected);
        var separatorSize = Renderer.MeasureString(HudFontName, HudFontSize, separator);
        var totalSize = Renderer.MeasureString(HudFontName, HudFontSize, total);
        float coinTextWidth = collectedSize.X + separatorSize.X + totalSize.X;
        float coinBlockWidth = iconSize + gap + coinTextWidth;

        Renderer.BeginDraw(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        float tagX = mazeLeft;
        var tagBg = new Rectangle(
            (int)(tagX - padding), (int)(y - padding * 0.6f),
            (int)(tagBlockWidth + padding * 2f), (int)(textHeight + padding * 1.2f));
        Renderer.DrawSprite(Renderer.GetSolidTexture(Color.White), tagBg, new Color(0, 0, 0, 180));
        Renderer.DrawString(HudFontName, HudFontSize, LevelLabel, new Vector2(tagX, y), new Color(255, 255, 255));

        float coinX = mazeRight - coinBlockWidth;
        var coinBg = new Rectangle(
            (int)(coinX - padding), (int)(y - padding * 0.6f),
            (int)(coinBlockWidth + padding * 2f), (int)(textHeight + padding * 1.2f));
        Renderer.DrawSprite(Renderer.GetSolidTexture(Color.White), coinBg, new Color(0, 0, 0, 180));

        var coinTex = Renderer.GetCircleTexture((int)iconSize);
        var iconRect = new Rectangle(
            (int)coinX, (int)(y + (textHeight - iconSize) / 2f),
            (int)iconSize, (int)iconSize);
        Renderer.DrawSprite(coinTex, iconRect, Player.CoinColor);

        float textX = coinX + iconSize + gap;
        Renderer.DrawString(HudFontName, HudFontSize, collected, new Vector2(textX, y), Player.CoinColor);
        textX += collectedSize.X;
        Renderer.DrawString(HudFontName, HudFontSize, separator, new Vector2(textX, y), new Color(200, 200, 200));
        textX += separatorSize.X;
        Renderer.DrawString(HudFontName, HudFontSize, total, new Vector2(textX, y), new Color(255, 255, 255));

        Renderer.EndDraw();
    }
}
