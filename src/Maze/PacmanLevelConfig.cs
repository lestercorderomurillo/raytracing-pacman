using Microsoft.Xna.Framework;

namespace RaytracingPacman;

/// <summary>Ghost personalities used to select targeting behavior and appearance.</summary>
public enum PacmanGhostType : byte { Blinky, Pinky, Inky, Clyde, Dinky, Shadow, Rainbow }
/// <summary>Regular ghost behavior states for chasing, scattering, and frightened movement.</summary>
public enum PacmanGhostMode : byte { Scatter, Chase, Frightened }
/// <summary>Behavior phases of the rainbow ghost and its clones.</summary>
public enum RainbowPhase : byte { Solo, Duo, Trio, Merging }

/// <summary>Ghost personality, appearance, and release settings for a level.</summary>
public struct GhostEntry
{
    public PacmanGhostType Type;
    public (int x, int y)? StartCell;
    public float ReleaseAfter;
    public float ReleaseAtCoinPercent;
}

/// <summary>Maze, character, collectible, and lighting settings for one level.</summary>
public class PacmanLevelConfig
{
    public string Tag { get; set; } = "1-1";
    public string[] Layout { get; set; }
    public bool Procedural { get; set; }
    public int MazeSeed { get; set; }
    public Color WallEmissive { get; set; } = new Color((byte)120, (byte)80, (byte)255, (byte)255);
    public (int left, int top, int right, int bottom) GhostHouse { get; set; } = (11, 13, 16, 15);
    public (int x, int y)[] NoUpTiles { get; set; } = [(12, 11), (15, 11)];

    public float CoinRadius { get; set; } = 6f;
    public Color CoinColor { get; set; } = new Color(165, 130, 15);

    public float PowerPelletRadius { get; set; } = 16f;
    public Color PowerPelletColor { get; set; } = new Color((byte)255, (byte)255, (byte)255, (byte)255);
    public float FrightenedDuration { get; set; } = 5f;
    public (int x, int y)[] PowerPelletCells { get; set; }

    public GhostEntry[] Ghosts { get; set; } = [];
    public float GhostSpeed { get; set; } = 200f;

    public float RainbowSpeed { get; set; } = 200f;

    public (int x, int y) PlayerStartCell { get; set; } = (14, 23);
}
