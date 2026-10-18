using System.Collections.Generic;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace RaytracingPacman;

[RunAfter(typeof(PacmanMazeBuilder))]
[RunBefore(typeof(Geometry))]
[SystemTag("Pacman")]
public class PacmanMazeRenderer : com.radiant.engine.core.System
{
    private RenderTarget2D MazeEmissiveTarget;
    private RenderTarget2D MazeAbsorptionTarget;
    private PacmanMazeBuilder Maze;
    private Geometry Geometry;

    public override void Initialize()
    {
        Maze = Scene.ECS.GetSystem<PacmanMazeBuilder>();
        Geometry = Scene.ECS.GetSystem<Geometry>();
    }

    public void RenderMaze()
    {
        if (Maze == null || !Maze.HasGrid) return;

        DrawMazeToRenderTargets();
    }

    public void ClearRenderTargets()
    {
        MazeEmissiveTarget?.Dispose();
        MazeEmissiveTarget = null;

        MazeAbsorptionTarget?.Dispose();
        MazeAbsorptionTarget = null;
    }

    public override void Render()
    {
        if (Geometry == null) return;

        Geometry.BackgroundEmissive = MazeEmissiveTarget;
        Geometry.BackgroundAbsorption = MazeAbsorptionTarget;
    }

    public override void OnResize()
    {
        if (Maze != null && Maze.HasGrid) DrawMazeToRenderTargets();
    }

    public override void Dispose()
    {
        ClearRenderTargets();
    }

    private void DrawMazeToRenderTargets()
    {
        MazeEmissiveTarget?.Dispose();
        MazeAbsorptionTarget?.Dispose();

        MazeEmissiveTarget = Renderer.CreateRenderTarget(Renderer.ScreenWidth, Renderer.ScreenHeight);
        MazeAbsorptionTarget = Renderer.CreateRenderTarget(Renderer.ScreenWidth, Renderer.ScreenHeight);

        var solidTexture = Renderer.GetSolidTexture(Color.White);
        int columnCount = Maze.Columns;
        int rowCount = Maze.Rows;
        float cellSize = Maze.CellSize;
        float offsetX = Maze.OffsetX;
        float offsetY = Maze.OffsetY;
        float wallThickness = Maze.WallThickness;
        float wallMargin = Maze.WallMargin;

        float[] gridX = new float[columnCount + 1];
        float[] gridY = new float[rowCount + 1];

        for (int column = 0; column <= columnCount; column++)
            gridX[column] = offsetX + column * cellSize;

        for (int row = 0; row <= rowCount; row++)
            gridY[row] = offsetY + row * cellSize;

        var wallFills = new List<Rectangle>();
        var wallBorders = new List<(Rectangle rect, Color color)>();

        for (int row = 0; row < rowCount; row++)
            for (int column = 0; column < columnCount; column++)
            {
                if (!Maze.IsWall(column, row)) continue;

                float cellLeft = gridX[column];
                float cellTop = gridY[row];
                float cellRight = gridX[column + 1];
                float cellBottom = gridY[row + 1];

                bool leftExposed = !Maze.IsWall(column - 1, row);
                bool rightExposed = !Maze.IsWall(column + 1, row);
                bool topExposed = !Maze.IsWall(column, row - 1);
                bool bottomExposed = !Maze.IsWall(column, row + 1);

                float wallLeft = cellLeft + (leftExposed ? wallMargin : 0);
                float wallTop = cellTop + (topExposed ? wallMargin : 0);
                float wallRight = cellRight - (rightExposed ? wallMargin : 0);
                float wallBottom = cellBottom - (bottomExposed ? wallMargin : 0);

                bool cutTopLeft = !leftExposed && !topExposed && !Maze.IsWall(column - 1, row - 1);
                bool cutTopRight = !rightExposed && !topExposed && !Maze.IsWall(column + 1, row - 1);
                bool cutBottomLeft = !leftExposed && !bottomExposed && !Maze.IsWall(column - 1, row + 1);
                bool cutBottomRight = !rightExposed && !bottomExposed && !Maze.IsWall(column + 1, row + 1);

                if (!cutTopLeft && !cutTopRight && !cutBottomLeft && !cutBottomRight)
                {
                    wallFills.Add(Renderer.VirtualToScreenRect(wallLeft, wallTop, wallRight - wallLeft, wallBottom - wallTop));
                }
                else
                {
                    float innerTop = cellTop + wallMargin;
                    float innerBottom = cellBottom - wallMargin;

                    if (wallTop < innerTop)
                    {
                        float segmentLeft = cutTopLeft ? cellLeft + wallMargin : wallLeft;
                        float segmentRight = cutTopRight ? cellRight - wallMargin : wallRight;
                        wallFills.Add(Renderer.VirtualToScreenRect(segmentLeft, wallTop, segmentRight - segmentLeft, innerTop - wallTop));
                    }

                    if (innerTop < innerBottom)
                        wallFills.Add(Renderer.VirtualToScreenRect(wallLeft, innerTop, wallRight - wallLeft, innerBottom - innerTop));

                    if (innerBottom < wallBottom)
                    {
                        float segmentLeft = cutBottomLeft ? cellLeft + wallMargin : wallLeft;
                        float segmentRight = cutBottomRight ? cellRight - wallMargin : wallRight;
                        wallFills.Add(Renderer.VirtualToScreenRect(segmentLeft, innerBottom, segmentRight - segmentLeft, wallBottom - innerBottom));
                    }
                }

                if (!leftExposed && !rightExposed && !topExposed && !bottomExposed && !cutTopLeft && !cutTopRight && !cutBottomLeft && !cutBottomRight) continue;

                Color color = Maze.GetWallColor(column, row);

                if (topExposed)
                    wallBorders.Add((Renderer.VirtualToScreenRect(wallLeft, wallTop - wallThickness, wallRight - wallLeft, wallThickness), color));
                if (bottomExposed)
                    wallBorders.Add((Renderer.VirtualToScreenRect(wallLeft, wallBottom, wallRight - wallLeft, wallThickness), color));

                if (leftExposed)
                {
                    float segmentTop = topExposed ? wallTop - wallThickness : wallTop;
                    float segmentBottom = bottomExposed ? wallBottom + wallThickness : wallBottom;
                    wallBorders.Add((Renderer.VirtualToScreenRect(wallLeft - wallThickness, segmentTop, wallThickness, segmentBottom - segmentTop), color));
                }

                if (rightExposed)
                {
                    float segmentTop = topExposed ? wallTop - wallThickness : wallTop;
                    float segmentBottom = bottomExposed ? wallBottom + wallThickness : wallBottom;
                    wallBorders.Add((Renderer.VirtualToScreenRect(wallRight, segmentTop, wallThickness, segmentBottom - segmentTop), color));
                }

                if (cutTopLeft)
                {
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellLeft, cellTop + wallMargin - wallThickness, wallMargin, wallThickness), color));
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellLeft + wallMargin - wallThickness, cellTop, wallThickness, wallMargin - wallThickness), color));
                }

                if (cutTopRight)
                {
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellRight - wallMargin, cellTop + wallMargin - wallThickness, wallMargin, wallThickness), color));
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellRight - wallMargin, cellTop, wallThickness, wallMargin - wallThickness), color));
                }

                if (cutBottomLeft)
                {
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellLeft, cellBottom - wallMargin, wallMargin, wallThickness), color));
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellLeft + wallMargin - wallThickness, cellBottom - wallMargin + wallThickness, wallThickness, wallMargin - wallThickness), color));
                }

                if (cutBottomRight)
                {
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellRight - wallMargin, cellBottom - wallMargin, wallMargin, wallThickness), color));
                    wallBorders.Add((Renderer.VirtualToScreenRect(cellRight - wallMargin, cellBottom - wallMargin + wallThickness, wallThickness, wallMargin - wallThickness), color));
                }
            }

        // Draw wall absorption.
        Renderer.Reset().Configure(BlendState.Opaque).SetTarget(MazeAbsorptionTarget).Clear(Color.Transparent);

        for (int fillIndex = 0; fillIndex < wallFills.Count; fillIndex++)
            Renderer.DrawTexture(solidTexture, wallFills[fillIndex], Color.White);

        for (int borderIndex = 0; borderIndex < wallBorders.Count; borderIndex++)
            Renderer.DrawTexture(solidTexture, wallBorders[borderIndex].rect, wallBorders[borderIndex].color);

        Renderer.Commit();

        // Draw emissive wall borders.
        Renderer.Reset().Configure(BlendState.Opaque).SetTarget(MazeEmissiveTarget).Clear(Color.Transparent);

        for (int borderIndex = 0; borderIndex < wallBorders.Count; borderIndex++)
            Renderer.DrawTexture(solidTexture, wallBorders[borderIndex].rect, wallBorders[borderIndex].color);

        Renderer.Commit();

        Renderer.SetTarget(null);
    }
}
