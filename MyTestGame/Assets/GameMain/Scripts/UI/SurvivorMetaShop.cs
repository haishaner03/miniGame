using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable UGUI modal for permanent unlocks, sharing the battle UI theme.</summary>
public sealed class SurvivorMetaShop : MonoBehaviour
{
    [SerializeField] private Text balance, message;
    [SerializeField] private Button bow, hammer, cards, vitality, close;
    private Action onClosed;

    private void Awake()
    {
        bow.onClick.AddListener(() => Buy(SurvivorMetaProgress.TryUnlockBow, "猎弓已解锁，开局时可以选择。"));
        hammer.onClick.AddListener(() => Buy(SurvivorMetaProgress.TryUnlockHammer, "重锤已解锁，开局时可以选择。"));
        cards.onClick.AddListener(() => Buy(SurvivorMetaProgress.TryUnlockBowCards, "6 张进阶弓箭卡已加入弓箭卡池。"));
        vitality.onClick.AddListener(() => Buy(SurvivorMetaProgress.TryBuyVitality, "永久初始生命 +5%，下一局生效。"));
        close.onClick.AddListener(Close);
    }
    public void Show(Action callback = null)
    {
        onClosed = callback;
        gameObject.SetActive(true);
        message.text = "每个通关房间 +20 点 · 每 5 击杀 +1 点 · 整局通关额外 +50 点";
        Refresh();
    }
    private void Buy(Func<bool> purchase, string success)
    {
        message.text = purchase() ? success : "幸存者点数不足，或尚未满足解锁条件。";
        Refresh();
    }
    private void Refresh()
    {
        balance.text = "幸存者点数  " + SurvivorMetaProgress.Points;
        Configure(bow, SurvivorMetaProgress.BowUnlocked, SurvivorMetaProgress.BowCost);
        Configure(hammer, SurvivorMetaProgress.HammerUnlocked, SurvivorMetaProgress.HammerCost);
        Configure(cards, SurvivorMetaProgress.BowCardsUnlocked, SurvivorMetaProgress.BowCardsCost, SurvivorMetaProgress.BowUnlocked);
        Configure(vitality, SurvivorMetaProgress.VitalityUnlocked, SurvivorMetaProgress.VitalityCost);
    }
    private static void Configure(Button button, bool unlocked, int cost, bool prerequisite = true)
    {
        button.interactable = !unlocked && prerequisite && SurvivorMetaProgress.Points >= cost;
        button.GetComponentInChildren<Text>().text = unlocked ? "已解锁" : prerequisite ? "解锁 · " + cost + " 点" : "先解锁猎弓";
    }
    public void Close()
    {
        var callback = onClosed; onClosed = null;
        gameObject.SetActive(false);
        callback?.Invoke();
    }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Close(); }

    public void Build(Font font, Sprite bowSprite, Sprite hammerSprite)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 360;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ZombieHudTheme.ReferenceSize; scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform shade = Widget("Shade", transform, Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(0f, 0.015f, 0.01f, 0.88f);
        var panel = Widget("SurvivorGrowth", transform, Vector2.zero, new Vector2(1020f, 570f));
        panel.gameObject.AddComponent<Image>().color = ZombieHudTheme.Panel;
        Label("Title", "幸存者成长", panel, font, 30, new Vector2(0f, 230f), new Vector2(920f, 48f));
        balance = Label("Balance", "幸存者点数  0", panel, font, 20, new Vector2(0f, 180f), new Vector2(920f, 34f));
        bow = Card("BowUnlock", "猎弓", "远程瞄准 · 伤害 200\n默认穿透 1 只丧尸\n开放 6 张基础弓箭卡", panel, font, bowSprite, -360f);
        hammer = Card("HammerUnlock", "重锤", "大范围近战重击\n伤害 145 · 强力击退\n适合控制尸群", panel, font, hammerSprite, -120f);
        cards = Card("BowCardsUnlock", "弓箭进阶卡包", "加入 6 张专属联动卡\n散射 / 暴击 / 远猎\n只在猎弓局中抽取", panel, font, bowSprite, 120f);
        vitality = Card("VitalityUnlock", "强健体魄", "永久初始生命 +5%\n基础生命 100 → 105\n仅解锁一次", panel, font, null, 360f);
        message = Label("Message", "", panel, font, 16, new Vector2(0f, -194f), new Vector2(950f, 42f));
        close = MakeButton("Close", "返回主菜单", panel, font, new Vector2(0f, -250f), new Vector2(300f, 44f));
    }
    private static Button Card(string name, string heading, string description, Transform parent, Font font, Sprite sprite, float x)
    {
        var card = Widget(name, parent, new Vector2(x, -6f), new Vector2(220f, 300f));
        card.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.10f, 0.075f, 1f);
        Label("Heading", heading, card, font, 20, new Vector2(0f, 118f), new Vector2(208f, 32f));
        if (sprite != null)
        {
            var icon = Widget("Icon", card, new Vector2(0f, 50f), new Vector2(70f, 80f)).gameObject.AddComponent<Image>();
            icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
        }
        else Label("Bonus", "+5% HP", card, font, 28, new Vector2(0f, 48f), new Vector2(208f, 60f)).color = ZombieHudTheme.Accent;
        Label("Description", description, card, font, 16, new Vector2(0f, -34f), new Vector2(208f, 96f));
        return MakeButton("Purchase", "解锁", card, font, new Vector2(0f, -115f), new Vector2(196f, 44f));
    }
    private static RectTransform Widget(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static Text Label(string name, string caption, Transform parent, Font font, int size, Vector2 position, Vector2 dimensions)
    {
        var label = Widget(name, parent, position, dimensions).gameObject.AddComponent<Text>();
        label.font = font; label.fontSize = size; label.text = caption; label.alignment = TextAnchor.MiddleCenter;
        label.color = ZombieHudTheme.Text; label.raycastTarget = false; return label;
    }
    private static Button MakeButton(string name, string caption, Transform parent, Font font, Vector2 position, Vector2 size)
    {
        var rect = Widget(name, parent, position, size); var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.26f, 0.16f, 1f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Label("Label", caption, rect, font, 18, Vector2.zero, size); return button;
    }
}
