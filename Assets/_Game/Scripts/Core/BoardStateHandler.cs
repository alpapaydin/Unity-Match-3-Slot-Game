public class BoardStateHandler
{
    private readonly int gridSize;
    private readonly DeterministicBoardStateGenerator boardGenerator;
    private int[] currentOffsets;
    private Tile[,] board;

    public BoardStateHandler(int gridSize, DeterministicBoardStateGenerator boardGenerator, Tile[,] board)
    {
        this.gridSize = gridSize;
        this.boardGenerator = boardGenerator;
        this.board = board;
        currentOffsets = new int[gridSize];
    }

    public void SyncBoardState(int[] stopOffsets)
    {
        // Store the new state
        currentOffsets = (int[])stopOffsets.Clone();

        // Update each tile on the board
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (board[x, y] == null) continue;

                // Calculate the configuration index for this position
                int configIndex = (stopOffsets[x] + y) % boardGenerator.ColumnConfigurations[x].Length;
                TileType newType = boardGenerator.GetTileAtOffset(x, stopOffsets[x], y);

                // Update the tile type
                board[x, y].SetTileType(newType);
            }
        }
    }

    public bool ValidateState(int[] offsets)
    {
        // Verify horizontal matches
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 2; x++)
            {
                TileType type1 = boardGenerator.GetTileAtOffset(x, offsets[x], y);
                TileType type2 = boardGenerator.GetTileAtOffset(x + 1, offsets[x + 1], y);
                TileType type3 = boardGenerator.GetTileAtOffset(x + 2, offsets[x + 2], y);

                if (type1 == type2 && type2 == type3)
                    return false;
            }
        }

        // Verify vertical matches
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 2; y++)
            {
                TileType type1 = boardGenerator.GetTileAtOffset(x, offsets[x], y);
                TileType type2 = boardGenerator.GetTileAtOffset(x, offsets[x], y + 1);
                TileType type3 = boardGenerator.GetTileAtOffset(x, offsets[x], y + 2);

                if (type1 == type2 && type2 == type3)
                    return false;
            }
        }

        return true;
    }

    public int[] GetCurrentOffsets()
    {
        return (int[])currentOffsets.Clone();
    }
}