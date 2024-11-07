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

    [Header("Board Configuration")]
    [SerializeField] private bool randomizeSize = true;
    [SerializeField] private int defaultSize = 5;

    public event Action OnBoardInitialized;
    public event Action OnSpinComplete;

    private BoardStateHandler boardStateHandler;
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
    private DeterministicBoardStateGenerator boardGenerator;
    private int[] currentStopOffsets;
    private float[] currentPositions;
    private Dictionary<int, TileType[]> targetPatterns;
    private ColumnState[] columnStates;
    private bool isFirstInitialization = true;

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
    private class ColumnState
    {
        public TileType[] configuration;  // Fixed tile sequence for this column
        public List<int> validStopOffsets;  // List of valid stopping positions
    }

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

        // Initialize board generator
        boardGenerator = new DeterministicBoardStateGenerator(gridSize, tileTypes, 3);
        columnStates = new ColumnState[gridSize];
        currentStopOffsets = new int[gridSize];
        currentPositions = new float[gridSize];

        // Initialize column states
        for (int i = 0; i < gridSize; i++)
        {
            columnStates[i] = new ColumnState
            {
                configuration = boardGenerator.ColumnConfigurations[i],
                validStopOffsets = new List<int>(boardGenerator.ValidStopOffsets.Select(offset => offset[i]))
            };
        }

        PrecalculateValidStopPositions();

        if (isFirstInitialization)
        {
            // Only randomize positions on first initialization
            currentStopOffsets = GetRandomValidStopPosition();
            for (int i = 0; i < gridSize; i++)
            {
                currentPositions[i] = currentStopOffsets[i];
            }
            isFirstInitialization = false;
        }

        CreateInitialBoard();

        isInitialized = true;
        OnBoardInitialized?.Invoke();
    }

    private int[] GetRandomValidStopPosition()
    {
        // Fetch a random valid stop offset from the preconstructed list
        if (boardGenerator.ValidStopOffsets.Count == 0)
        {
            Debug.LogWarning("No valid stop positions available, using fallback.");
            return new int[gridSize]; // Fallback to zero offsets if none available
        }
        int[] offsets = boardGenerator.ValidStopOffsets[UnityEngine.Random.Range(0, boardGenerator.ValidStopOffsets.Count)];
        Debug.Log("Offsets: " + string.Join(", ", offsets));

        return boardGenerator.ValidStopOffsets[UnityEngine.Random.Range(0, boardGenerator.ValidStopOffsets.Count)];
    }

    private void CreateInitialBoard()
    {
        float boardWidth = gridSize * tileSize + (gridSize - 1) * tileSpacing;
        float boardHeight = boardWidth;

        Vector3 startPos = transform.position - new Vector3(
            boardWidth / 2f - tileSize / 2f,
            boardHeight / 2f - tileSize / 2f,
            0
        );

        // Create tiles for initial board state
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // Calculate tile position
                Vector3 tilePosition = startPos + new Vector3(
                    x * (tileSize + tileSpacing),
                    y * (tileSize + tileSpacing),
                    0
                );

                // Create tile object
                GameObject tileObj = Instantiate(tilePrefab, tilePosition, Quaternion.identity, transform);
                tileObj.name = $"Tile ({x}, {y})";

                // Scale tile to desired size
                float spriteSize = tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
                float scaleFactor = tileSize / spriteSize;
                tileObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

                // Initialize tile component
                Tile tile = tileObj.GetComponent<Tile>();
                tile.gridPosition = new Vector2Int(x, y);
                board[x, y] = tile;

                // Set initial tile type based on column configuration
                int configIndex = (currentStopOffsets[x] + y) % columnStates[x].configuration.Length;
                TileType tileType = columnStates[x].configuration[configIndex];
                tile.SetTileType(tileType);
            }
        }
    }

    public void StartSpin()
    {
        if (isSpinning || spinningColumns != null)
        {
            return;
        }

        // Choose target stop position from predetermined valid positions
        currentStopOffsets = GetRandomValidStopPosition();

        // Start spinning from current positions (they're already in currentPositions)
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
            stopSequenceCoroutine = StartCoroutine(SmoothStopSequence());
        }
    }

    private IEnumerator SmoothStopSequence()
    {
        if (isStoppingSequence) yield break;
        isStoppingSequence = true;

        // Wait for all columns to reach full speed if they haven't already
        while (!AreAllColumnsAtFullSpeed())
        {
            yield return null;
        }

        // Gradually decelerate each column to the target stop offset
        for (int i = 0; i < gridSize; i++)
        {
            SpinningColumn spinningColumn = spinningColumns[i];
            spinningColumn.isStoppingRequested = true;
            yield return StartCoroutine(SmoothDecelerateAndAlignColumn(i, spinningColumn));
        }

        stopSequenceCoroutine = null;
        isStoppingSequence = false;
        isSpinning = false;
        OnSpinComplete?.Invoke();
    }

    private IEnumerator SmoothDecelerateAndAlignColumn(int column, SpinningColumn spinningColumn)
    {
        float decelerationTimer = decelerationTime;
        float initialSpeed = spinningColumn.currentSpeed;

        while (decelerationTimer > 0)
        {
            decelerationTimer -= Time.deltaTime;
            float progress = 1f - (decelerationTimer / decelerationTime);
            float speedProgress = 1f - Mathf.SmoothStep(0f, 1f, progress);
            spinningColumn.currentSpeed = initialSpeed * speedProgress;

            // Align to target stop offset during deceleration
            AlignColumnForStop(column, spinningColumn);
            UpdateSpinningColumn(column, spinningColumn);

            yield return null;
        }

        // Ensure final alignment to the target stop offset
        spinningColumn.currentSpeed = 0;
        AlignColumnForStop(column, spinningColumn);
        UpdateSpinningColumn(column, spinningColumn);
    }

    private void AlignColumnForStop(int column, SpinningColumn spinningColumn)
    {
        float currentOffset = currentPositions[column];
        float targetOffset = currentStopOffsets[column];
        float configLength = spinningColumn.columnConfiguration.Length;

        // Calculate shortest path to target (considering wrap-around)
        float diff = targetOffset - currentOffset;

        // Adjust for wrap-around
        if (Mathf.Abs(diff) > configLength / 2)
        {
            if (diff > 0)
                diff -= configLength;
            else
                diff += configLength;
        }

        // Smoothly move towards target
        float alignmentSpeed = maxSpinSpeed * 0.1f;
        float step = alignmentSpeed * Time.deltaTime;
        float newOffset = currentOffset + Mathf.Clamp(diff, -step, step);

        // Ensure we stay within bounds
        while (newOffset >= configLength)
            newOffset -= configLength;
        while (newOffset < 0)
            newOffset += configLength;

        // Update position
        currentPositions[column] = newOffset;
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
            isAppearing = true,
            columnConfiguration = columnStates[column].configuration
        };

        Vector3 basePosition = GetTileBasePosition(new Vector2Int(column, 0));
        float spacing = tileSize + tileSpacing;

        // Start from current position
        float startPosition = currentPositions[column];

        // Initialize main grid tiles - matching current visible state
        for (int i = 0; i < gridSize; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, transform);
            tileObj.name = $"SpinningTile_{column}_{i}";

            float yOffset = i * spacing;
            tileObj.transform.position = basePosition + new Vector3(0, yOffset, 0);

            // Get the current tile type from the board
            TileType currentType = board[column, i].GetCurrentType();

            spinningColumn.tiles[i] = new SpinningTile
            {
                visualObject = tileObj,
                tileType = currentType,
                currentPosition = yOffset,
                configIndex = ((int)startPosition + i) % spinningColumn.columnConfiguration.Length,
                visualScale = 1f,
                alpha = 1f,
                isGridTile = true
            };

            SpriteRenderer spriteRenderer = tileObj.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                float spriteSize = tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
                float scaleFactor = tileSize / spriteSize;
                tileObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
                spriteRenderer.sprite = currentType.sprite;
            }
        }

        // Initialize extra tiles
        for (int i = 0; i < spinningColumn.extraTiles.Length; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, transform);
            tileObj.name = $"ExtraTile_{column}_{i}";

            float yOffset = (i < spinningColumn.extraTiles.Length / 2) ?
                -spacing * (i + 1) :
                spacing * (gridSize + (i - spinningColumn.extraTiles.Length / 2));

            int configIndex = (int)startPosition;
            if (i < spinningColumn.extraTiles.Length / 2)
            {
                configIndex = ((int)startPosition - (i + 1) + spinningColumn.columnConfiguration.Length)
                    % spinningColumn.columnConfiguration.Length;
            }
            else
            {
                configIndex = ((int)startPosition + gridSize + (i - spinningColumn.extraTiles.Length / 2))
                    % spinningColumn.columnConfiguration.Length;
            }

            TileType tileType = spinningColumn.columnConfiguration[configIndex];

            spinningColumn.extraTiles[i] = new SpinningTile
            {
                visualObject = tileObj,
                tileType = tileType,
                currentPosition = yOffset,
                configIndex = configIndex,
                visualScale = 0.7f,
                alpha = 0f,
                isGridTile = false
            };

            SpriteRenderer spriteRenderer = tileObj.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                float spriteSize = tilePrefab.GetComponent<SpriteRenderer>().bounds.size.x;
                float scaleFactor = tileSize / spriteSize;
                tileObj.transform.position = basePosition + new Vector3(0, yOffset, 0);
                tileObj.transform.localScale = new Vector3(scaleFactor * 0.7f, scaleFactor * 0.7f, 1f);
                spriteRenderer.sprite = tileType.sprite;
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

        currentPositions[column] -= deltaMovement;

        // Keep position within bounds of configuration length
        while (currentPositions[column] < 0)
        {
            currentPositions[column] += spinningColumn.columnConfiguration.Length;
        }

        // Update all tiles
        SpinningTile[] allTiles = spinningColumn.tiles.Concat(spinningColumn.extraTiles).ToArray();
        foreach (var tile in allTiles)
        {
            if (tile == null || tile.visualObject == null) continue;

            // Update position
            tile.currentPosition -= deltaMovement;

            // Handle wrapping
            float totalHeight = spacing * (spinningColumn.tiles.Length + spinningColumn.extraTiles.Length);
            float wrapThreshold = -spacing * 1.5f;

            if (tile.currentPosition < wrapThreshold)
            {
                tile.currentPosition += totalHeight;

                // Calculate new config index based on current position to maintain sequence
                int positionIndex = Mathf.FloorToInt(currentPositions[column]);
                int offset = Mathf.FloorToInt(tile.currentPosition / spacing);
                int newConfigIndex = (positionIndex + offset + spinningColumn.columnConfiguration.Length)
                    % spinningColumn.columnConfiguration.Length;

                tile.configIndex = newConfigIndex;
                tile.tileType = spinningColumn.columnConfiguration[newConfigIndex];

                var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = tile.tileType.sprite;
                }
            }

            // Visual effects
            float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * spacing / 2f));
            float maxDistance = gridSize * spacing;
            float perspectiveScale = Mathf.Lerp(1f, 0.3f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
            float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);

            tile.visualScale = perspectiveScale;
            tile.alpha = fadeAlpha;

            // Update transform
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

    private void FinalizeColumn(int column)
    {
        SpinningColumn spinningColumn = spinningColumns[column];
        if (spinningColumn == null) return;

        // Update current position for next spin
        currentPositions[column] = currentStopOffsets[column] % columnStates[column].configuration.Length;

        // Update the board state for this column
        for (int row = 0; row < gridSize; row++)
        {
            if (board[column, row] != null)
            {
                int configIndex = (currentStopOffsets[column] + row) % columnStates[column].configuration.Length;
                TileType tileType = columnStates[column].configuration[configIndex];
                board[column, row].SetTileType(tileType);
                board[column, row].gameObject.SetActive(true);
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

    // Add this method to validate a full board state
    private bool ValidateBoardState(int[] stopOffsets)
    {
        // Check for vertical matches
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 2; y++)
            {
                int index1 = (stopOffsets[x] + y) % columnStates[x].configuration.Length;
                int index2 = (stopOffsets[x] + y + 1) % columnStates[x].configuration.Length;
                int index3 = (stopOffsets[x] + y + 2) % columnStates[x].configuration.Length;

                TileType type1 = columnStates[x].configuration[index1];
                TileType type2 = columnStates[x].configuration[index2];
                TileType type3 = columnStates[x].configuration[index3];

                if (type1 == type2 && type2 == type3)
                    return false;
            }
        }

        // Check for horizontal matches
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 2; x++)
            {
                int index1 = (stopOffsets[x] + y) % columnStates[x].configuration.Length;
                int index2 = (stopOffsets[x + 1] + y) % columnStates[x + 1].configuration.Length;
                int index3 = (stopOffsets[x + 2] + y) % columnStates[x + 2].configuration.Length;

                TileType type1 = columnStates[x].configuration[index1];
                TileType type2 = columnStates[x + 1].configuration[index2];
                TileType type3 = columnStates[x + 2].configuration[index3];

                if (type1 == type2 && type2 == type3)
                    return false;
            }
        }

        return true;
    }

    private void PrecalculateValidStopPositions()
    {
        int maxAttempts = 1000;
        int attemptCount = 0;

        // Clear existing stop positions
        foreach (var state in columnStates)
        {
            state.validStopOffsets.Clear();
        }

        while (attemptCount < maxAttempts)
        {
            int[] stopOffsets = new int[gridSize];

            // Generate random offsets
            for (int col = 0; col < gridSize; col++)
            {
                stopOffsets[col] = UnityEngine.Random.Range(0, columnStates[col].configuration.Length);
            }

            // Validate the full board state
            if (ValidateBoardState(stopOffsets))
            {
                // Add valid offsets to each column's list
                for (int col = 0; col < gridSize; col++)
                {
                    if (!columnStates[col].validStopOffsets.Contains(stopOffsets[col]))
                    {
                        columnStates[col].validStopOffsets.Add(stopOffsets[col]);
                    }
                }
            }

            attemptCount++;
        }

        // Ensure each column has at least one valid stop position
        for (int col = 0; col < gridSize; col++)
        {
            if (columnStates[col].validStopOffsets.Count == 0)
            {
                Debug.LogError($"Column {col} has no valid stop positions!");
                columnStates[col].validStopOffsets.Add(0); // Add default position as fallback
            }
        }

        Debug.Log("Valid stop positions precalculated for all columns");
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

        // Acceleration phase
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

        // Calculate target configuration when stop is requested
        float spacing = tileSize + tileSpacing;
        float configLength = columnStates[column].configuration.Length;
        Dictionary<SpinningTile, TileType> targetTypes = new Dictionary<SpinningTile, TileType>();

        // Main spinning loop
        while (!spinningColumn.isStoppingRequested)
        {
            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }

        // Pre-calculate target types for tiles when stop is requested
        foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
        {
            if (tile == null) continue;
            float normalizedPosition = tile.currentPosition / spacing;
            int rowOffset = Mathf.RoundToInt(normalizedPosition);
            int configIndex = (currentStopOffsets[column] + rowOffset + (int)configLength) % (int)configLength;
            targetTypes[tile] = columnStates[column].configuration[configIndex];
        }

        float initialSpeed = spinningColumn.currentSpeed;
        float decelerationTimer = decelerationTime;

        // Parameters for the clicking effect
        float clickMagnitude = 0.3f;
        float clickDuration = 0.15f;
        int numClicks = 3;

        // Calculate target position and adjust for shortest path
        float targetPosition = currentStopOffsets[column];
        float currentPosition = currentPositions[column];

        while (targetPosition - currentPosition > configLength / 2)
            targetPosition -= configLength;
        while (currentPosition - targetPosition > configLength / 2)
            targetPosition += configLength;

        // Initial deceleration phase
        float initialDecelTime = decelerationTime * 0.6f;
        float timer = 0f;

        while (timer < initialDecelTime)
        {
            timer += Time.deltaTime;
            float progress = timer / initialDecelTime;

            // Exponential slowdown
            float speedMultiplier = Mathf.Pow(1f - progress, 2f);
            spinningColumn.currentSpeed = initialSpeed * speedMultiplier;

            // Start updating tile types based on their projected final positions
            foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
            {
                if (tile == null || tile.visualObject == null) continue;

                // Calculate how close this tile is to its final grid position
                float distanceToFinal = Mathf.Abs(tile.currentPosition / spacing - Mathf.Round(tile.currentPosition / spacing));

                // If tile is approaching a grid position, start transitioning its type
                if (distanceToFinal < 0.3f && targetTypes.ContainsKey(tile))
                {
                    // Probability of updating increases as we slow down
                    float updateChance = progress * (1f - distanceToFinal);
                    if (UnityEngine.Random.value < updateChance)
                    {
                        var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null && tile.tileType != targetTypes[tile])
                        {
                            tile.tileType = targetTypes[tile];
                            spriteRenderer.sprite = targetTypes[tile].sprite;
                        }
                    }
                }
            }

            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }

        // Clicking phase
        float remainingDistance = targetPosition - currentPositions[column];
        float distancePerClick = remainingDistance / numClicks;

        for (int click = 0; click < numClicks; click++)
        {
            float clickTimer = 0f;
            float clickStartPos = currentPositions[column];
            float clickTargetPos = clickStartPos + distancePerClick;
            float currentClickDuration = clickDuration * (1f + click * 0.2f);

            // Update all visible tiles to their target types before each click
            foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
            {
                if (tile == null || tile.visualObject == null || !targetTypes.ContainsKey(tile)) continue;

                var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && tile.tileType != targetTypes[tile])
                {
                    tile.tileType = targetTypes[tile];
                    spriteRenderer.sprite = targetTypes[tile].sprite;
                }
            }

            while (clickTimer < currentClickDuration)
            {
                clickTimer += Time.deltaTime;
                float clickProgress = clickTimer / currentClickDuration;

                float bounceStrength = clickMagnitude * (1f - (float)click / numClicks);
                float bounce = Mathf.Sin(clickProgress * Mathf.PI) * bounceStrength;

                float easeOutProgress = 1f - Mathf.Pow(1f - clickProgress, 3f);
                float newPosition = Mathf.Lerp(clickStartPos, clickTargetPos, easeOutProgress);
                newPosition += bounce * (1f - clickProgress);

                currentPositions[column] = newPosition;
                while (currentPositions[column] >= configLength)
                    currentPositions[column] -= configLength;
                while (currentPositions[column] < 0)
                    currentPositions[column] += configLength;

                UpdateSpinningColumnWithTransition(column, spinningColumn, clickProgress);
                yield return null;
            }

            yield return new WaitForSeconds(0.05f);
        }

        // Final alignment
        float finalAlignTimer = 0f;
        float finalAlignDuration = 0.2f;
        float startPos = currentPositions[column];

        while (finalAlignTimer < finalAlignDuration)
        {
            finalAlignTimer += Time.deltaTime;
            float progress = finalAlignTimer / finalAlignDuration;
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            currentPositions[column] = Mathf.Lerp(startPos, targetPosition, smoothProgress);

            UpdateSpinningColumnWithTransition(column, spinningColumn, progress);
            yield return null;
        }

        currentPositions[column] = targetPosition % configLength;
        UpdateSpinningColumnWithTransition(column, spinningColumn, 1f);

        yield return StartCoroutine(TransitionToFinalState(column, spinningColumn));
        FinalizeColumn(column);

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

    private void UpdateSpinningColumnWithTransition(int column, SpinningColumn spinningColumn, float transitionProgress)
    {
        if (spinningColumn == null) return;

        float spacing = tileSize + tileSpacing;
        Vector3 basePosition = GetTileBasePosition(new Vector2Int(column, 0));

        // Update all tiles
        SpinningTile[] allTiles = spinningColumn.tiles.Concat(spinningColumn.extraTiles).ToArray();
        foreach (var tile in allTiles)
        {
            if (tile == null || tile.visualObject == null) continue;

            // Calculate target configuration based on current position
            float normalizedPosition = tile.currentPosition / spacing;
            int targetIndex = Mathf.RoundToInt(normalizedPosition);
            int configIndex = (currentStopOffsets[column] + targetIndex + columnStates[column].configuration.Length)
                % columnStates[column].configuration.Length;

            // Update tile type if needed
            TileType targetType = columnStates[column].configuration[configIndex];
            if (tile.tileType != targetType)
            {
                tile.tileType = targetType;
                var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = targetType.sprite;
                }
            }

            // Visual effects
            float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * spacing / 2f));
            float maxDistance = gridSize * spacing;
            float perspectiveScale = Mathf.Lerp(1f, 0.7f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
            float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);

            tile.visualScale = perspectiveScale;
            tile.alpha = fadeAlpha;

            // Update transform
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

    private IEnumerator TransitionToFinalState(int column, SpinningColumn spinningColumn)
    {
        float transitionDuration = 0.3f;
        float timer = 0f;

        // Get the final grid positions
        Dictionary<SpinningTile, Vector3> finalPositions = new Dictionary<SpinningTile, Vector3>();
        Dictionary<SpinningTile, TileType> finalTypes = new Dictionary<SpinningTile, TileType>();

        for (int row = 0; row < gridSize; row++)
        {
            float yPos = row * (tileSize + tileSpacing);
            var closestTile = GetClosestTileToPosition(spinningColumn, yPos);

            if (closestTile != null)
            {
                Vector3 finalPos = GetTileBasePosition(new Vector2Int(column, row));
                finalPositions[closestTile] = finalPos;

                int configIndex = (currentStopOffsets[column] + row) % columnStates[column].configuration.Length;
                finalTypes[closestTile] = columnStates[column].configuration[configIndex];
            }
        }

        // Animate transition
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / transitionDuration;
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
            {
                if (tile?.visualObject == null) continue;

                if (finalPositions.ContainsKey(tile))
                {
                    // Transition to final position
                    Vector3 startPos = tile.visualObject.transform.position;
                    tile.visualObject.transform.position = Vector3.Lerp(startPos, finalPositions[tile], smoothProgress);

                    // Update visuals
                    var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sprite = finalTypes[tile].sprite;
                        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                    }
                }
                else
                {
                    // Fade out non-grid tiles
                    var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        Color color = spriteRenderer.color;
                        color.a = 1f - smoothProgress;
                        spriteRenderer.color = color;
                    }
                }
            }

            yield return null;
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