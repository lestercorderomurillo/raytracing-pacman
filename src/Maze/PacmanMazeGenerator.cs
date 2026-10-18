using System.Collections.Generic;
using System;

namespace RaytracingPacman;

/// <summary>
/// Procedural Pac-Man maze generator based on the Tetris algorithm
/// from shaunlebron/pacman-mazegen. Generates symmetric 28×31 mazes.
/// </summary>
public static class PacmanMazeGenerator
{
    private const int CellRowCount = 9;
    private const int CellColumnCount = 5;
    private const int TileRowCount = 31;  // CellRowCount * 3 + 1 + 3
    private const int HalfTileColumnCount = 16;  // CellColumnCount * 3 - 1 + 2
    private const int MirrorColumn = 14;  // HalfTileColumnCount - 2
    private const int FullColumnCount = 28; // MirrorColumn * 2

    private const int Up = 0;
    private const int Right = 1;
    private const int Down = 2;
    private const int Left = 3;

    public static string[] Generate(int seed = -1)
    {
        var randomGenerator = seed >= 0 ? new Random(seed) : new Random();

        for (int attempt = 0; attempt < 300; attempt++)
        {
            var builder = new MazeBuilder(randomGenerator);
            var result = builder.Build();
            if (result != null) return result;
        }

        return FallbackLayout();
    }

    private sealed class Cell
    {
        public int Column, Row;
        public bool Filled;
        public readonly bool[] Connections = new bool[4];
        public readonly Cell[] Neighbors = new Cell[4];
        public int GroupIndex = -1;
        public bool IsRaiseHeightCandidate;
        public bool IsShrinkWidthCandidate;
        public bool RaiseHeight;
        public bool ShrinkWidth;
        public bool TopTunnel;
        public int SingleDeadEndDirection;
        public int FinalColumn, FinalRow, FinalWidth, FinalHeight;
    }

    private sealed class MazeBuilder
    {
        private readonly Random RandomGenerator;
        private readonly Cell[] Cells = new Cell[CellRowCount * CellColumnCount];
        private readonly int[] TallRows = new int[CellColumnCount];
        private readonly int[] NarrowColumns = new int[CellRowCount];

        public MazeBuilder(Random randomGenerator) { this.RandomGenerator = randomGenerator; }

        public string[] Build()
        {
            Reset();
            GeneratePieces();
            if (!IsDesirable()) return null;

            SetUpScaledCoordinates();
            JoinWalls();
            if (!CreateTunnels()) return null;

            return GetTiles();
        }

        private void Shuffle<T>(List<T> items)
        {
            for (int itemIndex = items.Count - 1; itemIndex > 0; itemIndex--)
            {
                int swapIndex = RandomGenerator.Next(itemIndex + 1);
                (items[itemIndex], items[swapIndex]) = (items[swapIndex], items[itemIndex]);
            }
        }

        private Cell RandomElement(List<Cell> candidates)
        {
            return candidates.Count == 0 ? null : candidates[RandomGenerator.Next(candidates.Count)];
        }

        private void Reset()
        {
            for (int cellIndex = 0; cellIndex < CellRowCount * CellColumnCount; cellIndex++)
                Cells[cellIndex] = new Cell { Column = cellIndex % CellColumnCount, Row = cellIndex / CellColumnCount };

            for (int cellIndex = 0; cellIndex < CellRowCount * CellColumnCount; cellIndex++)
            {
                var cell = Cells[cellIndex];
                if (cell.Column > 0) cell.Neighbors[Left] = Cells[cellIndex - 1];
                if (cell.Column < CellColumnCount - 1) cell.Neighbors[Right] = Cells[cellIndex + 1];
                if (cell.Row > 0) cell.Neighbors[Up] = Cells[cellIndex - CellColumnCount];
                if (cell.Row < CellRowCount - 1) cell.Neighbors[Down] = Cells[cellIndex + CellColumnCount];
            }

            // Ghost house: cells (0,3), (1,3), (0,4), (1,4)
            int houseCellIndex = 3 * CellColumnCount;
            var ghostHouseCell = Cells[houseCellIndex];
            ghostHouseCell.Filled = true;
            ghostHouseCell.Connections[Left] = ghostHouseCell.Connections[Right] = ghostHouseCell.Connections[Down] = true;

            houseCellIndex++;
            ghostHouseCell = Cells[houseCellIndex];
            ghostHouseCell.Filled = true;
            ghostHouseCell.Connections[Left] = ghostHouseCell.Connections[Down] = true;

            houseCellIndex += CellColumnCount - 1;
            ghostHouseCell = Cells[houseCellIndex];
            ghostHouseCell.Filled = true;
            ghostHouseCell.Connections[Left] = ghostHouseCell.Connections[Up] = ghostHouseCell.Connections[Right] = true;

            houseCellIndex++;
            ghostHouseCell = Cells[houseCellIndex];
            ghostHouseCell.Filled = true;
            ghostHouseCell.Connections[Up] = ghostHouseCell.Connections[Left] = true;

            for (int cellIndex = 0; cellIndex < CellColumnCount; cellIndex++)
                TallRows[cellIndex] = CellRowCount;

            for (int cellIndex = 0; cellIndex < CellRowCount; cellIndex++)
                NarrowColumns[cellIndex] = CellColumnCount;
        }

        private List<Cell> GetLeftMostEmpty()
        {
            var emptyCells = new List<Cell>();

            for (int column = 0; column < CellColumnCount; column++)
            {
                for (int row = 0; row < CellRowCount; row++)
                {
                    var cell = Cells[column + row * CellColumnCount];
                    if (!cell.Filled) emptyCells.Add(cell);
                }

                if (emptyCells.Count > 0) break;
            }

            return emptyCells;
        }

        private bool IsOpenCell(Cell cell, int direction, int previousDirection = -1, int pieceSize = -1)
        {
            if (cell.Row == 6 && cell.Column == 0 && direction == Down) return false;
            if (cell.Row == 7 && cell.Column == 0 && direction == Up) return false;
            if (pieceSize == 2 && (direction == previousDirection || (direction + 2) % 4 == previousDirection)) return false;

            var adjacentCell = cell.Neighbors[direction];

            if (adjacentCell != null && !adjacentCell.Filled)
            {
                if (adjacentCell.Neighbors[Left] != null && !adjacentCell.Neighbors[Left].Filled) return false;

                return true;
            }

            return false;
        }

        private List<int> GetOpenDirections(Cell cell, int previousDirection, int pieceSize)
        {
            var openDirections = new List<int>();

            for (int direction = 0; direction < 4; direction++)
                if (IsOpenCell(cell, direction, previousDirection, pieceSize)) openDirections.Add(direction);

            return openDirections;
        }

        private void ConnectCell(Cell cell, int direction)
        {
            cell.Connections[direction] = true;
            cell.Neighbors[direction].Connections[(direction + 2) % 4] = true;
            if (cell.Column == 0 && direction == Right) cell.Connections[Left] = true;
        }

        private void FillCell(Cell cell, int groupIndex)
        {
            cell.Filled = true;
            cell.GroupIndex = groupIndex;
        }

        private static double StopProbability(int pieceSize) => pieceSize switch
        {
            <= 1 => 0,
            2 => 0.10,
            3 => 0.5,
            4 => 0.75,
            _ => 1
        };

        private void GeneratePieces()
        {
            int groupCount = 0;
            var singleCellCounts = new Dictionary<int, int> { { 0, 0 }, { CellRowCount - 1, 0 } };
            int longPieceCount = 0;

            while (true)
            {
                var leftmostEmptyCells = GetLeftMostEmpty();
                if (leftmostEmptyCells.Count == 0) break;

                var firstCell = leftmostEmptyCells[RandomGenerator.Next(leftmostEmptyCells.Count)];
                var currentCell = firstCell;
                FillCell(currentCell, groupCount);

                // Single cell at top/bottom boundary
                if (currentCell.Column < CellColumnCount - 1 && singleCellCounts.ContainsKey(currentCell.Row) && RandomGenerator.NextDouble() <= 0.35)
                {
                    if (singleCellCounts[currentCell.Row] == 0)
                    {
                        currentCell.Connections[currentCell.Row == 0 ? Up : Down] = true;
                        singleCellCounts[currentCell.Row]++;
                        groupCount++;
                        continue;
                    }
                }

                int pieceSize = 1;

                if (currentCell.Column == CellColumnCount - 1)
                {
                    currentCell.Connections[Right] = true;
                    currentCell.IsRaiseHeightCandidate = true;
                }
                else
                {
                    Cell newCell = null;
                    int direction = -1;

                    while (pieceSize < 5)
                    {
                        bool shouldStop = false;

                        // At size 2: try L-piece
                        if (pieceSize == 2)
                        {
                            var cell = firstCell;

                            if (cell.Column > 0 && cell.Connections[Right] && cell.Neighbors[Right]?.Neighbors[Right] != null)
                            {
                                if (longPieceCount < 1 && RandomGenerator.NextDouble() <= 1.0)
                                {
                                    cell = cell.Neighbors[Right].Neighbors[Right];
                                    bool canExtendUp = IsOpenCell(cell, Up);
                                    bool canExtendDown = IsOpenCell(cell, Down);
                                    int extensionDirection = -1;
                                    if (canExtendUp && canExtendDown) extensionDirection = RandomGenerator.Next(2) == 0 ? Up : Down;
                                    else if (canExtendUp) extensionDirection = Up;
                                    else if (canExtendDown) extensionDirection = Down;

                                    if (extensionDirection >= 0)
                                    {
                                        ConnectCell(cell, Left);
                                        FillCell(cell, groupCount);
                                        ConnectCell(cell, extensionDirection);
                                        FillCell(cell.Neighbors[extensionDirection], groupCount);
                                        longPieceCount++;
                                        pieceSize += 2;
                                        shouldStop = true;
                                    }
                                }
                            }
                        }

                        if (!shouldStop)
                        {
                            var openDirections = GetOpenDirections(currentCell, direction, pieceSize);

                            if (openDirections.Count == 0 && pieceSize == 2)
                            {
                                currentCell = newCell;
                                openDirections = GetOpenDirections(currentCell, direction, pieceSize);
                            }

                            if (openDirections.Count == 0)
                            {
                                shouldStop = true;
                            }
                            else
                            {
                                direction = openDirections[RandomGenerator.Next(openDirections.Count)];
                                newCell = currentCell.Neighbors[direction];
                                ConnectCell(currentCell, direction);
                                FillCell(newCell, groupCount);
                                pieceSize++;

                                if (firstCell.Column == 0 && pieceSize == 3) shouldStop = true;
                                if (!shouldStop && RandomGenerator.NextDouble() <= StopProbability(pieceSize)) shouldStop = true;
                            }
                        }

                        if (shouldStop)
                        {
                            if (pieceSize == 2)
                            {
                                // Attach vertical pair at right edge to wall
                                var cell = firstCell;

                                if (cell.Column == CellColumnCount - 1)
                                {
                                    if (cell.Connections[Up]) cell = cell.Neighbors[Up];
                                    cell.Connections[Right] = true;
                                    cell.Neighbors[Down].Connections[Right] = true;
                                }
                            }
                            else if (pieceSize == 3 || pieceSize == 4)
                            {
                                // Try long leg extension
                                if (longPieceCount < 1 && firstCell.Column > 0 && RandomGenerator.NextDouble() <= 0.5)
                                {
                                    var extensionDirections = new List<int>();

                                    for (int candidateDirection = 0; candidateDirection < 4; candidateDirection++)
                                        if (currentCell.Connections[candidateDirection] && IsOpenCell(currentCell.Neighbors[candidateDirection], candidateDirection))
                                            extensionDirections.Add(candidateDirection);

                                    if (extensionDirections.Count > 0)
                                    {
                                        int extensionDirection = extensionDirections[RandomGenerator.Next(extensionDirections.Count)];
                                        var extensionCell = currentCell.Neighbors[extensionDirection];
                                        ConnectCell(extensionCell, extensionDirection);
                                        FillCell(extensionCell.Neighbors[extensionDirection], groupCount);
                                        longPieceCount++;
                                    }
                                }
                            }

                            break;
                        }
                    }
                }

                groupCount++;
            }

            SetResizeCandidates();
        }

        private void SetResizeCandidates()
        {
            for (int cellIndex = 0; cellIndex < CellRowCount * CellColumnCount; cellIndex++)
            {
                var cell = Cells[cellIndex];
                var connections = cell.Connections;

                if ((cell.Column == 0 || !connections[Left]) && (cell.Column == CellColumnCount - 1 || !connections[Right]) && connections[Up] != connections[Down])
                    cell.IsRaiseHeightCandidate = true;

                var rightCell = cell.Neighbors[Right];

                if (rightCell != null)
                {
                    var rightConnections = rightCell.Connections;
                    if ((cell.Column == 0 || !connections[Left]) && !connections[Up] && !connections[Down] &&
                        (rightCell.Column == CellColumnCount - 1 || !rightConnections[Right]) && !rightConnections[Up] && !rightConnections[Down])
                        cell.IsRaiseHeightCandidate = rightCell.IsRaiseHeightCandidate = true;
                }

                if (cell.Column == CellColumnCount - 1 && connections[Right])
                    cell.IsShrinkWidthCandidate = true;

                if ((cell.Row == 0 || !connections[Up]) && (cell.Row == CellRowCount - 1 || !connections[Down]) && connections[Left] != connections[Right])
                    cell.IsShrinkWidthCandidate = true;
            }
        }

        private static bool IsCross(Cell cell) =>
            cell.Connections[Up] && cell.Connections[Right] && cell.Connections[Down] && cell.Connections[Left];

        private bool IsHorizontalPiece(int column, int row)
        {
            var firstConnections = Cells[column + row * CellColumnCount].Connections;
            var neighborConnections = Cells[column + 1 + row * CellColumnCount].Connections;
            return !firstConnections[Up] && !firstConnections[Down] && (column == 0 || !firstConnections[Left]) && firstConnections[Right] &&
                   !neighborConnections[Up] && !neighborConnections[Down] && neighborConnections[Left] && !neighborConnections[Right];
        }

        private bool IsVerticalPiece(int column, int row)
        {
            var firstConnections = Cells[column + row * CellColumnCount].Connections;
            var neighborConnections = Cells[column + (row + 1) * CellColumnCount].Connections;
            if (column == CellColumnCount - 1)
                return !firstConnections[Left] && !firstConnections[Up] && !firstConnections[Down] && !neighborConnections[Left] && !neighborConnections[Up] && !neighborConnections[Down];

            return !firstConnections[Left] && !firstConnections[Right] && !firstConnections[Up] && firstConnections[Down] &&
                   !neighborConnections[Left] && !neighborConnections[Right] && neighborConnections[Up] && !neighborConnections[Down];
        }

        private bool IsDesirable()
        {
            // Solid corners
            if (Cells[4].Connections[Up] || Cells[4].Connections[Right]) return false;
            if (Cells[CellRowCount * CellColumnCount - 1].Connections[Down] || Cells[CellRowCount * CellColumnCount - 1].Connections[Right]) return false;

            // Fix stacked 2-cell pieces
            for (int row = 0; row < CellRowCount - 1; row++)
            {
                for (int column = 0; column < CellColumnCount - 1; column++)
                {
                    if ((IsHorizontalPiece(column, row) && IsHorizontalPiece(column, row + 1)) || (IsVerticalPiece(column, row) && IsVerticalPiece(column + 1, row)))
                    {
                        if (column == 0) return false;

                        int groupIndex = Cells[column + row * CellColumnCount].GroupIndex;
                        Cells[column + row * CellColumnCount].Connections[Down] = Cells[column + row * CellColumnCount].Connections[Right] = true;
                        Cells[column + row * CellColumnCount].GroupIndex = groupIndex;
                        Cells[column + 1 + row * CellColumnCount].Connections[Down] = Cells[column + 1 + row * CellColumnCount].Connections[Left] = true;
                        Cells[column + 1 + row * CellColumnCount].GroupIndex = groupIndex;
                        Cells[column + (row + 1) * CellColumnCount].Connections[Up] = Cells[column + (row + 1) * CellColumnCount].Connections[Right] = true;
                        Cells[column + (row + 1) * CellColumnCount].GroupIndex = groupIndex;
                        Cells[column + 1 + (row + 1) * CellColumnCount].Connections[Up] = Cells[column + 1 + (row + 1) * CellColumnCount].Connections[Left] = true;
                        Cells[column + 1 + (row + 1) * CellColumnCount].GroupIndex = groupIndex;
                    }
                }
            }

            if (!ChooseTallRows()) return false;
            if (!ChooseNarrowColumns()) return false;

            return true;
        }

        private bool ChooseTallRows()
        {
            for (int row = 0; row < 3; row++)
            {
                var cell = Cells[row * CellColumnCount];

                if (cell.IsRaiseHeightCandidate && CanRaiseHeight(0, row))
                {
                    cell.RaiseHeight = true;
                    TallRows[cell.Column] = cell.Row;
                    return true;
                }
            }

            return false;
        }

        private bool CanRaiseHeight(int column, int row)
        {
            if (column == CellColumnCount - 1) return true;

            Cell rightCell = null;

            for (int scanRow = row; scanRow >= 0; scanRow--)
            {
                var cell = Cells[column + scanRow * CellColumnCount];
                rightCell = cell.Neighbors[Right];
                if ((!cell.Connections[Up] || IsCross(cell)) && (!rightCell.Connections[Up] || IsCross(rightCell)))
                    break;
            }

            var candidates = new List<Cell>();

            for (; rightCell != null; rightCell = rightCell.Neighbors[Down])
            {
                if (rightCell.IsRaiseHeightCandidate) candidates.Add(rightCell);
                if ((!rightCell.Connections[Down] || IsCross(rightCell)) &&
                    rightCell.Neighbors[Left] != null && (!rightCell.Neighbors[Left].Connections[Down] || IsCross(rightCell.Neighbors[Left])))
                    break;
            }

            Shuffle(candidates);

            foreach (var candidate in candidates)
            {
                if (CanRaiseHeight(candidate.Column, candidate.Row))
                {
                    candidate.RaiseHeight = true;
                    TallRows[candidate.Column] = candidate.Row;
                    return true;
                }
            }

            return false;
        }

        private bool ChooseNarrowColumns()
        {
            for (int column = CellColumnCount - 1; column >= 0; column--)
            {
                var cell = Cells[column];

                if (cell.IsShrinkWidthCandidate && CanShrinkWidth(column, 0))
                {
                    cell.ShrinkWidth = true;
                    NarrowColumns[cell.Row] = cell.Column;
                    return true;
                }
            }

            return false;
        }

        private bool CanShrinkWidth(int column, int row)
        {
            if (row == CellRowCount - 1) return true;

            Cell lowerCell = null;

            for (int scanColumn = column; scanColumn < CellColumnCount; scanColumn++)
            {
                var cell = Cells[scanColumn + row * CellColumnCount];
                lowerCell = cell.Neighbors[Down];
                if ((!cell.Connections[Right] || IsCross(cell)) && (!lowerCell.Connections[Right] || IsCross(lowerCell)))
                    break;
            }

            var candidates = new List<Cell>();

            for (; lowerCell != null; lowerCell = lowerCell.Neighbors[Left])
            {
                if (lowerCell.IsShrinkWidthCandidate) candidates.Add(lowerCell);
                if ((!lowerCell.Connections[Left] || IsCross(lowerCell)) &&
                    lowerCell.Neighbors[Up] != null && (!lowerCell.Neighbors[Up].Connections[Left] || IsCross(lowerCell.Neighbors[Up])))
                    break;
            }

            Shuffle(candidates);

            foreach (var candidate in candidates)
            {
                if (CanShrinkWidth(candidate.Column, candidate.Row))
                {
                    candidate.ShrinkWidth = true;
                    NarrowColumns[candidate.Row] = candidate.Column;
                    return true;
                }
            }

            return false;
        }

        private void SetUpScaledCoordinates()
        {
            for (int cellIndex = 0; cellIndex < CellRowCount * CellColumnCount; cellIndex++)
            {
                var cell = Cells[cellIndex];
                cell.FinalColumn = cell.Column * 3 - (NarrowColumns[cell.Row] < cell.Column ? 1 : 0);
                cell.FinalRow = cell.Row * 3 + (TallRows[cell.Column] < cell.Row ? 1 : 0);
                cell.FinalWidth = cell.ShrinkWidth ? 2 : 3;
                cell.FinalHeight = cell.RaiseHeight ? 4 : 3;
            }
        }

        private void JoinWalls()
        {
            // Join to top boundary
            for (int column = 0; column < CellColumnCount; column++)
            {
                var cell = Cells[column];

                if (!cell.Connections[Left] && !cell.Connections[Right] && !cell.Connections[Up] &&
                    (!cell.Connections[Down] || !cell.Neighbors[Down].Connections[Down]))
                {
                    if ((cell.Neighbors[Left] == null || !cell.Neighbors[Left].Connections[Up]) &&
                        cell.Neighbors[Right] != null && !cell.Neighbors[Right].Connections[Up])
                    {
                        if (!(cell.Neighbors[Down] != null && cell.Neighbors[Down].Connections[Right] &&
                              cell.Neighbors[Down].Neighbors[Right] != null && cell.Neighbors[Down].Neighbors[Right].Connections[Right]))
                        {
                            if (RandomGenerator.NextDouble() <= 0.25) cell.Connections[Up] = true;
                        }
                    }
                }
            }

            // Join to bottom boundary
            for (int column = 0; column < CellColumnCount; column++)
            {
                var cell = Cells[column + (CellRowCount - 1) * CellColumnCount];

                if (!cell.Connections[Left] && !cell.Connections[Right] && !cell.Connections[Down] &&
                    (!cell.Connections[Up] || !cell.Neighbors[Up].Connections[Up]))
                {
                    if ((cell.Neighbors[Left] == null || !cell.Neighbors[Left].Connections[Down]) &&
                        cell.Neighbors[Right] != null && !cell.Neighbors[Right].Connections[Down])
                    {
                        if (!(cell.Neighbors[Up] != null && cell.Neighbors[Up].Connections[Right] &&
                              cell.Neighbors[Up].Neighbors[Right] != null && cell.Neighbors[Up].Neighbors[Right].Connections[Right]))
                        {
                            if (RandomGenerator.NextDouble() <= 0.25) cell.Connections[Down] = true;
                        }
                    }
                }
            }

            // Join to right boundary
            for (int row = 1; row < CellRowCount - 1; row++)
            {
                var cell = Cells[CellColumnCount - 1 + row * CellColumnCount];
                if (cell.RaiseHeight) continue;

                if (!cell.Connections[Right] && !cell.Connections[Up] && !cell.Connections[Down] &&
                    !cell.Neighbors[Up].Connections[Right] && !cell.Neighbors[Down].Connections[Right])
                {
                    if (cell.Connections[Left])
                    {
                        var leftCell = cell.Neighbors[Left];

                        if (!leftCell.Connections[Up] && !leftCell.Connections[Down] && !leftCell.Connections[Left])
                        {
                            if (RandomGenerator.NextDouble() <= 0.5) cell.Connections[Right] = true;
                        }
                    }
                }
            }
        }

        private bool CreateTunnels()
        {
            var singleDeadEnds = new List<Cell>();
            var topSingleDeadEnds = new List<Cell>();
            var bottomSingleDeadEnds = new List<Cell>();
            var voidTunnels = new List<Cell>();
            var topVoidTunnels = new List<Cell>();
            var bottomVoidTunnels = new List<Cell>();
            var edgeTunnels = new List<Cell>();
            var topEdgeTunnels = new List<Cell>();
            var bottomEdgeTunnels = new List<Cell>();
            var doubleDeadEnds = new List<Cell>();

            for (int row = 0; row < CellRowCount; row++)
            {
                var cell = Cells[CellColumnCount - 1 + row * CellColumnCount];
                if (cell.Connections[Up]) continue;

                if (cell.Row > 1 && cell.Row < CellRowCount - 2)
                {
                    edgeTunnels.Add(cell);
                    if (cell.Row <= 2) topEdgeTunnels.Add(cell);
                    else if (cell.Row >= 5) bottomEdgeTunnels.Add(cell);
                }

                bool upBlocked = cell.Neighbors[Up] == null || cell.Neighbors[Up].Connections[Right];
                bool downBlocked = cell.Neighbors[Down] == null || cell.Neighbors[Down].Connections[Right];

                if (cell.Connections[Right])
                {
                    if (upBlocked)
                    {
                        voidTunnels.Add(cell);
                        if (cell.Row <= 2) topVoidTunnels.Add(cell);
                        else if (cell.Row >= 6) bottomVoidTunnels.Add(cell);
                    }
                }
                else
                {
                    if (cell.Connections[Down]) continue;

                    if (upBlocked != downBlocked)
                    {
                        if (!cell.RaiseHeight && row < CellRowCount - 1 && cell.Neighbors[Left] != null && !cell.Neighbors[Left].Connections[Left])
                        {
                            singleDeadEnds.Add(cell);
                            cell.SingleDeadEndDirection = upBlocked ? Up : Down;
                            int rowOffset = upBlocked ? 1 : 0;
                            if (cell.Row <= 1 + rowOffset) topSingleDeadEnds.Add(cell);
                            else if (cell.Row >= 5 + rowOffset) bottomSingleDeadEnds.Add(cell);
                        }
                    }
                    else if (upBlocked && downBlocked)
                    {
                        if (row > 0 && row < CellRowCount - 1 && cell.Neighbors[Left] != null)
                        {
                            if (cell.Neighbors[Left].Connections[Up] && cell.Neighbors[Left].Connections[Down])
                            {
                                if (cell.Row >= 2 && cell.Row <= 5) doubleDeadEnds.Add(cell);
                            }
                        }
                    }
                }
            }

            // Choose tunnels
            int desiredTunnelCount = RandomGenerator.NextDouble() <= 0.45 ? 2 : 1;
            Cell selectedCell;

            if (desiredTunnelCount == 1)
            {
                if ((selectedCell = RandomElement(voidTunnels)) != null) selectedCell.TopTunnel = true;
                else if ((selectedCell = RandomElement(singleDeadEnds)) != null) SelectSingleDeadEnd(selectedCell);
                else if ((selectedCell = RandomElement(edgeTunnels)) != null) selectedCell.TopTunnel = true;
                else return false;
            }
            else
            {
                if ((selectedCell = RandomElement(doubleDeadEnds)) != null)
                {
                    selectedCell.Connections[Right] = true;
                    selectedCell.TopTunnel = true;
                    selectedCell.Neighbors[Down].TopTunnel = true;
                }
                else
                {
                    int createdTunnelCount = 1;
                    if ((selectedCell = RandomElement(topVoidTunnels)) != null) selectedCell.TopTunnel = true;
                    else if ((selectedCell = RandomElement(topSingleDeadEnds)) != null) SelectSingleDeadEnd(selectedCell);
                    else if ((selectedCell = RandomElement(topEdgeTunnels)) != null) selectedCell.TopTunnel = true;
                    else createdTunnelCount = 0;

                    if ((selectedCell = RandomElement(bottomVoidTunnels)) != null) selectedCell.TopTunnel = true;
                    else if ((selectedCell = RandomElement(bottomSingleDeadEnds)) != null) SelectSingleDeadEnd(selectedCell);
                    else if ((selectedCell = RandomElement(bottomEdgeTunnels)) != null) selectedCell.TopTunnel = true;
                    else if (createdTunnelCount == 0) return false;
                }
            }

            // No straight-through horizontal path
            for (int row = 0; row < CellRowCount; row++)
            {
                var cell = Cells[CellColumnCount - 1 + row * CellColumnCount];
                if (!cell.TopTunnel) continue;

                bool isStraight = true;
                int tunnelTop = cell.FinalRow;
                var walkCell = cell;

                while (walkCell.Neighbors[Left] != null)
                {
                    walkCell = walkCell.Neighbors[Left];
                    if (!walkCell.Connections[Up] && walkCell.FinalRow == tunnelTop) continue;

                    isStraight = false;
                    break;
                }

                if (isStraight) return false;
            }

            // Clear unused void tunnels
            foreach (var voidCell in voidTunnels)
            {
                if (!voidCell.TopTunnel)
                {
                    ReplaceGroup(voidCell.GroupIndex, voidCell.Neighbors[Up].GroupIndex);
                    voidCell.Connections[Up] = true;
                    voidCell.Neighbors[Up].Connections[Down] = true;
                }
            }

            return true;
        }

        private void SelectSingleDeadEnd(Cell cell)
        {
            cell.Connections[Right] = true;
            if (cell.SingleDeadEndDirection == Up) cell.TopTunnel = true;
            else cell.Neighbors[Down].TopTunnel = true;
        }

        private void ReplaceGroup(int oldGroup, int newGroup)
        {
            for (int cellIndex = 0; cellIndex < CellRowCount * CellColumnCount; cellIndex++)
                if (Cells[cellIndex].GroupIndex == oldGroup) Cells[cellIndex].GroupIndex = newGroup;
        }

        private string[] GetTiles()
        {
            var tiles = new char[TileRowCount * FullColumnCount];
            var tileCells = new Cell[TileRowCount * HalfTileColumnCount];

            for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
                tiles[tileIndex] = '_';

            void SetMirroredTile(int column, int row, char tile)
            {
                if (column < 0 || column > HalfTileColumnCount - 1 || row < 0 || row > TileRowCount - 1) return;

                int mirrorOffset = column - 2;
                int rightTileIndex = MirrorColumn + mirrorOffset + row * FullColumnCount;
                int leftTileIndex = MirrorColumn - 1 - mirrorOffset + row * FullColumnCount;
                if (rightTileIndex >= 0 && rightTileIndex < tiles.Length) tiles[rightTileIndex] = tile;
                if (leftTileIndex >= 0 && leftTileIndex < tiles.Length) tiles[leftTileIndex] = tile;
            }

            char GetTile(int column, int row)
            {
                if (column < 0 || column > HalfTileColumnCount - 1 || row < 0 || row > TileRowCount - 1) return '\0';

                int tileIndex = MirrorColumn + (column - 2) + row * FullColumnCount;
                return (tileIndex >= 0 && tileIndex < tiles.Length) ? tiles[tileIndex] : '\0';
            }

            void SetTileCell(int column, int row, Cell cell)
            {
                if (column < 0 || column > HalfTileColumnCount - 1 || row < 0 || row > TileRowCount - 1) return;

                int cellIndex = column + row * HalfTileColumnCount;
                if (cellIndex >= 0 && cellIndex < tileCells.Length) tileCells[cellIndex] = cell;
            }

            Cell GetTileCell(int column, int row)
            {
                if (column < 0 || column > HalfTileColumnCount - 1 || row < 0 || row > TileRowCount - 1) return null;

                int cellIndex = column + row * HalfTileColumnCount;
                return (cellIndex >= 0 && cellIndex < tileCells.Length) ? tileCells[cellIndex] : null;
            }

            // Build mirrored paths from the cell connections.
            for (int cellIndex = 0; cellIndex < CellRowCount * CellColumnCount; cellIndex++)
            {
                var cell = Cells[cellIndex];

                for (int tileOffsetX = 0; tileOffsetX < cell.FinalWidth; tileOffsetX++)
                    for (int tileOffsetY = 0; tileOffsetY < cell.FinalHeight; tileOffsetY++)
                        SetTileCell(cell.FinalColumn + tileOffsetX, cell.FinalRow + 1 + tileOffsetY, cell);
            }

            for (int row = 0; row < TileRowCount; row++)
            {
                for (int column = 0; column < HalfTileColumnCount; column++)
                {
                    var cell = GetTileCell(column, row);
                    var leftCell = GetTileCell(column - 1, row);
                    var upperCell = GetTileCell(column, row - 1);

                    if (cell != null)
                    {
                        if ((leftCell != null && cell.GroupIndex != leftCell.GroupIndex) ||
                            (upperCell != null && cell.GroupIndex != upperCell.GroupIndex) ||
                            (upperCell == null && !cell.Connections[Up]))
                            SetMirroredTile(column, row, '.');
                    }
                    else
                    {
                        if ((leftCell != null && (!leftCell.Connections[Right] || GetTile(column - 1, row) == '.')) ||
                            (upperCell != null && (!upperCell.Connections[Down] || GetTile(column, row - 1) == '.')))
                            SetMirroredTile(column, row, '.');
                    }

                    if (GetTile(column - 1, row) == '.' && GetTile(column, row - 1) == '.' && GetTile(column - 1, row - 1) == '_')
                        SetMirroredTile(column, row, '.');
                }
            }

            for (var cell = Cells[CellColumnCount - 1]; cell != null; cell = cell.Neighbors[Down])
            {
                if (cell.TopTunnel)
                {
                    int row = cell.FinalRow + 1;
                    SetMirroredTile(HalfTileColumnCount - 1, row, '.');
                    SetMirroredTile(HalfTileColumnCount - 2, row, '.');
                }
            }

            for (int row = 0; row < TileRowCount; row++)
            {
                for (int column = 0; column < HalfTileColumnCount; column++)
                {
                    if (GetTile(column, row) != '.' &&
                        (GetTile(column - 1, row) == '.' || GetTile(column, row - 1) == '.' ||
                         GetTile(column + 1, row) == '.' || GetTile(column, row + 1) == '.' ||
                         GetTile(column - 1, row - 1) == '.' || GetTile(column + 1, row - 1) == '.' ||
                         GetTile(column + 1, row + 1) == '.' || GetTile(column - 1, row + 1) == '.'))
                        SetMirroredTile(column, row, '|');
                }
            }

            // Keep the ghost house at columns 9–18 and rows 11–17, matching the fallback layout.
            void SetTile(int column, int row, char tile) { tiles[column + row * FullColumnCount] = tile; }

            for (int column = 9; column <= 18; column++)
                SetTile(column, 11, '.');

            SetTile(9, 12, '.');
            SetTile(18, 12, '.');

            for (int column = 10; column <= 12; column++)
                SetTile(column, 12, '|');

            SetTile(13, 12, '.');
            SetTile(14, 12, '.');

            for (int column = 15; column <= 17; column++)
                SetTile(column, 12, '|');

            for (int row = 13; row <= 15; row++)
            {
                SetTile(9, row, '.');
                SetTile(10, row, '|');

                for (int column = 11; column <= 16; column++)
                    SetTile(column, row, '.');

                SetTile(17, row, '|');
                SetTile(18, row, '.');
            }

            SetTile(9, 16, '.');
            SetTile(18, 16, '.');

            for (int column = 10; column <= 17; column++)
                SetTile(column, 16, '|');

            for (int column = 9; column <= 18; column++)
                SetTile(column, 17, '.');

            var layout = new string[TileRowCount];

            for (int row = 0; row < TileRowCount; row++)
            {
                var rowTiles = new char[FullColumnCount];

                for (int column = 0; column < FullColumnCount; column++)
                {
                    char tile = tiles[column + row * FullColumnCount];
                    rowTiles[column] = (tile == '.') ? '0' : '1';
                }

                layout[row] = new string(rowTiles);
            }

            return layout;
        }
    }

    private static string[] FallbackLayout() =>
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
}
