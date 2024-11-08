using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
public class TextAnimator : MonoBehaviour
{
    [Header("Text Settings")]
    [SerializeField] private string textToAnimate = "Good\n Job!";
    [SerializeField] private GameObject letterPrefab;
    [SerializeField] private float lineHeight = 1.2f;
    [SerializeField] private float letterSpacing = 0.6f;
    [SerializeField] private float maxWidth = 10f;

    [Header("Background Settings")]
    [SerializeField] private Color backgroundColor = new Color(1f, 0.92f, 0.016f, 1f);
    [SerializeField] private Color backgroundBorderColor = Color.white;
    [SerializeField] private float backgroundPadding = 1f;
    [SerializeField] private float borderWidth = 0.2f;
    [SerializeField] private float cornerRadius = 0.5f;
    [SerializeField] private Vector2 backgroundScaleMultiplier = Vector2.one;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip letterSound;
    [SerializeField] private AudioClip fireworkSound;
    [SerializeField] private float startPitch = 0.8f;
    [SerializeField] private float endPitch = 1.2f;
    [SerializeField] private float pitchVariation = 0.1f;
    [SerializeField] private float volume = 0.5f;
    [SerializeField] private AnimationCurve pitchProgressionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Speed Settings")]
    [SerializeField] private float initialDelay = 0.05f;
    [SerializeField] private float finalDelay = 0.02f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stamp Animation Settings")]
    [SerializeField] private float stampDuration = 0.5f;
    [SerializeField] private float initialStampScale = 2f;
    [SerializeField] private float overshootScale = 0.8f;
    [SerializeField] private float springStrength = 0.5f;
    [SerializeField] private int springOscillations = 2;
    [SerializeField] private float delayBeforeText = 0.2f;

    [Header("Idle Animation Settings")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private float waveSpeed = 2f;
    [SerializeField] private float waveAmount = 0.2f;
    [SerializeField] private float wavePeriod = 1f;
    [SerializeField] private float idleAnimationFadeInDuration = 1f; // Duration to fade in the idle animation

    [Header("Sorting Settings")]
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private int sortingOrder = 110;

    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.3f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Vector3 popScale = new Vector3(1.5f, 1.5f, 1.5f);

    [Header("Visual Effects")]
    [SerializeField] private GameObject fireworksPrefab;

    public event Action OnTextAnimEnd;

    private List<GameObject> spawnedLetters = new List<GameObject>();
    private GameObject background;
    private Vector3 currentPosition;
    private Vector3 baseLetterScale;
    private AudioSource audioSource;
    private AudioSource winAudioSource;
    private AudioSource fireworksAudioSource;
    private Vector2 totalTextSize;
    private float actualLetterWidth;
    private float actualLetterHeight;
    private float actualLineHeight;
    private float actualLetterSpacing;
    private int currentLineIndex = 1;
    private int totalLetterCount;
    private int currentLetterIndex;
    private bool isAnimating = true;
    private Vector3[] originalPositions;
    private Coroutine idleAnimationCoroutine;
    private void Awake()
    {
        setupAudio();
    }

    private void Start()
    {
        InitializeLetterMetrics();
        winAudioSource.PlayOneShot(winSound);
        StartCoroutine(AnimateSequence());
    }

    private void setupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        winAudioSource = GetComponent<AudioSource>();
        fireworksAudioSource = GetComponent<AudioSource>();
        if (audioSource == null || winAudioSource == null || fireworksAudioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            winAudioSource = gameObject.AddComponent<AudioSource>();
            fireworksAudioSource = gameObject.AddComponent<AudioSource>();
        }
        fireworksAudioSource.playOnAwake = false;
        audioSource.playOnAwake = false;
        winAudioSource.playOnAwake = false;
        fireworksAudioSource.spatialBlend = 0f;
        audioSource.spatialBlend = 0f;
        winAudioSource.spatialBlend = 0f;
    }

    private void CreateBackground()
    {
        if (background != null)
        {
            Destroy(background);
        }
        background = new GameObject("TextBackground");
        background.transform.SetParent(transform);
        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        Vector2 bgSize = new Vector2(
            (totalTextSize.x + (backgroundPadding * 2)) * backgroundScaleMultiplier.x,
            (totalTextSize.y + (backgroundPadding * 2)) * backgroundScaleMultiplier.y
        );
        int textureSize = 256;
        Texture2D bgTexture = CreateRoundedRectTexture(textureSize, textureSize,
            backgroundColor, backgroundBorderColor, cornerRadius * textureSize / 10, borderWidth * textureSize / 10);

        Sprite bgSprite = Sprite.Create(bgTexture,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f), 100f);
        renderer.sprite = bgSprite;

        background.transform.position = transform.position;
        background.transform.localScale = Vector3.zero;
    }

    private IEnumerator StampAnimation()
    {
        float elapsed = 0f;
        Vector3 finalScale = new Vector3(
            (totalTextSize.x + (backgroundPadding * 2)) * backgroundScaleMultiplier.x,
            (totalTextSize.y + (backgroundPadding * 2)) * backgroundScaleMultiplier.y,
            1
        );
        Vector3 startScale = finalScale * initialStampScale;
        background.transform.localScale = startScale;
        background.transform.position = transform.position;
        while (elapsed < stampDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stampDuration;
            float scale = CalculateSpringScale(t);
            Vector3 currentScale = finalScale * scale;
            background.transform.localScale = currentScale;
            yield return null;
        }
        background.transform.localScale = finalScale;
    }

    private float CalculateSpringScale(float t)
    {
        float amplitude = initialStampScale - 1f;
        float decay = 1.0f - t;
        float oscillation = Mathf.Cos(t * springOscillations * Mathf.PI * 2f);
        float springEffect = 1f + (oscillation * decay * springStrength);
        if (t < 0.2f)
        {
            springEffect = Mathf.Lerp(initialStampScale, overshootScale, t * 5f);
        }
        return springEffect;
    }

    private Texture2D CreateRoundedRectTexture(int width, int height, Color fillColor, Color borderColor, float radius, float borderSize)
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        float halfWidth = width / 2f;
        float halfHeight = height / 2f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float distX = Mathf.Abs(x - halfWidth);
                float distY = Mathf.Abs(y - halfHeight);
                if (distX > halfWidth - radius && distY > halfHeight - radius)
                {
                    float cornerX = x < halfWidth ? halfWidth - radius : halfWidth + radius;
                    float cornerY = y < halfHeight ? halfHeight - radius : halfHeight + radius;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cornerX, cornerY));
                    if (distance > radius)
                    {
                        colors[y * width + x] = Color.clear;
                    }
                    else if (distance > radius - borderSize)
                    {
                        colors[y * width + x] = borderColor;
                    }
                    else
                    {
                        colors[y * width + x] = fillColor;
                    }
                }
                else
                {
                    if (distX > halfWidth - borderSize || distY > halfHeight - borderSize)
                    {
                        colors[y * width + x] = borderColor;
                    }
                    else
                    {
                        colors[y * width + x] = fillColor;
                    }
                }
            }
        }
        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    private IEnumerator AnimateSequence()
    {
        GameObject fireworks = null;
        if (fireworksPrefab != null)
        {
            fireworks = Instantiate(
                fireworksPrefab,
                fireworksPrefab.transform.position,
                fireworksPrefab.transform.rotation
            );
            fireworksAudioSource.PlayOneShot(fireworkSound);
            Destroy(fireworks, 6f);
        }
        List<List<char>> lines = CalculateLineBreaks(textToAnimate, out totalTextSize);
        CreateBackground();
        yield return StartCoroutine(StampAnimation());
        yield return new WaitForSeconds(delayBeforeText);
        yield return StartCoroutine(AnimateText());
        OnTextAnimEnd?.Invoke();
    }

    private List<List<char>> CalculateLineBreaks(string text, out Vector2 totalSize)
    {
        List<List<char>> lines = new List<List<char>>();
        List<char> currentLine = new List<char>();
        float currentWidth = 0;
        float maxLineWidth = 0;
        int totalChars = 0;
        foreach (char c in text)
        {
            if (c == '\n')
            {
                maxLineWidth = Mathf.Max(maxLineWidth, currentWidth);
                lines.Add(new List<char>(currentLine));
                currentLine.Clear();
                currentWidth = 0;
                continue;
            }
            float charWidth = actualLetterSpacing;
            if (currentWidth + charWidth > maxWidth)
            {
                maxLineWidth = Mathf.Max(maxLineWidth, currentWidth);
                lines.Add(new List<char>(currentLine));
                currentLine.Clear();
                currentWidth = 0;
            }
            currentLine.Add(c);
            currentWidth += charWidth;
            totalChars++;
        }
        if (currentLine.Count > 0)
        {
            maxLineWidth = Mathf.Max(maxLineWidth, currentWidth);
            lines.Add(currentLine);
        }
        totalSize = new Vector2(
            maxLineWidth - actualLetterSpacing * 0.5f,
            lines.Count * actualLineHeight
        );
        return lines;
    }

    private IEnumerator AnimateText()
    {
        Vector2 totalTextSize;
        List<List<char>> lines = CalculateLineBreaks(textToAnimate, out totalTextSize);
        totalLetterCount = 0;
        foreach (var line in lines)
        {
            totalLetterCount += line.Count;
        }
        currentLetterIndex = 0;
        currentPosition = transform.position;
        currentPosition.y += totalTextSize.y / 2f - actualLineHeight * 0.5f;
        currentLineIndex = 1;
        foreach (var line in lines)
        {
            float lineWidth = (line.Count * actualLetterSpacing) - actualLetterSpacing * 0.5f;
            currentPosition.x = transform.position.x - (lineWidth / 2f);

            foreach (char letter in line)
            {
                if (!char.IsWhiteSpace(letter))
                {
                    yield return SpawnLetter(letter);
                }
                else
                {
                    currentPosition.x += actualLetterSpacing;
                    currentLetterIndex++;
                }
            }

            currentPosition.y -= actualLineHeight;
            currentPosition.x = transform.position.x;
            currentLineIndex++;
        }
        originalPositions = new Vector3[spawnedLetters.Count];
        for (int i = 0; i < spawnedLetters.Count; i++)
        {
            originalPositions[i] = spawnedLetters[i].transform.position;
        }
        idleAnimationCoroutine = StartCoroutine(IdleAnimation());
        yield break;
    }

    private IEnumerator IdleAnimation()
    {
        float animationTime = 0f;
        float fadeInTime = 0f;
        isAnimating = true;
        originalPositions = new Vector3[spawnedLetters.Count];
        for (int i = 0; i < spawnedLetters.Count; i++)
        {
            originalPositions[i] = spawnedLetters[i].transform.position;
        }
        while (isAnimating)
        {
            animationTime += Time.deltaTime;
            fadeInTime = Mathf.Min(fadeInTime + Time.deltaTime, idleAnimationFadeInDuration);
            float fadeInProgress = fadeInTime / idleAnimationFadeInDuration;
            float animationStrength = Mathf.SmoothStep(0f, 1f, fadeInProgress);
            for (int i = 0; i < spawnedLetters.Count; i++)
            {
                if (spawnedLetters[i] == null) continue;
                float pulse = 1f + (Mathf.Sin(animationTime * pulseSpeed) * pulseAmount * animationStrength);
                float wave = Mathf.Sin((animationTime * waveSpeed) + (i * wavePeriod)) *
                            waveAmount *
                            animationStrength;
                Vector3 originalPos = originalPositions[i];
                Vector3 newPos = originalPos + new Vector3(0f, wave, 0f);
                spawnedLetters[i].transform.position = newPos;
                spawnedLetters[i].transform.localScale = baseLetterScale * pulse;
            }
            yield return null;
        }
        float returnDuration = 0.5f;
        float elapsed = 0f;
        Vector3[] currentPositions = new Vector3[spawnedLetters.Count];
        Vector3[] currentScales = new Vector3[spawnedLetters.Count];
        for (int i = 0; i < spawnedLetters.Count; i++)
        {
            if (spawnedLetters[i] != null)
            {
                currentPositions[i] = spawnedLetters[i].transform.position;
                currentScales[i] = spawnedLetters[i].transform.localScale;
            }
        }
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < spawnedLetters.Count; i++)
            {
                if (spawnedLetters[i] == null) continue;

                spawnedLetters[i].transform.position = Vector3.Lerp(
                    currentPositions[i],
                    originalPositions[i],
                    smoothT
                );
                spawnedLetters[i].transform.localScale = Vector3.Lerp(
                    currentScales[i],
                    baseLetterScale,
                    smoothT
                );
            }
            yield return null;
        }
    }

    private IEnumerator SpawnLetter(char letter)
    {
        GameObject letterObj = Instantiate(letterPrefab, currentPosition, Quaternion.identity, transform);
        letterObj.transform.localScale = baseLetterScale;
        spawnedLetters.Add(letterObj);
        TextMesh textMesh = letterObj.GetComponent<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = letter.ToString();
        }
        Transform outlineTransform = letterObj.transform.Find("Outline");
        if (outlineTransform != null)
        {
            TextMesh outlineTextMesh = outlineTransform.GetComponent<TextMesh>();
            if (outlineTextMesh != null)
            {
                outlineTextMesh.text = letter.ToString();
            }
        }
        int lineBasedSortingOrder = sortingOrder + currentLineIndex * 2;
        MeshRenderer meshRenderer = letterObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = lineBasedSortingOrder;
        }
        if (outlineTransform != null)
        {
            MeshRenderer outlineRenderer = outlineTransform.GetComponent<MeshRenderer>();
            if (outlineRenderer != null)
            {
                outlineRenderer.sortingLayerName = sortingLayerName;
                outlineRenderer.sortingOrder = lineBasedSortingOrder - 1;
            }
        }
        PlayLetterSound();
        yield return StartCoroutine(PopAnimation(letterObj));
        currentPosition.x += actualLetterSpacing;
        float progress = (float)currentLetterIndex / totalLetterCount;
        float currentDelay = Mathf.Lerp(initialDelay, finalDelay, speedCurve.Evaluate(progress));
        currentLetterIndex++;
        yield return new WaitForSeconds(currentDelay);
    }

    private void PlayLetterSound()
    {
        if (letterSound != null && audioSource != null)
        {
            float progress = (float)currentLetterIndex / totalLetterCount;
            float progressivePitch = Mathf.Lerp(startPitch, endPitch, pitchProgressionCurve.Evaluate(progress));
            float finalPitch = progressivePitch + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            audioSource.pitch = finalPitch;
            audioSource.volume = volume;
            audioSource.PlayOneShot(letterSound);
        }
    }

    private void InitializeLetterMetrics()
    {
        GameObject tempLetter = Instantiate(letterPrefab, Vector3.zero, Quaternion.identity);
        baseLetterScale = tempLetter.transform.localScale;
        MeshRenderer renderer = tempLetter.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Bounds combinedBounds = renderer.bounds;
            Transform outlineTransform = tempLetter.transform.Find("Outline");
            if (outlineTransform != null)
            {
                MeshRenderer outlineRenderer = outlineTransform.GetComponent<MeshRenderer>();
                if (outlineRenderer != null)
                {
                    combinedBounds.Encapsulate(outlineRenderer.bounds);
                }
            }
            actualLetterWidth = combinedBounds.size.x;
            actualLetterHeight = combinedBounds.size.y;
            actualLetterSpacing = letterSpacing * baseLetterScale.x;
            actualLineHeight = lineHeight * baseLetterScale.y;
        }
        Destroy(tempLetter);
    }

    private IEnumerator PopAnimation(GameObject letter)
    {
        float elapsed = 0f;
        letter.transform.localScale = Vector3.zero;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / popDuration;
            float curveValue = popCurve.Evaluate(normalizedTime);
            Vector3 targetScale = baseLetterScale;
            Vector3 maxPopScale = Vector3.Scale(baseLetterScale, popScale);
            Vector3 currentScale;
            if (normalizedTime <= 0.5f)
            {
                currentScale = Vector3.Lerp(Vector3.zero, maxPopScale, curveValue * 2f);
            }
            else
            {
                float bounceProgress = (normalizedTime - 0.5f) * 2f;
                currentScale = Vector3.Lerp(maxPopScale, targetScale, bounceProgress);
            }
            letter.transform.localScale = currentScale;
            yield return null;
        }
        letter.transform.localScale = baseLetterScale;
    }

    public IEnumerator FadeOutText()
    {
        isAnimating = false;
        if (idleAnimationCoroutine != null)
        {
            yield return idleAnimationCoroutine;
        }
        float fadeDuration = 0.5f;
        float elapsed = 0;
        Dictionary<TextMesh, Color> initialColors = new Dictionary<TextMesh, Color>();
        Dictionary<TextMesh, Color> initialOutlineColors = new Dictionary<TextMesh, Color>();
        SpriteRenderer bgRenderer = background?.GetComponent<SpriteRenderer>();
        Color initialBgColor = bgRenderer != null ? bgRenderer.color : Color.white;
        foreach (GameObject letter in spawnedLetters)
        {
            TextMesh textMesh = letter.GetComponent<TextMesh>();
            TextMesh outlineTextMesh = letter.transform.Find("Outline")?.GetComponent<TextMesh>();

            if (textMesh)
                initialColors[textMesh] = textMesh.color;
            if (outlineTextMesh)
                initialOutlineColors[outlineTextMesh] = outlineTextMesh.color;
        }
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1 - (elapsed / fadeDuration);
            foreach (GameObject letter in spawnedLetters)
            {
                TextMesh textMesh = letter.GetComponent<TextMesh>();
                TextMesh outlineTextMesh = letter.transform.Find("Outline")?.GetComponent<TextMesh>();
                if (textMesh)
                {
                    Color newColor = initialColors[textMesh];
                    newColor.a = alpha;
                    textMesh.color = newColor;
                }

                if (outlineTextMesh)
                {
                    Color newOutlineColor = initialOutlineColors[outlineTextMesh];
                    newOutlineColor.a = alpha;
                    outlineTextMesh.color = newOutlineColor;
                }
            }
            if (bgRenderer != null)
            {
                Color newBgColor = initialBgColor;
                newBgColor.a = alpha;
                bgRenderer.color = newBgColor;
            }

            yield return null;
        }
        if (bgRenderer != null)
        {
            Color finalColor = initialBgColor;
            finalColor.a = 0;
            bgRenderer.color = finalColor;
        }
    }
}