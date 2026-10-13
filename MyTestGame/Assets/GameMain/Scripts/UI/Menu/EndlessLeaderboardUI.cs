using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Editable UGUI local top-six panel, using the menu's existing palette.</summary>
public sealed class EndlessLeaderboardUI : MonoBehaviour
{
    [SerializeField] private Text[] ranks, weapons, times, kills;
    [SerializeField] private Button close;
    private Action closed;

    private void Awake() { UiButtonSound.Ensure(close); close.onClick.AddListener(Close); }
    public void Show(Action onClose = null)
    {
        closed = onClose;
        gameObject.SetActive(true);
        var entries = EndlessLeaderboard.Load();
        for (int i = 0; i < ranks.Length; i++)
        {
            ranks[i].text = (i + 1).ToString("00");
            weapons[i].text = i < entries.Count ? EndlessLeaderboard.WeaponName(entries[i].weapon) : "——";
            times[i].text = i < entries.Count ? EndlessLeaderboard.FormatTime(entries[i].seconds) : "暂无记录";
            kills[i].text = i < entries.Count ? entries[i].kills.ToString() : "——";
            Color color = i == 0 && entries.Count > 0 ? ZombieHudTheme.Accent : ZombieHudTheme.Text;
            ranks[i].color = weapons[i].color = times[i].color = kills[i].color = color;
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
    }
    public void Close()
    {
        gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        var callback = closed; closed = null; callback?.Invoke();
    }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Close(); }

    public void Build(Font font)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 340;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ZombieHudTheme.ReferenceSize; scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var shade = Widget("Shade", transform, Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(0, .02f, .01f, .86f);
        var panel = Widget("Leaderboard", transform, Vector2.zero, new Vector2(860, 566));
        panel.gameObject.AddComponent<Image>().color = ZombieHudTheme.Panel;
        Label("无尽生存榜", panel, font, 30, new Vector2(0, 230), new Vector2(760, 50));
        Label("本地最佳 6 局 · 仅计算战斗时间，暂停和选卡不计时", panel, font, 16, new Vector2(0, 185), new Vector2(780, 30));
        float[] positions = { -300, -145, 85, 295 };
        string[] headings = { "排名", "初始武器", "生存时间", "本局击杀" };
        for (int i = 0; i < headings.Length; i++) Label(headings[i], panel, font, 18, new Vector2(positions[i], 138), new Vector2(180, 32));
        ranks = new Text[EndlessLeaderboard.Capacity]; weapons = new Text[ranks.Length];
        times = new Text[ranks.Length]; kills = new Text[ranks.Length];
        for (int i = 0; i < ranks.Length; i++)
        {
            var line = Widget("Record_" + (i + 1), panel, new Vector2(0, 93 - i * 47), new Vector2(780, 40));
            line.gameObject.AddComponent<Image>().color = i % 2 == 0 ? new Color(.16f, .20f, .15f, .65f) : new Color(.1f, .13f, .1f, .65f);
            ranks[i] = Label("00", line, font, 20, new Vector2(positions[0], 0), new Vector2(180, 38));
            weapons[i] = Label("——", line, font, 20, new Vector2(positions[1], 0), new Vector2(180, 38));
            times[i] = Label("暂无记录", line, font, 20, new Vector2(positions[2], 0), new Vector2(180, 38));
            kills[i] = Label("——", line, font, 20, new Vector2(positions[3], 0), new Vector2(180, 38));
        }
        var button = Widget("Close", panel, new Vector2(0, -228), new Vector2(240, 48));
        button.gameObject.AddComponent<Image>().color = new Color(.23f, .29f, .20f);
        close = button.gameObject.AddComponent<Button>(); close.targetGraphic = button.GetComponent<Image>();
        UiButtonSound.Ensure(close);
        Label("返回", button, font, 22, Vector2.zero, button.sizeDelta);
    }
    private static RectTransform Widget(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static Text Label(string value, Transform parent, Font font, int size, Vector2 position, Vector2 dimensions)
    {
        var rect = Widget("Label", parent, position, dimensions); var text = rect.gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.text = value; text.alignment = TextAnchor.MiddleCenter;
        text.color = ZombieHudTheme.Text; text.raycastTarget = false; return text;
    }
}
