using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameBoard : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField] private TileType[] tileTypes;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private float padding = 0.1f;

    [Header("Spin Settings")]
    [SerializeField] private float maxSpinSpeed = 20f;
    [SerializeField] private float accelerationTime = 0.2f;
    [SerializeField] private float decelerationTime = 1.5f;
    [SerializeField] private float columnStartDelay = 0.1f;
    [SerializeField] private AnimationCurve spinSpeedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stop Settings")]
    [SerializeField] private float minSpinTimeBeforeStop = 1f; // Minimum spin time before allowing stop
    [SerializeField] private float columnStopInterval = 0.5f; // Time between each column stop
    [SerializeField] private int minSpinningColumns = 3; // Minimum columns that must keep spinning after stop is pressed

    [Header("Board Configuration")]
    [SerializeField] private bool randomizeSize = true;
    [SerializeField] private int defaultSize = 5;

    public event Action OnBoardInitialized;
    public event Action OnSpinComplete;

    private Dictionary<TileType, int> tileTypeCounts;
    private TileType[][] preconstructedColumns;
    private Tile[,] board;
    private int gridSize;
    private bool isInitialized = false;
    private float tileSize;
    private float tileSpacing;
    private bool isSpinning = false;
    private bool earlyStopRequested = false;
    private SpinningColumn[] spinningColumns;
    private int startupColumnsRemaining;
    private bool isStartingUp = false;
    private TileType[][] columnConfigurations;  // Stores the fixed tile configurations for each column
    private int extraTilesPerColumn;  // Number of extra tiles above the grid for spinning
    private float spinStartTime;
    private bool isStoppingSequence = false;
    private Coroutine stopSequenceCoroutine;
    private BoardStateGenerator boardGenerator;
    private int[] targetStopOffsets;
    private float[] currentPositions;  // Track continuous positions for smooth movement

    public int GridSize => gridSize;
    public bool IsInitialized => isInitialized;
    public bool IsSpinning => isSpinning;
    private class SpinningColumn
    {
        public SpinningTile[] tiles;
        public float currentSpeed;
        public float targetSpeed;
        public float spinProgress;
        public bool isSpinning;
        public bool isStoppingRequested;
        public float speedMultiplier = 1f;
        public bool hasReachedFullSpeed = false;
        public float currentSpacing = 0f;
        public float targetSpacing = 0f;
        public float spacingTransitionProgress = 0f;
        public bool isFinalizing = false;
        public float finalizingProgress = 0f;
        public TileType[] columnConfiguration;
        public float perspectiveStrength = 0.3f;    // How strong the perspective effect is
        public float fadeStrength = 0.3f;           // How much tiles fade at edges
        public float appearanceProgress = 0f;        // Progress of appearance animation
        public bool isAppearing = true;             // Whether tiles are still appearing
        public int lastUsedConfigIndex; // Added to track the last used configuration index
        public SpinningTile[] extraTiles;           // Separate array for extra tiles that appear
    }

    // Update SpinningTile class
    private class SpinningTile
    {
        public GameObject visualObject;
        public TileType tileType;
        public float currentPosition;
        public float targetPosition;
        public float currentSpacing = 0f;
        public int configIndex; // Added to track position in configuration
        public float visualScale = 1f;
        public float alpha = 1f;
        public bool isGridTile = false;  // Added to track if this is a main grid tile
    }

    private void Start()
    {
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        gridSize = randomizeSize ? (UnityEngine.Random.Range(0, 2) == 0 ? 5 : 7) : defaultSize;
        CalculateOptimalTileSize();
        board = new Tile[gridSize, gridSize];

        // Initialize board generator and configurations
        boardGenerator = new BoardStateGenerator(gridSize, tileTypes, 3);
        currentPositions = new float[gridSize];
        columnConfigurations = new TileType[gridSize][];

        for (int i = 0; i < gridSize; i++)
        {
            columnConfigurations[i] = boardGenerator.GetColumnConfiguration(i);
        }

        // Get initial stop position
        targetStopOffsets = boardGenerator.GetRandomValidStopOffset();
        for (int i = 0; i < gridSize; i++)
        {
            currentPositions[i] = targetStopOffsets[i];
        }

        // Initialize board with initial state
        var initialState = boardGenerator.GetBoardStateFromOffsets(targetStopOffsets);

        float boardWidth = gridSize * tileSize + (gridSize - 1) * tileSpacing;
        float boardHeight = boardWidth;

        Vector3 startPos = transform.position - new Vector3(
            boardWidth / 2f - tileSize / 2f,
            boardHeight / 2f - tileSize / 2f,
            0
        );

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                CreateTileAt(x, y, startPos, initialState[x][y]);
            }
        }

        isInitialized = true;
        OnBoardInitialized?.Invoke();
    }


    private void CreateTileAt(int x, int y, Vector3 startPos, TileType tileType)
    {
        Vector3 tilePosition = startPos + new Vector3(
            x * (tileSize + tileSpacing),
            y * (tileSize + tileSpacing),
            0
        );

        GameObject tileObj = Instantiate(tilePrefab, tilePosition, Quaternion.identity, transform);
        tileObj.name = $"Tile ({x}, {y})";

        float spriteSize = tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
        float scaleFactor = tileSize / spriteSize;
        tileObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

        Tile tile = tileObj.GetComponent<Tile>();
        tile.gridPosition = new Vector2Int(x, y);
        board[x, y] = tile;

        tile.SetTileType(tileType);
    }

    private void AlignColumnForStop(int column, SpinningColumn spinningColumn)
    {
        float spacing = tileSize + tileSpacing;

        // Find the closest tile that's part of the actual grid
        var gridTile = spinningColumn.tiles.FirstOrDefault(t => t != null && t.isGridTile);
        if (gridTile == null) return;

        // Calculate how far off the grid this tile is
        float currentGridPosition = gridTile.currentPosition;
        float targetGridPosition = gridTile.configIndex * spacing;
        float offset = currentGridPosition - targetGridPosition;

        // Apply offset to all tiles to align with grid
        foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
        {
            if (tile == null) continue;
            tile.currentPosition -= offset;
        }
    }

    private SpinningTile GetClosestTileToPosition(SpinningColumn column, float targetPosition)
    {
        if (column == null || column.tiles == null) return null;

        // First try to find a grid tile at this position
        var gridTile = column.tiles
            .Where(t => t != null && t.isGridTile)
            .OrderBy(t => Mathf.Abs(t.currentPosition - targetPosition))
            .FirstOrDefault();

        if (gridTile != null && Mathf.Abs(gridTile.currentPosition - targetPosition) < (tileSize + tileSpacing) * 0.5f)
        {
            return gridTile;
        }

        // If no grid tile found, look through all tiles
        return column.tiles
            .Concat(column.extraTiles)
            .Where(t => t != null)
            .OrderBy(t => Mathf.Abs(t.currentPosition - targetPosition))
            .FirstOrDefault();
    }

    private SpinningColumn InitializeSpinningColumn(int column)
    {
        SpinningColumn spinningColumn = new SpinningColumn
        {
            tiles = new SpinningTile[gridSize],
            extraTiles = new SpinningTile[gridSize * 2],
            currentSpeed = 0f,
            targetSpeed = maxSpinSpeed,
            isSpinning = true,
            isStoppingRequested = false,
            hasReachedFullSpeed = false,
            lastUsedConfigIndex = (int)currentPositions[column],  // Use currentPositions instead of targetStopPosition
            isAppearing = true
        };

        // Use the fixed column configuration
        spinningColumn.columnConfiguration = columnConfigurations[column];

        Vector3 basePosition = GetTileBasePosition(new Vector2Int(column, 0));
        float spacing = tileSize + tileSpacing;

        // Initialize main visible tiles
        for (int i = 0; i < gridSize; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, transform);
            tileObj.name = $"SpinningTile_{column}_{i}";

            SpriteRenderer spriteRenderer = tileObj.GetComponent<SpriteRenderer>();
            float spriteSize = tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
            float scaleFactor = tileSize / spriteSize;
            tileObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

            float yOffset = i * spacing;
            Vector3 position = basePosition + new Vector3(0, yOffset, 0);
            tileObj.transform.position = position;

            int configIndex = ((int)currentPositions[column] + i) % spinningColumn.columnConfiguration.Length;

            spinningColumn.tiles[i] = new SpinningTile
            {
                visualObject = tileObj,
                tileType = spinningColumn.columnConfiguration[configIndex],
                currentPosition = yOffset,
                configIndex = configIndex,
                visualScale = 1f,
                alpha = 1f,
                isGridTile = true
            };

            if (spriteRenderer != null && spinningColumn.tiles[i].tileType != null)
            {
                spriteRenderer.sprite = spinningColumn.tiles[i].tileType.sprite;
            }
        }

        // Initialize extra tiles
        for (int i = 0; i < spinningColumn.extraTiles.Length; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, transform);
            tileObj.name = $"ExtraTile_{column}_{i}";

            SpriteRenderer spriteRenderer = tileObj.GetComponent<SpriteRenderer>();
            float spriteSize = tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
            float scaleFactor = tileSize / spriteSize;

            float yOffset = (i < spinningColumn.extraTiles.Length / 2) ?
                -spacing * (i + 1) :  // Below board
                spacing * (gridSize + (i - spinningColumn.extraTiles.Length / 2));  // Above board

            int baseIndex = (int)currentPositions[column];
            int configIndex;
            if (i < spinningColumn.extraTiles.Length / 2)
            {
                configIndex = (baseIndex - (i + 1) + spinningColumn.columnConfiguration.Length)
                    % spinningColumn.columnConfiguration.Length;
            }
            else
            {
                configIndex = (baseIndex + gridSize + (i - spinningColumn.extraTiles.Length / 2))
                    % spinningColumn.columnConfiguration.Length;
            }

            spinningColumn.extraTiles[i] = new SpinningTile
            {
                visualObject = tileObj,
                tileType = spinningColumn.columnConfiguration[configIndex],
                currentPosition = yOffset,
                configIndex = configIndex,
                visualScale = 0.7f,
                alpha = 0f,
                isGridTile = false
            };

            tileObj.transform.position = basePosition + new Vector3(0, yOffset, 0);
            tileObj.transform.localScale = new Vector3(scaleFactor * 0.7f, scaleFactor * 0.7f, 1f);

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = spinningColumn.extraTiles[i].tileType.sprite;
                spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            }
        }
        return spinningColumn;
    }


    private void UpdateSpinningColumn(int column, SpinningColumn spinningColumn)
    {
        if (spinningColumn == null) return;

        float deltaMovement = spinningColumn.currentSpeed * Time.deltaTime;
        float spacing = tileSize + tileSpacing;
        Vector3 basePosition = GetTileBasePosition(new Vector2Int(column, 0));

        // Update all tiles
        SpinningTile[] allTiles = spinningColumn.tiles.Concat(spinningColumn.extraTiles).ToArray();
        foreach (var tile in allTiles)
        {
            if (tile == null || tile.visualObject == null) continue;

            // Update position
            tile.currentPosition -= deltaMovement;

            // Handle wrapping
            float wrapThreshold = -spacing * 1.5f;
            if (tile.currentPosition < wrapThreshold)
            {
                float totalHeight = spacing * (spinningColumn.tiles.Length + spinningColumn.extraTiles.Length);
                tile.currentPosition += totalHeight;

                if (tile.isGridTile)
                {
                    // For grid tiles, maintain their original configuration
                    spinningColumn.lastUsedConfigIndex = (spinningColumn.lastUsedConfigIndex + 1) % gridSize;
                    tile.tileType = spinningColumn.columnConfiguration[spinningColumn.lastUsedConfigIndex];
                    tile.configIndex = spinningColumn.lastUsedConfigIndex;
                }

                var wrapSpriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                if (wrapSpriteRenderer != null && tile.tileType != null && tile.tileType.sprite != null)
                {
                    wrapSpriteRenderer.sprite = tile.tileType.sprite;
                }
            }

            // Calculate and apply perspective effect
            float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * spacing / 2f));
            float maxDistance = gridSize * spacing;
            float perspectiveScale = Mathf.Lerp(1f, 0.7f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
            float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);

            // Apply visual effects
            tile.visualScale = perspectiveScale;
            tile.alpha = fadeAlpha;

            // Update visual properties
            tile.visualObject.transform.position = basePosition + new Vector3(0, tile.currentPosition, 0);

            var visualSpriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
            if (visualSpriteRenderer != null)
            {
                visualSpriteRenderer.color = new Color(1f, 1f, 1f, tile.alpha);
                float baseScale = tileSize / tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
                tile.visualObject.transform.localScale = new Vector3(
                    baseScale * tile.visualScale,
                    baseScale * tile.visualScale,
                    1f
                );
            }
        }
    }
    private void CalculateOptimalTileSize()
    {
        float cameraHeight = Camera.main.orthographicSize * 2f;
        float cameraWidth = cameraHeight * Camera.main.aspect;
        float availableHeight = cameraHeight * (1f - padding * 2f);
        float availableWidth = cameraWidth * (1f - padding * 2f);
        float availableSpace = Mathf.Min(availableWidth, availableHeight);
        tileSize = availableSpace / (gridSize + 0.1f * (gridSize - 1));
        tileSpacing = tileSize * 0.1f;
    }
    public void StartSpin()
    {
        if (isSpinning || spinningColumns != null)
        {
            return;
        }

        // Get new random valid stop position from the board generator
        targetStopOffsets = boardGenerator.GetRandomValidStopOffset();

        isSpinning = true;
        isStartingUp = true;
        isStoppingSequence = false;
        startupColumnsRemaining = gridSize;
        spinStartTime = Time.time;
        stopSequenceCoroutine = null;
        earlyStopRequested = false;

        StartCoroutine(SpinSequence());
    }
    public void StopSpin()
    {
        if (!isSpinning) return;

        // If we're still starting up, store the stop request
        if (isStartingUp)
        {
            earlyStopRequested = true;
            return;
        }

        // Don't allow stopping if minimum spin time hasn't elapsed
        if (Time.time - spinStartTime < minSpinTimeBeforeStop)
            return;

        // Start the sequential stopping process if we're not already stopping
        if (!isStoppingSequence && stopSequenceCoroutine == null)
        {
            stopSequenceCoroutine = StartCoroutine(SequentialColumnStop());
        }
    }

    private IEnumerator SequentialColumnStop()
    {
        if (isStoppingSequence) yield break;
        isStoppingSequence = true;

        // Wait for all columns to reach full speed if they haven't already
        while (!AreAllColumnsAtFullSpeed())
        {
            yield return null;
        }

        // Calculate how many columns should keep spinning
        int columnsToKeepSpinning = Mathf.Min(minSpinningColumns, gridSize - 1);

        // Stop columns one by one with randomized delay
        for (int i = 0; i < gridSize - columnsToKeepSpinning; i++)
        {
            float randomizedInterval = columnStopInterval * UnityEngine.Random.Range(0.8f, 1.2f);
            yield return new WaitForSeconds(randomizedInterval);

            spinningColumns[i].isStoppingRequested = true;
            spinningColumns[i].targetSpacing = 0f;
            spinningColumns[i].speedMultiplier = UnityEngine.Random.Range(0.9f, 1.1f);

            while (spinningColumns[i].isSpinning)
            {
                yield return null;
            }
        }

        // Stop remaining columns
        for (int i = gridSize - columnsToKeepSpinning; i < gridSize; i++)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.2f));

            spinningColumns[i].isStoppingRequested = true;
            spinningColumns[i].targetSpacing = 0f;
            spinningColumns[i].speedMultiplier = UnityEngine.Random.Range(0.9f, 1.1f);
        }

        stopSequenceCoroutine = null;
        isStoppingSequence = false;
    }

    private bool AreAllColumnsAtFullSpeed()
    {
        if (spinningColumns == null) return false;

        for (int i = 0; i < gridSize; i++)
        {
            if (spinningColumns[i] == null || !spinningColumns[i].hasReachedFullSpeed)
            {
                return false;
            }
        }
        return true;
    }

    private IEnumerator SpinSequence()
    {
        spinningColumns = new SpinningColumn[gridSize];

        // Initialize all columns
        for (int x = 0; x < gridSize; x++)
        {
            spinningColumns[x] = InitializeSpinningColumn(x);

            // Hide original board tiles
            for (int y = 0; y < gridSize; y++)
            {
                if (board[x, y] != null)
                {
                    board[x, y].gameObject.SetActive(false);
                }
            }
        }

        // Animate extra tiles appearing
        for (int x = 0; x < gridSize; x++)
        {
            StartCoroutine(AnimateExtraTilesAppearance(x));
            yield return new WaitForSeconds(columnStartDelay * 0.5f);
        }

        // Wait for appearance animations to complete
        while (AnyColumnsAppearing())
        {
            yield return null;
        }

        // Start spinning columns
        for (int x = 0; x < gridSize; x++)
        {
            StartCoroutine(SpinColumn(x));
            yield return new WaitForSeconds(columnStartDelay);
        }

        // Wait for full speed
        while (!AreAllColumnsAtFullSpeed())
        {
            yield return null;
        }

        isStartingUp = false;
        if (earlyStopRequested)
        {
            StopSpin();
        }
    }

    private bool AnyColumnsAppearing()
    {
        if (spinningColumns == null) return false;
        return spinningColumns.Any(col => col != null && col.isAppearing);
    }

    private IEnumerator AnimateExtraTilesAppearance(int column)
    {
        SpinningColumn spinningColumn = spinningColumns[column];
        float appearanceDuration = 0.5f;
        float timer = 0f;

        while (timer < appearanceDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / appearanceDuration;
            spinningColumn.appearanceProgress = Mathf.SmoothStep(0f, 1f, progress);

            // Animate extra tiles
            foreach (var tile in spinningColumn.extraTiles)
            {
                if (tile == null || tile.visualObject == null) continue;

                // Calculate perspective effect
                float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * (tileSize + tileSpacing) / 2f));
                float maxDistance = gridSize * (tileSize + tileSpacing);
                float perspectiveScale = Mathf.Lerp(1f, 0.7f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
                float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);

                // Apply appearance animation
                tile.visualScale = Mathf.Lerp(0.7f, perspectiveScale, spinningColumn.appearanceProgress);
                tile.alpha = Mathf.Lerp(0f, fadeAlpha, spinningColumn.appearanceProgress);

                // Update visual properties
                SpriteRenderer spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = new Color(1f, 1f, 1f, tile.alpha);
                    float baseScale = tileSize / tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
                    tile.visualObject.transform.localScale = new Vector3(
                        baseScale * tile.visualScale,
                        baseScale * tile.visualScale,
                        1f
                    );
                }
            }

            yield return null;
        }

        spinningColumn.isAppearing = false;
    }

    private IEnumerator SpinColumn(int column)
    {
        SpinningColumn spinningColumn = spinningColumns[column];
        float accelerationTimer = 0f;

        // Acceleration
        while (accelerationTimer < accelerationTime)
        {
            accelerationTimer += Time.deltaTime;
            float progress = accelerationTimer / accelerationTime;
            spinningColumn.currentSpeed = maxSpinSpeed * spinSpeedCurve.Evaluate(progress);
            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }

        spinningColumn.hasReachedFullSpeed = true;
        startupColumnsRemaining--;

        // Main spinning loop
        while (!spinningColumn.isStoppingRequested)
        {
            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }

        // Find the closest position for a clean stop
        AlignColumnForStop(column, spinningColumn);

        // Deceleration
        float decelerationTimer = decelerationTime;
        float initialSpeed = spinningColumn.currentSpeed;

        while (decelerationTimer > 0)
        {
            decelerationTimer -= Time.deltaTime;
            float progress = 1f - (decelerationTimer / decelerationTime);
            float speedProgress = 1f - Mathf.SmoothStep(0f, 1f, progress);
            spinningColumn.currentSpeed = initialSpeed * speedProgress;
            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }

        spinningColumn.currentSpeed = 0;

        // Smooth fade out transition
        yield return StartCoroutine(FadeOutExtraTiles(column, spinningColumn));

        // Final cleanup
        FinalizeColumn(column);

        // Check if all columns have stopped
        bool allStopped = true;
        for (int x = 0; x < gridSize; x++)
        {
            if (spinningColumns[x] != null && spinningColumns[x].isSpinning)
            {
                allStopped = false;
                break;
            }
        }

        if (allStopped)
        {
            isSpinning = false;
            spinningColumns = null;
            OnSpinComplete?.Invoke();
        }
    }

    private IEnumerator FadeOutExtraTiles(int column, SpinningColumn spinningColumn)
    {
        float fadeOutDuration = 0.3f;
        float timer = 0f;

        // Store initial alpha values
        Dictionary<SpinningTile, float> initialAlphas = new Dictionary<SpinningTile, float>();
        foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
        {
            if (tile != null && tile.visualObject != null)
            {
                initialAlphas[tile] = tile.alpha;
            }
        }

        // Calculate which tiles should remain visible (the main grid)
        HashSet<SpinningTile> gridTiles = new HashSet<SpinningTile>();
        float spacing = tileSize + tileSpacing;
        for (int i = 0; i < gridSize; i++)
        {
            float targetY = i * spacing;
            var closestTile = GetClosestTileToPosition(spinningColumn, targetY);
            if (closestTile != null)
            {
                gridTiles.Add(closestTile);
            }
        }

        // Fade out animation
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeOutDuration;
            float fadeProgress = Mathf.SmoothStep(0f, 1f, progress);

            foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
            {
                if (tile == null || tile.visualObject == null) continue;

                // Keep grid tiles visible, fade out others
                float targetAlpha = gridTiles.Contains(tile) ? 1f : 0f;
                tile.alpha = Mathf.Lerp(initialAlphas[tile], targetAlpha, fadeProgress);

                var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = new Color(1f, 1f, 1f, tile.alpha);
                }
            }

            yield return null;
        }
    }

    private void FinalizeColumn(int column)
    {
        SpinningColumn spinningColumn = spinningColumns[column];
        if (spinningColumn == null) return;

        float spacing = tileSize + tileSpacing;
        Vector3 basePosition = GetTileBasePosition(new Vector2Int(column, 0));

        // Ensure we're using the target stop position
        currentPositions[column] = targetStopOffsets[column];

        // Get the resulting board state from the target stop offsets
        var resultingBoard = boardGenerator.GetBoardStateFromOffsets(targetStopOffsets);

        // Update board tiles
        for (int i = 0; i < gridSize; i++)
        {
            if (board[column, i] != null)
            {
                board[column, i].gameObject.SetActive(true);
                board[column, i].SetTileType(resultingBoard[column][i]);
                board[column, i].transform.position = basePosition + new Vector3(0, i * spacing, 0);
            }
        }

        // Clean up spinning tiles
        foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
        {
            if (tile?.visualObject != null)
            {
                Destroy(tile.visualObject);
            }
        }

        spinningColumn.isSpinning = false;
    }

    private Vector3 GetTileBasePosition(Vector2Int gridPos)
    {
        float boardWidth = gridSize * tileSize + (gridSize - 1) * tileSpacing;
        float boardHeight = boardWidth;

        Vector3 startPos = transform.position - new Vector3(
            boardWidth / 2f - tileSize / 2f,
            boardHeight / 2f - tileSize / 2f,
            0
        );

        return startPos + new Vector3(
            gridPos.x * (tileSize + tileSpacing),
            gridPos.y * (tileSize + tileSpacing),
            0
        );
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        float boardWidth = gridSize * tileSize + (gridSize - 1) * tileSpacing;
        float boardHeight = boardWidth;

        Vector3 center = transform.position;
        Vector3 size = new Vector3(boardWidth, boardHeight, 0.1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}