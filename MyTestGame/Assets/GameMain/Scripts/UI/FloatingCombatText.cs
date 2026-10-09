using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间 UGUI 飘字。伤害数字从命中点向上漂移并渐隐，适合通过预制体复用。
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingCombatText : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private Color defaultColor = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private float lifetime = 0.62f;
    [SerializeField] private float riseSpeed = 0.85f;
    [SerializeField] private float horizontalJitter = 0.22f;
    [SerializeField] private float worldScale = 0.01f;

    private CanvasGroup canvasGroup;
    private float elapsed;
    private Vector3 velocity;

    private void Awake()
    {
        EnsureVisuals();
        velocity = new Vector3(Random.Range(-horizontalJitter, horizontalJitter), riseSpeed, 0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, lifetime));
        if (canvasGroup != null)
            canvasGroup.alpha = 1f - normalized;

        if (normalized >= 1f)
            Destroy(gameObject);
    }

    public void Initialize(string message, Color color)
    {
        EnsureVisuals();
        if (label != null)
        {
            label.text = message;
            label.color = color;
        }
    }

    public static void Spawn(GameObject prefab, Vector3 worldPosition, string message, Color color)
    {
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, worldPosition, Quaternion.identity);
        FloatingCombatText text = instance.GetComponent<FloatingCombatText>();
        if (text != null)
            text.Initialize(message, color);
    }

    private void EnsureVisuals()
    {
        if (label == null)
            label = GetComponentInChildren<Text>(true);

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "Default";
        canvas.sortingOrder = 9000;
        canvas.pixelPerfect = true;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();
        root.sizeDelta = new Vector2(180f, 60f);
        root.localScale = Vector3.one * Mathf.Max(0.001f, worldScale);

        if (label == null)
        {
            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(transform, false);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 42;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        if (label.color.a <= 0.001f)
            label.color = defaultColor;
    }
}
