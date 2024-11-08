using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public DeterministicBoardStateGenerator(int gridSize, TileType[] tileTypes, int minTilesPerType = 3, int columnLength = 32)
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

    private void GenerateFixedColumnConfigurations()
    {
        ColumnConfigurations = new TileType[gridSize][];
        for (int col = 0; col < gridSize; col++)
        {
            ColumnConfigurations[col] = GenerateDeterministicSequence(col);
        }
    }

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
            if (i >= 2 && sequence[i - 1] == sequence[i - 2])
            {
                availableTypes.Remove(sequence[i - 1]);
            }
            if (col > 0 && i < gridSize)
            {
                TileType leftNeighbor = ColumnConfigurations[col - 1][i];
                if (i >= 2 && ColumnConfigurations[col - 1][i - 1] == leftNeighbor && sequence[i - 1] == leftNeighbor)
                {
                    availableTypes.Remove(leftNeighbor);
                }
            }
            TileType selectedType = availableTypes.OrderBy(t => tileCounts[t]).First();
            sequence[i] = selectedType;
            tileCounts[selectedType]++;
        }
        return sequence;
    }

    private void PreconstructValidBoardConfigurations()
    {
        int maxAttempts = 1000;
        int attempts = 0;

        while (attempts < maxAttempts && possibleStopOffsets.Count < 100)
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
            attempts++;
        }

        if (possibleStopOffsets.Count == 0)
        {
            Debug.LogWarning("Could not generate any valid board configurations. Consider relaxing constraints.");
        }
        else
        {
            Debug.Log($"Preconstructed {possibleStopOffsets.Count} valid stop positions after {attempts} attempts.");
        }
    }

    private bool IsValidStopPosition(int[] offsets)
    {
        if (!IsValidMatchPattern(offsets))
            return false;
        return HasMinimumTilesPerType(offsets);
    }

    private bool IsValidMatchPattern(int[] offsets)
    {
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

    private bool HasMinimumTilesPerType(int[] offsets)
    {
        var tileCounts = new Dictionary<TileType, int>();
        foreach (var type in tileTypes)
        {
            tileCounts[type] = 0;
        }
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                TileType tileType = GetTileAtOffset(x, offsets[x], y);
                tileCounts[tileType]++;
            }
        }
        return tileCounts.All(kvp => kvp.Value >= minTilesPerType);
    }

    public TileType GetTileAtOffset(int column, int offset, int row)
    {
        int index = (offset + row) % columnLength;
        return ColumnConfigurations[column][index];
    }
}