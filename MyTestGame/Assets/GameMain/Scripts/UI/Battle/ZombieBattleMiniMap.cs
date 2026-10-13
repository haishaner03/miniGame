using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>Transparent compass map showing only the survivor and the room exit.</summary>
[DisallowMultipleComponent]
public sealed class ZombieBattleMiniMap : MonoBehaviour
{
    private const float RootSize = 176f;
    private const float CircleSize = 126f;
    private const float MapRadius = 47f;
    private const float Edge = 16f;
    private const float TopOffset = 104f;

    private static Sprite circleSprite;
    private static Sprite diamondSprite;

    private RunState run;
    private RectTransform root;
    private RectTransform playerMarker;
    private RectTransform exitMarker;
    private LevelFlowController room;
    private Bounds worldBounds;
    private bool hasWorldBounds;
    private bool boundsCalculated;

    public void Initialize(RunState state, RectTransform canvasRect)
    {
        run = state;
        if (run == null || run.IsEndless) return;
        if (root == null) BuildUi(canvasRect);
    }

    private void Update()
    {
        if (run == null) return;
        if (run.IsEndless)
        {
            if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }
        if (root == null) return;

        LevelFlowController currentRoom = run.CurrentRoom;
        bool visible = currentRoom != null && run.Player != null &&
            run.Phase != RunState.RunPhase.Loading &&
            run.Phase != RunState.RunPhase.Won &&
            run.Phase != RunState.RunPhase.Lost;
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        if (!visible) return;

        if (room != currentRoom)
        {
            room = currentRoom;
            boundsCalculated = false;
        }
        if (!boundsCalculated)
        {
            hasWorldBounds = CalculateWorldBounds(room, out worldBounds);
            boundsCalculated = true;
        }

        // The mini map is player-centered: the survivor stays at the compass center
        // while the exit moves relative to the survivor's current world position.
        Vector2 playerPosition = Vector2.zero;
        Vector2 exitPosition = room.ExitDoor != null
            ? MapOffset(room.ExitDoor.transform.position - run.Player.transform.position)
            : playerPosition;
        if (hasWorldBounds && (playerPosition - exitPosition).sqrMagnitude < 18f * 18f)
            exitPosition += Vector2.left * 9f;

        playerMarker.anchoredPosition = playerPosition;
        exitMarker.anchoredPosition = exitPosition;
    }

    private void BuildUi(RectTransform canvasRect)
    {
        root = MakeRect("MiniMap", canvasRect, new Vector2(-Edge, -TopOffset), new Vector2(RootSize, RootSize));
        root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
        CanvasGroup inputPassthrough = root.gameObject.AddComponent<CanvasGroup>();
        inputPassthrough.interactable = false;
        inputPassthrough.blocksRaycasts = false;

        Image outerRing = MakeImage("CompassRing", root, Vector2.zero, Vector2.one * CircleSize, Circle);
        CenterOnMap(outerRing.rectTransform);
        outerRing.rectTransform.anchoredPosition = Vector2.zero;
        outerRing.color = new Color(0.72f, 0.65f, 0.43f, 0.72f);
        Image inner = MakeImage("TransparentInterior", root, Vector2.zero, Vector2.one * (CircleSize - 4f), Circle);
        CenterOnMap(inner.rectTransform);
        inner.rectTransform.anchoredPosition = Vector2.zero;
        inner.color = new Color(0.025f, 0.035f, 0.03f, 0.09f);

        TMP_FontAsset font = FindHudFont(canvasRect);
        MakeDirection(root, "North", "N", new Vector2(0f, 76f), font);
        MakeDirection(root, "East", "E", new Vector2(76f, 0f), font);
        MakeDirection(root, "South", "S", new Vector2(0f, -76f), font);
        MakeDirection(root, "West", "W", new Vector2(-76f, 0f), font);

        playerMarker = MakeRect("PlayerMarker", root, Vector2.zero, new Vector2(18f, 18f));
        CenterOnMap(playerMarker);
        playerMarker.anchoredPosition = Vector2.zero;
        Image playerOutline = playerMarker.gameObject.AddComponent<Image>();
        playerOutline.sprite = Circle;
        playerOutline.color = new Color(0.92f, 0.95f, 0.82f, 1f);
        playerOutline.raycastTarget = false;
        RectTransform playerCore = MakeRect("PlayerCore", playerMarker, Vector2.zero, new Vector2(10f, 10f));
        CenterOnMap(playerCore);
        Image playerFill = playerCore.gameObject.AddComponent<Image>();
        playerFill.sprite = Circle;
        playerFill.color = new Color(0.29f, 0.92f, 0.47f, 1f);
        playerFill.raycastTarget = false;

        exitMarker = MakeRect("ExitMarker", root, Vector2.zero, new Vector2(19f, 19f));
        CenterOnMap(exitMarker);
        exitMarker.anchoredPosition = Vector2.zero;
        Image exitIcon = exitMarker.gameObject.AddComponent<Image>();
        exitIcon.sprite = Diamond;
        exitIcon.color = new Color(1f, 0.77f, 0.28f, 1f);
        exitIcon.raycastTarget = false;
        TextMeshProUGUI exitLabel = MakeText("ExitGlyph", exitMarker, "X", font, 9f);
        exitLabel.color = new Color(0.14f, 0.11f, 0.06f, 1f);
        exitLabel.fontStyle = FontStyles.Bold;
    }

    private Vector2 MapOffset(Vector3 offset)
    {
        if (!hasWorldBounds) return Vector2.zero;
        Vector2 size = new Vector2(worldBounds.size.x, worldBounds.size.y);
        float maxSize = Mathf.Max(Mathf.Max(size.x, size.y), 1f);
        Vector2 relative = new Vector2(offset.x, offset.y);
        return Vector2.ClampMagnitude(relative * (MapRadius * 2f / maxSize), MapRadius);
    }

    private static bool CalculateWorldBounds(LevelFlowController flow, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        Scene scene = flow.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                TilemapRenderer[] renderers = roots[i].GetComponentsInChildren<TilemapRenderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    TilemapRenderer renderer = renderers[j];
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Bounds tileBounds = renderer.bounds;
                    if (tileBounds.size.x < 0.01f || tileBounds.size.y < 0.01f) continue;
                    Include(ref bounds, ref found, tileBounds.min);
                    Include(ref bounds, ref found, tileBounds.max);
                }
            }
        }

        if (flow.StartDoor != null) Include(ref bounds, ref found, flow.StartDoor.transform.position);
        if (flow.ExitDoor != null) Include(ref bounds, ref found, flow.ExitDoor.transform.position);
        if (!found) return false;

        Vector3 size = bounds.size;
        if (size.x < 1f) { bounds.min -= Vector3.right * (1f - size.x) * 0.5f; bounds.max += Vector3.right * (1f - size.x) * 0.5f; }
        if (size.y < 1f) { bounds.min -= Vector3.up * (1f - size.y) * 0.5f; bounds.max += Vector3.up * (1f - size.y) * 0.5f; }
        return true;
    }

    private static void Include(ref Bounds bounds, ref bool found, Vector3 point)
    {
        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
        else bounds.Encapsulate(point);
    }

    private static void MakeDirection(Transform parent, string name, string value, Vector2 position, TMP_FontAsset font)
    {
        RectTransform rect = MakeRect(name, parent, position, new Vector2(22f, 20f));
        CenterOnMap(rect);
        rect.anchoredPosition = position;
        TextMeshProUGUI label = MakeText("Label", rect, value, font, 11f);
        label.color = ZombieHudTheme.Text;
        label.fontStyle = FontStyles.Bold;
    }

    private static TMP_FontAsset FindHudFont(RectTransform canvasRect)
    {
        TMP_Text[] texts = canvasRect.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length > 0 ? texts[0].font : null;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string value, TMP_FontAsset font, float size)
    {
        RectTransform parentRect = parent as RectTransform;
        Vector2 dimensions = parentRect != null ? parentRect.rect.size : Vector2.one * 100f;
        RectTransform rect = MakeRect(name, parent, Vector2.zero, dimensions);
        CenterOnMap(rect);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.color = ZombieHudTheme.Text;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void CenterOnMap(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image MakeImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
    {
        RectTransform rect = MakeRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite Circle
    {
        get
        {
            if (circleSprite == null) circleSprite = CreateShape("MiniMapCircle", false);
            return circleSprite;
        }
    }

    private static Sprite Diamond
    {
        get
        {
            if (diamondSprite == null) diamondSprite = CreateShape("MiniMapDiamond", true);
            return diamondSprite;
        }
    }

    private static Sprite CreateShape(string name, bool diamond)
    {
        const int resolution = 64;
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = name + "Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = Mathf.Abs(x + 0.5f - resolution * 0.5f);
            float dy = Mathf.Abs(y + 0.5f - resolution * 0.5f);
            float distance = diamond ? dx + dy : Mathf.Sqrt(dx * dx + dy * dy);
            byte alpha = (byte)(Mathf.Clamp01(resolution * 0.5f - 1f - distance) * 255f);
            pixels[y * resolution + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
