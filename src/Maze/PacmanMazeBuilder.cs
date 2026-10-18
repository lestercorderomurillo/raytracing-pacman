using System.Collections.Generic;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace RaytracingPacman;

[SystemTag("Pacman")]
public class PacmanMazeBuilder : com.radiant.engine.core.System
{
    public string[] Layout { get; set; }
    public float CellSize { get; set; } = 70f;
    public float WallThickness { get; set; } = 6f;
    public float WallMargin { get; set; } = 20f;
    public Color WallLight { get; set; } = new Color(100, 180, 255);
    public Color[] WallColors { get; set; }
    public string[] Sections { get; set; }

    public Dictionary<(int, int), int> CoinCells { get; private set; } = new();
    public Dictionary<(int, int), int> PowerPelletCells { get; private set; } = new();

    // Ghost house bounds (grid coordinates, inclusive). (-1,-1,-1,-1) = none.
    public (int left, int top, int right, int bottom) GhostHouse { get; set; } = (-1, -1, -1, -1);

    // Tiles where ghosts cannot turn upward in scatter/chase mode.
    public (int x, int y)[] NoUpTiles { get; set; }

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public bool HasGrid => Grid != null;

    private bool[,] Grid;

    private int GetSection(int cellX, int cellY)
    {
        if (Sections == null || cellY < 0 || cellY >= Rows || cellX < 0 || cellX >= Columns) return 0;

        char sectionCode = Sections[cellY][cellX];
        if (sectionCode >= '0' && sectionCode <= '9') return sectionCode - '0';
        if (sectionCode >= 'a' && sectionCode <= 'z') return sectionCode - 'a' + 10;

        return 0;
    }

    public Color GetWallColor(int cellX, int cellY)
    {
        if (WallColors == null || WallColors.Length == 0) return WallLight;

        int section = GetSection(cellX, cellY);
        return WallColors[section % WallColors.Length];
    }

    public bool IsWall(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= Columns || cellY < 0 || cellY >= Rows) return true;

        return Grid[cellX, cellY];
    }

    public bool CanMove(int cellX, int cellY, int directionX, int directionY)
    {
        int nextCellX = cellX + directionX, nextCellY = cellY + directionY;
        if (nextCellY < 0 || nextCellY >= Rows) return false;

        // Horizontal tunnels wrap to the opposite side.
        if (nextCellX < 0) nextCellX += Columns;
        else if (nextCellX >= Columns) nextCellX -= Columns;

        return !Grid[nextCellX, nextCellY];
    }

    public int WrapX(int cellX)
    {
        if (cellX < 0) return cellX + Columns;
        if (cellX >= Columns) return cellX - Columns;

        return cellX;
    }

    public int HouseDoorY => GhostHouse.top - 1;
    public int HouseDoorLeft => (GhostHouse.left + GhostHouse.right) / 2;
    public int HouseDoorRight => HouseDoorLeft + 1;

    public bool InGhostHouse(int cellX, int cellY)
    {
        var (left, top, right, bottom) = GhostHouse;
        return left >= 0 && cellX >= left && cellX <= right && cellY >= top && cellY <= bottom;
    }

    public bool IsGhostDoor(int cellX, int cellY) =>
        GhostHouse.left >= 0 && cellY == HouseDoorY && cellX >= HouseDoorLeft && cellX <= HouseDoorRight;

    public bool IsNoUpTile(int cellX, int cellY)
    {
        if (NoUpTiles == null) return false;

        for (int tileIndex = 0; tileIndex < NoUpTiles.Length; tileIndex++)
            if (NoUpTiles[tileIndex].x == cellX && NoUpTiles[tileIndex].y == cellY)
                return true;

        return false;
    }

    public Vector2 CellCenter(int cellX, int cellY) => new(
        OffsetX + cellX * CellSize + CellSize / 2f,
        OffsetY + cellY * CellSize + CellSize / 2f);

    public int[] SpawnCoins(float radius, Color emissive, float depth, (int x, int y)? exclude = null)
    {
        var entities = Scene.ECS;
        CoinCells.Clear();

        int coinCount = 0;

        for (int cellY = 0; cellY < Rows; cellY++)
            for (int cellX = 0; cellX < Columns; cellX++)
                if (!Grid[cellX, cellY] && !InGhostHouse(cellX, cellY) && !IsGhostDoor(cellX, cellY)
                    && !(exclude.HasValue && exclude.Value.x == cellX && exclude.Value.y == cellY))
                    coinCount++;

        var entityIds = new int[coinCount];
        int coinIndex = 0;

        for (int cellY = 0; cellY < Rows; cellY++)
            for (int cellX = 0; cellX < Columns; cellX++)
            {
                if (Grid[cellX, cellY] || InGhostHouse(cellX, cellY) || IsGhostDoor(cellX, cellY)) continue;
                if (exclude.HasValue && exclude.Value.x == cellX && exclude.Value.y == cellY) continue;

                int entityId = LightFactory.CreateLight(entities, CellCenter(cellX, cellY), radius,
                    emissive, emissive, depth);
                entityIds[coinIndex++] = entityId;
                CoinCells[(cellX, cellY)] = entityId;
            }

        return entityIds;
    }

    public bool TryCollectCoin(int cellX, int cellY)
    {
        if (!CoinCells.Remove((cellX, cellY), out int entityId)) return false;

        Scene.ECS.DestroyEntity(entityId);
        return true;
    }

    public void ClearCoins()
    {
        var entities = Scene.ECS;

        foreach (var entityId in CoinCells.Values)
            entities.DestroyEntity(entityId);

        CoinCells.Clear();

        foreach (var entityId in PowerPelletCells.Values)
            entities.DestroyEntity(entityId);

        PowerPelletCells.Clear();
    }

    public void SpawnPowerPellets((int x, int y)[] positions, float radius, Color color, float depth)
    {
        var entities = Scene.ECS;
        PowerPelletCells.Clear();

        for (int pelletIndex = 0; pelletIndex < positions.Length; pelletIndex++)
        {
            var (cellX, cellY) = positions[pelletIndex];

            if (CoinCells.Remove((cellX, cellY), out int coinId))
                entities.DestroyEntity(coinId);

            int pelletId = LightFactory.CreateLight(entities, CellCenter(cellX, cellY), radius, color, color, depth);
            PowerPelletCells[(cellX, cellY)] = pelletId;
        }
    }

    public bool TryCollectPowerPellet(int cellX, int cellY)
    {
        if (!PowerPelletCells.Remove((cellX, cellY), out int entityId)) return false;

        Scene.ECS.DestroyEntity(entityId);
        return true;
    }

    public (int x, int y)[] FindCornerPelletPositions()
    {
        var corners = new (int tx, int ty)[]
        {
            (1, 1),
            (Columns - 2, 1),
            (1, Rows - 2),
            (Columns - 2, Rows - 2)
        };

        var pelletPositions = new (int x, int y)[4];

        for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
        {
            float bestDistanceSquared = float.MaxValue;
            pelletPositions[cornerIndex] = (Columns / 2, Rows / 2);

            for (int cellY = 0; cellY < Rows; cellY++)
                for (int cellX = 0; cellX < Columns; cellX++)
                {
                    if (IsWall(cellX, cellY) || InGhostHouse(cellX, cellY) || IsGhostDoor(cellX, cellY)) continue;

                    float horizontalOffset = cellX - corners[cornerIndex].tx;
                    float verticalOffset = cellY - corners[cornerIndex].ty;
                    float distanceSquared = horizontalOffset * horizontalOffset + verticalOffset * verticalOffset;

                    if (distanceSquared < bestDistanceSquared)
                    {
                        bestDistanceSquared = distanceSquared;
                        pelletPositions[cornerIndex] = (cellX, cellY);
                    }
                }
        }

        return pelletPositions;
    }

    public int[] SpawnAtCells((int x, int y)[] cells, Color[] colors, float radius, float depth,
        Texture2D texture = null)
    {
        var entities = Scene.ECS;
        var entityIds = new int[cells.Length];

        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            var cellCenter = CellCenter(cells[cellIndex].x, cells[cellIndex].y);
            entityIds[cellIndex] = LightFactory.CreateLight(entities, cellCenter, radius, colors[cellIndex], colors[cellIndex], depth, texture);
            entities.AddComponent<MotionTrackable>(entityIds[cellIndex]);
        }

        return entityIds;
    }

    public void BuildMaze()
    {
        Columns = Layout[0].Length;
        Rows = Layout.Length;

        float mazeWidth = Columns * CellSize;
        float mazeHeight = Rows * CellSize;
        var virtualSize = Renderer.VirtualSize;
        OffsetX = (virtualSize.X - mazeWidth) / 2f;
        OffsetY = (virtualSize.Y - mazeHeight) / 2f;

        Grid = new bool[Columns, Rows];

        for (int cellY = 0; cellY < Rows; cellY++)
            for (int cellX = 0; cellX < Columns; cellX++)
                Grid[cellX, cellY] = Layout[cellY][cellX] == '1';
    }

    public void ClearMaze()
    {
        Grid = null;
    }
}
