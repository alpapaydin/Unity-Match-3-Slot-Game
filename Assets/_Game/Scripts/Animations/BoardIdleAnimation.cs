using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoardIdleAnimation : MonoBehaviour
{
    [Header("Idle Animation Settings")]
    [SerializeField] private float bounceAmplitude = 0.05f;
    [SerializeField] private float bounceFrequency = 1f;
    [SerializeField] private float columnDelay = 0.1f;
    [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Header("Wave Effect Settings")]
    [SerializeField] private float waveSpeed = 1f;
    [SerializeField] private float waveAmplitude = 0.025f;
    [Header("Scale Pulse Settings")]
    [SerializeField] private float scalePulseAmount = 0.02f;
    [SerializeField] private float scalePulseSpeed = 2f;
    [Header("Transition Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private GameBoard gameBoard;
    private List<ColumnAnimationState> columnStates;
    private Coroutine idleAnimationCoroutine;
    private bool isAnimating = false;
    private float currentAnimationStrength = 1f;
    private bool isFadingOut = false;
    private bool isFadingOutComplete = false;

    private class ColumnAnimationState
    {
        public Vector3[] originalPositions;
        public Vector3[] originalScales;
        public Transform[] tileTrans;
        public float timeOffset;
        public float currentTime;
        public Vector3[] initialPositions;
    }

    private void Start()
    {
        gameBoard = GetComponent<GameBoard>();
        if (gameBoard != null)
        {
            gameBoard.OnBoardInitialized += StartIdleAnimation;
            gameBoard.OnSpinStarted += SmoothStopAnimation;
        }
    }

    private void OnDestroy()
    {
        if (gameBoard != null)
        {
            gameBoard.OnBoardInitialized -= StartIdleAnimation;
            gameBoard.OnSpinStarted -= SmoothStopAnimation;
        }
    }

    public void StartIdleAnimation()
    {
        StopAllCoroutines();
        InitializeAnimationStates();
        currentAnimationStrength = 1f;
        isFadingOut = false;
        StartCoroutine(FadeInAnimation());
    }

    public IEnumerator WaitForFadeOut()
    {
        isFadingOutComplete = false;
        while (!isFadingOutComplete)
        {
            yield return null;
        }
    }

    private IEnumerator FadeInAnimation()
    {
        float timer = 0f;
        idleAnimationCoroutine = StartCoroutine(AnimateIdle());
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            currentAnimationStrength = transitionCurve.Evaluate(timer / fadeInDuration);
            yield return null;
        }
        currentAnimationStrength = 1f;
    }

    public void SmoothStopAnimation()
    {
        if (!isAnimating) return;
        StartCoroutine(FadeOutAnimation());
    }

    private IEnumerator FadeOutAnimation()
    {
        isFadingOut = true;
        float timer = 0f;
        float startStrength = 1f;
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            currentAnimationStrength = Mathf.Lerp(startStrength, 0f, transitionCurve.Evaluate(timer / fadeOutDuration));
            yield return null;
        }
        StopIdleAnimation();
        isFadingOutComplete = true;
    }

    public void StopIdleAnimation()
    {
        if (idleAnimationCoroutine != null)
        {
            StopCoroutine(idleAnimationCoroutine);
            idleAnimationCoroutine = null;
        }
        if (columnStates != null)
        {
            foreach (var state in columnStates)
            {
                if (state.tileTrans != null)
                {
                    for (int i = 0; i < state.tileTrans.Length; i++)
                    {
                        if (state.tileTrans[i] != null)
                        {
                            state.tileTrans[i].position = state.originalPositions[i];
                            state.tileTrans[i].localScale = state.originalScales[i];
                        }
                    }
                }
            }
        }
        isAnimating = false;
        currentAnimationStrength = 0f;
        isFadingOut = false;
    }

    private void InitializeAnimationStates()
    {
        int gridSize = gameBoard.GridSize;
        columnStates = new List<ColumnAnimationState>();

        for (int x = 0; x < gridSize; x++)
        {
            ColumnAnimationState state = new ColumnAnimationState
            {
                originalPositions = new Vector3[gridSize],
                originalScales = new Vector3[gridSize],
                tileTrans = new Transform[gridSize],
                timeOffset = x * columnDelay,
                currentTime = x * columnDelay,
                initialPositions = new Vector3[gridSize]
            };
            for (int y = 0; y < gridSize; y++)
            {
                Tile tile = gameBoard.GetTileAt(x, y);
                if (tile != null)
                {
                    state.tileTrans[y] = tile.transform;
                    state.originalPositions[y] = tile.transform.position;
                    state.originalScales[y] = tile.transform.localScale;
                    state.initialPositions[y] = tile.transform.position;
                }
            }
            columnStates.Add(state);
        }
    }

    private IEnumerator AnimateIdle()
    {
        isAnimating = true;
        while (!isFadingOut || currentAnimationStrength > 0)
        {
            foreach (var state in columnStates)
            {
                state.currentTime += Time.deltaTime;
                float columnTime = state.currentTime + state.timeOffset;
                float bounceOffset = Mathf.Sin(columnTime * bounceFrequency * Mathf.PI * 2f) * bounceAmplitude;
                float waveOffset = Mathf.Sin(columnTime * waveSpeed) * waveAmplitude;
                float totalOffset = (bounceOffset + waveOffset) * currentAnimationStrength;
                for (int i = 0; i < state.tileTrans.Length; i++)
                {
                    if (state.tileTrans[i] != null)
                    {
                        Vector3 newPosition = state.initialPositions[i] + Vector3.up * totalOffset;
                        float scaleOffset = 1f + (Mathf.Sin(columnTime * scalePulseSpeed) * scalePulseAmount * currentAnimationStrength);
                        Vector3 newScale = Vector3.Scale(state.originalScales[i], Vector3.one * scaleOffset);
                        float curveValue = bounceCurve.Evaluate(Mathf.PingPong(columnTime, 1f));
                        state.tileTrans[i].position = Vector3.Lerp(
                            state.initialPositions[i],
                            newPosition,
                            curveValue * currentAnimationStrength
                        );
                        state.tileTrans[i].localScale = newScale;
                    }
                }
            }
            yield return null;
        }
        StopIdleAnimation();
    }
}