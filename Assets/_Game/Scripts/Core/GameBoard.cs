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
    [SerializeField] private BoardIdleAnimation idleAnimation;
    [SerializeField] private float padding = 0.1f;

    [Header("Spin Settings")]
    [SerializeField] private float maxSpinSpeed = 20f;
    [SerializeField] private float accelerationTime = 0.2f;
    [SerializeField] private float decelerationTime = 1.5f;
    [SerializeField] private float decelerationIncrease = 0.3f;
    [SerializeField] private float columnStartDelay = 0.1f;
    [SerializeField] private AnimationCurve spinSpeedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stop Settings")]
    [SerializeField] private float minSpinTimeBeforeStop = 1f;

    [Header("Board Configuration")]
    [SerializeField] private bool randomizeBoard = true;
    [SerializeField] private bool randomizeSize = true;
    [SerializeField] private int defaultSize = 5;

    public event Action OnBoardInitialized;
    public event Action OnSpinStarted;
    public event Action OnSpinComplete;
    public event Action OnSpinAnimationStarted;
    public event Action OnSingleColumnStopped;
    public event Action OnExtrasAppeared;
    public event Action OnSwipePhaseStarted;
    public event Action <int> OnSwipe;
    public event Action OnGameOver;
    public event Action OnGameWon;

    private Dictionary<TileType, int> tileTypeCounts;
    private TileType[][] preconstructedColumns;
    private Tile[,] board;
    private SpinningColumn[] spinningColumns;
    private Coroutine stopSequenceCoroutine;
    private DeterministicBoardStateGenerator boardGenerator;
    private Dictionary<int, TileType[]> targetPatterns;
    private TileType[][] columnConfigurations;
    private ColumnState[] columnStates;
    private int gridSize;
    private bool isInitialized = false;
    private float tileSize;
    private float tileSpacing;
    private bool isSpinning = false;
    private bool earlyStopRequested = false;
    private int startupColumnsRemaining;
    private bool isStartingUp = false;
    private int extraTilesPerColumn;
    private float spinStartTime;
    private bool isStoppingSequence = false;
    private int[] currentStopOffsets;
    private float[] currentPositions;
    private int spinCount = 0;
    private int remainingSwipes = 0;
    private float currentDeceleration = 0;

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
        public float perspectiveStrength = 0.5f;
        public float fadeStrength = 0.7f;
        public float appearanceProgress = 0f;
        public bool isAppearing = true;
        public int lastUsedConfigIndex;
        public SpinningTile[] extraTiles;
    }
    private class ColumnState
    {
        public TileType[] configuration;
        public List<int> validStopOffsets;
    }

    private class SpinningTile
    {
        public GameObject visualObject;
        public TileType tileType;
        public float currentPosition;
        public float targetPosition;
        public float currentSpacing = 0f;
        public int configIndex;
        public float visualScale = 1f;
        public float alpha = 1f;
        public bool isGridTile = false;
    }

    private void Start()
    {
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
        spinCount = 0;
        currentDeceleration = decelerationTime;
        isSpinning = false;
        spinningColumns = null;
        isStoppingSequence = false;
        earlyStopRequested = false;
        isStartingUp = false;
        stopSequenceCoroutine = null;
        gridSize = randomizeSize ? (UnityEngine.Random.Range(0, 2) == 0 ? 5 : 7) : defaultSize;
        CalculateOptimalTileSize();
        board = new Tile[gridSize, gridSize];
        boardGenerator = new DeterministicBoardStateGenerator(gridSize, tileTypes, 3);
        columnStates = new ColumnState[gridSize];
        currentStopOffsets = new int[gridSize];
        currentPositions = new float[gridSize];
        PrecalculateValidStopOffsets();
        PrecalculateValidStopPositions();
        TryRandomizeBoard();
        CreateInitialBoard();
        isInitialized = true;
        OnBoardInitialized.Invoke();
    }

    private void PrecalculateValidStopOffsets()
    {
        for (int i = 0; i < gridSize; i++)
        {
            columnStates[i] = new ColumnState
            {
                configuration = boardGenerator.ColumnConfigurations[i],
                validStopOffsets = new List<int>(boardGenerator.ValidStopOffsets.Select(offset => offset[i]))
            };
        }
    }

    private void TryRandomizeBoard()
    {
        if (randomizeBoard)
        {
            currentStopOffsets = GetRandomValidStopPosition();
            for (int i = 0; i < gridSize; i++)
            {
                currentPositions[i] = currentStopOffsets[i];
            }
        }
    }

    public int GetMinimumMovesToMatch()
    {
        remainingSwipes = MinMovesSolver.FindMinimumMovesToMatch(this);
        return remainingSwipes;
    }

    public void GameOver()
    { OnGameOver?.Invoke(); }

    public void GameWon()
    { OnGameWon?.Invoke(); }

    public int GetRemainingMoves()
    { return remainingSwipes; }

    public void StartSwipePhase()
    {
        OnSwipePhaseStarted.Invoke();
    }

    public void AfterSwipe()
    {
        remainingSwipes--;
        OnSwipe?.Invoke(remainingSwipes);
    }

    private int[] GetRandomValidStopPosition()
    {
        if (boardGenerator.ValidStopOffsets.Count == 0)
        {
            return new int[gridSize];
        }
        int[] offsets = boardGenerator.ValidStopOffsets[UnityEngine.Random.Range(0, boardGenerator.ValidStopOffsets.Count)];

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
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
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
                int configIndex = (currentStopOffsets[x] + y) % columnStates[x].configuration.Length;
                TileType tileType = columnStates[x].configuration[configIndex];
                tile.SetTileType(tileType);
            }
        }
    }

    private IEnumerator StartSpinSequence()
    {
        if (isSpinning || spinningColumns != null)
        {
            yield break;
        }
        OnSpinStarted?.Invoke();
        if (spinCount == 0)
        {
            yield return StartCoroutine(idleAnimation.WaitForFadeOut());
        }
        spinCount += 1;
        currentStopOffsets = GetRandomValidStopPosition();
        isSpinning = true;
        isStartingUp = true;
        isStoppingSequence = false;
        startupColumnsRemaining = gridSize;
        spinStartTime = Time.time;
        stopSequenceCoroutine = null;
        earlyStopRequested = false;
        StartCoroutine(SpinSequence());
    }

    public void StartSpin()
    {
        StartCoroutine(StartSpinSequence());
    }

    public void StopSpin()
    {
        if (!isSpinning) return;
        if (isStartingUp)
        {
            earlyStopRequested = true;
            return;
        }
        if (Time.time - spinStartTime < minSpinTimeBeforeStop)
            return;
        if (!isStoppingSequence && stopSequenceCoroutine == null)
        {
            stopSequenceCoroutine = StartCoroutine(SmoothStopSequence());
        }
    }

    public void SwapTilesInBoard(Vector2Int pos1, Vector2Int pos2)
    {
        if (pos1.x < 0 || pos1.x >= gridSize || pos1.y < 0 || pos1.y >= gridSize ||
            pos2.x < 0 || pos2.x >= gridSize || pos2.y < 0 || pos2.y >= gridSize)
            return;
        Tile tile1 = board[pos1.x, pos1.y];
        Tile tile2 = board[pos2.x, pos2.y];
        TileType type1 = tile1.GetCurrentType();
        TileType type2 = tile2.GetCurrentType();
        board[pos1.x, pos1.y] = tile2;
        board[pos2.x, pos2.y] = tile1;
        tile1.SetTileType(type2);
        tile2.SetTileType(type1);
    }

    private IEnumerator SmoothStopSequence()
    {
        if (isStoppingSequence) yield break;
        isStoppingSequence = true;
        while (!AreAllColumnsAtFullSpeed())
        {
            yield return null;
        }
        for (int i = 0; i < gridSize; i++)
        {
            SpinningColumn spinningColumn = spinningColumns[i];
            spinningColumn.isStoppingRequested = true;
            yield return StartCoroutine(SmoothDecelerateAndAlignColumn(i, spinningColumn));
        }
        stopSequenceCoroutine = null;
        isStoppingSequence = false;
        isSpinning = false;
    }

    private IEnumerator SmoothDecelerateAndAlignColumn(int column, SpinningColumn spinningColumn)
    {
        currentDeceleration += decelerationIncrease / gridSize;
        float decelerationTimer = currentDeceleration;
        float initialSpeed = spinningColumn.currentSpeed;
        while (decelerationTimer > 0)
        {
            decelerationTimer -= Time.deltaTime;
            float progress = 1f - (decelerationTimer / currentDeceleration);
            float speedProgress = 1f - Mathf.SmoothStep(0f, 1f, progress);
            spinningColumn.currentSpeed = initialSpeed * speedProgress;
            AlignColumnForStop(column, spinningColumn);
            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }
        spinningColumn.currentSpeed = 0;
        AlignColumnForStop(column, spinningColumn);
        UpdateSpinningColumn(column, spinningColumn);
    }

    private void AlignColumnForStop(int column, SpinningColumn spinningColumn)
    {
        float currentOffset = currentPositions[column];
        float targetOffset = currentStopOffsets[column];
        float configLength = spinningColumn.columnConfiguration.Length;
        float diff = targetOffset - currentOffset;
        if (Mathf.Abs(diff) > configLength / 2)
        {
            if (diff > 0)
                diff -= configLength;
            else
                diff += configLength;
        }
        float alignmentSpeed = maxSpinSpeed * 0.1f;
        float step = alignmentSpeed * Time.deltaTime;
        float newOffset = currentOffset + Mathf.Clamp(diff, -step, step);
        while (newOffset >= configLength)
            newOffset -= configLength;
        while (newOffset < 0)
            newOffset += configLength;
        currentPositions[column] = newOffset;
    }

    private SpinningTile GetClosestTileToPosition(SpinningColumn column, float targetPosition)
    {
        if (column == null || column.tiles == null) return null;
        var gridTile = column.tiles
            .Where(t => t != null && t.isGridTile)
            .OrderBy(t => Mathf.Abs(t.currentPosition - targetPosition))
            .FirstOrDefault();
        if (gridTile != null && Mathf.Abs(gridTile.currentPosition - targetPosition) < (tileSize + tileSpacing) * 0.5f)
        {
            return gridTile;
        }
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
        float startPosition = currentPositions[column];
        for (int i = 0; i < gridSize; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, transform);
            tileObj.name = $"SpinningTile_{column}_{i}";
            float yOffset = i * spacing;
            tileObj.transform.position = basePosition + new Vector3(0, yOffset, 0);
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
        while (currentPositions[column] < 0)
        {
            currentPositions[column] += spinningColumn.columnConfiguration.Length;
        }
        SpinningTile[] allTiles = spinningColumn.tiles.Concat(spinningColumn.extraTiles).ToArray();
        foreach (var tile in allTiles)
        {
            if (tile == null || tile.visualObject == null) continue;
            tile.currentPosition -= deltaMovement;
            float totalHeight = spacing * (spinningColumn.tiles.Length + spinningColumn.extraTiles.Length);
            float wrapThreshold = -spacing * 1.5f;
            if (tile.currentPosition < wrapThreshold)
            {
                tile.currentPosition += totalHeight;
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
            float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * spacing / 2f));
            float maxDistance = gridSize * spacing;
            float perspectiveScale = Mathf.Lerp(1f, 0.3f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
            float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);
            tile.visualScale = perspectiveScale;
            tile.alpha = fadeAlpha;
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
        currentPositions[column] = currentStopOffsets[column] % columnStates[column].configuration.Length;
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
        foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
        {
            if (tile?.visualObject != null)
            {
                Destroy(tile.visualObject);
            }
        }
        spinningColumn.isSpinning = false;
    }

    private bool ValidateBoardState(int[] stopOffsets)
    {
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
        foreach (var state in columnStates)
        {
            state.validStopOffsets.Clear();
        }
        while (attemptCount < maxAttempts)
        {
            int[] stopOffsets = new int[gridSize];
            for (int col = 0; col < gridSize; col++)
            {
                stopOffsets[col] = UnityEngine.Random.Range(0, columnStates[col].configuration.Length);
            }
            if (ValidateBoardState(stopOffsets))
            {
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
        for (int col = 0; col < gridSize; col++)
        {
            if (columnStates[col].validStopOffsets.Count == 0)
            {
                columnStates[col].validStopOffsets.Add(0);
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
        OnExtrasAppeared.Invoke();
        spinningColumns = new SpinningColumn[gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            spinningColumns[x] = InitializeSpinningColumn(x);
            for (int y = 0; y < gridSize; y++)
            {
                if (board[x, y] != null)
                {
                    board[x, y].gameObject.SetActive(false);
                }
            }
        }
        for (int x = 0; x < gridSize; x++)
        {
            StartCoroutine(AnimateExtraTilesAppearance(x));
            yield return new WaitForSeconds(columnStartDelay * 0.5f);
        }
        while (AnyColumnsAppearing())
        {
            yield return null;
        }
        OnSpinAnimationStarted.Invoke();
        for (int x = 0; x < gridSize; x++)
        {
            StartCoroutine(SpinColumn(x));
            yield return new WaitForSeconds(columnStartDelay);
        }
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
            foreach (var tile in spinningColumn.extraTiles)
            {
                if (tile == null || tile.visualObject == null) continue;
                float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * (tileSize + tileSpacing) / 2f));
                float maxDistance = gridSize * (tileSize + tileSpacing);
                float perspectiveScale = Mathf.Lerp(1f, 0.7f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
                float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);
                tile.visualScale = Mathf.Lerp(0.7f, perspectiveScale, spinningColumn.appearanceProgress);
                tile.alpha = Mathf.Lerp(0f, fadeAlpha, spinningColumn.appearanceProgress);
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
        float spacing = tileSize + tileSpacing;
        float configLength = columnStates[column].configuration.Length;
        Dictionary<SpinningTile, TileType> targetTypes = new Dictionary<SpinningTile, TileType>();
        while (!spinningColumn.isStoppingRequested)
        {
            UpdateSpinningColumn(column, spinningColumn);
            yield return null;
        }
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
        float clickMagnitude = 0.3f;
        float clickDuration = 0.15f;
        int numClicks = 3;
        float targetPosition = currentStopOffsets[column];
        float currentPosition = currentPositions[column];
        while (targetPosition - currentPosition > configLength / 2)
            targetPosition -= configLength;
        while (currentPosition - targetPosition > configLength / 2)
            targetPosition += configLength;
        float initialDecelTime = decelerationTime * 0.6f;
        float timer = 0f;
        while (timer < initialDecelTime)
        {
            timer += Time.deltaTime;
            float progress = timer / initialDecelTime;
            float speedMultiplier = Mathf.Pow(1f - progress, 2f);
            spinningColumn.currentSpeed = initialSpeed * speedMultiplier;
            foreach (var tile in spinningColumn.tiles.Concat(spinningColumn.extraTiles))
            {
                if (tile == null || tile.visualObject == null) continue;
                float distanceToFinal = Mathf.Abs(tile.currentPosition / spacing - Mathf.Round(tile.currentPosition / spacing));
                if (distanceToFinal < 0.3f && targetTypes.ContainsKey(tile))
                {
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
        float remainingDistance = targetPosition - currentPositions[column];
        float distancePerClick = remainingDistance / numClicks;
        for (int click = 0; click < numClicks; click++)
        {
            float clickTimer = 0f;
            float clickStartPos = currentPositions[column];
            float clickTargetPos = clickStartPos + distancePerClick;
            float currentClickDuration = clickDuration * (1f + click * 0.2f);
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
        OnSingleColumnStopped.Invoke();
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
            GetMinimumMovesToMatch();
            OnSpinComplete?.Invoke();
        }
    }

    private void UpdateSpinningColumnWithTransition(int column, SpinningColumn spinningColumn, float transitionProgress)
    {
        if (spinningColumn == null) return;
        float spacing = tileSize + tileSpacing;
        Vector3 basePosition = GetTileBasePosition(new Vector2Int(column, 0));
        SpinningTile[] allTiles = spinningColumn.tiles.Concat(spinningColumn.extraTiles).ToArray();
        foreach (var tile in allTiles)
        {
            if (tile == null || tile.visualObject == null) continue;
            float normalizedPosition = tile.currentPosition / spacing;
            int targetIndex = Mathf.RoundToInt(normalizedPosition);
            int configIndex = (currentStopOffsets[column] + targetIndex + columnStates[column].configuration.Length)
                % columnStates[column].configuration.Length;
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
            float distanceFromCenter = Mathf.Abs(tile.currentPosition - (gridSize * spacing / 2f));
            float maxDistance = gridSize * spacing;
            float perspectiveScale = Mathf.Lerp(1f, 0.7f, (distanceFromCenter / maxDistance) * spinningColumn.perspectiveStrength);
            float fadeAlpha = Mathf.Lerp(1f, 0f, (distanceFromCenter / maxDistance) * spinningColumn.fadeStrength);
            tile.visualScale = perspectiveScale;
            tile.alpha = fadeAlpha;
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
                    Vector3 startPos = tile.visualObject.transform.position;
                    tile.visualObject.transform.position = Vector3.Lerp(startPos, finalPositions[tile], smoothProgress);
                    var spriteRenderer = tile.visualObject.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sprite = finalTypes[tile].sprite;
                        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                    }
                }
                else
                {
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

    public Tile GetTileAt(int x, int y)
    {
        if (x >= 0 && x < gridSize && y >= 0 && y < gridSize)
        {
            return board[x, y];
        }
        return null;
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

    public List<Tile> GetCurrentMatches()
    {
        HashSet<Tile> matches = new HashSet<Tile>();
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize - 2; x++)
            {
                Tile tile1 = GetTileAt(x, y);
                Tile tile2 = GetTileAt(x + 1, y);
                Tile tile3 = GetTileAt(x + 2, y);

                if (tile1 != null && tile2 != null && tile3 != null &&
                    tile1.GetCurrentType() == tile2.GetCurrentType() &&
                    tile2.GetCurrentType() == tile3.GetCurrentType())
                {
                    matches.Add(tile1);
                    matches.Add(tile2);
                    matches.Add(tile3);
                }
            }
        }
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize - 2; y++)
            {
                Tile tile1 = GetTileAt(x, y);
                Tile tile2 = GetTileAt(x, y + 1);
                Tile tile3 = GetTileAt(x, y + 2);

                if (tile1 != null && tile2 != null && tile3 != null &&
                    tile1.GetCurrentType() == tile2.GetCurrentType() &&
                    tile2.GetCurrentType() == tile3.GetCurrentType())
                {
                    matches.Add(tile1);
                    matches.Add(tile2);
                    matches.Add(tile3);
                }
            }
        }

        return matches.ToList();
    }

    public void ResetGame()
    {
        StartCoroutine(ResetGameCoroutine());
    }

    private IEnumerator ResetGameCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (board != null)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (board[x, y] != null)
                    {
                        Destroy(board[x, y].gameObject);
                        board[x, y] = null;
                    }
                }
            }
        }
        if (spinningColumns != null)
        {
            foreach (var column in spinningColumns)
            {
                if (column != null)
                {
                    if (column.tiles != null)
                    {
                        foreach (var tile in column.tiles)
                        {
                            if (tile?.visualObject != null)
                            {
                                Destroy(tile.visualObject);
                            }
                        }
                    }
                    if (column.extraTiles != null)
                    {
                        foreach (var tile in column.extraTiles)
                        {
                            if (tile?.visualObject != null)
                            {
                                Destroy(tile.visualObject);
                            }
                        }
                    }
                }
            }
            spinningColumns = null;
        }
        isSpinning = false;
        isInitialized = false;
        earlyStopRequested = false;
        isStartingUp = false;
        isStoppingSequence = false;
        startupColumnsRemaining = 0;
        if (stopSequenceCoroutine != null)
        {
            StopCoroutine(stopSequenceCoroutine);
            stopSequenceCoroutine = null;
        }
        yield return null;
        InitializeBoard();
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