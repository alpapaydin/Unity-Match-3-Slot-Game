using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the generation and validation of slot machine board configurations and valid stop positions.
/// Each column maintains a fixed sequence of tiles that repeats during spinning.
/// </summary>
public class BoardStateGenerator
{
    private readonly int gridSize;
    private readonly TileType[] tileTypes;
    private readonly int minTilesPerType;
    private readonly int columnLength;  // Length of each column's fixed sequence
    private readonly int maxAttempts = 1000;

    public TileType[][] ColumnConfigurations { get; private set; }  // Fixed configurations for each column
    private List<int[]> validStopOffsets;  // Precalculated valid stop positions

    /// <summary>
    /// Creates a new BoardStateGenerator with specified parameters.
    /// </summary>
    /// <param name="gridSize">Size of the grid (e.g., 5 for 5x5)</param>
    /// <param name="tileTypes">Array of available tile types</param>
    /// <param name="minTilesPerType">Minimum number of each tile type required on the board</param>
    /// <param name="columnLength">Length of the repeating sequence for each column</param>
    public BoardStateGenerator(int gridSize, TileType[] tileTypes, int minTilesPerType = 3, int columnLength = 30)
    {
        this.gridSize = gridSize;
        this.tileTypes = tileTypes;
        this.minTilesPerType = minTilesPerType;
        this.columnLength = columnLength;

        ValidateParameters();
        GenerateFixedColumnConfigurations();
        PrecalculateValidStopPositions();
    }

    /// <summary>
    /// Validates that the provided parameters can satisfy the game requirements.
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
    /// Generates the fixed configuration for each column. These configurations will not change
    /// during gameplay and form the basis for all possible board states.
    /// </summary>
    private void GenerateFixedColumnConfigurations()
    {
        ColumnConfigurations = new TileType[gridSize][];

        for (int col = 0; col < gridSize; col++)
        {
            bool validSequenceFound = false;
            TileType[] sequence = null;

            for (int attempt = 0; attempt < maxAttempts && !validSequenceFound; attempt++)
            {
                sequence = GenerateColumnSequence();
                validSequenceFound = ValidateColumnSequence(sequence);
            }

            if (!validSequenceFound || sequence == null)
            {
                throw new InvalidOperationException($"Failed to generate valid sequence for column {col}");
            }

            ColumnConfigurations[col] = sequence;
        }

        Debug.Log($"Generated {gridSize} column configurations of length {columnLength}");
    }

    /// <summary>
    /// Generates a single column sequence ensuring no three-in-a-row matches.
    /// </summary>
    private TileType[] GenerateColumnSequence()
    {
        var sequence = new TileType[columnLength];
        var tileCounts = new Dictionary<TileType, int>();
        foreach (var type in tileTypes)
        {
            tileCounts[type] = 0;
        }

        for (int i = 0; i < columnLength; i++)
        {
            var availableTypes = new List<TileType>();

            foreach (var type in tileTypes)
            {
                // Avoid three in a row in the sequence
                if (i >= 2 && sequence[i - 1] == type && sequence[i - 2] == type)
                    continue;

                availableTypes.Add(type);
            }

            // If we're below minimum counts, prioritize those tiles
            var priorityTypes = availableTypes
                .Where(t => tileCounts[t] < Mathf.CeilToInt((float)minTilesPerType / gridSize))
                .ToList();

            var selectedTypes = priorityTypes.Count > 0 ? priorityTypes : availableTypes;

            if (selectedTypes.Count == 0)
            {
                // If no valid types available, take any type
                selectedTypes = tileTypes.ToList();
            }

            TileType selectedType = selectedTypes[UnityEngine.Random.Range(0, selectedTypes.Count)];
            sequence[i] = selectedType;
            tileCounts[selectedType]++;
        }

        return sequence;
    }

    /// <summary>
    /// Validates that a column sequence meets all requirements.
    /// </summary>
    private bool ValidateColumnSequence(TileType[] sequence)
    {
        // Check for three in a row
        for (int i = 0; i < sequence.Length - 2; i++)
        {
            if (sequence[i] == sequence[i + 1] && sequence[i + 1] == sequence[i + 2])
                return false;
        }

        // Check for minimum tile counts
        var tileCounts = new Dictionary<TileType, int>();
        foreach (var type in tileTypes)
        {
            tileCounts[type] = 0;
        }

        foreach (var tile in sequence)
        {
            tileCounts[tile]++;
        }

        // Verify each tile type appears at least the minimum required times
        return tileCounts.All(kvp => kvp.Value >= minTilesPerType);
    }

    /// <summary>
    /// Precalculates valid stop positions that ensure no matches and proper tile distribution.
    /// </summary>
    private void PrecalculateValidStopPositions()
    {
        validStopOffsets = new List<int[]>();
        int desiredPositions = 20; // Number of valid stop positions we want

        for (int attempt = 0; attempt < maxAttempts && validStopOffsets.Count < desiredPositions; attempt++)
        {
            int[] offsets = new int[gridSize];
            for (int i = 0; i < gridSize; i++)
            {
                offsets[i] = UnityEngine.Random.Range(0, columnLength);
            }

            if (IsValidStopPosition(offsets))
            {
                validStopOffsets.Add(offsets);
            }
        }

        if (validStopOffsets.Count == 0)
        {
            Debug.LogError("Failed to find any valid stop positions!");
            // Add a fallback position
            validStopOffsets.Add(new int[gridSize]);
        }

        Debug.Log($"Precalculated {validStopOffsets.Count} valid stop positions");
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

        // Check for minimum tile counts
        Dictionary<TileType, int> tileCounts = new Dictionary<TileType, int>();
        foreach (var type in tileTypes)
        {
            tileCounts[type] = 0;
        }

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                TileType type = GetTileAtOffset(x, offsets[x], y);
                tileCounts[type]++;
            }
        }

        return tileCounts.All(kvp => kvp.Value >= minTilesPerType);
    }

    /// <summary>
    /// Gets a tile type at a specific position in a column's sequence.
    /// </summary>
    public TileType GetTileAtOffset(int column, int offset, int row)
    {
        int index = (offset + row) % columnLength;
        return ColumnConfigurations[column][index];
    }

    /// <summary>
    /// Returns a random valid stop position from the precalculated set.
    /// </summary>
    public int[] GetRandomValidStopOffset()
    {
        return validStopOffsets[UnityEngine.Random.Range(0, validStopOffsets.Count)].Clone() as int[];
    }

    /// <summary>
    /// Returns the fixed configuration for a specific column.
    /// </summary>
    public TileType[] GetColumnConfiguration(int column)
    {
        if (column < 0 || column >= gridSize)
            throw new ArgumentOutOfRangeException(nameof(column));

        return ColumnConfigurations[column];
    }

    /// <summary>
    /// Returns the complete board state that would result from a set of column offsets.
    /// </summary>
    public TileType[][] GetBoardStateFromOffsets(int[] offsets)
    {
        var state = new TileType[gridSize][];
        for (int x = 0; x < gridSize; x++)
        {
            state[x] = new TileType[gridSize];
            for (int y = 0; y < gridSize; y++)
            {
                state[x][y] = GetTileAtOffset(x, offsets[x], y);
            }
        }
        return state;
    }
}