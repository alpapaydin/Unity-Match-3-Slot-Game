using UnityEngine;

[System.Serializable]
public class TileType
{
    public string name;
    public Sprite sprite;
}

public class Tile : MonoBehaviour
{
    public Vector2Int gridPosition { get; set; }
    private SpriteRenderer spriteRenderer;
    private TileType currentType;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetTileType(TileType type)
    {
        currentType = type;
        if (spriteRenderer != null && type != null)
        {
            spriteRenderer.sprite = type.sprite;
        }
    }

    public TileType GetCurrentType()
    {
        return currentType;
    }
}