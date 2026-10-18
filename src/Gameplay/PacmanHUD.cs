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
        float hudFontSize = MathF.Round(Math.Clamp(Renderer.ScreenHeight / 720f * 14f, 10f, 24f));
        float padding = 8f;
        float iconSize = hudFontSize + 4f;
        float gap = 8f;

        var mazeTopLeft = Renderer.WorldToScreen(new Vector2(Maze.OffsetX + 40f, Maze.OffsetY));
        float mazeLeft = mazeTopLeft.X;
        float mazeRight = Renderer.WorldToScreen(new Vector2(Maze.OffsetX + Maze.Columns * Maze.CellSize - 40f, Maze.OffsetY)).X;
        float textY = MathF.Max(8f, mazeTopLeft.Y - hudFontSize - 8f);

        string levelLabel = $"LEVEL {Player.LevelTag}";
        var levelTextSize = Renderer.MeasureString(HudFontName, hudFontSize, levelLabel);
        float levelBlockWidth = levelTextSize.X;
        float textHeight = levelTextSize.Y;

        string collectedText = Player.CoinsCollected.ToString();
        string separator = " / ";
        string totalText = Player.CoinsTotal.ToString();
        var collectedTextSize = Renderer.MeasureString(HudFontName, hudFontSize, collectedText);
        var separatorSize = Renderer.MeasureString(HudFontName, hudFontSize, separator);
        var totalTextSize = Renderer.MeasureString(HudFontName, hudFontSize, totalText);
        float coinTextWidth = collectedTextSize.X + separatorSize.X + totalTextSize.X;
        float coinBlockWidth = iconSize + gap + coinTextWidth;

        Renderer.BeginDraw(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        float levelX = mazeLeft;
        var levelBackground = new Rectangle(
            (int)(levelX - padding), (int)(textY - padding * 0.6f),
            (int)(levelBlockWidth + padding * 2f), (int)(textHeight + padding * 1.2f));
        Renderer.DrawSprite(Renderer.GetSolidTexture(Color.White), levelBackground, new Color(0, 0, 0, 180));
        Renderer.DrawString(HudFontName, hudFontSize, levelLabel, new Vector2(levelX, textY), new Color(255, 255, 255));

        float coinX = mazeRight - coinBlockWidth;
        var coinBackground = new Rectangle(
            (int)(coinX - padding), (int)(textY - padding * 0.6f),
            (int)(coinBlockWidth + padding * 2f), (int)(textHeight + padding * 1.2f));
        Renderer.DrawSprite(Renderer.GetSolidTexture(Color.White), coinBackground, new Color(0, 0, 0, 180));

        var coinTexture = Renderer.GetCircleTexture((int)iconSize);
        var iconRectangle = new Rectangle(
            (int)coinX, (int)(textY + (textHeight - iconSize) / 2f),
            (int)iconSize, (int)iconSize);
        Renderer.DrawSprite(coinTexture, iconRectangle, Player.CoinColor);

        float textX = coinX + iconSize + gap;
        Renderer.DrawString(HudFontName, hudFontSize, collectedText, new Vector2(textX, textY), Player.CoinColor);
        textX += collectedTextSize.X;
        Renderer.DrawString(HudFontName, hudFontSize, separator, new Vector2(textX, textY), new Color(200, 200, 200));
        textX += separatorSize.X;
        Renderer.DrawString(HudFontName, hudFontSize, totalText, new Vector2(textX, textY), new Color(255, 255, 255));

        Renderer.EndDraw();
    }
}
