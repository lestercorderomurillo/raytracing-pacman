using System;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace RaytracingPacman;

[Pausable(PauseGroup.Gameplay | PauseGroup.Animation)]
[SystemTag("Pacman")]
public class PacmanGhostAI : com.radiant.engine.core.System
{
    public float GhostSpeed { get; set; } = 200f;
    public float RainbowSpeed { get; set; } = 200f;
    public float GhostDepth { get; set; } = 65530f;
    public Texture2D BodyTexture { get; set; }
    public float BodyRadius { get; set; } = 30f;
    public PacmanPlayerController Player { get; set; }

    public int GhostCount => GhostIds?.Length ?? 0;
    public Vector2 GetGhostEyePosition(int ghostIndex) => EyePositions[ghostIndex];
    public (int dx, int dy) GetGhostDirection(int ghostIndex) => GhostDirections[ghostIndex];
    public bool IsGhostFrightened(int ghostIndex) => Frightened[ghostIndex];
    public bool IsGhostEaten(int ghostIndex) => Eaten[ghostIndex];
    public PacmanGhostType GetGhostType(int ghostIndex) => GhostEntries[ghostIndex].Type;

    public int RainbowTotalCount => RainbowInitialized ? 1 + RainbowCloneCount : 0;
    public Vector2 GetRainbowEyePosition(int eyeIndex) => RainbowEyePositions[eyeIndex];
    public (int dx, int dy) GetRainbowEyeDirection(int eyeIndex) => RainbowEyeDirections[eyeIndex];
    public bool RainbowIsMainEaten => RainbowMainEaten;
    public bool RainbowIsFrightened => RainbowFrightenedTimer > 0f;

    private const float RespawnDelay = 6f;
    private const float EatenSpeedMultiplier = 2.5f;
    private const float FrightenedSpeedMultiplier = 0.4f;
    private const float FrightenedShrink = 0.85f;
    private const float FrightenedExtraDuration = 5f;
    private const float FrightenedBlinkThreshold = 1.5f;
    private const float FrightenedBlinkRate = 8f;
    private static readonly Color FrightenedColor = new Color(30, 30, 200);
    private static readonly Color FrightenedBlinkColor = new Color(100, 150, 255);

    private static readonly int[] HorizontalDirections = [0, -1, 0, 1];
    private static readonly int[] VerticalDirections = [-1, 0, 1, 0];

    private int[] GhostIds;
    private PacmanMazeBuilder Maze;
    private Geometry Geometry;
    private (int x, int y)[] GhostCells;
    private (int x, int y)[] GhostTargets;
    private (int dx, int dy)[] GhostDirections;
    private GhostEntry[] GhostEntries;
    private (int x, int y)[] ChaseTargets;
    private bool[] ExitedHouse;
    private bool[] Eaten;
    private float[] RespawnTimer;
    private Color[] GhostColors;
    private Vector2[] EyePositions;
    private Vector2[] PreviousPositions;
    private Vector2[] Positions;
    private float ElapsedTime;
    private float IdleTime;
    private Random RandomGenerator = new();

    private PacmanGhostMode CurrentMode = PacmanGhostMode.Scatter;
    private float ModeTimer;
    private int ModePhase;

    private bool[] Frightened;
    private float[] FrightenedTimers;

    private static readonly (PacmanGhostMode mode, float duration)[] ModeCycle =
    [
        (PacmanGhostMode.Scatter, 7f),
        (PacmanGhostMode.Chase, 20f),
        (PacmanGhostMode.Scatter, 7f),
        (PacmanGhostMode.Chase, 20f),
        (PacmanGhostMode.Scatter, 5f),
        (PacmanGhostMode.Chase, 20f),
        (PacmanGhostMode.Scatter, 5f),
    ];

    private const int MaxClones = 2;
    private const float SoloDuration = 5f;
    private const float DuoSplitDelay = 3f;
    private const float TrioDuration = 7f;
    private static readonly float[] MergeSpeedMultipliers = [1.8f, 1.5f, 1.3f];
    private const float HueCycleSpeed = 0.35f;

    private int RainbowMainId;
    private (int x, int y) RainbowMainCell;
    private (int x, int y) RainbowMainTarget;
    private (int dx, int dy) RainbowMainDirection;
    private float RainbowMainHue;

    private int[] RainbowCloneIds = new int[MaxClones];
    private (int x, int y)[] RainbowCloneCells = new (int, int)[MaxClones];
    private (int x, int y)[] RainbowCloneTargets = new (int, int)[MaxClones];
    private (int dx, int dy)[] RainbowCloneDirections = new (int, int)[MaxClones];
    private int RainbowCloneCount;

    private Vector2[] RainbowEyePositions;
    private Vector2[] RainbowPreviousPositions;
    private (int dx, int dy)[] RainbowEyeDirections;
    private Vector2[] RainbowLogicalPositions;

    private bool RainbowMainExitedHouse;
    private float RainbowFrightenedTimer;
    private bool RainbowMainEaten;
    private float RainbowMainRespawnTimer;

    private RainbowPhase RainbowPhaseState = RainbowPhase.Solo;
    private float RainbowPhaseTimer;
    private float RainbowReleaseTime;
    private float RainbowReleaseAtCoinPercent;
    private float RainbowElapsedTime;
    private bool RainbowInitialized;

    private (int x, int y)[] RainbowWanderTargetTiles = new (int, int)[1 + MaxClones];
    private (int x, int y)[] RainbowCornerTargets = new (int, int)[1 + MaxClones];
    private float RainbowIdleTime;

    public static Color PersonalityColor(PacmanGhostType ghostType) => ghostType switch
    {
        PacmanGhostType.Blinky => new Color(255, 0, 20),
        PacmanGhostType.Pinky => new Color(255, 184, 255),
        PacmanGhostType.Inky => new Color(0, 255, 255),
        PacmanGhostType.Clyde => new Color(255, 184, 82),
        PacmanGhostType.Dinky => new Color(0, 220, 80),
        PacmanGhostType.Shadow => new Color(100, 0, 160),
        PacmanGhostType.Rainbow => Color.White,
        _ => Color.White
    };

    private (int x, int y) RandomScatterCorner() => RandomGenerator.Next(4) switch
    {
        0 => (Maze.Columns - 3, 0),
        1 => (2, 0),
        2 => (Maze.Columns - 1, Maze.Rows - 1),
        _ => (0, Maze.Rows - 1)
    };

    private (int x, int y) ScatterTarget(PacmanGhostType ghostType) => ghostType switch
    {
        PacmanGhostType.Blinky => (Maze.Columns - 3, 0),
        PacmanGhostType.Pinky => (2, 0),
        PacmanGhostType.Inky => (Maze.Columns - 1, Maze.Rows - 1),
        PacmanGhostType.Clyde => (0, Maze.Rows - 1),
        PacmanGhostType.Dinky => RandomScatterCorner(),
        PacmanGhostType.Shadow => (Maze.Columns / 2, Maze.Rows / 2),
        _ => (Maze.Columns / 2, 0)
    };

    Texture2D EyesTexture;

    public override void Initialize()
    {
        Maze = Scene.ECS.GetSystem<PacmanMazeBuilder>();
        Geometry = Scene.ECS.GetSystem<Geometry>();
        EyesTexture = Renderer.GetTexture("Eyes");
    }

    public void Track(int[] entityIds, (int x, int y)[] startCells, GhostEntry[] entries)
    {
        int ghostCount = entityIds.Length;
        GhostIds = entityIds;
        GhostEntries = entries;
        GhostCells = new (int, int)[ghostCount];
        GhostTargets = new (int, int)[ghostCount];
        GhostDirections = new (int, int)[ghostCount];
        ChaseTargets = new (int, int)[ghostCount];

        ExitedHouse = new bool[ghostCount];
        Eaten = new bool[ghostCount];
        RespawnTimer = new float[ghostCount];
        Frightened = new bool[ghostCount];
        FrightenedTimers = new float[ghostCount];

        GhostColors = new Color[ghostCount];
        EyePositions = new Vector2[ghostCount];
        PreviousPositions = new Vector2[ghostCount];
        Positions = new Vector2[ghostCount];

        for (int ghostIndex = 0; ghostIndex < ghostCount; ghostIndex++)
        {
            GhostCells[ghostIndex] = startCells[ghostIndex];
            GhostTargets[ghostIndex] = startCells[ghostIndex];
            GhostDirections[ghostIndex] = (1, 0);
            ChaseTargets[ghostIndex] = PickRandomWalkable();
            ExitedHouse[ghostIndex] = false;
            Eaten[ghostIndex] = false;
            RespawnTimer[ghostIndex] = 0f;

            GhostColors[ghostIndex] = PersonalityColor(entries[ghostIndex].Type);
            ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);

            if (entries[ghostIndex].Type == PacmanGhostType.Shadow)
            {
                material.Albedo = GhostColors[ghostIndex];
                material.Emissive = Color.Black;
            }
            else
            {
                material.Albedo = Color.Transparent;
                material.Emissive = GhostColors[ghostIndex];
            }

            ref var transform = ref Scene.ECS.GetComponent<Transform>(GhostIds[ghostIndex]);
            var position = new Vector2(transform.Position.X, transform.Position.Y);
            EyePositions[ghostIndex] = position;
            PreviousPositions[ghostIndex] = position;
            Positions[ghostIndex] = position;
        }

        ElapsedTime = 0f;
        ModeTimer = 0f;
        ModePhase = 0;
        CurrentMode = PacmanGhostMode.Scatter;
    }

    public void Clear()
    {
        if (GhostIds == null) return;

        for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
            Scene.ECS.DestroyEntity(GhostIds[ghostIndex]);

        GhostIds = null;
    }

    public void SetFrightened(float duration)
    {
        if (GhostIds == null) return;

        float timer = duration + FrightenedExtraDuration;

        for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
        {
            if (Eaten[ghostIndex] || RespawnTimer[ghostIndex] > 0f || !ExitedHouse[ghostIndex]) continue;

            Frightened[ghostIndex] = true;
            FrightenedTimers[ghostIndex] = timer;

            ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
            material.Albedo = Color.Transparent;
            material.Emissive = FrightenedColor;

            ref var circle = ref Scene.ECS.GetComponent<Circle2D>(GhostIds[ghostIndex]);
            circle.Radius = BodyRadius * FrightenedShrink;

            var (previousDirectionX, previousDirectionY) = GhostDirections[ghostIndex];
            if (previousDirectionX == 0 && previousDirectionY == 0) continue;

            var (cellX, cellY) = GhostCells[ghostIndex];
            GhostDirections[ghostIndex] = (-previousDirectionX, -previousDirectionY);
            if (CanGhostMove(ghostIndex, cellX, cellY, -previousDirectionX, -previousDirectionY))
                GhostTargets[ghostIndex] = (cellX - previousDirectionX, cellY - previousDirectionY);
        }
    }

    public override void Update()
    {
        float deltaTime = (float)GameTime.ElapsedGameTime.TotalSeconds;
        UpdateRegularGhosts(deltaTime);
        UpdateRainbowGhost(deltaTime);
    }

    public void HideGhostEntities()
    {
        if (GhostIds != null)
        {
            for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
            {
                if (GhostIds[ghostIndex] == -1 || !Scene.ECS.IsAlive(GhostIds[ghostIndex])) continue;

                ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
                material.Albedo = Color.Transparent;
                material.Emissive = Color.Transparent;
            }
        }

        if (RainbowInitialized)
        {
            if (Scene.ECS.IsAlive(RainbowMainId))
            {
                ref var material = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
                material.Albedo = Color.Transparent;
                material.Emissive = Color.Transparent;
            }

            for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
            {
                if (!Scene.ECS.IsAlive(RainbowCloneIds[cloneIndex])) continue;

                ref var material = ref Scene.ECS.GetComponent<Material>(RainbowCloneIds[cloneIndex]);
                material.Albedo = Color.Transparent;
                material.Emissive = Color.Transparent;
            }
        }
    }

    public void ShowGhostEntities()
    {
        if (GhostIds != null)
        {
            for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
            {
                if (GhostIds[ghostIndex] == -1 || !Scene.ECS.IsAlive(GhostIds[ghostIndex])) continue;
                if (Eaten[ghostIndex]) continue;

                if (Frightened[ghostIndex])
                {
                    ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
                    material.Albedo = Color.Transparent;
                    material.Emissive = FrightenedColor;
                    continue;
                }

                ref var ghostMaterial = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);

                if (GhostEntries[ghostIndex].Type == PacmanGhostType.Shadow)
                {
                    ghostMaterial.Albedo = GhostColors[ghostIndex];
                    ghostMaterial.Emissive = Color.Black;
                }
                else
                {
                    ghostMaterial.Albedo = Color.Transparent;
                    ghostMaterial.Emissive = GhostColors[ghostIndex];
                }
            }
        }

        if (RainbowInitialized)
        {
            if (Scene.ECS.IsAlive(RainbowMainId) && !RainbowMainEaten)
                RainbowUpdateMainColor();

            for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
            {
                if (Scene.ECS.IsAlive(RainbowCloneIds[cloneIndex]))
                    RainbowUpdateCloneColor(cloneIndex);
            }
        }
    }

    public override void Dispose()
    {
        if (!RainbowInitialized) return;

        for (int cloneIndex = RainbowCloneCount - 1; cloneIndex >= 0; cloneIndex--)
            RainbowDestroyClone(cloneIndex);
    }

    private void UpdateRegularGhosts(float deltaTime)
    {
        if (GhostIds == null) return;

        IdleTime += deltaTime;

        for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
        {
            EyePositions[ghostIndex] = PreviousPositions[ghostIndex];
            if (GhostIds[ghostIndex] == -1 || !Scene.ECS.IsAlive(GhostIds[ghostIndex])) continue;
            if (Scene.ECS.IsDisabled(GhostIds[ghostIndex])) continue;

            ref var transform = ref Scene.ECS.GetComponent<Transform>(GhostIds[ghostIndex]);
            PreviousPositions[ghostIndex] = new Vector2(transform.Position.X, transform.Position.Y);
        }

        if (Player == null || !Player.HasMoved)
        {
            for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
            {
                if (GhostIds[ghostIndex] == -1 || !Scene.ECS.IsAlive(GhostIds[ghostIndex])) continue;
                if (Scene.ECS.IsDisabled(GhostIds[ghostIndex])) continue;

                ref var transform = ref Scene.ECS.GetComponent<Transform>(GhostIds[ghostIndex]);
                float wobble = MathF.Sin(IdleTime * 3f + ghostIndex * 1.7f) * 3f;
                var position = Positions[ghostIndex];
                transform.Position = new Vector3(position.X, position.Y + wobble, GhostDepth);
            }

            return;
        }

        ElapsedTime += deltaTime;
        UpdateMode(deltaTime);

        float moveDistance = GhostSpeed * deltaTime;
        float frightenedMoveDistance = moveDistance * FrightenedSpeedMultiplier;

        for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
        {
            if (GhostIds[ghostIndex] == -1 || !Scene.ECS.IsAlive(GhostIds[ghostIndex])) continue;
            if (Scene.ECS.IsDisabled(GhostIds[ghostIndex])) continue;

            if (RespawnTimer[ghostIndex] > 0f)
            {
                RespawnTimer[ghostIndex] -= deltaTime;
                if (RespawnTimer[ghostIndex] <= 0f)
                    RespawnGhost(ghostIndex);
            }

            float currentMoveDistance;
            if (Eaten[ghostIndex])
                currentMoveDistance = moveDistance * EatenSpeedMultiplier;
            else if (!ExitedHouse[ghostIndex] && !IsReleased(ghostIndex))
                currentMoveDistance = moveDistance * 0.5f;
            else if (Frightened[ghostIndex])
                currentMoveDistance = frightenedMoveDistance;
            else
                currentMoveDistance = moveDistance;

            if (GhostEntries[ghostIndex].Type == PacmanGhostType.Shadow && ExitedHouse[ghostIndex]
                && !Eaten[ghostIndex] && !Frightened[ghostIndex])
            {
                var (shadowCellX, shadowCellY) = GhostCells[ghostIndex];
                var (chaseCellX, chaseCellY) = Player != null ? Player.Cell : ChaseTargets[ghostIndex];
                float shadowDistance = MathF.Sqrt((shadowCellX - chaseCellX) * (shadowCellX - chaseCellX) + (shadowCellY - chaseCellY) * (shadowCellY - chaseCellY));
                float speedMultiplier = shadowDistance < 4f ? 1.0f : shadowDistance < 12f ? 1.5f : 1.25f;
                currentMoveDistance = moveDistance * speedMultiplier;
            }

            ref var transform = ref Scene.ECS.GetComponent<Transform>(GhostIds[ghostIndex]);
            var position = Positions[ghostIndex];
            var targetPosition = Maze.CellCenter(GhostTargets[ghostIndex].x, GhostTargets[ghostIndex].y);

            var targetOffset = targetPosition - position;
            float distanceToTarget = targetOffset.Length();

            if (distanceToTarget <= currentMoveDistance)
            {
                var unwrappedTargetCell = GhostTargets[ghostIndex];
                GhostCells[ghostIndex] = (Maze.WrapX(unwrappedTargetCell.x), unwrappedTargetCell.y);
                position = Maze.CellCenter(GhostCells[ghostIndex].x, GhostCells[ghostIndex].y);
                PickDirection(ghostIndex);

                targetPosition = Maze.CellCenter(GhostTargets[ghostIndex].x, GhostTargets[ghostIndex].y);
                targetOffset = targetPosition - position;
                distanceToTarget = targetOffset.Length();
            }

            if (distanceToTarget > 0.01f)
            {
                var movementOffset = targetOffset / distanceToTarget * MathF.Min(currentMoveDistance, distanceToTarget);
                position += movementOffset;
            }

            Positions[ghostIndex] = position;

            // Apply visual wobble without changing the maze position.
            var (directionX, directionY) = GhostDirections[ghostIndex];
            float wobble = MathF.Sin(ElapsedTime * 5f + ghostIndex * 2.5f) * 4f;
            transform.Position = new Vector3(
                position.X + (directionY != 0 ? wobble : 0f),
                position.Y + (directionX != 0 ? wobble : 0f), GhostDepth);

            if (ExitedHouse[ghostIndex] && !Eaten[ghostIndex] && RespawnTimer[ghostIndex] <= 0f
                && Player != null && !Player.PlayerCaught)
            {
                float playerOffsetX = position.X - Player.WorldPosition.X;
                float playerOffsetY = position.Y - Player.WorldPosition.Y;
                float catchRadius = BodyRadius + Player.HitboxRadius;

                if (playerOffsetX * playerOffsetX + playerOffsetY * playerOffsetY < catchRadius * catchRadius)
                {
                    if (Frightened[ghostIndex])
                        EatGhost(ghostIndex);
                    else
                        Player.PlayerCaught = true;
                }
            }
        }
    }

    private void EatGhost(int ghostIndex)
    {
        Eaten[ghostIndex] = true;
        Frightened[ghostIndex] = false;
        FrightenedTimers[ghostIndex] = 0f;

        ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
        material.Albedo = Color.Transparent;
        material.Emissive = Color.Transparent;

        ref var circle = ref Scene.ECS.GetComponent<Circle2D>(GhostIds[ghostIndex]);
        circle.Radius = BodyRadius;
    }

    private void RespawnGhost(int ghostIndex)
    {
        RespawnTimer[ghostIndex] = 0f;
        ExitedHouse[ghostIndex] = false;

        ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);

        if (GhostEntries[ghostIndex].Type == PacmanGhostType.Shadow)
        {
            material.Albedo = GhostColors[ghostIndex];
            material.Emissive = Color.Black;
        }
        else
        {
            material.Albedo = Color.Transparent;
            material.Emissive = GhostColors[ghostIndex];
        }
    }

    private void RestoreGhostColor(int ghostIndex)
    {
        if (Eaten[ghostIndex] || RespawnTimer[ghostIndex] > 0f) return;

        ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);

        if (GhostEntries[ghostIndex].Type == PacmanGhostType.Shadow)
        {
            material.Albedo = GhostColors[ghostIndex];
            material.Emissive = Color.Black;
        }
        else
        {
            material.Albedo = Color.Transparent;
            material.Emissive = GhostColors[ghostIndex];
        }
    }

    private void UpdateMode(float deltaTime)
    {
        for (int ghostIndex = 0; ghostIndex < GhostIds.Length; ghostIndex++)
        {
            if (!Frightened[ghostIndex]) continue;

            FrightenedTimers[ghostIndex] -= deltaTime;

            if (FrightenedTimers[ghostIndex] <= 0f)
            {
                Frightened[ghostIndex] = false;
                FrightenedTimers[ghostIndex] = 0f;
                RestoreGhostColor(ghostIndex);
                ref var circle = ref Scene.ECS.GetComponent<Circle2D>(GhostIds[ghostIndex]);
                circle.Radius = BodyRadius;
            }
            else if (FrightenedTimers[ghostIndex] <= FrightenedBlinkThreshold)
            {
                bool blinkOn = MathF.Sin(FrightenedTimers[ghostIndex] * FrightenedBlinkRate * MathF.PI) > 0f;
                ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
                material.Emissive = blinkOn ? FrightenedBlinkColor : FrightenedColor;
            }
            else
            {
                float hueShift = MathF.Sin(IdleTime * 2.5f + ghostIndex * 1.3f);
                byte red = (byte)(30 + 42 * MathF.Max(0, -hueShift));
                byte green = (byte)(30 + 55 * MathF.Max(0, hueShift));
                byte blue = (byte)(200 + 42 * MathF.Abs(hueShift));
                ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
                material.Emissive = new Color(red, green, blue);
            }
        }

        if (ModePhase >= ModeCycle.Length)
        {
            CurrentMode = PacmanGhostMode.Chase;
            return;
        }

        ModeTimer += deltaTime;

        if (ModeTimer >= ModeCycle[ModePhase].duration)
        {
            ModeTimer -= ModeCycle[ModePhase].duration;
            ModePhase++;

            CurrentMode = ModePhase < ModeCycle.Length
                ? ModeCycle[ModePhase].mode
                : PacmanGhostMode.Chase;
        }
    }

    private bool CanGhostMove(int ghostIndex, int cellX, int cellY, int directionX, int directionY)
    {
        if (!Maze.CanMove(cellX, cellY, directionX, directionY)) return false;

        if (ExitedHouse[ghostIndex])
        {
            int nextCellX = Maze.WrapX(cellX + directionX), nextCellY = cellY + directionY;
            if (Maze.IsGhostDoor(nextCellX, nextCellY) && !Eaten[ghostIndex]) return false;
            if (GhostEntries[ghostIndex].Type == PacmanGhostType.Shadow && !Eaten[ghostIndex] && nextCellX != cellX + directionX) return false;
        }

        return true;
    }

    private (int x, int y) GetTargetTile(int ghostIndex)
    {
        if (CurrentMode == PacmanGhostMode.Scatter)
            return ScatterTarget(GhostEntries[ghostIndex].Type);

        var (cellX, cellY) = GhostCells[ghostIndex];

        if (Player != null)
        {
            var (playerCellX, playerCellY) = Player.Cell;

            if (GhostEntries[ghostIndex].Type == PacmanGhostType.Clyde)
            {
                float distanceToTarget = MathF.Sqrt((cellX - playerCellX) * (cellX - playerCellX) + (cellY - playerCellY) * (cellY - playerCellY));
                if (distanceToTarget < 8f)
                    return ScatterTarget(PacmanGhostType.Clyde);
            }

            if (GhostEntries[ghostIndex].Type == PacmanGhostType.Dinky)
            {
                float distanceToTarget = MathF.Sqrt((cellX - playerCellX) * (cellX - playerCellX) + (cellY - playerCellY) * (cellY - playerCellY));
                if (distanceToTarget < 10f)
                    return RandomScatterCorner();
            }

            return (playerCellX, playerCellY);
        }

        var (targetCellX, targetCellY) = ChaseTargets[ghostIndex];

        if (GhostEntries[ghostIndex].Type == PacmanGhostType.Clyde)
        {
            float distanceToTarget = MathF.Sqrt((cellX - targetCellX) * (cellX - targetCellX) + (cellY - targetCellY) * (cellY - targetCellY));
            if (distanceToTarget < 8f)
                return ScatterTarget(PacmanGhostType.Clyde);
        }

        if (GhostEntries[ghostIndex].Type == PacmanGhostType.Dinky)
        {
            float distanceToTarget = MathF.Sqrt((cellX - targetCellX) * (cellX - targetCellX) + (cellY - targetCellY) * (cellY - targetCellY));
            if (distanceToTarget < 10f)
                return RandomScatterCorner();
        }

        if (GhostEntries[ghostIndex].Type == PacmanGhostType.Shadow)
        {
            float manhattanDistance = MathF.Abs(cellX - targetCellX) + MathF.Abs(cellY - targetCellY);
            if (manhattanDistance <= 1 || Maze.IsWall(targetCellX, targetCellY))
                ChaseTargets[ghostIndex] = PickRandomWalkable();
            return ChaseTargets[ghostIndex];
        }

        float targetManhattanDistance = MathF.Abs(cellX - targetCellX) + MathF.Abs(cellY - targetCellY);
        if (targetManhattanDistance <= 2 || Maze.IsWall(targetCellX, targetCellY))
            ChaseTargets[ghostIndex] = PickRandomWalkable();

        return ChaseTargets[ghostIndex];
    }

    private void PickDirection(int ghostIndex)
    {
        var (cellX, cellY) = GhostCells[ghostIndex];
        var (previousDirectionX, previousDirectionY) = GhostDirections[ghostIndex];

        if (!ExitedHouse[ghostIndex])
        {
            if (!Maze.InGhostHouse(cellX, cellY) && !Maze.IsGhostDoor(cellX, cellY))
            {
                ExitedHouse[ghostIndex] = true;
            }
            else
            {
                if (IsReleased(ghostIndex))
                    PickHouseExit(ghostIndex);
                else
                    PickHouseWander(ghostIndex);
                return;
            }
        }

        if (Eaten[ghostIndex])
        {
            PickEatenPath(ghostIndex);
            return;
        }

        if (Frightened[ghostIndex])
        {
            PickRandom(ghostIndex);
            return;
        }

        var targetTile = GetTargetTile(ghostIndex);

        Span<(int dx, int dy)> availableDirections = stackalloc (int, int)[4];
        int directionCount = 0;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            if (!CanGhostMove(ghostIndex, cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex])) continue;
            if (HorizontalDirections[directionIndex] == -previousDirectionX && VerticalDirections[directionIndex] == -previousDirectionY) continue;
            if (VerticalDirections[directionIndex] == -1 && Maze.IsNoUpTile(cellX, cellY)) continue;

            availableDirections[directionCount++] = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);
        }

        if (directionCount == 0)
        {
            GhostDirections[ghostIndex] = (-previousDirectionX, -previousDirectionY);
            GhostTargets[ghostIndex] = (cellX - previousDirectionX, cellY - previousDirectionY);
            return;
        }

        if (directionCount == 1)
        {
            var onlyDirection = availableDirections[0];
            GhostDirections[ghostIndex] = onlyDirection;
            GhostTargets[ghostIndex] = (cellX + onlyDirection.dx, cellY + onlyDirection.dy);
            return;
        }

        float bestDistanceSquared = float.MaxValue;
        (int dx, int dy) bestDirection = availableDirections[0];

        for (int optionIndex = 0; optionIndex < directionCount; optionIndex++)
        {
            float targetOffsetX = cellX + availableDirections[optionIndex].dx - targetTile.x;
            float targetOffsetY = cellY + availableDirections[optionIndex].dy - targetTile.y;
            float distanceSquared = targetOffsetX * targetOffsetX + targetOffsetY * targetOffsetY;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestDirection = availableDirections[optionIndex];
            }
        }

        GhostDirections[ghostIndex] = bestDirection;
        GhostTargets[ghostIndex] = (cellX + bestDirection.dx, cellY + bestDirection.dy);
    }

    private void PickEatenPath(int ghostIndex)
    {
        var (cellX, cellY) = GhostCells[ghostIndex];
        var (previousDirectionX, previousDirectionY) = GhostDirections[ghostIndex];

        if (Maze.InGhostHouse(cellX, cellY))
        {
            Eaten[ghostIndex] = false;
            RespawnTimer[ghostIndex] = RespawnDelay;
            ExitedHouse[ghostIndex] = false;

            ref var material = ref Scene.ECS.GetComponent<Material>(GhostIds[ghostIndex]);
            material.Albedo = Color.Transparent;
            material.Emissive = GhostColors[ghostIndex];

            PickHouseWander(ghostIndex);
            return;
        }

        if (Maze.IsGhostDoor(cellX, cellY))
        {
            GhostDirections[ghostIndex] = (0, 1);
            GhostTargets[ghostIndex] = (cellX, cellY + 1);
            return;
        }

        int targetX = Maze.HouseDoorLeft;
        int targetY = Maze.HouseDoorY;

        Span<(int dx, int dy)> availableDirections = stackalloc (int, int)[4];
        int directionCount = 0;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            if (!CanGhostMove(ghostIndex, cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex])) continue;
            if (HorizontalDirections[directionIndex] == -previousDirectionX && VerticalDirections[directionIndex] == -previousDirectionY) continue;

            availableDirections[directionCount++] = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);
        }

        if (directionCount == 0)
        {
            GhostDirections[ghostIndex] = (-previousDirectionX, -previousDirectionY);
            GhostTargets[ghostIndex] = (cellX - previousDirectionX, cellY - previousDirectionY);
            return;
        }

        if (directionCount == 1)
        {
            var onlyDirection = availableDirections[0];
            GhostDirections[ghostIndex] = onlyDirection;
            GhostTargets[ghostIndex] = (cellX + onlyDirection.dx, cellY + onlyDirection.dy);
            return;
        }

        float bestDistanceSquared = float.MaxValue;
        (int dx, int dy) bestDirection = availableDirections[0];

        for (int optionIndex = 0; optionIndex < directionCount; optionIndex++)
        {
            float targetOffsetX = cellX + availableDirections[optionIndex].dx - targetX;
            float targetOffsetY = cellY + availableDirections[optionIndex].dy - targetY;
            float distanceSquared = targetOffsetX * targetOffsetX + targetOffsetY * targetOffsetY;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestDirection = availableDirections[optionIndex];
            }
        }

        GhostDirections[ghostIndex] = bestDirection;
        GhostTargets[ghostIndex] = (cellX + bestDirection.dx, cellY + bestDirection.dy);
    }

    private void PickHouseExit(int ghostIndex)
    {
        var (cellX, cellY) = GhostCells[ghostIndex];
        int doorLeft = Maze.HouseDoorLeft;
        int doorRight = Maze.HouseDoorRight;

        if (cellX < doorLeft && Maze.CanMove(cellX, cellY, 1, 0))
        {
            GhostDirections[ghostIndex] = (1, 0);
            GhostTargets[ghostIndex] = (cellX + 1, cellY);
        }
        else if (cellX > doorRight && Maze.CanMove(cellX, cellY, -1, 0))
        {
            GhostDirections[ghostIndex] = (-1, 0);
            GhostTargets[ghostIndex] = (cellX - 1, cellY);
        }
        else if (Maze.CanMove(cellX, cellY, 0, -1))
        {
            GhostDirections[ghostIndex] = (0, -1);
            GhostTargets[ghostIndex] = (cellX, cellY - 1);
        }
        else
        {
            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                if (Maze.CanMove(cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex]))
                {
                    GhostDirections[ghostIndex] = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);
                    GhostTargets[ghostIndex] = (cellX + HorizontalDirections[directionIndex], cellY + VerticalDirections[directionIndex]);
                    return;
                }
        }
    }

    private void PickHouseWander(int ghostIndex)
    {
        var (cellX, cellY) = GhostCells[ghostIndex];
        var (previousDirectionX, _) = GhostDirections[ghostIndex];

        if (previousDirectionX == 0)
            previousDirectionX = RandomGenerator.Next(2) == 0 ? -1 : 1;

        if (Maze.CanMove(cellX, cellY, previousDirectionX, 0) && Maze.InGhostHouse(cellX + previousDirectionX, cellY))
        {
            GhostDirections[ghostIndex] = (previousDirectionX, 0);
            GhostTargets[ghostIndex] = (cellX + previousDirectionX, cellY);
        }
        else if (Maze.CanMove(cellX, cellY, -previousDirectionX, 0) && Maze.InGhostHouse(cellX - previousDirectionX, cellY))
        {
            GhostDirections[ghostIndex] = (-previousDirectionX, 0);
            GhostTargets[ghostIndex] = (cellX - previousDirectionX, cellY);
        }
        else
        {
            GhostDirections[ghostIndex] = (0, 0);
            GhostTargets[ghostIndex] = (cellX, cellY);
        }
    }

    private void PickRandom(int ghostIndex)
    {
        var (cellX, cellY) = GhostCells[ghostIndex];
        var (previousDirectionX, previousDirectionY) = GhostDirections[ghostIndex];

        Span<(int dx, int dy)> availableDirections = stackalloc (int, int)[4];
        int directionCount = 0;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            if (CanGhostMove(ghostIndex, cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex]) && !(HorizontalDirections[directionIndex] == -previousDirectionX && VerticalDirections[directionIndex] == -previousDirectionY))
                availableDirections[directionCount++] = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);

        if (directionCount == 0)
        {
            GhostDirections[ghostIndex] = (-previousDirectionX, -previousDirectionY);
            GhostTargets[ghostIndex] = (cellX - previousDirectionX, cellY - previousDirectionY);
            return;
        }

        var chosenDirection = availableDirections[RandomGenerator.Next(directionCount)];
        GhostDirections[ghostIndex] = chosenDirection;
        GhostTargets[ghostIndex] = (cellX + chosenDirection.dx, cellY + chosenDirection.dy);
    }

    private bool IsReleased(int ghostIndex)
    {
        if (RespawnTimer[ghostIndex] > 0f) return false;

        ref var entry = ref GhostEntries[ghostIndex];
        if (entry.ReleaseAfter > 0 && ElapsedTime >= entry.ReleaseAfter) return true;

        if (entry.ReleaseAtCoinPercent > 0 && Player != null && Player.CoinsTotal > 0)
        {
            float coinPercent = (float)Player.CoinsCollected / Player.CoinsTotal;
            if (coinPercent >= entry.ReleaseAtCoinPercent) return true;
        }

        if (entry.ReleaseAfter <= 0 && entry.ReleaseAtCoinPercent <= 0) return true;

        return false;
    }

    private (int x, int y) PickRandomWalkable()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int cellX = RandomGenerator.Next(Maze.Columns);
            int cellY = RandomGenerator.Next(Maze.Rows);
            if (!Maze.IsWall(cellX, cellY) && !Maze.InGhostHouse(cellX, cellY) && !Maze.IsGhostDoor(cellX, cellY))
                return (cellX, cellY);
        }

        return (Maze.Columns / 2, 0);
    }

    public void TrackRainbow(int entityId, (int x, int y) startCell, float releaseTime = 0f, float releaseAtCoinPercent = 0f)
    {
        RainbowMainId = entityId;
        RainbowMainCell = startCell;
        RainbowMainTarget = startCell;
        RainbowMainDirection = (1, 0);
        RainbowMainHue = 0f;

        RainbowMainExitedHouse = false;
        RainbowMainEaten = false;
        RainbowMainRespawnTimer = 0f;
        RainbowFrightenedTimer = 0f;

        RainbowCloneCount = 0;
        RainbowPhaseState = RainbowPhase.Solo;
        RainbowPhaseTimer = 0f;

        RainbowReleaseTime = releaseTime;
        RainbowReleaseAtCoinPercent = releaseAtCoinPercent;
        RainbowElapsedTime = 0f;
        RainbowInitialized = true;

        int rainbowCapacity = 1 + MaxClones;
        RainbowEyePositions = new Vector2[rainbowCapacity];
        RainbowPreviousPositions = new Vector2[rainbowCapacity];
        RainbowEyeDirections = new (int, int)[rainbowCapacity];
        RainbowLogicalPositions = new Vector2[rainbowCapacity];

        ref var transform = ref Scene.ECS.GetComponent<Transform>(RainbowMainId);
        var position = new Vector2(transform.Position.X, transform.Position.Y);
        RainbowEyePositions[0] = position;
        RainbowPreviousPositions[0] = position;
        RainbowEyeDirections[0] = (1, 0);
        RainbowLogicalPositions[0] = position;

        RainbowWanderTargetTiles[0] = RainbowPickRandomWalkable();

        RainbowUpdateMainColor();
    }

    public void ClearRainbow()
    {
        if (!RainbowInitialized) return;

        for (int cloneIndex = RainbowCloneCount - 1; cloneIndex >= 0; cloneIndex--)
            RainbowDestroyClone(cloneIndex);

        Scene.ECS.DestroyEntity(RainbowMainId);
        RainbowInitialized = false;
    }

    public void SetRainbowFrightened(float duration)
    {
        if (!RainbowInitialized) return;

        RainbowFrightenedTimer = duration + FrightenedExtraDuration;

        if (!RainbowMainEaten && RainbowMainExitedHouse)
        {
            ref var mainMaterial = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
            mainMaterial.Albedo = Color.Transparent;
            mainMaterial.Emissive = FrightenedColor;

            ref var mainCircle = ref Scene.ECS.GetComponent<Circle2D>(RainbowMainId);
            mainCircle.Radius = BodyRadius * FrightenedShrink;

            var (previousDirectionX, previousDirectionY) = RainbowMainDirection;

            if (previousDirectionX != 0 || previousDirectionY != 0)
            {
                RainbowMainDirection = (-previousDirectionX, -previousDirectionY);
                var (cellX, cellY) = RainbowMainCell;
                if (RainbowCanMove(cellX, cellY, -previousDirectionX, -previousDirectionY))
                    RainbowMainTarget = (cellX - previousDirectionX, cellY - previousDirectionY);
            }
        }

        for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
        {
            ref var cloneMaterial = ref Scene.ECS.GetComponent<Material>(RainbowCloneIds[cloneIndex]);
            cloneMaterial.Albedo = Color.Transparent;
            cloneMaterial.Emissive = FrightenedColor;

            ref var cloneCircle = ref Scene.ECS.GetComponent<Circle2D>(RainbowCloneIds[cloneIndex]);
            cloneCircle.Radius = BodyRadius * FrightenedShrink;

            var (cloneDirectionX, cloneDirectionY) = RainbowCloneDirections[cloneIndex];

            if (cloneDirectionX != 0 || cloneDirectionY != 0)
            {
                RainbowCloneDirections[cloneIndex] = (-cloneDirectionX, -cloneDirectionY);
                var (cloneCellX, cloneCellY) = RainbowCloneCells[cloneIndex];
                if (RainbowCanMove(cloneCellX, cloneCellY, -cloneDirectionX, -cloneDirectionY))
                    RainbowCloneTargets[cloneIndex] = (cloneCellX - cloneDirectionX, cloneCellY - cloneDirectionY);
            }
        }
    }

    private void UpdateRainbowGhost(float deltaTime)
    {
        if (!RainbowInitialized) return;

        RainbowIdleTime += deltaTime;

        if (!RainbowMainEaten && RainbowMainRespawnTimer <= 0f)
            RainbowMainHue = (RainbowMainHue + HueCycleSpeed * deltaTime) % 1f;

        RainbowShiftEyeHistory();

        if (RainbowFrightenedTimer <= 0f && !RainbowMainEaten && RainbowMainRespawnTimer <= 0f)
        {
            RainbowUpdateMainColor();

            for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
                RainbowUpdateCloneColor(cloneIndex);
        }

        if (RainbowFrightenedTimer > 0f)
        {
            RainbowFrightenedTimer -= deltaTime;

            if (RainbowFrightenedTimer <= 0f)
            {
                if (!RainbowMainEaten && RainbowMainRespawnTimer <= 0f)
                {
                    RainbowUpdateMainColor();
                    ref var mainCircle = ref Scene.ECS.GetComponent<Circle2D>(RainbowMainId);
                    mainCircle.Radius = BodyRadius;
                }

                for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
                {
                    RainbowUpdateCloneColor(cloneIndex);
                    ref var cloneCircle = ref Scene.ECS.GetComponent<Circle2D>(RainbowCloneIds[cloneIndex]);
                    cloneCircle.Radius = BodyRadius;
                }
            }
            else if (RainbowFrightenedTimer <= FrightenedBlinkThreshold)
            {
                bool blinkOn = MathF.Sin(RainbowFrightenedTimer * FrightenedBlinkRate * MathF.PI) > 0f;
                var blinkColor = blinkOn ? FrightenedBlinkColor : FrightenedColor;

                if (!RainbowMainEaten && RainbowMainRespawnTimer <= 0f && RainbowMainExitedHouse)
                {
                    ref var mainMaterial = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
                    mainMaterial.Emissive = blinkColor;
                }

                for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
                {
                    ref var cloneMaterial = ref Scene.ECS.GetComponent<Material>(RainbowCloneIds[cloneIndex]);
                    cloneMaterial.Emissive = blinkColor;
                }
            }
            else
            {
                float hueShift = MathF.Sin(RainbowIdleTime * 2.5f);
                byte red = (byte)(30 + 42 * MathF.Max(0, -hueShift));
                byte green = (byte)(30 + 55 * MathF.Max(0, hueShift));
                byte blue = (byte)(200 + 42 * MathF.Abs(hueShift));
                var cycledColor = new Color(red, green, blue);

                if (!RainbowMainEaten && RainbowMainRespawnTimer <= 0f && RainbowMainExitedHouse)
                {
                    ref var mainMaterial = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
                    mainMaterial.Emissive = cycledColor;
                }

                for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
                {
                    ref var cloneMaterial = ref Scene.ECS.GetComponent<Material>(RainbowCloneIds[cloneIndex]);
                    cloneMaterial.Emissive = cycledColor;
                }
            }
        }

        if (RainbowMainRespawnTimer > 0f)
        {
            RainbowMainRespawnTimer -= deltaTime;
            if (RainbowMainRespawnTimer <= 0f)
                RainbowRespawnMain();
        }

        if (Player == null || !Player.HasMoved)
        {
            ref var transform = ref Scene.ECS.GetComponent<Transform>(RainbowMainId);
            var position = RainbowLogicalPositions[0];
            float wobble = MathF.Sin(RainbowIdleTime * 3f) * 3f;
            transform.Position = new Vector3(position.X, position.Y + wobble, GhostDepth);
            return;
        }

        RainbowElapsedTime += deltaTime;

        if (!RainbowIsReleased())
        {
            RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime, 0);
            return;
        }

        if (RainbowMainEaten)
        {
            RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime, 0);
            return;
        }

        if (RainbowMainRespawnTimer > 0f)
        {
            RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime, 0);
            return;
        }

        RainbowPhaseTimer += deltaTime;

        switch (RainbowPhaseState)
        {
            case RainbowPhase.Solo:
                RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime, 0);

                if (RainbowPhaseTimer >= SoloDuration)
                {
                    RainbowCornerTargets[0] = RainbowGetCorner(0);
                    RainbowSpawnClone(0);
                    RainbowPhaseState = RainbowPhase.Duo;
                    RainbowPhaseTimer = 0f;
                }

                break;

            case RainbowPhase.Duo:
                RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime, 0);
                RainbowMoveEntity(ref RainbowCloneCells[0], ref RainbowCloneTargets[0], ref RainbowCloneDirections[0], RainbowCloneIds[0], deltaTime, 1);

                if (RainbowPhaseTimer >= DuoSplitDelay)
                {
                    RainbowSpawnClone(1);
                    RainbowPhaseState = RainbowPhase.Trio;
                    RainbowPhaseTimer = 0f;
                }

                break;

            case RainbowPhase.Trio:
                RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime, 0);

                for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
                    RainbowMoveEntity(ref RainbowCloneCells[cloneIndex], ref RainbowCloneTargets[cloneIndex], ref RainbowCloneDirections[cloneIndex], RainbowCloneIds[cloneIndex], deltaTime, 1 + cloneIndex);

                if (RainbowPhaseTimer >= TrioDuration)
                {
                    RainbowPhaseState = RainbowPhase.Merging;
                    RainbowPhaseTimer = 0f;
                }

                break;

            case RainbowPhase.Merging:
                RainbowMoveEntity(ref RainbowMainCell, ref RainbowMainTarget, ref RainbowMainDirection, RainbowMainId, deltaTime * MergeSpeedMultipliers[0], 0);

                for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
                    RainbowMoveEntity(ref RainbowCloneCells[cloneIndex], ref RainbowCloneTargets[cloneIndex], ref RainbowCloneDirections[cloneIndex], RainbowCloneIds[cloneIndex], deltaTime * MergeSpeedMultipliers[1 + cloneIndex], 1 + cloneIndex);

                RainbowCheckMerges();

                if (RainbowCloneCount == 0)
                {
                    RainbowPhaseState = RainbowPhase.Solo;
                    RainbowPhaseTimer = 0f;
                    RainbowWanderTargetTiles[0] = RainbowPickRandomWalkable();
                }

                break;
        }
    }

    private void RainbowEatEntity(int eyeIndex)
    {
        if (eyeIndex == 0)
        {
            for (int cloneIndex = RainbowCloneCount - 1; cloneIndex >= 0; cloneIndex--)
                RainbowDestroyClone(cloneIndex);

            RainbowMainEaten = true;
            ref var material = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
            material.Albedo = Color.Transparent;
            material.Emissive = Color.Transparent;
        }
        else
        {
            RainbowDestroyClone(eyeIndex - 1);
        }
    }

    private void RainbowRespawnMain()
    {
        RainbowMainEaten = false;
        RainbowMainRespawnTimer = 0f;
        RainbowMainExitedHouse = false;
        RainbowPhaseState = RainbowPhase.Solo;
        RainbowPhaseTimer = 0f;
        RainbowUpdateMainColor();
    }

    private void RainbowSpawnClone(int sourceEyeIndex)
    {
        int cloneIndex = RainbowCloneCount;
        var sourceCell = sourceEyeIndex == 0 ? RainbowMainCell : RainbowCloneCells[sourceEyeIndex - 1];
        var position = RainbowLogicalPositions[sourceEyeIndex];

        var color = RainbowFrightenedTimer > 0f ? FrightenedColor : LightFactory.HueToRGB(RainbowMainHue);
        int entityId = LightFactory.CreateLight(Scene.ECS, position, BodyRadius,
            Color.Transparent, color, GhostDepth, BodyTexture);
        Scene.ECS.AddComponent<MotionTrackable>(entityId);

        var sourceDirection = sourceEyeIndex == 0 ? RainbowMainDirection : RainbowCloneDirections[sourceEyeIndex - 1];
        var oppositeDirection = (-sourceDirection.dx, -sourceDirection.dy);

        if (oppositeDirection != (0, 0) && RainbowCanMove(sourceCell.x, sourceCell.y, oppositeDirection.Item1, oppositeDirection.Item2))
        {
            RainbowCloneDirections[cloneIndex] = oppositeDirection;
            RainbowCloneTargets[cloneIndex] = (sourceCell.x + oppositeDirection.Item1, sourceCell.y + oppositeDirection.Item2);
        }
        else
        {
            RainbowCloneDirections[cloneIndex] = sourceDirection;
            RainbowCloneTargets[cloneIndex] = (sourceCell.x + sourceDirection.dx, sourceCell.y + sourceDirection.dy);
        }

        RainbowCloneIds[cloneIndex] = entityId;
        RainbowCloneCells[cloneIndex] = sourceCell;

        RainbowEyePositions[1 + cloneIndex] = position;
        RainbowPreviousPositions[1 + cloneIndex] = position;
        RainbowEyeDirections[1 + cloneIndex] = RainbowCloneDirections[cloneIndex];
        RainbowLogicalPositions[1 + cloneIndex] = position;

        RainbowWanderTargetTiles[1 + cloneIndex] = RainbowPickRandomWalkable();
        RainbowCornerTargets[1 + cloneIndex] = RainbowGetCorner(1 + cloneIndex);
        RainbowCloneCount++;
    }

    private void RainbowCheckMerges()
    {
        float mergeDistanceSquared = BodyRadius * 2f * BodyRadius * 2f;

        for (int cloneIndex = RainbowCloneCount - 1; cloneIndex >= 0; cloneIndex--)
        {
            ref var cloneTransform = ref Scene.ECS.GetComponent<Transform>(RainbowCloneIds[cloneIndex]);
            var clonePosition = new Vector2(cloneTransform.Position.X, cloneTransform.Position.Y);

            ref var mainTransform = ref Scene.ECS.GetComponent<Transform>(RainbowMainId);
            var mainPosition = new Vector2(mainTransform.Position.X, mainTransform.Position.Y);

            if ((clonePosition - mainPosition).LengthSquared() < mergeDistanceSquared)
            {
                RainbowDestroyClone(cloneIndex);
                continue;
            }

            for (int otherIndex = cloneIndex - 1; otherIndex >= 0; otherIndex--)
            {
                ref var otherTransform = ref Scene.ECS.GetComponent<Transform>(RainbowCloneIds[otherIndex]);
                var otherPosition = new Vector2(otherTransform.Position.X, otherTransform.Position.Y);

                if ((clonePosition - otherPosition).LengthSquared() < mergeDistanceSquared)
                {
                    RainbowDestroyClone(cloneIndex);
                    break;
                }
            }
        }
    }

    private void RainbowDestroyClone(int cloneIndex)
    {
        Scene.ECS.DestroyEntity(RainbowCloneIds[cloneIndex]);

        int lastCloneIndex = RainbowCloneCount - 1;

        if (cloneIndex < lastCloneIndex)
        {
            RainbowCloneIds[cloneIndex] = RainbowCloneIds[lastCloneIndex];
            RainbowCloneCells[cloneIndex] = RainbowCloneCells[lastCloneIndex];
            RainbowCloneTargets[cloneIndex] = RainbowCloneTargets[lastCloneIndex];
            RainbowCloneDirections[cloneIndex] = RainbowCloneDirections[lastCloneIndex];
            RainbowEyePositions[1 + cloneIndex] = RainbowEyePositions[1 + lastCloneIndex];
            RainbowPreviousPositions[1 + cloneIndex] = RainbowPreviousPositions[1 + lastCloneIndex];
            RainbowEyeDirections[1 + cloneIndex] = RainbowEyeDirections[1 + lastCloneIndex];
            RainbowLogicalPositions[1 + cloneIndex] = RainbowLogicalPositions[1 + lastCloneIndex];
            RainbowWanderTargetTiles[1 + cloneIndex] = RainbowWanderTargetTiles[1 + lastCloneIndex];
        }

        RainbowCloneCount--;
    }

    private void RainbowMoveEntity(ref (int x, int y) cell, ref (int x, int y) targetCell,
        ref (int dx, int dy) direction, int entityId, float deltaTime, int eyeIndex)
    {
        float moveDistance = RainbowSpeed * deltaTime;
        if (eyeIndex == 0 && RainbowMainEaten)
            moveDistance = RainbowSpeed * deltaTime * EatenSpeedMultiplier;
        else if (eyeIndex == 0 && !RainbowMainExitedHouse && !RainbowIsReleased())
            moveDistance *= 0.5f;
        else if (RainbowFrightenedTimer > 0f)
            moveDistance *= FrightenedSpeedMultiplier;

        ref var transform = ref Scene.ECS.GetComponent<Transform>(entityId);
        var position = RainbowLogicalPositions[eyeIndex];
        var targetPosition = Maze.CellCenter(targetCell.x, targetCell.y);

        var targetOffset = targetPosition - position;
        float distanceToTarget = targetOffset.Length();

        if (distanceToTarget <= moveDistance)
        {
            var unwrappedTargetCell = targetCell;
            cell = (Maze.WrapX(unwrappedTargetCell.x), unwrappedTargetCell.y);
            position = Maze.CellCenter(cell.x, cell.y);

            if (eyeIndex == 0 && !RainbowMainExitedHouse && !RainbowMainEaten)
            {
                if (!Maze.InGhostHouse(cell.x, cell.y) && !Maze.IsGhostDoor(cell.x, cell.y))
                {
                    RainbowMainExitedHouse = true;

                    if (RainbowFrightenedTimer > 0f)
                    {
                        ref var material = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
                        material.Albedo = Color.Transparent;
                        material.Emissive = FrightenedColor;
                    }
                }
                else
                {
                    if (RainbowIsReleased())
                        RainbowPickHouseExit(ref cell, ref targetCell, ref direction);
                    else
                        RainbowPickHouseWander(ref cell, ref targetCell, ref direction);
                    targetPosition = Maze.CellCenter(targetCell.x, targetCell.y);
                    targetOffset = targetPosition - position;
                    distanceToTarget = targetOffset.Length();
                    goto applyMove;
                }
            }

            if (eyeIndex == 0 && RainbowMainEaten)
            {
                RainbowPickEatenPath(ref cell, ref targetCell, ref direction);
                targetPosition = Maze.CellCenter(targetCell.x, targetCell.y);
                targetOffset = targetPosition - position;
                distanceToTarget = targetOffset.Length();
                goto applyMove;
            }

            if (eyeIndex == 0 && RainbowMainRespawnTimer > 0f)
            {
                RainbowPickHouseWander(ref cell, ref targetCell, ref direction);
                targetPosition = Maze.CellCenter(targetCell.x, targetCell.y);
                targetOffset = targetPosition - position;
                distanceToTarget = targetOffset.Length();
                goto applyMove;
            }

            if (RainbowFrightenedTimer > 0f)
                RainbowPickRandom(ref cell, ref targetCell, ref direction);
            else
                RainbowPickDirection(ref cell, ref targetCell, ref direction, eyeIndex);

            targetPosition = Maze.CellCenter(targetCell.x, targetCell.y);
            targetOffset = targetPosition - position;
            distanceToTarget = targetOffset.Length();
        }

    applyMove:
        if (distanceToTarget > 0.01f)
        {
            var movementOffset = targetOffset / distanceToTarget * MathF.Min(moveDistance, distanceToTarget);
            position += movementOffset;
        }

        RainbowLogicalPositions[eyeIndex] = position;

        // Apply visual wobble without changing the maze position.
        float wobble = MathF.Sin(RainbowElapsedTime * 5f + eyeIndex * 2.5f) * 4f;
        transform.Position = new Vector3(
            position.X + (direction.dy != 0 ? wobble : 0f),
            position.Y + (direction.dx != 0 ? wobble : 0f), GhostDepth);
        RainbowEyeDirections[eyeIndex] = direction;

        if (Player != null && !Player.PlayerCaught && RainbowMainExitedHouse
            && !RainbowMainEaten && RainbowMainRespawnTimer <= 0f)
        {
            float playerOffsetX = position.X - Player.WorldPosition.X;
            float playerOffsetY = position.Y - Player.WorldPosition.Y;
            float catchRadius = BodyRadius + Player.HitboxRadius;

            if (playerOffsetX * playerOffsetX + playerOffsetY * playerOffsetY < catchRadius * catchRadius)
            {
                if (RainbowFrightenedTimer > 0f)
                    RainbowEatEntity(eyeIndex);
                else
                    Player.PlayerCaught = true;
            }
        }
    }

    private void RainbowPickEatenPath(ref (int x, int y) cell, ref (int x, int y) targetCell,
        ref (int dx, int dy) direction)
    {
        var (cellX, cellY) = cell;
        var (previousDirectionX, previousDirectionY) = direction;

        if (Maze.InGhostHouse(cellX, cellY))
        {
            RainbowMainEaten = false;
            RainbowMainRespawnTimer = RespawnDelay;
            RainbowMainExitedHouse = false;

            ref var material = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
            material.Albedo = Color.Transparent;
            material.Emissive = LightFactory.HueToRGB(RainbowMainHue);

            RainbowPickHouseWander(ref cell, ref targetCell, ref direction);
            return;
        }

        if (Maze.IsGhostDoor(cellX, cellY))
        {
            direction = (0, 1);
            targetCell = (cellX, cellY + 1);
            return;
        }

        int targetX = Maze.HouseDoorLeft;
        int targetY = Maze.HouseDoorY;

        Span<(int dx, int dy)> availableDirections = stackalloc (int, int)[4];
        int directionCount = 0;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            int nextDirectionX = HorizontalDirections[directionIndex], nextDirectionY = VerticalDirections[directionIndex];
            if (!Maze.CanMove(cellX, cellY, nextDirectionX, nextDirectionY)) continue;
            if (nextDirectionX == -previousDirectionX && nextDirectionY == -previousDirectionY) continue;

            availableDirections[directionCount++] = (nextDirectionX, nextDirectionY);
        }

        if (directionCount == 0)
        {
            direction = (-previousDirectionX, -previousDirectionY);
            targetCell = (cellX - previousDirectionX, cellY - previousDirectionY);
            return;
        }

        if (directionCount == 1)
        {
            var onlyDirection = availableDirections[0];
            direction = onlyDirection;
            targetCell = (cellX + onlyDirection.dx, cellY + onlyDirection.dy);
            return;
        }

        float bestDistanceSquared = float.MaxValue;
        (int dx, int dy) bestDirection = availableDirections[0];

        for (int optionIndex = 0; optionIndex < directionCount; optionIndex++)
        {
            float targetOffsetX = cellX + availableDirections[optionIndex].dx - targetX;
            float targetOffsetY = cellY + availableDirections[optionIndex].dy - targetY;
            float distanceSquared = targetOffsetX * targetOffsetX + targetOffsetY * targetOffsetY;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestDirection = availableDirections[optionIndex];
            }
        }

        direction = bestDirection;
        targetCell = (cellX + bestDirection.dx, cellY + bestDirection.dy);
    }

    private void RainbowPickRandom(ref (int x, int y) cell, ref (int x, int y) targetCell,
        ref (int dx, int dy) direction)
    {
        var (cellX, cellY) = cell;
        var (previousDirectionX, previousDirectionY) = direction;

        Span<(int dx, int dy)> availableDirections = stackalloc (int, int)[4];
        int directionCount = 0;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            if (!RainbowCanMove(cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex])) continue;
            if (HorizontalDirections[directionIndex] == -previousDirectionX && VerticalDirections[directionIndex] == -previousDirectionY) continue;

            availableDirections[directionCount++] = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);
        }

        if (directionCount == 0)
        {
            direction = (-previousDirectionX, -previousDirectionY);
            targetCell = (cellX - previousDirectionX, cellY - previousDirectionY);
            return;
        }

        var chosenDirection = availableDirections[RandomGenerator.Next(directionCount)];
        direction = chosenDirection;
        targetCell = (cellX + chosenDirection.dx, cellY + chosenDirection.dy);
    }

    private void RainbowPickHouseExit(ref (int x, int y) cell, ref (int x, int y) targetCell,
        ref (int dx, int dy) direction)
    {
        var (cellX, cellY) = cell;
        int doorLeft = Maze.HouseDoorLeft;
        int doorRight = Maze.HouseDoorRight;

        if (cellX < doorLeft && Maze.CanMove(cellX, cellY, 1, 0))
        {
            direction = (1, 0);
            targetCell = (cellX + 1, cellY);
        }
        else if (cellX > doorRight && Maze.CanMove(cellX, cellY, -1, 0))
        {
            direction = (-1, 0);
            targetCell = (cellX - 1, cellY);
        }
        else if (Maze.CanMove(cellX, cellY, 0, -1))
        {
            direction = (0, -1);
            targetCell = (cellX, cellY - 1);
        }
        else
        {
            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                if (Maze.CanMove(cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex]))
                {
                    direction = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);
                    targetCell = (cellX + HorizontalDirections[directionIndex], cellY + VerticalDirections[directionIndex]);
                    return;
                }
        }
    }

    private void RainbowPickHouseWander(ref (int x, int y) cell, ref (int x, int y) targetCell,
        ref (int dx, int dy) direction)
    {
        var (cellX, cellY) = cell;
        var (previousDirectionX, _) = direction;

        if (previousDirectionX == 0)
            previousDirectionX = 1;

        if (Maze.CanMove(cellX, cellY, previousDirectionX, 0) && Maze.InGhostHouse(cellX + previousDirectionX, cellY))
        {
            direction = (previousDirectionX, 0);
            targetCell = (cellX + previousDirectionX, cellY);
        }
        else if (Maze.CanMove(cellX, cellY, -previousDirectionX, 0) && Maze.InGhostHouse(cellX - previousDirectionX, cellY))
        {
            direction = (-previousDirectionX, 0);
            targetCell = (cellX - previousDirectionX, cellY);
        }
        else
        {
            direction = (0, 0);
            targetCell = (cellX, cellY);
        }
    }

    private void RainbowPickDirection(ref (int x, int y) cell, ref (int x, int y) targetCell,
        ref (int dx, int dy) direction, int eyeIndex)
    {
        var (cellX, cellY) = cell;
        var (previousDirectionX, previousDirectionY) = direction;

        (int tx, int ty) targetTile;

        if (RainbowPhaseState == RainbowPhase.Merging)
        {
            targetTile = RainbowNearestSiblingCell(eyeIndex);
        }
        else if ((RainbowPhaseState == RainbowPhase.Duo || RainbowPhaseState == RainbowPhase.Trio) && Player != null)
        {
            var playerCell = Player.Cell;
            var cornerCell = RainbowCornerTargets[eyeIndex];
            targetTile = ((playerCell.x * 3 + cornerCell.x * 2) / 5, (playerCell.y * 3 + cornerCell.y * 2) / 5);
        }
        else
        {
            targetTile = RainbowWanderTargetTiles[eyeIndex];
            float manhattanDistance = MathF.Abs(cellX - targetTile.tx) + MathF.Abs(cellY - targetTile.ty);
            if (manhattanDistance <= 2 || Maze.IsWall(targetTile.tx, targetTile.ty))
                RainbowWanderTargetTiles[eyeIndex] = targetTile = RainbowPickRandomWalkable();
        }

        Span<(int dx, int dy)> availableDirections = stackalloc (int, int)[4];
        int directionCount = 0;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            if (!RainbowCanMove(cellX, cellY, HorizontalDirections[directionIndex], VerticalDirections[directionIndex])) continue;
            if (HorizontalDirections[directionIndex] == -previousDirectionX && VerticalDirections[directionIndex] == -previousDirectionY) continue;

            availableDirections[directionCount++] = (HorizontalDirections[directionIndex], VerticalDirections[directionIndex]);
        }

        if (directionCount == 0)
        {
            direction = (-previousDirectionX, -previousDirectionY);
            targetCell = (cellX - previousDirectionX, cellY - previousDirectionY);
            return;
        }

        if (directionCount == 1)
        {
            var onlyDirection = availableDirections[0];
            direction = onlyDirection;
            targetCell = (cellX + onlyDirection.dx, cellY + onlyDirection.dy);
            return;
        }

        float bestDistanceSquared = float.MaxValue;
        (int dx, int dy) bestDirection = availableDirections[0];

        for (int optionIndex = 0; optionIndex < directionCount; optionIndex++)
        {
            float targetOffsetX = cellX + availableDirections[optionIndex].dx - targetTile.tx;
            float targetOffsetY = cellY + availableDirections[optionIndex].dy - targetTile.ty;
            float distanceSquared = targetOffsetX * targetOffsetX + targetOffsetY * targetOffsetY;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestDirection = availableDirections[optionIndex];
            }
        }

        direction = bestDirection;
        targetCell = (cellX + bestDirection.dx, cellY + bestDirection.dy);
    }

    private (int x, int y) RainbowNearestSiblingCell(int eyeIndex)
    {
        var currentCell = eyeIndex == 0 ? RainbowMainCell : RainbowCloneCells[eyeIndex - 1];
        float bestDistanceSquared = float.MaxValue;
        (int x, int y) nearestCell = currentCell;

        int rainbowGhostCount = 1 + RainbowCloneCount;

        for (int siblingIndex = 0; siblingIndex < rainbowGhostCount; siblingIndex++)
        {
            if (siblingIndex == eyeIndex) continue;

            var siblingCell = siblingIndex == 0 ? RainbowMainCell : RainbowCloneCells[siblingIndex - 1];
            float targetOffsetX = currentCell.x - siblingCell.x;
            float targetOffsetY = currentCell.y - siblingCell.y;
            float distanceSquared = targetOffsetX * targetOffsetX + targetOffsetY * targetOffsetY;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearestCell = siblingCell;
            }
        }

        return nearestCell;
    }

    private bool RainbowCanMove(int cellX, int cellY, int directionX, int directionY)
    {
        if (!Maze.CanMove(cellX, cellY, directionX, directionY)) return false;
        if (RainbowMainEaten) return true;

        int nextCellX = Maze.WrapX(cellX + directionX), nextCellY = cellY + directionY;
        if (Maze.InGhostHouse(nextCellX, nextCellY)) return false;
        if (Maze.IsGhostDoor(nextCellX, nextCellY)) return false;

        return true;
    }

    private bool RainbowIsReleased()
    {
        if (RainbowMainRespawnTimer > 0f) return false;
        if (RainbowReleaseTime > 0 && RainbowElapsedTime >= RainbowReleaseTime) return true;

        if (RainbowReleaseAtCoinPercent > 0 && Player != null && Player.CoinsTotal > 0)
        {
            float coinPercent = (float)Player.CoinsCollected / Player.CoinsTotal;
            if (coinPercent >= RainbowReleaseAtCoinPercent) return true;
        }

        if (RainbowReleaseTime <= 0 && RainbowReleaseAtCoinPercent <= 0) return true;

        return false;
    }

    private void RainbowShiftEyeHistory()
    {
        RainbowEyePositions[0] = RainbowPreviousPositions[0];
        ref var mainTransform = ref Scene.ECS.GetComponent<Transform>(RainbowMainId);
        RainbowPreviousPositions[0] = new Vector2(mainTransform.Position.X, mainTransform.Position.Y);

        for (int cloneIndex = 0; cloneIndex < RainbowCloneCount; cloneIndex++)
        {
            RainbowEyePositions[1 + cloneIndex] = RainbowPreviousPositions[1 + cloneIndex];
            ref var cloneTransform = ref Scene.ECS.GetComponent<Transform>(RainbowCloneIds[cloneIndex]);
            RainbowPreviousPositions[1 + cloneIndex] = new Vector2(cloneTransform.Position.X, cloneTransform.Position.Y);
        }
    }

    private void RainbowUpdateMainColor()
    {
        var color = LightFactory.HueToRGB(RainbowMainHue);
        ref var material = ref Scene.ECS.GetComponent<Material>(RainbowMainId);
        material.Albedo = Color.Transparent;
        material.Emissive = color;
    }

    private void RainbowUpdateCloneColor(int cloneIndex)
    {
        var color = LightFactory.HueToRGB(RainbowMainHue);
        ref var material = ref Scene.ECS.GetComponent<Material>(RainbowCloneIds[cloneIndex]);
        material.Albedo = Color.Transparent;
        material.Emissive = color;
    }

    private (int x, int y) RainbowGetCorner(int eyeIndex) => (eyeIndex % 4) switch
    {
        0 => (2, 1),
        1 => (Maze.Columns - 3, 1),
        2 => (0, Maze.Rows - 2),
        _ => (Maze.Columns - 1, Maze.Rows - 2),
    };

    private (int x, int y) RainbowPickRandomWalkable()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int cellX = RandomGenerator.Next(Maze.Columns);
            int cellY = RandomGenerator.Next(Maze.Rows);
            if (!Maze.IsWall(cellX, cellY) && !Maze.InGhostHouse(cellX, cellY) && !Maze.IsGhostDoor(cellX, cellY))
                return (cellX, cellY);
        }

        return (Maze.Columns / 2, 0);
    }

    public override void LateRender()
    {
        if (Geometry.IsDebugHidingGameplay) return;

        DrawGhostEyes();
    }

    void DrawGhostEyes()
    {
        int regularGhostCount = GhostCount;
        int rainbowGhostCount = RainbowTotalCount;
        if (regularGhostCount == 0 && rainbowGhostCount == 0) return;

        float horizontalScale = Renderer.ScreenWidth / Renderer.VirtualSize.X;
        float verticalScale = Renderer.ScreenHeight / Renderer.VirtualSize.Y;

        Renderer.Reset()
            .Configure(BlendState.AlphaBlend)
            .SetTarget(null);

        for (int ghostIndex = 0; ghostIndex < regularGhostCount; ghostIndex++)
        {
            if (GhostIds[ghostIndex] == -1 || !Scene.ECS.IsAlive(GhostIds[ghostIndex])) continue;
            if (Scene.ECS.IsDisabled(GhostIds[ghostIndex])) continue;

            float radius = IsGhostFrightened(ghostIndex) ? BodyRadius * FrightenedShrink : BodyRadius;
            float eyeRadius = radius * 0.667f;
            float eyeDiameter = eyeRadius * 2f;
            float eyeOffset = radius * 0.133f;
            var direction = GetGhostDirection(ghostIndex);
            var eyePosition = GetGhostEyePosition(ghostIndex);
            float eyeCenterX = eyePosition.X + direction.dx * eyeOffset;
            float eyeCenterY = eyePosition.Y + direction.dy * eyeOffset;

            Color eyeColor;
            if (IsGhostEaten(ghostIndex))
                eyeColor = Color.White;
            else if (IsGhostFrightened(ghostIndex))
                eyeColor = Color.White;
            else
                eyeColor = GetGhostType(ghostIndex) == PacmanGhostType.Shadow ? Color.White : Color.Black;

            if (EyesTexture != null)
                Renderer.DrawTexture(EyesTexture,
                    new Rectangle(
                        (int)((eyeCenterX - eyeRadius) * horizontalScale),
                        (int)((eyeCenterY - eyeRadius) * verticalScale),
                        (int)(eyeDiameter * horizontalScale),
                        (int)(eyeDiameter * verticalScale)),
                    eyeColor);
        }

        for (int rainbowIndex = 0; rainbowIndex < rainbowGhostCount; rainbowIndex++)
        {
            int rainbowEntityId = rainbowIndex == 0 ? RainbowMainId : RainbowCloneIds[rainbowIndex - 1];
            if (!Scene.ECS.IsAlive(rainbowEntityId)) continue;

            float radius = RainbowIsFrightened ? BodyRadius * FrightenedShrink : BodyRadius;
            float eyeRadius = radius * 0.667f;
            float eyeDiameter = eyeRadius * 2f;
            float eyeOffset = radius * 0.133f;
            var direction = GetRainbowEyeDirection(rainbowIndex);
            var eyePosition = GetRainbowEyePosition(rainbowIndex);
            float eyeCenterX = eyePosition.X + direction.dx * eyeOffset;
            float eyeCenterY = eyePosition.Y + direction.dy * eyeOffset;

            Color eyeColor;
            if (rainbowIndex == 0 && RainbowIsMainEaten)
                eyeColor = Color.White;
            else if (RainbowIsFrightened)
                eyeColor = Color.White;
            else
                eyeColor = Color.Black;

            if (EyesTexture != null)
                Renderer.DrawTexture(EyesTexture,
                    new Rectangle(
                        (int)((eyeCenterX - eyeRadius) * horizontalScale),
                        (int)((eyeCenterY - eyeRadius) * verticalScale),
                        (int)(eyeDiameter * horizontalScale),
                        (int)(eyeDiameter * verticalScale)),
                    eyeColor);
        }

        Renderer.Commit();
    }
}
