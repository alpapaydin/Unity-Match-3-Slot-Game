using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class GameButton : MonoBehaviour
{
    [Header("Button Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Text Settings")]
    [SerializeField] private string buttonText = "Button";
    [SerializeField] private TextMesh textMesh;
    [SerializeField] private TextMesh outlineTextMesh;
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private int sortingOrder = 1;
    [SerializeField] private Vector2 padding = new Vector2(0.5f, 0.2f);

    [Header("Color Animation Settings")]
    [SerializeField] private bool enableColorAnimation = true;
    [SerializeField] private Color colorFrom = Color.white;
    [SerializeField] private Color colorTo = Color.yellow;
    [SerializeField] private float colorCycleSpeed = 2f;

    [Header("Bounce Animation Settings")]
    [SerializeField] private bool enableBounceOnClick = true;
    [SerializeField] private float bounceScale = 1.2f;
    [SerializeField] private float bounceSpeed = 10f;
    [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Idle Animation Settings")]
    [SerializeField] private bool enableIdleAnimation = true;
    [SerializeField] private float idleFloatSpeed = 2f;
    [SerializeField] private float idleFloatAmount = 0.1f;
    [SerializeField] private float idleRotationAmount = 3f;
    [SerializeField] private bool useIdleRotation = true;

    // References
    private SpriteRenderer spriteRenderer;
    private Vector3 originalTextPosition;
    private Vector3 originalTextScale;
    private Quaternion originalTextRotation;
    private Coroutine colorAnimationCoroutine;
    private Coroutine bounceAnimationCoroutine;
    private Coroutine idleAnimationCoroutine;

    public string ButtonText
    {
        get => buttonText;
        set
        {
            buttonText = value;
            UpdateButtonText();
        }
    }

    // State
    private bool isInteractable = true;
    public bool IsInteractable
    {
        get => isInteractable;
        set
        {
            bool wasInteractable = isInteractable;
            isInteractable = value;
            UpdateVisualState();
            if (wasInteractable != isInteractable)
            {
                if (isInteractable)
                {
                    if (enableColorAnimation) StartColorAnimation();
                    if (enableIdleAnimation) StartIdleAnimation();
                }
                else
                {
                    StopNonBounceAnimations();
                    ResetTextTransform();
                }
            }
        }
    }

    // Events
    public System.Action OnClick;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (textMesh == null)
            textMesh = transform.Find("Text")?.GetComponent<TextMesh>();
        if (outlineTextMesh == null)
            outlineTextMesh = transform.Find("Text/Outline")?.GetComponent<TextMesh>();

        if (textMesh != null)
        {
            originalTextScale = textMesh.transform.localScale;
            originalTextPosition = textMesh.transform.localPosition;
            originalTextRotation = textMesh.transform.localRotation;
        }

        InitializeText();
        UpdateButtonText();
    }

    private void OnEnable()
    {
        if (isInteractable)
        {
            if (enableColorAnimation) StartColorAnimation();
            if (enableIdleAnimation) StartIdleAnimation();
        }
    }

    private void OnDisable()
    {
        StopAllAnimations();
    }

    private void StopNonBounceAnimations()
    {
        if (colorAnimationCoroutine != null)
        {
            StopCoroutine(colorAnimationCoroutine);
            colorAnimationCoroutine = null;
        }
        if (idleAnimationCoroutine != null)
        {
            StopCoroutine(idleAnimationCoroutine);
            idleAnimationCoroutine = null;
        }
    }

    private void ResetTextTransform()
    {
        if (textMesh != null)
        {
            if (bounceAnimationCoroutine == null)
            {
                textMesh.transform.localPosition = originalTextPosition;
                textMesh.transform.localScale = originalTextScale;
                textMesh.transform.localRotation = originalTextRotation;
            }
            textMesh.color = colorFrom;
        }
    }

    private void StopAllAnimations()
    {
        StopNonBounceAnimations();
        if (bounceAnimationCoroutine != null)
        {
            StopCoroutine(bounceAnimationCoroutine);
            bounceAnimationCoroutine = null;
        }
        ResetTextTransform();
    }

    private void StartColorAnimation()
    {
        if (colorAnimationCoroutine != null)
            StopCoroutine(colorAnimationCoroutine);

        colorAnimationCoroutine = StartCoroutine(AnimateTextColor());
    }

    private void StartIdleAnimation()
    {
        if (idleAnimationCoroutine != null)
            StopCoroutine(idleAnimationCoroutine);

        idleAnimationCoroutine = StartCoroutine(IdleAnimation());
    }

    private IEnumerator AnimateTextColor()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * colorCycleSpeed) + 1f) * 0.5f;
            if (textMesh != null)
            {
                textMesh.color = Color.Lerp(colorFrom, colorTo, t);
            }
            yield return null;
        }
    }

    private IEnumerator IdleAnimation()
    {
        if (textMesh == null) yield break;
        float time = 0f;
        Transform textTransform = textMesh.transform;
        Transform outlineTransform = outlineTextMesh?.transform;
        while (true)
        {
            time += Time.deltaTime;
            float yOffset = Mathf.Sin(time * idleFloatSpeed) * idleFloatAmount;
            Vector3 newPosition = originalTextPosition + new Vector3(0f, yOffset, 0f);
            float rotation = useIdleRotation ? Mathf.Sin(time * idleFloatSpeed) * idleRotationAmount : 0f;
            Quaternion newRotation = Quaternion.Euler(0f, 0f, rotation);
            textTransform.localPosition = newPosition;
            textTransform.localRotation = newRotation;
            if (outlineTransform != null)
            {
                outlineTransform.localRotation = newRotation;
            }

            yield return null;
        }
    }

    private IEnumerator BounceAnimation()
    {
        if (textMesh == null) yield break;
        Transform textTransform = textMesh.transform;
        Vector3 startScale = textTransform.localScale;
        Vector3 targetScale = startScale * bounceScale;
        float time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime * bounceSpeed;
            float curveValue = bounceCurve.Evaluate(time);
            textTransform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            yield return null;
        }

        time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime * bounceSpeed;
            float curveValue = bounceCurve.Evaluate(time);
            textTransform.localScale = Vector3.Lerp(targetScale, startScale, curveValue);
            yield return null;
        }
        textTransform.localScale = startScale;
        bounceAnimationCoroutine = null;
    }

    private void InitializeText()
    {
        if (textMesh != null)
        {
            MeshRenderer textRenderer = textMesh.GetComponent<MeshRenderer>();
            textRenderer.sortingLayerName = sortingLayerName;
            textRenderer.sortingOrder = sortingOrder;
        }
        if (outlineTextMesh != null)
        {
            MeshRenderer outlineRenderer = outlineTextMesh.GetComponent<MeshRenderer>();
            outlineRenderer.sortingLayerName = sortingLayerName;
            outlineRenderer.sortingOrder = sortingOrder - 1;
        }
    }

    private void UpdateButtonText()
    {
        if (textMesh != null)
        {
            textMesh.text = buttonText;
        }
        if (outlineTextMesh != null)
        {
            outlineTextMesh.text = buttonText;
        }
        CenterTextAndResizeButton();
    }

    private void CenterTextAndResizeButton()
    {
        if (textMesh == null) return;
        Bounds textBounds = textMesh.GetComponent<Renderer>().bounds;
        float textWidth = textBounds.size.x / transform.lossyScale.x;
        float textHeight = textBounds.size.y / transform.lossyScale.y;
        float requiredWidth = textWidth + padding.x * 2;
        float requiredHeight = textHeight + padding.y * 2;
        SetSize(requiredWidth, requiredHeight);
        Vector3 centerPosition = new Vector3(0, 0, -0.1f);
        if (textMesh != null)
        {
            textMesh.transform.localPosition = centerPosition;
            originalTextPosition = centerPosition;
        }
        if (outlineTextMesh != null)
        {
            outlineTextMesh.transform.localPosition = new Vector3(0, 0, 0.1f);
        }
    }

    private void OnMouseDown()
    {
        if (!isInteractable) return;
        spriteRenderer.color = pressedColor;
    }

    private void OnMouseUp()
    {
        if (!isInteractable) return;
        spriteRenderer.color = normalColor;
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (spriteRenderer.bounds.Contains(mousePosition))
        {
            if (enableBounceOnClick && bounceAnimationCoroutine == null)
            {
                bounceAnimationCoroutine = StartCoroutine(BounceAnimation());
            }
            OnClick?.Invoke();
        }
    }

    private void OnMouseExit()
    {
        if (!isInteractable) return;
        spriteRenderer.color = normalColor;
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isInteractable ? normalColor : disabledColor;
        }
    }

    public void SetSize(float width, float height)
    {
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        Vector3 newScale = new Vector3(
            width / spriteSize.x,
            height / spriteSize.y,
            1f
        );
        transform.localScale = newScale;
    }
}