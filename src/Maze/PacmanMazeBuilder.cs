using System.Collections.Generic;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using Microsoft.Xna.Framework.Graphics;

namespace RaytracingPacman;

/// <summary>Builds maze entities and tracks walls, collectibles, and movement constraints.</summary>
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

    /// <summary>Finds the maze section containing a tile for section-based wall colors.</summary>
    private int GetSection(int cellX, int cellY)
    {
        if (Sections == null || cellY < 0 || cellY >= Rows || cellX < 0 || cellX >= Columns) return 0;

        char sectionCode = Sections[cellY][cellX];
        if (sectionCode >= '0' && sectionCode <= '9') return sectionCode - '0';
        if (sectionCode >= 'a' && sectionCode <= 'z') return sectionCode - 'a' + 10;

        return 0;
    }

    /// <summary>Returns the configured wall color for the tile's maze section.</summary>
    public Color GetWallColor(int cellX, int cellY)
    {
        if (WallColors == null || WallColors.Length == 0) return WallLight;

        int section = GetSection(cellX, cellY);
        return WallColors[section % WallColors.Length];
    }

    /// <summary>Reports whether a cell blocks normal maze movement.</summary>
    public bool IsWall(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= Columns || cellY < 0 || cellY >= Rows) return true;

        return Grid[cellX, cellY];
    }

    /// <summary>Checks whether a step from a cell reaches a traversable maze tile.</summary>
    public bool CanMove(int cellX, int cellY, int directionX, int directionY)
    {
        int nextCellX = cellX + directionX, nextCellY = cellY + directionY;
        if (nextCellY < 0 || nextCellY >= Rows) return false;

        // Horizontal tunnels wrap to the opposite side.
        if (nextCellX < 0) nextCellX += Columns;
        else if (nextCellX >= Columns) nextCellX -= Columns;

        return !Grid[nextCellX, nextCellY];
    }

    /// <summary>Wraps horizontal cell coordinates through the maze's side tunnels.</summary>
    public int WrapX(int cellX)
    {
        if (cellX < 0) return cellX + Columns;
        if (cellX >= Columns) return cellX - Columns;

        return cellX;
    }

    public int HouseDoorY => GhostHouse.top - 1;
    public int HouseDoorLeft => (GhostHouse.left + GhostHouse.right) / 2;
    public int HouseDoorRight => HouseDoorLeft + 1;

    /// <summary>Reports whether a cell belongs to the ghost-house interior.</summary>
    public bool InGhostHouse(int cellX, int cellY)
    {
        var (left, top, right, bottom) = GhostHouse;
        return left >= 0 && cellX >= left && cellX <= right && cellY >= top && cellY <= bottom;
    }

    /// <summary>Reports whether a cell is part of the ghost-house doorway.</summary>
    public bool IsGhostDoor(int cellX, int cellY) =>
        GhostHouse.left >= 0 && cellY == HouseDoorY && cellX >= HouseDoorLeft && cellX <= HouseDoorRight;

    /// <summary>Reports whether regular ghost movement forbids an upward turn at this cell.</summary>
    public bool IsNoUpTile(int cellX, int cellY)
    {
        if (NoUpTiles == null) return false;

        for (int tileIndex = 0; tileIndex < NoUpTiles.Length; tileIndex++)
            if (NoUpTiles[tileIndex].x == cellX && NoUpTiles[tileIndex].y == cellY)
                return true;

        return false;
    }

    /// <summary>Converts maze-cell coordinates to the cell's world-space center.</summary>
    public Vector2 CellCenter(int cellX, int cellY) => new(
        OffsetX + cellX * CellSize + CellSize / 2f,
        OffsetY + cellY * CellSize + CellSize / 2f);

    /// <summary>Creates coin entities on eligible tiles, optionally excluding one cell.</summary>
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

    /// <summary>Removes a coin from the requested cell and reports whether one was collected.</summary>
    public bool TryCollectCoin(int cellX, int cellY)
    {
        if (!CoinCells.Remove((cellX, cellY), out int entityId)) return false;

        Scene.ECS.DestroyEntity(entityId);
        return true;
    }

    /// <summary>Destroys collectible entities and clears their cell lookups.</summary>
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

    /// <summary>Creates power-pellet entities at the requested maze cells.</summary>
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

    /// <summary>Removes a power pellet from the cell and reports whether one was collected.</summary>
    public bool TryCollectPowerPellet(int cellX, int cellY)
    {
        if (!PowerPelletCells.Remove((cellX, cellY), out int entityId)) return false;

        Scene.ECS.DestroyEntity(entityId);
        return true;
    }

    /// <summary>Finds walkable cells near the maze corners for power-pellet placement.</summary>
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

    /// <summary>Creates colored character entities at the supplied maze-cell positions.</summary>
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

    /// <summary>Creates wall geometry and movement lookups from the active level layout.</summary>
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

    /// <summary>Destroys generated maze entities and resets cached layout data.</summary>
    public void ClearMaze()
    {
        Grid = null;
    }
}
