using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SurvivorDepthSort : MonoBehaviour
{
    [SerializeField] private float groundOffsetY;
    [SerializeField] private int orderOffset;

    private SpriteRenderer spriteRenderer;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        RefreshOrder();
    }

    private void LateUpdate()
    {
        RefreshOrder();
    }

    public void Configure(float offsetY, int offset = 0)
    {
        groundOffsetY = offsetY;
        orderOffset = offset;
        spriteRenderer = GetComponent<SpriteRenderer>();
        RefreshOrder();
    }

    public void RefreshOrder()
    {
        if (spriteRenderer == null) return;
        float groundY = transform.position.y + groundOffsetY;
        spriteRenderer.sortingOrder = Mathf.Clamp(6000 - Mathf.RoundToInt(groundY * 100f) + orderOffset, 1, 32767);
    }
}
