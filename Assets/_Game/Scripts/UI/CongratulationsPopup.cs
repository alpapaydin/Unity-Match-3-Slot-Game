using UnityEngine;
using System.Collections;

public class CongratulationsPopup : MonoBehaviour
{
    [Header("Background Settings")]
    [SerializeField] private SpriteRenderer backgroundSprite;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.5f);
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Sorting Settings")]
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private int sortingOrder = 100;

    [Header("Background Options")]
    [SerializeField] private bool useCustomBackground = false;
    [SerializeField] private Sprite customBackgroundSprite;

    [Header("Debug Settings")]
    [SerializeField] private bool showOnStart = true;

    private TextAnimator textAnimator;
    private void Awake()
    {
        InitializeBackground();
        textAnimator = GetComponent<TextAnimator>();
    }

    private void Start()
    {
        if (showOnStart)
        {
            Show();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void InitializeBackground()
    {
        if (backgroundSprite == null)
        {
            var bgObject = new GameObject("PopupBackground");
            bgObject.transform.SetParent(transform);
            backgroundSprite = bgObject.AddComponent<SpriteRenderer>();
            if (!useCustomBackground || customBackgroundSprite == null)
            {
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                Sprite defaultSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                backgroundSprite.sprite = defaultSprite;
                backgroundSprite.drawMode = SpriteDrawMode.Simple;
                backgroundSprite.size = new Vector2(1, 1);
            }
            else
            {
                backgroundSprite.sprite = customBackgroundSprite;
            }
        }
        backgroundSprite.sortingLayerName = sortingLayerName;
        backgroundSprite.sortingOrder = sortingOrder;
        Color initialColor = backgroundColor;
        initialColor.a = 0;
        backgroundSprite.color = initialColor;
        AdjustBackgroundSize();
    }

    private void AdjustBackgroundSize()
    {
        if (Camera.main == null) return;
        float height = Camera.main.orthographicSize * 2;
        float width = height * Camera.main.aspect;
        if (backgroundSprite.drawMode == SpriteDrawMode.Sliced)
        {
            backgroundSprite.size = new Vector2(width, height);
            backgroundSprite.transform.localPosition = Vector3.zero;
        }
        else
        {
            backgroundSprite.transform.localScale = new Vector3(width, height, 1);
        }
        Vector3 centerPosition = Camera.main.transform.position;
        centerPosition.z = transform.position.z;
        transform.position = centerPosition;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(FadeIn());
    }

    public void Hide()
    {
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeIn()
    {
        float elapsedTime = 0;
        Color startColor = backgroundSprite.color;
        Color targetColor = backgroundColor;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / fadeDuration;
            backgroundSprite.color = Color.Lerp(startColor, targetColor, normalizedTime);
            yield return null;
        }

        backgroundSprite.color = targetColor;
    }

    public IEnumerator HideAndWait()
    {
        if (textAnimator != null)
        {
            yield return textAnimator.FadeOutText();
        }
        yield return StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float elapsedTime = 0;
        Color startColor = backgroundSprite.color;
        Color targetColor = backgroundColor;
        targetColor.a = 0;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / fadeDuration;
            backgroundSprite.color = Color.Lerp(startColor, targetColor, normalizedTime);
            yield return null;
        }
        backgroundSprite.color = targetColor;
        gameObject.SetActive(false);
    }
}