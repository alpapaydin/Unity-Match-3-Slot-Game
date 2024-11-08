using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MatchAnimationManager : MonoBehaviour
{
    [Header("Match Animation Settings")]
    [SerializeField] private float matchAnimDuration = 0.6f;
    [SerializeField] private int pulseCount = 2;
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.3f;
    [SerializeField] private Color glowColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private float glowIntensity = 0.5f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Animation Intensity")]
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Sequential Animation")]
    [SerializeField] private float delayBetweenTiles = 0.1f;
    [SerializeField] private bool orderByPosition = true; // If true, orders by position, if false, randomizes

    [Header("Particle Effects")]
    [SerializeField] private GameObject sparkleParticlePrefab;

    private GameBoard gameBoard;
    private class MatchAnimData
    {
        public Vector3 originalScale;
        public Vector3 originalPosition;
        public Color originalColor;
        public Transform transform;
        public SpriteRenderer spriteRenderer;
        public Tile tile;
        public float animationStartTime;
        public bool isAnimating;
        public int originalSortingOrder;
    }

    private void Start()
    {
        gameBoard = GetComponent<GameBoard>();
    }

    public IEnumerator AnimateMatches(List<Tile> matches)
    {
        List<MatchAnimData> animDataList = new List<MatchAnimData>();
        foreach (Tile tile in matches)
        {
            SpriteRenderer spriteRenderer = tile.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                MatchAnimData animData = new MatchAnimData
                {
                    originalScale = tile.transform.localScale,
                    originalColor = spriteRenderer.color,
                    originalPosition = tile.transform.position,
                    transform = tile.transform,
                    spriteRenderer = spriteRenderer,
                    tile = tile,
                    animationStartTime = 0f,
                    isAnimating = false,
                    originalSortingOrder = spriteRenderer.sortingOrder  // Store original order
                };
                spriteRenderer.sortingOrder = 10;
                animDataList.Add(animData);
            }
        }
        if (orderByPosition)
        {
            animDataList = animDataList
                .OrderBy(data => data.tile.gridPosition.x)
                .ThenBy(data => data.tile.gridPosition.y)
                .ToList();
        }
        else
        {
            animDataList = animDataList.OrderBy(x => Random.value).ToList();
        }
        for (int i = 0; i < animDataList.Count; i++)
        {
            animDataList[i].animationStartTime = i * delayBetweenTiles;
        }
        float totalDuration = (animDataList.Count - 1) * delayBetweenTiles + matchAnimDuration;
        float globalTimer = 0f;
        while (globalTimer < totalDuration)
        {
            globalTimer += Time.deltaTime;
            foreach (var animData in animDataList)
            {
                if (!animData.isAnimating && globalTimer >= animData.animationStartTime)
                {
                    animData.isAnimating = true;
                    if (sparkleParticlePrefab != null)
                    {
                        GameObject sparkle = Instantiate(sparkleParticlePrefab,
                            animData.transform.position,
                            Quaternion.identity);
                        Destroy(sparkle, matchAnimDuration + 0.5f);
                    }
                }
                if (animData.isAnimating)
                {
                    float localTimer = globalTimer - animData.animationStartTime;
                    if (localTimer < matchAnimDuration)
                    {
                        float progress = localTimer / matchAnimDuration;
                        float intensity = intensityCurve.Evaluate(progress);
                        float pulsePhase = progress * pulseCount * Mathf.PI * 2f;
                        float pulseSine = Mathf.Sin(pulsePhase);
                        float normalizedPulse = (pulseSine + 1f) / 2f;
                        float curvedPulse = scaleCurve.Evaluate(normalizedPulse);
                        float scaleRange = maxScale - minScale;
                        float intensityAdjustedMinScale = Mathf.Lerp(1f, minScale, intensity);
                        float intensityAdjustedMaxScale = Mathf.Lerp(1f, maxScale, intensity);
                        float currentScale = Mathf.Lerp(intensityAdjustedMinScale, intensityAdjustedMaxScale, curvedPulse);
                        float randomOffset = Mathf.PerlinNoise(animData.transform.position.x * 100f, Time.time * 5f) * 0.1f * intensity;
                        Vector3 newScale = animData.originalScale * (currentScale + randomOffset);
                        float glowAmount = Mathf.InverseLerp(minScale, maxScale, currentScale) * glowIntensity * intensity;
                        Color newColor = Color.Lerp(animData.originalColor, glowColor, glowAmount);
                        float bounceHeight = (currentScale - 1f) * 0.1f * intensity;
                        Vector3 newPos = animData.originalPosition + new Vector3(0, bounceHeight, 0);
                        animData.transform.localScale = newScale;
                        animData.spriteRenderer.color = newColor;
                        animData.transform.position = newPos;
                    }
                    else
                    {
                        animData.transform.localScale = animData.originalScale;
                        animData.transform.position = animData.originalPosition;
                        animData.spriteRenderer.color = animData.originalColor;
                    }
                }
            }
            yield return null;
        }
        foreach (var animData in animDataList)
        {
            animData.transform.localScale = animData.originalScale;
            animData.transform.position = animData.originalPosition;
            animData.spriteRenderer.color = animData.originalColor;
            animData.spriteRenderer.sortingOrder = animData.originalSortingOrder;
        }
    }
}