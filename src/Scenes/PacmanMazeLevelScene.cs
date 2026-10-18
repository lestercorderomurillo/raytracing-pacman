using System;
using com.radiant.engine.bundle;
using com.radiant.engine.core;
using com.radiant.engine.runtime;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace RaytracingPacman;

public class PacmanMazeLevelScene : Scene
{
    private SystemGroup LightingGroup;
    private SystemGroup UpscalerGroup;
    private bool UpscalerEnabled = true;
    private ColorManagement Tonemapper;
    private GizmosRenderer Gizmos;

    private PacmanMazeBuilder Maze;
    private PacmanMazeRenderer MazeRenderer;
    private PacmanGhostAI GhostAI;
    private PacmanPlayerController PlayerController;

    private Color BaseCoinColor;
    private Color BasePowerPelletColor;
    private float CoinWaveTime;
    private int CurrentLevel;
    private bool GhostAIWasEnabled = true;
    private bool PlayerWasEnabled = true;

    private static readonly string[] PacmanLayout =
    [
        "1111111111111111111111111111",
        "1000000000000110000000000001",
        "1011110111110110111110111101",
        "1011110111110110111110111101",
        "1011110111110110111110111101",
        "1000000000000000000000000001",
        "1011110110111111110110111101",
        "1011110110111111110110111101",
        "1000000110000110000110000001",
        "1111110111110110111110111111",
        "1111110111110110111110111111",
        "1111110110000000000110111111",
        "1111110110111001110110111111",
        "1111110110100000010110111111",
        "0000000000100000010000000000",
        "1111110110100000010110111111",
        "1111110110111111110110111111",
        "1111110110000000000110111111",
        "1111110110111111110110111111",
        "1111110110111111110110111111",
        "1000000000000110000000000001",
        "1011110111110110111110111101",
        "1011110111110110111110111101",
        "1000110000000000000000110001",
        "1110110110111111110110110111",
        "1110110110111111110110110111",
        "1000000110000110000110000001",
        "1011111111110110111111111101",
        "1011111111110110111111111101",
        "1000000000000000000000000001",
        "1111111111111111111111111111",
    ];

    private static readonly (int x, int y)[] DefaultGhostHouseCells =
    [
        (11, 15), (12, 15), (13, 15), (14, 15), (15, 15), (16, 15),
        (11, 14), (12, 14), (13, 14), (14, 14), (15, 14), (16, 14),
        (11, 13), (12, 13), (13, 13), (14, 13), (15, 13), (16, 13),
    ];

    private static readonly PacmanLevelConfig[] Levels =
    [
        new PacmanLevelConfig
        {
            Tag = "1-1",
            Layout = PacmanLayout,
            WallEmissive = new Color((byte)90, (byte)45, (byte)160, (byte)170),
            CoinColor = new Color(225, 180, 35),
            PowerPelletCells = [(1, 3), (26, 3), (1, 23), (26, 23)],
            FrightenedDuration = 6f,
            Ghosts =
            [
                new() { Type = PacmanGhostType.Blinky, ReleaseAfter = 4f },
                new() { Type = PacmanGhostType.Pinky,  ReleaseAfter = 8f },
                new() { Type = PacmanGhostType.Inky,   ReleaseAfter = 12f },
                new() { Type = PacmanGhostType.Clyde,  ReleaseAfter = 16f },
            ],
            PlayerStartCell = (14, 23),
        },

        new PacmanLevelConfig
        {
            Tag = "1-2",
            Procedural = true,
            MazeSeed = 2482,
            WallEmissive = new Color(56, 84, 107),
            CoinColor = new Color(102, 210, 227),
            FrightenedDuration = 5f,
            Ghosts =
            [
                new() { Type = PacmanGhostType.Pinky,  ReleaseAfter = 4f },
                new() { Type = PacmanGhostType.Blinky, ReleaseAfter = 8f },
                new() { Type = PacmanGhostType.Shadow, ReleaseAfter = 24f },
            ],
            PlayerStartCell = (14, 23),
        },

        new PacmanLevelConfig
        {
            Tag = "1-3",
            Procedural = true,
            MazeSeed = 1344,
            WallEmissive = new Color(39, 38, 36),
            CoinColor = new Color(187, 186, 147),
            FrightenedDuration = 5f,
            Ghosts =
            [
                new() { Type = PacmanGhostType.Blinky,  ReleaseAfter = 4f },
                new() { Type = PacmanGhostType.Clyde,   ReleaseAfter = 8f },
                new() { Type = PacmanGhostType.Inky,    ReleaseAtCoinPercent = 0.25f },
                new() { Type = PacmanGhostType.Rainbow, ReleaseAtCoinPercent = 0.50f },
            ],
            PlayerStartCell = (14, 23),
        },

        new PacmanLevelConfig
        {
            Tag = "1-4",
            Procedural = true,
            MazeSeed = 1235,
            WallEmissive = new Color(53, 106, 74),
            CoinColor = new Color(183, 199, 65),
            FrightenedDuration = 7f,
            Ghosts =
            [
                new() { Type = PacmanGhostType.Dinky,  ReleaseAfter = 4f },
                new() { Type = PacmanGhostType.Clyde,  ReleaseAfter = 4f },
                new() { Type = PacmanGhostType.Blinky, ReleaseAfter = 4f },
            ],
            PlayerStartCell = (14, 23),
        },

        new PacmanLevelConfig
        {
            Tag = "2-1",
            Procedural = true,
            MazeSeed = 7131,
            WallEmissive = new Color(79, 43, 123),
            CoinColor = new Color(69, 186, 163),
            FrightenedDuration = 4f,
            Ghosts =
            [
                new() { Type = PacmanGhostType.Shadow,  ReleaseAfter = 4f },
                new() { Type = PacmanGhostType.Pinky,   ReleaseAfter = 8f },
                new() { Type = PacmanGhostType.Rainbow, ReleaseAfter = 16f },
                new() { Type = PacmanGhostType.Clyde,   ReleaseAtCoinPercent = 0.30f },
            ],
            PlayerStartCell = (14, 23),
        },

        new PacmanLevelConfig
        {
            Tag = "2-2",
            Procedural = true,
            MazeSeed = 8264,
            WallEmissive = new Color(20, 0, 0),
            CoinColor = new Color(236, 37, 0),
            FrightenedDuration = 3f,
            Ghosts =
            [
                new() { Type = PacmanGhostType.Shadow, ReleaseAfter = 1f },
                new() { Type = PacmanGhostType.Shadow, ReleaseAfter = 7f },
                new() { Type = PacmanGhostType.Shadow, ReleaseAfter = 13f },
            ],
            PlayerStartCell = (14, 23),
        },
    ];

    public override void SetupECS()
    {
        UDRQuality.Set(2); // Native resolution keeps small-window lighting and characters sharp.
        Renderer.LoadFont("PressStart2P", "fonts/PressStart2P.ttf");

        ECS.AddSystem<Inspector>();
        ECS.AddSystem<PerformanceMonitor>();
        ECS.AddSystem<Geometry>();

        LightingGroup = new SystemGroup("GI",
            ("Holographic Radiance Cascades", ECS.AddSystem<HRCGI>()),
            ("Radiance Cascades", ECS.AddSystem<RCGI>(enabled: false))
        );
        ECS.RegisterSystemGroup(LightingGroup);

        Tonemapper = ECS.AddSystem<ColorManagement>();

        UpscalerGroup = new SystemGroup("Upscaler",
            ("UDR1.0", ECS.AddSystem<UDR1>(enabled: false)),
            ("UDR2.0", ECS.AddSystem<UDR2>(enabled: false)),
            ("UDR3.0", ECS.AddSystem<UDR3>())
        );
        ECS.RegisterSystemGroup(UpscalerGroup);

        ECS.AddSystem<PacmanMazeBuilder>();
        ECS.AddSystem<PacmanMazeRenderer>();
        ECS.AddSystem<PacmanPlayerController>();
        ECS.AddSystem<PacmanGhostAI>();
        ECS.AddSystem<PacmanHUD>();

        Gizmos = ECS.AddSystem<GizmosRenderer>();

        base.SetupECS();
    }

    public override void SetupScene()
    {
        Maze = ECS.GetSystem<PacmanMazeBuilder>();
        MazeRenderer = ECS.GetSystem<PacmanMazeRenderer>();
        GhostAI = ECS.GetSystem<PacmanGhostAI>();
        PlayerController = ECS.GetSystem<PacmanPlayerController>();

        LoadLevel(0);
        UpdateUpscalerInput();

        Inspector.CreateWindow("scene", "Scene", 2, Visible: false);

        Inspector.AddLabel("scene", "level", $"Level: 1/{Levels.Length}");

        Inspector.AddButton("scene", "nextLevel", "Next Level", () => LoadLevel((CurrentLevel + 1) % Levels.Length));
        Inspector.AddButton("scene", "prevLevel", "Previous Level", () => LoadLevel((CurrentLevel - 1 + Levels.Length) % Levels.Length));
        Inspector.AddButton("scene", "restartLevel", "Restart Level", RestartLevel);
        Inspector.AddToggle("scene", "pause", "Pause", false, (paused) =>
        {
            Inspector.SetWidgetEnabled("scene", "pauseType", paused);
            ApplyPause(paused, paused ? 1 : -1);
        });
        Inspector.AddDropdown("scene", "pauseType", "Pause Type", ["Gameplay", "Gameplay + Animations"], 1, (index) => ApplyPause(true, index));
        Inspector.SetWidgetEnabled("scene", "pauseType", false);

        Inspector.AddSectionLabel("scene", "debugHeader", "Debugging");
        Inspector.AddButton("scene", "spawnDummy", "Spawn Dummy Entities", () =>
        {
            foreach (int entityId in LightFactory.SpawnRandom(ECS, 100_000, Renderer.VirtualSize))
                ECS.AddTag(entityId, "dummy");
        });
        Inspector.AddButton("scene", "destroyDummy", "Destroy Dummy Entities", () => ECS.DestroyEntitiesWithTag("dummy"));

        Inspector.AddSectionLabel("pipeline", "techniquesHeader", "Techniques");
        Inspector.AddToggle("pipeline", "upscalerToggle", "Enable Upscaler", UpscalerEnabled, (enabled) =>
        {
            UpscalerEnabled = enabled;
            Inspector.SetWidgetEnabled("pipeline", "upscaler", enabled);
            SetUpscalerEnabled(enabled);
        });
        Inspector.AddDropdown("pipeline", "upscaler", "Upscaler", UpscalerGroup.Names, UpscalerGroup.ActiveIdx, (index) =>
        {
            UpscalerGroup.SetActive(index);
            UpdateUpscalerInput();
            UpdateWindowVisibility();
        });
        Inspector.AddDropdown("pipeline", "lighting", "Lighting", LightingGroup.Names, LightingGroup.ActiveIdx, (index) =>
        {
            LightingGroup.SetActive(index);
            UpdateUpscalerInput();
            UpdateWindowVisibility();
        });

        Inspector.AddToggle("inspector", "gizmos", "Show Gizmos", Gizmos.Enabled, (enabled) => Gizmos.Enabled = enabled);

        Inspector.WindowsRestored += UpdateWindowVisibility;
        UpdateWindowVisibility();

        base.SetupScene();
    }

    private void ApplyPause(bool paused, int pauseType)
    {
        ECS.GameplayPaused = paused;
        ECS.AnimationPaused = paused && pauseType == 1;
    }

    private void RestartLevel()
    {
        ECS.DestroyAllEntities();
        GhostAI.Clear();
        GhostAI.ClearRainbow();
        PlayerController.Clear();
        Maze.ClearCoins();
        MazeRenderer.ClearRenderTargets();
        Maze.ClearMaze();

        LoadLevel(CurrentLevel);
    }

    private void LoadLevel(int levelIndex)
    {
        GhostAI.Clear();
        GhostAI.ClearRainbow();
        PlayerController.Clear();
        Maze.ClearCoins();
        MazeRenderer.ClearRenderTargets();
        Maze.ClearMaze();

        CurrentLevel = levelIndex;
        var levelConfig = Levels[levelIndex];
        float levelScale = 0.92f;

        Maze.Layout = levelConfig.Procedural
            ? PacmanMazeGenerator.Generate(levelIndex + levelConfig.MazeSeed)
            : levelConfig.Layout;
        Maze.CellSize = 70f * levelScale;
        Maze.WallLight = levelConfig.WallEmissive;
        Maze.GhostHouse = levelConfig.GhostHouse;
        Maze.NoUpTiles = levelConfig.NoUpTiles;

        Maze.BuildMaze();
        Renderer.FitWorld(Renderer.VirtualSize / 2f, new Vector2(Maze.Columns * Maze.CellSize + 160f, Renderer.VirtualHeight));
        MazeRenderer.RenderMaze();

        Maze.SpawnCoins(levelConfig.CoinRadius * levelScale, levelConfig.CoinColor, 1f, levelConfig.PlayerStartCell);
        var pelletPositions = levelConfig.PowerPelletCells ?? Maze.FindCornerPelletPositions();
        if (pelletPositions.Length > 0)
            Maze.SpawnPowerPellets(pelletPositions, levelConfig.PowerPelletRadius * levelScale, levelConfig.PowerPelletColor, 1f);

        BaseCoinColor = levelConfig.CoinColor;
        BasePowerPelletColor = levelConfig.PowerPelletColor;

        var ghostTexture = Renderer.GetTexture("Ghost");
        float bodyRadius = 28.5f * levelScale;

        int rainbowIndex = -1;

        for (int ghostIndex = 0; ghostIndex < levelConfig.Ghosts.Length; ghostIndex++)
            if (levelConfig.Ghosts[ghostIndex].Type == PacmanGhostType.Rainbow)
            {
                rainbowIndex = ghostIndex;
                break;
            }

        bool hasRainbow = rainbowIndex >= 0;

        int regularGhostCount = hasRainbow ? levelConfig.Ghosts.Length - 1 : levelConfig.Ghosts.Length;

        if (regularGhostCount > 0)
        {
            var regularGhostEntries = new GhostEntry[regularGhostCount];
            var startCells = new (int x, int y)[regularGhostCount];
            var colors = new Color[regularGhostCount];

            int regularGhostIndex = 0;

            for (int ghostIndex = 0; ghostIndex < levelConfig.Ghosts.Length; ghostIndex++)
            {
                if (levelConfig.Ghosts[ghostIndex].Type == PacmanGhostType.Rainbow) continue;

                regularGhostEntries[regularGhostIndex] = levelConfig.Ghosts[ghostIndex];
                startCells[regularGhostIndex] = levelConfig.Ghosts[ghostIndex].StartCell
                    ?? DefaultGhostHouseCells[ghostIndex % DefaultGhostHouseCells.Length];
                colors[regularGhostIndex] = PacmanGhostAI.PersonalityColor(levelConfig.Ghosts[ghostIndex].Type);
                regularGhostIndex++;
            }

            var ghostIds = Maze.SpawnAtCells(startCells, colors, bodyRadius, 65530f, ghostTexture);

            GhostAI.BodyTexture = ghostTexture;
            GhostAI.BodyRadius = bodyRadius;
            GhostAI.GhostSpeed = levelConfig.GhostSpeed * levelScale;
            GhostAI.Track(ghostIds, startCells, regularGhostEntries);
        }

        if (hasRainbow)
        {
            var rainbowEntry = levelConfig.Ghosts[rainbowIndex];
            var rainbowCell = rainbowEntry.StartCell
                ?? DefaultGhostHouseCells[rainbowIndex % DefaultGhostHouseCells.Length];
            var rainbowCenter = Maze.CellCenter(rainbowCell.x, rainbowCell.y);
            var rainbowColor = LightFactory.HueToRGB(0f);

            int rainbowId = LightFactory.CreateLight(ECS, rainbowCenter, bodyRadius,
                Color.Transparent, rainbowColor, 65530f, ghostTexture);
            ECS.AddComponent<MotionTrackable>(rainbowId);

            GhostAI.BodyTexture = ghostTexture;
            GhostAI.BodyRadius = bodyRadius;
            GhostAI.RainbowSpeed = levelConfig.RainbowSpeed * levelScale;
            GhostAI.TrackRainbow(rainbowId, rainbowCell, rainbowEntry.ReleaseAfter, rainbowEntry.ReleaseAtCoinPercent);
        }

        var playerCell = levelConfig.PlayerStartCell;
        var playerColor = new Color(255, 210, 30);
        var playerEmissive = new Color((byte)255, (byte)220, (byte)50, (byte)255);
        var playerCenter = Maze.CellCenter(playerCell.x, playerCell.y);

        int playerId = LightFactory.CreateLight(ECS, playerCenter, bodyRadius,
            playerColor, playerEmissive, 65530f);
        ECS.AddComponent<MotionTrackable>(playerId);

        PlayerController.Speed = levelConfig.GhostSpeed * levelScale;
        PlayerController.LevelTag = levelConfig.Tag;
        PlayerController.Track(playerId, playerCell, 65530f);
        PlayerController.CoinColor = levelConfig.CoinColor;
        PlayerController.GhostAI = GhostAI;
        PlayerController.FrightenedDuration = levelConfig.FrightenedDuration;

        GhostAI.Player = PlayerController;
    }

    public override void Update()
    {
        bool ghostEnabled = GhostAI.Enabled;

        if (ghostEnabled != GhostAIWasEnabled)
        {
            if (ghostEnabled)
                GhostAI.ShowGhostEntities();
            else
                GhostAI.HideGhostEntities();

            GhostAIWasEnabled = ghostEnabled;
        }

        bool playerEnabled = PlayerController.Enabled;

        if (playerEnabled != PlayerWasEnabled)
        {
            if (playerEnabled)
                PlayerController.ShowPlayer();
            else
                PlayerController.HidePlayer();

            PlayerWasEnabled = playerEnabled;
        }

        if (PlayerController.PlayerCaught)
        {
            LoadLevel(CurrentLevel);
            return;
        }

        if (PlayerController.CoinsTotal > 0 && PlayerController.CoinsCollected >= PlayerController.CoinsTotal)
        {
            LoadLevel((CurrentLevel + 1) % Levels.Length);
            return;
        }

        if (ECS.AnimationPaused) return;

        CoinWaveTime += (float)GameTime.ElapsedGameTime.TotalSeconds;
        var playerPosition = PlayerController.WorldPosition;
        const float attractRadius = 200f;
        const float attractStrength = 0.25f;
        const float glowRadius = 300f;

        foreach (var (cell, coinId) in Maze.CoinCells)
        {
            if (!ECS.IsAlive(coinId)) continue;

            float phase = CoinWaveTime * 1.8f - cell.Item1 * 0.5f;
            float waveSine = MathF.Sin(phase);
            float wave = 0.85f + 0.15f * waveSine;

            var cellCenter = Maze.CellCenter(cell.Item1, cell.Item2);
            float coinX = cellCenter.X;
            float coinY = cellCenter.Y + 4.5f * MathF.Sin(phase);

            float playerOffsetX = playerPosition.X - coinX;
            float playerOffsetY = playerPosition.Y - coinY;
            float playerDistance = MathF.Sqrt(playerOffsetX * playerOffsetX + playerOffsetY * playerOffsetY);

            float proximity = 0f;

            if (playerDistance < attractRadius && playerDistance > 0.1f)
            {
                proximity = 1f - playerDistance / attractRadius;
                float pull = proximity * attractStrength;
                coinX += playerOffsetX * pull;
                coinY += playerOffsetY * pull;
            }

            float glow = playerDistance < glowRadius ? 1f - playerDistance / glowRadius : 0f;
            glow *= glow;
            float brightness = wave * (1f - glow) + 1.0f * glow + proximity * 0.6f;
            byte alpha = (byte)(BaseCoinColor.A * ((0.9f + 0.1f * waveSine) * (1f - glow) + glow));

            ref var coinMaterial = ref ECS.GetComponent<Material>(coinId);
            coinMaterial.Emissive = new Color(
                (byte)MathF.Min(BaseCoinColor.R * brightness, 255f),
                (byte)MathF.Min(BaseCoinColor.G * brightness, 255f),
                (byte)MathF.Min(BaseCoinColor.B * brightness, 255f),
                alpha);

            ref var coinTransform = ref ECS.GetComponent<Transform>(coinId);
            coinTransform.Position.X = coinX;
            coinTransform.Position.Y = coinY;
            float coinScale = 1f + proximity * 0.6f;
            coinTransform.Scale = new Vector3(coinScale, coinScale, 1f);
        }

        foreach (var (cell, pelletId) in Maze.PowerPelletCells)
        {
            if (!ECS.IsAlive(pelletId)) continue;

            float phase = CoinWaveTime * 1.8f - cell.Item1 * 0.5f;

            var cellCenter = Maze.CellCenter(cell.Item1, cell.Item2);
            float pelletX = cellCenter.X + 3f * MathF.Sin(phase * 1.3f + 0.7f);
            float pelletY = cellCenter.Y + 6f * MathF.Sin(phase);

            float playerOffsetX = playerPosition.X - pelletX;
            float playerOffsetY = playerPosition.Y - pelletY;
            float playerDistance = MathF.Sqrt(playerOffsetX * playerOffsetX + playerOffsetY * playerOffsetY);

            float proximity = 0f;

            if (playerDistance < attractRadius && playerDistance > 0.1f)
            {
                proximity = 1f - playerDistance / attractRadius;
                float pull = proximity * attractStrength;
                pelletX += playerOffsetX * pull;
                pelletY += playerOffsetY * pull;
            }

            float brightness = 1f + proximity * 0.6f;

            float hue = (CoinWaveTime * 2.5f + cell.Item1 * 0.7f) % 1f;
            Color rainbow = LightFactory.HueToRGB(hue);
            const float tint = 0.15f;

            ref var pelletMaterial = ref ECS.GetComponent<Material>(pelletId);
            pelletMaterial.Emissive = new Color(
                (byte)MathF.Min((255f * (1f - tint) + rainbow.R * tint) * brightness, 255f),
                (byte)MathF.Min((255f * (1f - tint) + rainbow.G * tint) * brightness, 255f),
                (byte)MathF.Min((255f * (1f - tint) + rainbow.B * tint) * brightness, 255f),
                BasePowerPelletColor.A);

            ref var pelletTransform = ref ECS.GetComponent<Transform>(pelletId);
            pelletTransform.Position.X = pelletX;
            pelletTransform.Position.Y = pelletY;
            float pelletScale = 1f + proximity * 0.6f;
            pelletTransform.Scale = new Vector3(pelletScale, pelletScale, 1f);
        }

        Inspector.SetLabel("scene", "level", $"Level: {CurrentLevel + 1}/{Levels.Length}");
    }

    private void UpdateUpscalerInput()
    {
        Func<Texture2D> lightingSource = () =>
            LightingGroup.Active is HRCGI holographicLighting ? holographicLighting.GetOutput() :
            LightingGroup.Active is RCGI radianceLighting ? radianceLighting.GetOutput() : null;

        Tonemapper.SetInputSource(lightingSource);

        Func<Texture2D> colorOutput = Tonemapper.GetOutput;

        UpscalerGroup.ForEach(system =>
        {
            switch (system)
            {
                case UDR1 firstUpscaler: firstUpscaler.SetInputSource(colorOutput); break;
                case UDR2 secondUpscaler: secondUpscaler.SetInputSource(colorOutput); break;
                case UDR3 thirdUpscaler: thirdUpscaler.SetInputSource(colorOutput); break;
            }
        });
    }

    private void SetUpscalerEnabled(bool enabled)
    {
        if (enabled)
        {
            UpscalerGroup.Active?.Initialize();
            if (UpscalerGroup.Active != null) UpscalerGroup.Active.Enabled = true;
            UpdateUpscalerInput();
        }
        else
        {
            UpscalerGroup.Active?.Dispose();
            if (UpscalerGroup.Active != null) UpscalerGroup.Active.Enabled = false;
            Renderer.RenderScale = 1.0f;
        }

        UpdateWindowVisibility();
    }

    private void UpdateWindowVisibility()
    {
        if (LightingGroup.Active is HRCGI)
            Inspector.HideWindow("rcgi");
        else
            Inspector.HideWindow("hrcgi");

        string[] upscalerWindows = ["udr1", "udr2", "udr3"];
        string activeWindow = UpscalerEnabled ? UpscalerGroup.Active switch
        {
            UDR1 => "udr1",
            UDR2 => "udr2",
            UDR3 => "udr3",
            null => "",
            _ => ""
        } : "";

        foreach (var windowId in upscalerWindows)
        {
            if (windowId != activeWindow)
                Inspector.HideWindow(windowId);
        }
    }
}
