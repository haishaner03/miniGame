using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>UGUI loadout choice before a run; equipment no longer consumes upgrade choices.</summary>
public sealed class StartingWeaponPicker : MonoBehaviour
{
    [SerializeField] private Button machete, ironBar, bow, hammer, cancel;
    private string scenePath;
    private Action<SurvivorWeaponKind> confirm;
    private Action cancelled;
    private int closedFrame = -1;
    public bool ClosedThisFrame => closedFrame == Time.frameCount;
    private void Awake()
    {
        if (machete != null) machete.onClick.AddListener(() => Begin(SurvivorWeaponKind.Machete));
        if (ironBar != null) ironBar.onClick.AddListener(() => Begin(SurvivorWeaponKind.IronBar));
        if (bow != null) bow.onClick.AddListener(() => Begin(SurvivorWeaponKind.Bow));
        if (hammer != null) hammer.onClick.AddListener(() => Begin(SurvivorWeaponKind.Hammer));
        if (cancel != null) cancel.onClick.AddListener(Cancel);
    }
    public void Show(string path, Action<SurvivorWeaponKind> onConfirm = null, Action onCancel = null)
    {
        scenePath = path; confirm = onConfirm; cancelled = onCancel; gameObject.SetActive(true);
        closedFrame = -1;
        RefreshLockedChoices();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(machete.gameObject);
    }
    public void Close()
    {
        confirm = null; cancelled = null; closedFrame = Time.frameCount; gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void Cancel() { var callback = cancelled; Close(); callback?.Invoke(); }
    private void Begin(SurvivorWeaponKind kind)
    {
        if (!SurvivorMetaProgress.IsWeaponUnlocked(kind)) return;
        var callback = confirm; string path = scenePath; Close();
        if (callback != null) callback(kind); else RunState.StartNewRun(path, null, kind);
    }
    private void RefreshLockedChoices()
    {
        SetLocked(bow, SurvivorMetaProgress.BowUnlocked,
            "猎弓\n伤害 200 · 冷却 0.50 秒\n鼠标瞄准 · 基础穿透 1 只", SurvivorMetaProgress.BowCost);
        SetLocked(hammer, SurvivorMetaProgress.HammerUnlocked,
            "重锤\n伤害 145 · 冷却 0.65 秒\n大范围重击 · 强力击退", SurvivorMetaProgress.HammerCost);
    }
    private static void SetLocked(Button button, bool unlocked, string caption, int cost)
    {
        if (button == null) return;
        button.interactable = unlocked;
        var label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = unlocked ? caption : caption.Split('\n')[0] + " · 未解锁\n主菜单 · 幸存者成长\n需要 " + cost + " 点";
    }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Cancel(); }
    public void Build(Font font, Sprite blade, Sprite bar)
    {
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 350;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = ZombieHudTheme.ReferenceSize; scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var root = GetComponent<RectTransform>();
        var shade = Widget("Shade",root,Vector2.zero,Vector2.zero); shade.anchorMin=Vector2.zero; shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(0,.02f,.01f,.85f);
        var panel = Widget("Loadout", root,Vector2.zero,new Vector2(640,450));
        panel.gameObject.AddComponent<Image>().color=ZombieHudTheme.Panel;
        Text("选择起始武器",panel,font,28,new Vector2(0,175),new Vector2(580,50));
        Text("每局固定装备 · 升级决定你的打法",panel,font,17,new Vector2(0,132),new Vector2(580,32));
        machete=Choice("砍刀\n伤害 80 · 冷却 0.28 秒\n出手快，适合连续近战",panel,font,blade,new Vector2(-150,-10));
        ironBar=Choice("铁棍\n伤害 100 · 冷却 0.42 秒\n范围更长，击退更强",panel,font,bar,new Vector2(150,-10));
        cancel=Choice("返回",panel,font,null,new Vector2(0,-172),new Vector2(280,48));
    }
    public void ExpandChoices(Font font, Sprite bowSprite, Sprite hammerSprite)
    {
        var panel = machete.transform.parent as RectTransform;
        panel.sizeDelta = new Vector2(940f, 450f);
        (machete.transform as RectTransform).anchoredPosition = new Vector2(-330f, -10f);
        (ironBar.transform as RectTransform).anchoredPosition = new Vector2(-110f, -10f);
        foreach (var button in new[] { machete, ironBar })
        {
            (button.transform as RectTransform).sizeDelta = new Vector2(210f, 235f);
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = new Vector2(194f, 112f);
            label.fontSize = 16;
        }
        if (bow == null) bow = Choice("猎弓", panel, font, bowSprite, new Vector2(110f, -10f), new Vector2(210f, 235f));
        if (hammer == null) hammer = Choice("重锤", panel, font, hammerSprite, new Vector2(330f, -10f), new Vector2(210f, 235f));
        bow.name = "BowChoice"; hammer.name = "HammerChoice";
        foreach (var button in new[] { bow, hammer })
        {
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = new Vector2(194f, 112f);
            label.fontSize = 16;
        }
        RefreshLockedChoices();
    }
    private static RectTransform Widget(string name,Transform parent,Vector2 position,Vector2 size)
    {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;}
    private static void Text(string value,Transform parent,Font font,int size,Vector2 position,Vector2 dimensions)
    {var r=Widget("Label",parent,position,dimensions);var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.alignment=TextAnchor.MiddleCenter;t.color=ZombieHudTheme.Text;t.raycastTarget=false;}
    private static Button Choice(string caption,Transform parent,Font font,Sprite sprite,Vector2 position,Vector2? dimensions=null)
    {
        var r=Widget("Choice",parent,position,dimensions??new Vector2(280,235));var image=r.gameObject.AddComponent<Image>();image.color=new Color(.16f,.21f,.16f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        if(sprite!=null){var icon=Widget("Icon",r,new Vector2(0,55),new Vector2(70,100)).gameObject.AddComponent<Image>();icon.sprite=sprite;icon.preserveAspect=true;icon.raycastTarget=false;}
        Text(caption,r,font,18,sprite!=null?new Vector2(0,-55):Vector2.zero,sprite!=null?new Vector2(260,92):r.sizeDelta);
        return b;
    }
}
