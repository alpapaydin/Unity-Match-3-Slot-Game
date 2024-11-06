using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GameButton : MonoBehaviour
{
    [Header("Button Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // References
    private SpriteRenderer spriteRenderer;

    // State
    private bool isInteractable = true;
    public bool IsInteractable
    {
        get => isInteractable;
        set
        {
            isInteractable = value;
            UpdateVisualState();
        }
    }

    // Events
    public System.Action OnClick;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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

        // Only trigger if mouse is still over button
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (spriteRenderer.bounds.Contains(mousePosition))
        {
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
        // Get the current sprite size
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

        // Calculate scale needed to reach desired size
        Vector3 newScale = new Vector3(
            width / spriteSize.x,
            height / spriteSize.y,
            1f
        );

        transform.localScale = newScale;
    }
}