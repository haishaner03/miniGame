using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时生成的像素风世界空间 uGUI 血条，玩家和丧尸共用。
/// Canvas 使用 World Space，Image 使用运行时生成的 1x1 像素 Sprite。
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private float width = 0.82f;
    [SerializeField] private float height = 0.095f;
    [SerializeField] private float zombieHeight = 0.035f;
    [SerializeField] private float zombieOutlinePadding = 0.02f;
    [SerializeField] private float offsetY = 0.72f;
    [SerializeField] private bool hideWhenDead = true;
    [SerializeField] private Color fillColor = new Color(0.3f, 1f, 0.35f, 1f);
    [SerializeField] private Color zombieFillColor = new Color(1f, 0.2f, 0.16f, 1f);

    private static Sprite pixelSprite;
    private SurvivorHealth playerHealth;
    private ZombieChaser zombieHealth;
    private RectTransform barRoot;
    private Image fillImage;
    private bool isZombie;

    private void Awake()
    {
        playerHealth = GetComponent<SurvivorHealth>();
        zombieHealth = GetComponent<ZombieChaser>();
        isZombie = zombieHealth != null;
        CreateVisuals();
    }

    private void LateUpdate()
    {
        if (fillImage == null)
            return;

        int current;
        int max;
        bool dead;

        if (isZombie && zombieHealth != null)
        {
            current = zombieHealth.CurrentHealth;
            max = Mathf.Max(1, zombieHealth.CurrentMaxHealth);
            dead = current <= 0;
        }
        else if (playerHealth != null)
        {
            current = playerHealth.CurrentHealth;
            max = Mathf.Max(1, playerHealth.MaxHealth);
            dead = playerHealth.IsDead && playerHealth.IsGameOver;
        }
        else
        {
            return;
        }

        if (hideWhenDead && dead)
        {
            if (barRoot.gameObject.activeSelf)
                barRoot.gameObject.SetActive(false);
            return;
        }

        if (!barRoot.gameObject.activeSelf)
            barRoot.gameObject.SetActive(true);

        float normalized = Mathf.Clamp01(current / (float)max);
        fillImage.fillAmount = normalized;
        fillImage.color = isZombie ? zombieFillColor : fillColor;
    }

    private void CreateVisuals()
    {
        float barHeight = isZombie ? zombieHeight : height;
        float outlinePadding = isZombie ? zombieOutlinePadding : 0.06f;
        if (pixelSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "RuntimeHealthBarPixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            pixelSprite.name = "RuntimeHealthBarPixelSprite";
        }

        GameObject rootObject = new GameObject("WorldHealthBarCanvas", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "Default";
        canvas.sortingOrder = 7000;
        canvas.pixelPerfect = true;

        barRoot = rootObject.GetComponent<RectTransform>();
        barRoot.SetParent(transform, false);
        barRoot.sizeDelta = new Vector2(width, barHeight);

        // 抵消角色自身缩放，保证玩家和缩小后的丧尸血条尺寸一致；
        // 位置也按父物体缩放反算，避免玩家血条被整体抬高。
        Vector3 scale = transform.lossyScale;
        float scaleX = Mathf.Max(0.01f, Mathf.Abs(scale.x));
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(scale.y));
        barRoot.localScale = new Vector3(1f / scaleX, 1f / scaleY, 1f);
        barRoot.localPosition = new Vector3(0f, offsetY / scaleY, 0f);

        CreateImage("Outline", new Vector2(width + outlinePadding, barHeight + outlinePadding), new Color(0.02f, 0.02f, 0.025f, 0.95f));
        CreateImage("Background", new Vector2(width, barHeight), new Color(0.16f, 0.04f, 0.04f, 1f));
        fillImage = CreateImage("Fill", new Vector2(width, barHeight), fillColor);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 1f;
    }

    private Image CreateImage(string objectName, Vector2 size, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(barRoot, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = child.GetComponent<Image>();
        image.sprite = pixelSprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void OnDestroy()
    {
        if (barRoot != null)
            Destroy(barRoot.gameObject);
    }
}
