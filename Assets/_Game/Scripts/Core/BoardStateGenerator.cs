using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Determines board configurations in a more deterministic manner,
/// ensuring valid configurations without repeated attempts.
/// </summary>
public class DeterministicBoardStateGenerator
{
    private readonly int gridSize;
    private readonly TileType[] tileTypes;
    private readonly int minTilesPerType;
    private readonly int columnLength;
    private readonly List<TileType[]> possibleBoards;
    private readonly List<int[]> possibleStopOffsets;
    private readonly System.Random random;

    public TileType[][] ColumnConfigurations { get; private set; }
    public List<int[]> ValidStopOffsets => possibleStopOffsets;

    /// <summary>
    /// Constructor for DeterministicBoardStateGenerator.
    /// </summary>
    public DeterministicBoardStateGenerator(int gridSize, TileType[] tileTypes, int minTilesPerType = 3, int columnLength = 30)
    {
        this.gridSize = gridSize;
        this.tileTypes = tileTypes;
        this.minTilesPerType = minTilesPerType;
        this.columnLength = columnLength;
        possibleBoards = new List<TileType[]>();
        possibleStopOffsets = new List<int[]>();
        random = new System.Random();

        ValidateParameters();
        GenerateFixedColumnConfigurations();
        PreconstructValidBoardConfigurations();
    }

    /// <summary>
    /// Validates parameters before proceeding with board generation.
    /// </summary>
    private void ValidateParameters()
    {
        if (gridSize <= 0)
            throw new ArgumentException("Grid size must be positive");

        if (tileTypes == null || tileTypes.Length == 0)
            throw new ArgumentException("Must provide at least one tile type");

        if (columnLength < gridSize * 2)
            throw new ArgumentException("Column length must be at least twice the grid size");

        int totalMinimumTiles = tileTypes.Length * minTilesPerType;
        int totalTiles = gridSize * gridSize;
        if (totalMinimumTiles > totalTiles)
        {
            throw new ArgumentException(
                $"Cannot guarantee {minTilesPerType} tiles of each type with grid size {gridSize}. " +
                $"Need {totalMinimumTiles} tiles but only have {totalTiles} spaces."
            );
        }
    }

    /// <summary>
    /// Generate a fixed column configuration using a deterministic pattern.
    /// </summary>
    private void GenerateFixedColumnConfigurations()
    {
        ColumnConfigurations = new TileType[gridSize][];
        for (int col = 0; col < gridSize; col++)
        {
            ColumnConfigurations[col] = GenerateDeterministicSequence(col);
        }
    }

    /// <summary>
    /// Generates a deterministic sequence for each column.
    /// Ensures no consecutive three matches in rows or columns.
    /// </summary>
    private TileType[] GenerateDeterministicSequence(int col)
    {
        TileType[] sequence = new TileType[columnLength];
        var tileCounts = new Dictionary<TileType, int>();
        foreach (var type in tileTypes)
        {
            tileCounts[type] = 0;
        }

        for (int i = 0; i < columnLength; i++)
        {
            var availableTypes = new List<TileType>(tileTypes);

            // Avoid three in a row within the column
            if (i >= 2 && sequence[i - 1] == sequence[i - 2])
            {
                availableTypes.Remove(sequence[i - 1]);
            }

            // Avoid creating a row match with neighboring columns
            if (col > 0 && i < gridSize)
            {
                TileType leftNeighbor = ColumnConfigurations[col - 1][i];
                if (i >= 2 && ColumnConfigurations[col - 1][i - 1] == leftNeighbor && sequence[i - 1] == leftNeighbor)
                {
                    availableTypes.Remove(leftNeighbor);
                }
            }

            // Avoid placing a tile type that exceeds the minimum count excessively
            TileType selectedType = availableTypes.OrderBy(t => tileCounts[t]).First();
            sequence[i] = selectedType;
            tileCounts[selectedType]++;
        }

        return sequence;
    }

    /// <summary>
    /// Preconstructs a list of valid board configurations that meet all requirements.
    /// </summary>
    private void PreconstructValidBoardConfigurations()
    {
        int maxBoards = 100; // Limit to 100 possible board states
        for (int i = 0; i < maxBoards; i++)
        {
            int[] offsets = new int[gridSize];
            for (int j = 0; j < gridSize; j++)
            {
                offsets[j] = random.Next(0, columnLength);
            }

            if (IsValidStopPosition(offsets))
            {
                possibleStopOffsets.Add(offsets);
            }
        }

        Debug.Log($"Preconstructed {possibleStopOffsets.Count} valid stop positions.");
    }

    /// <summary>
    /// Checks if a set of column offsets creates a valid board state.
    /// </summary>
    private bool IsValidStopPosition(int[] offsets)
    {
        // Check for horizontal matches
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 2; x++)
            {
                TileType type1 = GetTileAtOffset(x, offsets[x], y);
                TileType type2 = GetTileAtOffset(x + 1, offsets[x + 1], y);
                TileType type3 = GetTileAtOffset(x + 2, offsets[x + 2], y);

                if (type1 == type2 && type2 == type3)
                    return false;
            }
        }

        // Check for vertical matches
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 2; y++)
            {
                TileType type1 = GetTileAtOffset(x, offsets[x], y);
                TileType type2 = GetTileAtOffset(x, offsets[x], y + 1);
                TileType type3 = GetTileAtOffset(x, offsets[x], y + 2);

                if (type1 == type2 && type2 == type3)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a tile type at a specific position in a column's sequence.
    /// </summary>
    public TileType GetTileAtOffset(int column, int offset, int row)
    {
        int index = (offset + row) % columnLength;
        return ColumnConfigurations[column][index];
    }
}
