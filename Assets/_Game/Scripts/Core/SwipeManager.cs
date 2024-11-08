using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class SwipeManager : MonoBehaviour
{
    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 0.5f;
    [SerializeField] private float swipeAnimationDuration = 1f;
    [SerializeField] private float returnAnimationDuration = 0.3f;
    [SerializeField] private AnimationCurve swipeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private int maxQueuedSwipes = 3;

    [Header("Popup Settings")]
    [SerializeField] private CongratulationsPopup congratulationsPopupPrefab;
    [SerializeField] private float popupDuration = 2f;
    [SerializeField] private Vector3 popupSpawnPosition = Vector3.zero;

    [Header("Drag Preview Settings")]
    [SerializeField] private float maxDragDistance = 1f;
    [SerializeField] private float dragAlpha = 0.7f;

    [Header("References")]
    [SerializeField] private GameBoard gameBoard;
    [SerializeField] private GameUIManager uiManager;
    [SerializeField] private GameObject tilePrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip swipeSound;
    [SerializeField] private AudioClip matchSound;

    public event Action<int> OnMatchFound;
    public event Action OnSwipeStarted;
    public event Action OnGameLost;

    private bool isSwipeEnabled = false;
    private bool hasSwipedOnce = false;
    private Vector2 swipeStart;
    private Vector3 tileStartPosition;
    private Tile selectedTile;
    private GameObject dragPreview;
    private SpriteRenderer dragPreviewRenderer;
    private Queue<SwipeAction> swipeQueue = new Queue<SwipeAction>();
    private HashSet<Tile> tilesInUse = new HashSet<Tile>();
    private int activeAnimations = 0;
    private Coroutine matchCheckCoroutine;
    private AudioSource audioSource;

    private struct SwipeAction
    {
        public Tile tile1;
        public Tile tile2;
        public Vector2Int direction;
        public Vector3 previewPosition;
        public Coroutine animationCoroutine;
        public SwipeAction(Tile t1, Tile t2, Vector2Int dir, Vector3 preview)
        {
            tile1 = t1;
            tile2 = t2;
            direction = dir;
            previewPosition = preview;
            animationCoroutine = null;
        }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (gameBoard != null)
        {
            gameBoard.OnSpinStarted += DisableSwiping;
            gameBoard.OnSpinComplete += EnableSwiping;
            gameBoard.OnBoardInitialized += () => {
                isSwipeEnabled = false;
            };
            OnMatchFound += (matchCount) => ResetDragPreview();
        }
        CreateDragPreview();
    }

    private void CreateDragPreview()
    {
        dragPreview = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, transform);
        dragPreview.name = "DragPreview";
        dragPreviewRenderer = dragPreview.GetComponent<SpriteRenderer>();
        if (dragPreviewRenderer != null)
        {
            Color color = dragPreviewRenderer.color;
            color.a = dragAlpha;
            dragPreviewRenderer.color = color;
            dragPreviewRenderer.sortingLayerName = "DragPreview";
        }
        dragPreview.SetActive(false);
    }

    private void ResetDragPreview()
    {
        if (selectedTile != null)
        {
            StopAllCoroutines();
            dragPreview.SetActive(false);
            if (dragPreviewRenderer != null)
            {
                Color color = dragPreviewRenderer.color;
                color.a = dragAlpha;
                dragPreviewRenderer.color = color;
            }
            selectedTile = null;
        }
    }


    private void OnDestroy()
    {
        if (gameBoard != null)
        {
            gameBoard.OnSpinComplete -= EnableSwiping;
        }
    }

    private void Update()
    {
        if (!isSwipeEnabled) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleSwipeStart();
        }
        else if (Input.GetMouseButton(0) && selectedTile != null)
        {
            HandleSwipeDrag();
        }
        else if (Input.GetMouseButtonUp(0) && selectedTile != null)
        {
            HandleSwipeEnd();
        }
    }

    private bool IsTileAvailable(Tile tile)
    {
        return !tilesInUse.Contains(tile);
    }

    private void HandleSwipeStart()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        if (hit.collider != null && hit.collider.TryGetComponent(out Tile tile))
        {
            if (IsTileAvailable(tile))
            {
                selectedTile = tile;
                swipeStart = mousePos;
                tileStartPosition = tile.transform.position;
                dragPreview.transform.position = tileStartPosition;
                dragPreview.transform.localScale = tile.transform.localScale;
                dragPreviewRenderer.sprite = tile.GetComponent<SpriteRenderer>().sprite;
                dragPreview.SetActive(true);
            }
        }
    }

    private void HandleSwipeDrag()
    {
        if (selectedTile == null) return;

        Vector2 dragDifference = CalculateDragDifference();
        UpdateDragPreviewPosition(dragDifference);

        if (IsDragDistanceSufficient(dragDifference))
        {
            ProcessSwipe(dragDifference);
        }
    }

    private Vector2 CalculateDragDifference()
    {
        Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return currentMousePos - swipeStart;
    }

    private void UpdateDragPreviewPosition(Vector2 difference)
    {
        Vector3 dragPosition = tileStartPosition;
        if (Mathf.Abs(difference.x) > Mathf.Abs(difference.y))
        {
            dragPosition.x += Mathf.Clamp(difference.x, -maxDragDistance, maxDragDistance);
        }
        else
        {
            dragPosition.y += Mathf.Clamp(difference.y, -maxDragDistance, maxDragDistance);
        }
        dragPreview.transform.position = dragPosition;
    }

    private bool IsDragDistanceSufficient(Vector2 difference)
    {
        return difference.magnitude >= minSwipeDistance;
    }

    private void ProcessSwipe(Vector2 difference)
    {
        Vector2Int direction = GetSwipeDirection(difference);
        Vector2Int neighborPos = selectedTile.gridPosition + direction;

        if (TryGetValidNeighborTile(neighborPos, out Tile neighborTile))
        {
            HandleValidSwipe(neighborTile, direction);
        }
        else
        {
            HandleInvalidSwipe();
        }

        selectedTile = null;
    }

    private bool TryGetValidNeighborTile(Vector2Int neighborPos, out Tile neighborTile)
    {
        neighborTile = null;

        if (!IsValidGridPosition(neighborPos)) return false;

        neighborTile = gameBoard.GetTileAt(neighborPos.x, neighborPos.y);
        return neighborTile != null;
    }

    private void HandleValidSwipe(Tile neighborTile, Vector2Int direction)
    {
        if (CanProcessSwipe(neighborTile))
        {
            ProcessValidSwipe(neighborTile, direction);
        }
        else
        {
            StartCoroutine(AnimateReturnToStart());
        }
    }

    private bool CanProcessSwipe(Tile neighborTile)
    {
        return IsTileAvailable(neighborTile) && swipeQueue.Count < maxQueuedSwipes;
    }

    private void ProcessValidSwipe(Tile neighborTile, Vector2Int direction)
    {
        dragPreview.SetActive(false);
        PlaySwipeSound();
        QueueSwipe(selectedTile, neighborTile, direction, dragPreview.transform.position);
        if (!hasSwipedOnce)
        {
            HandleFirstSwipe();
        }
        gameBoard.AfterSwipe();
        if (gameBoard.GetRemainingMoves() == 0)
        {
            HandleNoMovesRemaining();
        }
    }

    private void PlaySwipeSound()
    {
        if (audioSource != null && swipeSound != null)
        {
            audioSource.PlayOneShot(swipeSound);
        }
    }

    private void HandleFirstSwipe()
    {
        hasSwipedOnce = true;
        OnSwipeStarted?.Invoke();
        gameBoard.StartSwipePhase();

        if (uiManager != null)
        {
            uiManager.DisableAllButtons();
        }
    }

    private void HandleNoMovesRemaining()
    {
        DisableSwiping();
        OnGameLost?.Invoke();
    }

    private void HandleInvalidSwipe()
    {
        StartCoroutine(AnimateReturnToStart());
    }

    private void QueueSwipe(Tile tile1, Tile tile2, Vector2Int direction, Vector3 previewPosition)
    {
        tilesInUse.Add(tile1);
        tilesInUse.Add(tile2);
        SwipeAction swipeAction = new SwipeAction(tile1, tile2, direction, previewPosition);
        swipeQueue.Enqueue(swipeAction);
        StartCoroutine(ProcessNextSwipe());
    }

    private IEnumerator ProcessNextSwipe()
    {
        if (swipeQueue.Count == 0) yield break;
        SwipeAction currentSwipe = swipeQueue.Dequeue();
        Vector2Int pos1 = currentSwipe.tile1.gridPosition;
        Vector2Int pos2 = currentSwipe.tile2.gridPosition;
        currentSwipe.tile1.gridPosition = pos2;
        currentSwipe.tile2.gridPosition = pos1;
        gameBoard.SwapTilesInBoard(pos1, pos2);
        activeAnimations++;
        yield return StartCoroutine(AnimateSwap(currentSwipe));
        activeAnimations--;
        tilesInUse.Remove(currentSwipe.tile1);
        tilesInUse.Remove(currentSwipe.tile2);
        if (activeAnimations == 0 && swipeQueue.Count == 0)
        {
            List<Tile> matches = gameBoard.GetCurrentMatches();
            if (matches.Count >= 3)
            {
                audioSource.PlayOneShot(matchSound);
                OnMatchFound?.Invoke(matches.Count);
                yield return StartCoroutine(HandleMatchAndReset(matches));
                swipeQueue.Clear();
                tilesInUse.Clear();
            }
        }
    }

    private void HandleSwipeEnd()
    {
        if (selectedTile != null)
        {
            StartCoroutine(AnimateReturnToStart());
        }
        selectedTile = null;
    }

    private IEnumerator AnimateReturnToStart()
    {
        if (dragPreview == null || !dragPreview.activeSelf) yield break;
        float timer = 0f;
        Vector3 startPos = dragPreview.transform.position;
        Vector3 endPos = tileStartPosition;
        while (timer < returnAnimationDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / returnAnimationDuration;
            float curveProgress = returnCurve.Evaluate(progress);
            dragPreview.transform.position = Vector3.Lerp(startPos, endPos, curveProgress);
            if (progress > 0.5f)
            {
                Color color = dragPreviewRenderer.color;
                color.a = dragAlpha * (1 - ((progress - 0.5f) * 2));
                dragPreviewRenderer.color = color;
            }

            yield return null;
        }
        dragPreview.SetActive(false);
        Color finalColor = dragPreviewRenderer.color;
        finalColor.a = dragAlpha;
        dragPreviewRenderer.color = finalColor;
    }

    private IEnumerator AnimateSwap(SwipeAction swipeAction)
    {
        float timer = 0f;
        Vector3 startPos1 = swipeAction.previewPosition;
        Vector3 startPos2 = swipeAction.tile2.transform.position;
        Vector3 endPos1 = swipeAction.tile2.transform.position;
        Vector3 endPos2 = tileStartPosition;
        swipeAction.tile1.transform.position = startPos1;
        TileType originalType1 = swipeAction.tile1.GetCurrentType();
        TileType originalType2 = swipeAction.tile2.GetCurrentType();
        swipeAction.tile1.SetTileType(originalType2);
        swipeAction.tile2.SetTileType(originalType1);
        while (timer < swipeAnimationDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / swipeAnimationDuration;
            float curveProgress = swipeCurve.Evaluate(progress);
            swipeAction.tile1.transform.position = Vector3.Lerp(startPos1, endPos1, curveProgress);
            swipeAction.tile2.transform.position = Vector3.Lerp(startPos2, endPos2, curveProgress);
            yield return null;
        }
        swipeAction.tile1.transform.position = endPos1;
        swipeAction.tile2.transform.position = endPos2;
        yield return null;
    }

    private IEnumerator HandleMatchAndReset(List<Tile> matches)
    {
        DisableSwiping();
        yield return new WaitForSeconds(0.1f);
        var matchAnim = gameBoard.GetComponent<MatchAnimationManager>();
        if (matchAnim != null)
        {
            yield return StartCoroutine(matchAnim.AnimateMatches(matches));
        }
        if (congratulationsPopupPrefab != null)
        {
            yield return StartCoroutine(ShowCongratulationsPopup());
        }
        yield return new WaitForSeconds(0.2f);
        if (gameBoard != null)
        {
            gameBoard.ResetGame();
        }
    }

    private IEnumerator ShowCongratulationsPopup()
    {
        CongratulationsPopup popup = Instantiate(congratulationsPopupPrefab, popupSpawnPosition, Quaternion.identity);
        yield return new WaitForSeconds(popupDuration);
        yield return popup.HideAndWait();
        if (popup != null)
        {
            Destroy(popup.gameObject);
        }
    }

    private void EnableSwiping()
    {
        isSwipeEnabled = true;
        hasSwipedOnce = false;
        selectedTile = null;
        swipeQueue.Clear();
        tilesInUse.Clear();
        activeAnimations = 0;
        if (dragPreview != null)
        {
            dragPreview.SetActive(false);
            if (dragPreviewRenderer != null)
            {
                dragPreviewRenderer.color = new Color(1f, 1f, 1f, dragAlpha);
            }
        }
    }

    public void DisableSwiping()
    {
        isSwipeEnabled = false;
        selectedTile = null;
        swipeQueue.Clear();
        tilesInUse.Clear();
        activeAnimations = 0;
    }

    private Vector2Int GetSwipeDirection(Vector2 swipe)
    {
        if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
        {
            return new Vector2Int(swipe.x > 0 ? 1 : -1, 0);
        }
        else
        {
            return new Vector2Int(0, swipe.y > 0 ? 1 : -1);
        }
    }

    private bool IsValidGridPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < gameBoard.GridSize &&
               pos.y >= 0 && pos.y < gameBoard.GridSize;
    }
}