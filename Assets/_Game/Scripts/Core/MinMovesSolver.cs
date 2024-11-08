using System;
using System.Collections.Generic;
using UnityEngine;

public class MinMovesSolver
{
    private class BoardState
    {
        public int[,] Grid { get; private set; }
        public int Moves { get; private set; }
        public List<(Vector2Int, Vector2Int)> MovesHistory { get; private set; }

        public BoardState(int size)
        {
            Grid = new int[size, size];
            Moves = 0;
            MovesHistory = new List<(Vector2Int, Vector2Int)>();
        }

        public BoardState(BoardState other)
        {
            Grid = new int[other.Grid.GetLength(0), other.Grid.GetLength(1)];
            Array.Copy(other.Grid, Grid, other.Grid.Length);
            Moves = other.Moves;
            MovesHistory = new List<(Vector2Int, Vector2Int)>(other.MovesHistory);
        }

        public void SwapTiles(Vector2Int pos1, Vector2Int pos2)
        {
            int temp = Grid[pos1.x, pos1.y];
            Grid[pos1.x, pos1.y] = Grid[pos2.x, pos2.y];
            Grid[pos2.x, pos2.y] = temp;
            Moves++;
            MovesHistory.Add((pos1, pos2));
        }

        public string GetHash()
        {
            var hash = new System.Text.StringBuilder();
            int size = Grid.GetLength(0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    hash.Append(Grid[x, y]).Append(',');
                }
            }
            return hash.ToString();
        }
    }

    public static int FindMinimumMovesToMatch(GameBoard gameBoard)
    {
        try
        {
            int gridSize = gameBoard.GridSize;
            var initialState = new BoardState(gridSize);
            Dictionary<TileType, int> tileTypeToInt = new Dictionary<TileType, int>();
            int nextTypeId = 0;
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Tile tile = gameBoard.GetTileAt(x, y);
                    if (tile != null)
                    {
                        TileType tileType = tile.GetCurrentType();
                        if (!tileTypeToInt.ContainsKey(tileType))
                        {
                            tileTypeToInt[tileType] = nextTypeId++;
                        }
                        initialState.Grid[x, y] = tileTypeToInt[tileType];
                    }
                }
            }
            return BreadthFirstSearchSolution(initialState);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in FindMinimumMovesToMatch: {e}");
            return -1;
        }
    }

    private static int BreadthFirstSearchSolution(BoardState initialState)
    {
        var queue = new Queue<BoardState>();
        var visited = new HashSet<string>();
        queue.Enqueue(initialState);
        visited.Add(initialState.GetHash());
        int iterationCount = 0;
        int maxIterations = 10000;
        while (queue.Count > 0 && iterationCount++ < maxIterations)
        {
            BoardState currentState = queue.Dequeue();
            if (HasMatch(currentState.Grid))
            {
                Debug.Log($"Solution found in {currentState.Moves} moves!");
                return currentState.Moves;
            }
            foreach (var move in GetPossibleMoves(currentState.Grid.GetLength(0)))
            {
                BoardState newState = new BoardState(currentState);
                newState.SwapTiles(move.Item1, move.Item2);

                string newHash = newState.GetHash();
                if (!visited.Contains(newHash))
                {
                    queue.Enqueue(newState);
                    visited.Add(newHash);
                }
            }
        }
        Debug.LogWarning($"No solution found after checking {iterationCount} states.");
        return -1;
    }

    private static List<(Vector2Int, Vector2Int)> GetPossibleMoves(int gridSize)
    {
        var moves = new List<(Vector2Int, Vector2Int)>();
        // Horizontal swaps
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 1; x++)
            {
                moves.Add((new Vector2Int(x, y), new Vector2Int(x + 1, y)));
            }
        }
        // Vertical swaps
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 1; y++)
            {
                moves.Add((new Vector2Int(x, y), new Vector2Int(x, y + 1)));
            }
        }
        return moves;
    }

    private static bool HasMatch(int[,] grid)
    {
        int size = grid.GetLength(0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size - 2; x++)
            {
                if (grid[x, y] == grid[x + 1, y] &&
                    grid[x + 1, y] == grid[x + 2, y])
                {
                    return true;
                }
            }
        }
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size - 2; y++)
            {
                if (grid[x, y] == grid[x, y + 1] &&
                    grid[x, y + 1] == grid[x, y + 2])
                {
                    return true;
                }
            }
        }
        return false;
    }
}