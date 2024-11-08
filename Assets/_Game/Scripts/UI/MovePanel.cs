using UnityEngine;
using System.Collections;

public class MovePanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer panelRenderer;
    [SerializeField] private TextMesh panelText;

    [Header("Text")]
    [SerializeField] private string movesPretext = "Moves: ";

    [Header("Sorting Settings")]
    [SerializeField] private int panelSortingOrder = -1;
    [SerializeField] private int textSortingOrder = 0;
    [SerializeField] private string panelSortingLayer = "Default";
    [SerializeField] private string textSortingLayer = "Default";

    [Header("Gradient Animation Settings")]
    [SerializeField] private Color startColor = Color.red;
    [SerializeField] private Color endColor = Color.yellow;
    [SerializeField] private float gradientDuration = 1f;
    [SerializeField] private bool pingPong = true;

    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 0.5f;

    private Vector3 openPosition;
    private Vector3 closedPosition;
    private bool isSliding;
    private float slideStartTime;
    private Vector3 slideStartPosition;
    private Vector3 slideTargetPosition;
    private System.Action onSlideComplete;
    private Coroutine colorAnimationCoroutine;

    private void Awake()
    {
        if (panelRenderer == null)
            panelRenderer = GetComponent<SpriteRenderer>();
        panelRenderer.sortingOrder = panelSortingOrder;
        panelRenderer.sortingLayerName = panelSortingLayer;

        if (panelText != null)
        {
            var textRenderer = panelText.GetComponent<MeshRenderer>();
            textRenderer.sortingOrder = textSortingOrder;
            textRenderer.sortingLayerName = textSortingLayer;
        }
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPosition, System.Action onComplete = null)
    {
        closedPosition = startPosition;
        openPosition = targetPosition;
        transform.position = closedPosition;
        onSlideComplete = onComplete;
        StartSlide(openPosition);
    }

    private void Update()
    {
        if (isSliding)
        {
            float elapsed = (Time.time - slideStartTime) / slideDuration;
            if (elapsed >= 1f)
            {
                transform.position = slideTargetPosition;
                isSliding = false;
                onSlideComplete?.Invoke();
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed);
                transform.position = Vector3.Lerp(slideStartPosition, slideTargetPosition, t);
            }
        }
    }

    public void SetMoveCount(int remaining)
    {
        panelText.text = movesPretext + remaining.ToString();
        if (remaining == 0)
        {
            if (colorAnimationCoroutine != null)
            {
                StopCoroutine(colorAnimationCoroutine);
            }
            colorAnimationCoroutine = StartCoroutine(AnimateTextColor());
        }
    }

    private IEnumerator AnimateTextColor()
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = (elapsed / gradientDuration) % 1f;
            if (pingPong)
            {
                normalizedTime = Mathf.PingPong(elapsed / gradientDuration, 1f);
            }
            panelText.color = Color.Lerp(startColor, endColor, normalizedTime);
            yield return null;
        }
    }

    private void OnDisable()
    {
        if (colorAnimationCoroutine != null)
        {
            StopCoroutine(colorAnimationCoroutine);
        }
    }

    public void SetFinal()
    {
        StopCoroutine(colorAnimationCoroutine);
        panelText.color = startColor;
    }

    public void SlideDown(System.Action onComplete = null)
    {
        onSlideComplete = onComplete;
        StartSlide(closedPosition);
    }

    private void StartSlide(Vector3 target)
    {
        slideStartTime = Time.time;
        slideStartPosition = transform.position;
        slideTargetPosition = target;
        isSliding = true;
    }
}