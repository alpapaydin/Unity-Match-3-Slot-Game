using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MinMovesSolver
{
    private struct BoardState
    {
        public TileType[,] Grid { get; set; }
        public int Moves { get; set; }
        public Vector2Int LastSwap1 { get; set; }
        public Vector2Int LastSwap2 { get; set; }
    }

    private struct SwapMove
    {
        public Vector2Int Pos1;
        public Vector2Int Pos2;

        public SwapMove(Vector2Int pos1, Vector2Int pos2)
        {
            Pos1 = pos1;
            Pos2 = pos2;
        }
    }

    public static int FindMinimumMovesToMatch(GameBoard gameBoard)
    {
        int gridSize = gameBoard.GridSize;
        var initialState = new BoardState
        {
            Grid = new TileType[gridSize, gridSize],
            Moves = 0,
            LastSwap1 = new Vector2Int(-1, -1),
            LastSwap2 = new Vector2Int(-1, -1)
        };

        // Create initial grid state
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Tile tile = gameBoard.GetTileAt(x, y);
                if (tile != null)
                {
                    initialState.Grid[x, y] = tile.GetCurrentType();
                }
            }
        }

        // Use BFS to find minimum moves
        return BreadthFirstSearchSolution(initialState, gridSize);
    }

    private static int BreadthFirstSearchSolution(BoardState initialState, int gridSize)
    {
        var queue = new Queue<BoardState>();
        var visited = new HashSet<string>();
        queue.Enqueue(initialState);
        visited.Add(GetBoardHash(initialState.Grid));

        while (queue.Count > 0)
        {
            BoardState currentState = queue.Dequeue();

            // Check if current state has a match
            if (HasMatch(currentState.Grid, gridSize))
            {
                return currentState.Moves;
            }

            // Generate all possible moves from current state
            foreach (var move in GetPossibleMoves(gridSize))
            {
                // Skip if this would undo the last move
                if (move.Pos1 == currentState.LastSwap2 && move.Pos2 == currentState.LastSwap1)
                    continue;

                BoardState newState = new BoardState
                {
                    Grid = SwapTiles(currentState.Grid, move.Pos1, move.Pos2),
                    Moves = currentState.Moves + 1,
                    LastSwap1 = move.Pos1,
                    LastSwap2 = move.Pos2
                };

                string boardHash = GetBoardHash(newState.Grid);
                if (!visited.Contains(boardHash))
                {
                    queue.Enqueue(newState);
                    visited.Add(boardHash);
                }
            }
        }

        return -1; // No solution found
    }

    private static List<SwapMove> GetPossibleMoves(int gridSize)
    {
        var moves = new List<SwapMove>();

        // Horizontal swaps
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 1; x++)
            {
                moves.Add(new SwapMove(
                    new Vector2Int(x, y),
                    new Vector2Int(x + 1, y)
                ));
            }
        }

        // Vertical swaps
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 1; y++)
            {
                moves.Add(new SwapMove(
                    new Vector2Int(x, y),
                    new Vector2Int(x, y + 1)
                ));
            }
        }

        return moves;
    }

    private static TileType[,] SwapTiles(TileType[,] grid, Vector2Int pos1, Vector2Int pos2)
    {
        int size = grid.GetLength(0);
        TileType[,] newGrid = new TileType[size, size];
        Array.Copy(grid, newGrid, grid.Length);

        TileType temp = newGrid[pos1.x, pos1.y];
        newGrid[pos1.x, pos1.y] = newGrid[pos2.x, pos2.y];
        newGrid[pos2.x, pos2.y] = temp;

        return newGrid;
    }

    private static bool HasMatch(TileType[,] grid, int gridSize)
    {
        // Check horizontal matches
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 2; x++)
            {
                if (grid[x, y] == grid[x + 1, y] &&
                    grid[x + 1, y] == grid[x + 2, y])
                {
                    return true;
                }
            }
        }

        // Check vertical matches
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 2; y++)
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

    private static string GetBoardHash(TileType[,] grid)
    {
        int size = grid.GetLength(0);
        var hash = new System.Text.StringBuilder();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                hash.Append(grid[x, y].ToString());
                hash.Append(",");
            }
        }

        return hash.ToString();
    }
}