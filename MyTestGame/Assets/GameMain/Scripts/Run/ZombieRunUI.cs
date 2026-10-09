using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Editable UGUI battle prefab. All widget references are baked by ZombieHudSetup.</summary>
[DisallowMultipleComponent]
public sealed class ZombieRunUI : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private Sprite[] icons = new Sprite[17];
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Text healthText, roomText, objectiveText, waveText, timeText, killsText;
    [SerializeField] private Image healthFill;
    [SerializeField] private GameObject encounterPanel;
    [SerializeField] private Text encounterName, encounterHealth, encounterAction;
    [SerializeField] private Image encounterFill;
    [SerializeField] private Text levelText, experienceText, weaponText;
    [SerializeField] private Image experienceFill;
    [SerializeField] private Button pauseButton;
    [SerializeField] private RectTransform upgradeRow, upgradeContent;
    [SerializeField] private GameObject upgradeTemplate;
    [SerializeField] private ScrollRect upgradeScroll;
    [SerializeField] private Text branchSummary;
    [SerializeField] private Text upgradeScrollHint;
    private sealed class UpgradeWidget { public int id; public RectTransform rect; public Text count; }
    private readonly List<UpgradeWidget> upgradeWidgets = new List<UpgradeWidget>();
    [SerializeField] private Text upgradeHint;
    [SerializeField] private Image meleeMask, dashMask;
    [SerializeField] private Image equippedWeaponIcon;
    [SerializeField] private Text meleeStatus, dashStatus;
    [SerializeField] private GameObject modal;
    [SerializeField] private RectTransform window;
    [SerializeField] private Text title, detail;
    [SerializeField] private ScrollRect detailScroll;
    [SerializeField] private Button[] choices = new Button[3];
    [SerializeField] private Text[] choiceLabels = new Text[3];
    [SerializeField] private Image[] choiceIcons = new Image[3];
    [SerializeField] private Button resume, restart, menu;
    private RunState run;
    private SurvivorHealth boundPlayer;
    private SurvivorMeleeAttack melee;
    private SurvivorDash dash;
    private GameObject ownedEventSystem;
    private float nextRefresh;
    private readonly StringBuilder buffer = new StringBuilder(512);

    public void Initialize(RunState state)
    {
        run = state;
        if (canvasRect == null) BuildLayout(run.Config.uiFont, icons);
        BuildUpgradeWidgets();
        pauseButton.onClick.AddListener(run.TogglePause);
        resume.onClick.AddListener(run.TogglePause);
        restart.onClick.AddListener(run.RestartRun);
        menu.onClick.AddListener(run.ReturnToMenu);
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            choices[i].onClick.AddListener(() =>
            {
                if (index < run.OfferedUpgrades.Count)
                    run.ChooseUpgrade(run.OfferedUpgrades[index].Id);
            });
        }
        HideModal();
    }

    public void EnsureEventSystem()
    {
        if (ownedEventSystem != null) { ownedEventSystem.SetActive(true); return; }
        if (FindFirstObjectByType<EventSystem>() != null) return;
        ownedEventSystem = new GameObject("RunEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        ownedEventSystem.transform.SetParent(transform, false);
    }
    public void DisableOwnedEventSystem() { if (ownedEventSystem != null) ownedEventSystem.SetActive(false); }

    private void Update()
    {
        if (run == null) return;
        if (Input.GetKeyDown(KeyCode.Escape) && !run.SuppressPauseInput) run.TogglePause();
        if (boundPlayer != run.Player)
        {
            boundPlayer = run.Player;
            melee = boundPlayer != null ? boundPlayer.GetComponent<SurvivorMeleeAttack>() : null;
            dash = boundPlayer != null ? boundPlayer.GetComponent<SurvivorDash>() : null;
        }
        UpdateCooldown(meleeMask, meleeStatus, melee != null ? melee.CooldownRemaining : 0f, melee != null ? melee.AttackCooldown : 1f);
        UpdateCooldown(dashMask, dashStatus, dash != null ? dash.CooldownRemaining : 0f, dash != null ? dash.DashCooldown : 1f);
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        RefreshHud();
    }

    private void RefreshHud()
    {
        healthText.text = "生命  " + Mathf.Max(0, run.CurrentHealth) + " / " + run.MaxHealth;
        float ratio = run.MaxHealth > 0 ? Mathf.Clamp01(run.CurrentHealth / (float)run.MaxHealth) : 0f;
        healthFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        healthFill.color = ratio <= 0.3f ? ZombieHudTheme.Critical : ZombieHudTheme.Accent;
        healthText.color = ratio <= 0.3f ? ZombieHudTheme.Critical : ZombieHudTheme.Text;
        levelText.text = "等级 " + run.Level;
        experienceText.text = "经验 " + run.Experience + " / " + run.ExperienceToNextLevel;
        experienceFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(run.Experience / (float)run.ExperienceToNextLevel), 1f);
        weaponText.text = "武器 · " + run.WeaponName;
        if (melee != null && melee.EquippedWeapon != null && equippedWeaponIcon != null)
        {
            equippedWeaponIcon.sprite = melee.EquippedWeapon.sprite;
            equippedWeaponIcon.preserveAspect = true;
            meleeMask.sprite = equippedWeaponIcon.sprite;
            meleeMask.preserveAspect = true;
        }
        roomText.text = "房间 " + run.RoomNumber + " / " + run.RoomCount;
        var encounter = run.Encounter;
        bool encounterActive = encounter != null && encounter.IsRequired && encounter.Stage != RoomEncounterController.EncounterStage.Waiting;
        objectiveText.text = run.RewardClaimed ? "出口已解锁 · 前往安全门" : encounterActive ? "击败" + encounter.EnemyName :
            encounter != null && encounter.IsRequired ? "清理街区 · " + (encounter.IsBoss ? "决战将至" : "精英将至") : "清理街区";
        LevelFlowController flow = run.CurrentRoom;
        waveText.text = flow != null && !run.RewardClaimed
            ? "清理 " + run.RoomKills + "/" + flow.RequiredKills + (flow.TotalObjectiveWaves > 0 ? " · 区域 " + flow.CompletedObjectiveWaves + "/" + flow.TotalObjectiveWaves : string.Empty)
            : "向下一个房间进发";
        if (encounterActive && !run.RewardClaimed) waveText.text = encounter.IsBoss ? "最终遭遇 · 留意地面预警" : "精英遭遇 · 留意冲锋预警";
        RefreshEncounter(encounter);
        timeText.text = "生存 " + FormatTime(run.ElapsedSeconds);
        killsText.text = "击杀 " + run.Kills;
        pauseButton.interactable = run.Phase == RunState.RunPhase.Playing;
        upgradeHint.gameObject.SetActive(run.UpgradeStacks.Count == 0);
        branchSummary.text = "快攻 " + run.BranchStacks("Quick") + "   冰冻 " + run.BranchStacks("Frost") + "   燃烧 " + run.BranchStacks("Fire");
        int visibleSlot = 0;
        foreach (var widget in upgradeWidgets)
        {
            int count = run.StackCount(widget.id);
            widget.rect.gameObject.SetActive(count > 0);
            if (count > 0)
            {
                widget.rect.anchoredPosition = new Vector2((visibleSlot / 2) * 64f, -(visibleSlot % 2) * 64f);
                visibleSlot++;
            }
            widget.count.text = "×" + count;
        }
        upgradeRow.sizeDelta = new Vector2(Mathf.Max(150f, Mathf.Min(560f, canvasRect.rect.width - 240f)), 154f);
        upgradeContent.sizeDelta = new Vector2(Mathf.Max(upgradeScroll.viewport.rect.width, Mathf.Ceil(visibleSlot / 2f) * 64f), 120f);
        upgradeScroll.horizontal = upgradeContent.rect.width > upgradeScroll.viewport.rect.width + 1f;
        if (upgradeScrollHint != null) upgradeScrollHint.gameObject.SetActive(upgradeScroll.horizontal);
        if (modal.activeSelf) window.sizeDelta = new Vector2(Mathf.Min(760f, canvasRect.rect.width - 48f), 600f);
    }

    private void RefreshEncounter(RoomEncounterController encounter)
    {
        if (encounterPanel == null) return;
        bool show = encounter != null && encounter.IsRequired && encounter.Stage != RoomEncounterController.EncounterStage.Waiting &&
            (encounter.Enemy != null || encounter.Stage == RoomEncounterController.EncounterStage.Defeated) &&
            (encounter.Stage != RoomEncounterController.EncounterStage.Defeated || Time.time - encounter.DefeatedAt < 3f);
        encounterPanel.SetActive(show);
        if (!show) return;
        var enemy = encounter.Enemy;
        bool defeated = encounter.Stage == RoomEncounterController.EncounterStage.Defeated;
        int hp = !defeated && enemy != null ? enemy.Health.CurrentHealth : 0;
        int max = enemy != null ? enemy.Health.CurrentMaxHealth : 1;
        encounterName.text = (encounter.IsBoss ? "最终 Boss · " : "精英 · ") + encounter.EnemyName +
            (!defeated && enemy != null && enemy.IsEnraged ? "  [暴怒]" : "");
        encounterHealth.text = defeated ? "已击败" : hp + " / " + max;
        encounterFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hp / (float)Mathf.Max(1,max)),1);
        encounterFill.color = encounter.IsBoss ? ZombieHudTheme.Critical : new Color(1,.65f,.2f);
        encounterAction.text = defeated ? "出口已解锁 · 前往安全门" : enemy.ActionLabel;
        encounterAction.color = enemy != null && enemy.TelegraphVisible ? ZombieHudTheme.Critical : ZombieHudTheme.Text;
    }

    private static void UpdateCooldown(Image mask, Text label, float remaining, float total)
    {
        float amount = Mathf.Clamp01(remaining / Mathf.Max(0.05f, total));
        mask.gameObject.SetActive(amount > 0f);
        mask.fillAmount = amount;
        string value = remaining > 0f ? (Mathf.Ceil(remaining * 10f) / 10f).ToString("0.0") + "s" : "就绪";
        if (label.text != value) label.text = value;
        label.color = remaining > 0f ? ZombieHudTheme.Text : ZombieHudTheme.Accent;
    }

    public void ShowChoices()
    {
        SetModal("升级！选择奖励", "当前等级 " + run.Level + " · 选择一项奖励继续战斗", true, false);
        detail.fontSize = ZombieHudTheme.Body;
        for (int i = 0; i < choices.Length; i++)
        {
            bool available = i < run.OfferedUpgrades.Count;
            choices[i].gameObject.SetActive(available);
            if (!available) continue;
            var upgrade = run.OfferedUpgrades[i];
            choiceLabels[i].text = "[" + RunUpgradePresentation.BranchName(upgrade.Branch) + (string.IsNullOrEmpty(upgrade.RequiredEffect) ? "" : " · 联动") + "] " + upgrade.Name + "   " + (run.StackCount(upgrade.Id) + 1) + "/" + upgrade.MaxStacks + " 层\n" + upgrade.Description;
            choiceIcons[i].sprite = Icon(RunUpgradePresentation.Icon(upgrade));
            choiceIcons[i].color = RunUpgradePresentation.Color(upgrade.Branch);
        }
        Select(choices[0]);
    }

    public void ShowPause()
    {
        buffer.Clear();
        buffer.Append("房间 ").Append(run.RoomNumber).Append(" / ").Append(run.RoomCount)
            .Append("    生存 ").Append(FormatTime(run.ElapsedSeconds)).Append("    击杀 ").Append(run.Kills)
            .Append("\n等级 ").Append(run.Level).Append("    武器 ").Append(run.WeaponName).Append("\n\n已获得升级\n");
        AppendUpgrades(true);
        SetModal("已暂停", buffer.ToString(), false, true);
        detail.fontSize = ZombieHudTheme.Body;
        resume.gameObject.SetActive(true);
        Select(resume);
    }

    public void ShowResult(bool victory)
    {
        buffer.Clear();
        buffer.Append("到达房间   ").Append(run.RoomNumber).Append(" / ").Append(run.RoomCount)
            .Append("       生存时间   ").Append(FormatTime(run.ElapsedSeconds))
            .Append("\n总击杀数   ").Append(run.Kills).Append("       精英击杀   ").Append(run.EliteKills)
            .Append("\n造成伤害   ").Append(run.DamageDealt).Append("       受到伤害   ").Append(run.DamageTaken)
            .Append("\n恢复生命   ").Append(run.HealingReceived).Append("       等级   ").Append(run.Level)
            .Append("       总经验   ").Append(run.TotalExperience)
            .Append("\n强敌击败   ").Append(run.ChampionKills).Append("       Boss 击败   ").Append(run.BossKills).Append("\n\n本局升级\n");
        AppendUpgrades(false);
        buffer.Append("\n\n随机种子   ").Append(run.Seed);
        SetModal(victory ? "成功撤离 · 本局记录" : "本局结束 · 本局记录", buffer.ToString(), false, true);
        detail.fontSize = ZombieHudTheme.Body;
        title.color = victory ? ZombieHudTheme.Accent : ZombieHudTheme.Critical;
        Select(restart);
    }

    private void AppendUpgrades(bool descriptions)
    {
        if (run.UpgradeStacks.Count == 0) buffer.Append("暂未获得升级");
        int shown = 0;
        foreach (var upgrade in run.Upgrades)
        {
            int count = run.StackCount(upgrade.Id);
            if (count == 0) continue;
            buffer.Append('[').Append(RunUpgradePresentation.BranchName(upgrade.Branch)).Append("] ").Append(upgrade.Name).Append(" ×").Append(count);
            if (descriptions) buffer.Append("  ").Append(upgrade.Description).Append('\n');
            else buffer.Append(++shown % 4 == 0 ? "\n" : "    ");
        }
    }

    private void SetModal(string heading, string description, bool upgrades, bool actions)
    {
        modal.SetActive(true);
        title.text = heading;
        title.color = ZombieHudTheme.Text;
        detail.text = description;
        detail.fontSize = ZombieHudTheme.Body;
        if (detailScroll != null)
        {
            float height = upgrades ? 50f : 340f;
            detailScroll.viewport.sizeDelta = new Vector2(696f, height);
            detail.rectTransform.sizeDelta = new Vector2(696f, Mathf.Max(height, detail.preferredHeight + 12f));
            detailScroll.vertical = !upgrades;
            detailScroll.verticalNormalizedPosition = 1f;
            detail.rectTransform.anchoredPosition = Vector2.zero;
        }
        foreach (Button button in choices) button.gameObject.SetActive(upgrades);
        resume.gameObject.SetActive(false);
        restart.gameObject.SetActive(actions);
        menu.gameObject.SetActive(actions);
        EnsureEventSystem();
    }
    private static void Select(Button button) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject); }
    public void HideModal()
    {
        if (modal != null) modal.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public static string FormatTime(float seconds) => Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00");

    /// <summary>Editor builder saves these real Images, Texts and Buttons in the prefab.</summary>
    public void BuildLayout(Font uiFont, Sprite[] artwork)
    {
        font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        icons = artwork ?? new Sprite[17];
        canvasRect = Rect("BattleCanvas", transform);
        Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ZombieHudTheme.ReferenceSize;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();
        RectTransform hud = Rect("HUD", canvasRect);
        Stretch(hud);
        const float edge = ZombieHudTheme.Inset;
        RectTransform player = Panel("PlayerStatus", hud, new Vector2(0f, 1f), new Vector2(edge, -edge), new Vector2(284f, 144f));
        Art("HealthIcon", player, 0, new Vector2(12f, -16f), new Vector2(42f, 42f));
        healthText = Label("HealthValue", player, "生命  100 / 100", ZombieHudTheme.Heading, new Vector2(66f, -8f), new Vector2(206f, 38f));
        RectTransform track = Box("HealthTrack", player, new Vector2(66f, -50f), new Vector2(202f, 16f), new Color(0.27f, 0.11f, 0.1f));
        RectTransform fill = Rect("HealthFill", track);
        Stretch(fill);
        healthFill = Paint(fill, ZombieHudTheme.Accent);
        levelText = Label("Level", player, "等级 1", ZombieHudTheme.Small, new Vector2(14f, -77f), new Vector2(70f, 24f));
        experienceText = Label("ExperienceValue", player, "经验 0 / 80", ZombieHudTheme.Small, new Vector2(88f, -77f), new Vector2(180f, 24f));
        RectTransform xpTrack = Box("ExperienceTrack", player, new Vector2(14f, -104f), new Vector2(254f, 7f), ZombieHudTheme.Panel);
        RectTransform xpBar = Rect("ExperienceFill", xpTrack);
        Stretch(xpBar);
        experienceFill = Paint(xpBar, new Color(0.35f, 0.9f, 0.95f));
        weaponText = Label("Weapon", player, "武器 · 砍刀", ZombieHudTheme.Small, new Vector2(14f, -116f), new Vector2(250f, 24f));
        weaponText.color = ZombieHudTheme.Muted;

        RectTransform objective = Panel("RoomObjective", hud, new Vector2(0.5f, 1f), new Vector2(0f, -edge), new Vector2(348f, 92f));
        Art("RoomIcon", objective, 5, new Vector2(12f, -16f), new Vector2(42f, 42f));
        roomText = Label("Room", objective, "房间 1 / 5", ZombieHudTheme.Heading, new Vector2(64f, -3f), new Vector2(266f, 38f));
        objectiveText = Label("Objective", objective, "清理街区", ZombieHudTheme.Body, new Vector2(64f, -38f), new Vector2(266f, 26f));
        waveText = Label("WaveProgress", objective, "剩余丧尸 0", ZombieHudTheme.Small, new Vector2(64f, -65f), new Vector2(266f, 23f));
        waveText.color = ZombieHudTheme.Muted;

        var encounterRoot = Panel("EncounterStatus", hud, new Vector2(.5f,1), new Vector2(0,-132f),new Vector2(510,82));
        encounterPanel = encounterRoot.gameObject;
        encounterName = Label("EncounterName",encounterRoot,"最终 Boss · 街区暴君",18,new Vector2(16,-2),new Vector2(350,34));
        encounterHealth = Label("EncounterHealth",encounterRoot,"3600 / 3600",16,new Vector2(364,-2),new Vector2(130,34),TextAnchor.MiddleRight);
        var encounterTrack = Box("EncounterTrack",encounterRoot,new Vector2(16,-38),new Vector2(478,10),new Color(.22f,.05f,.04f));
        var encounterBar = Rect("EncounterFill",encounterTrack);Stretch(encounterBar);encounterFill=Paint(encounterBar,ZombieHudTheme.Critical);
        encounterAction = Label("EncounterAction",encounterRoot,"留意地面预警",16,new Vector2(16,-50),new Vector2(478,30),TextAnchor.MiddleCenter);
        encounterPanel.SetActive(false);

        RectTransform statistics = Panel("RunStatistics", hud, new Vector2(1f, 1f), new Vector2(-edge - 60f, -edge), new Vector2(216f, 76f));
        Art("TimeIcon", statistics, 3, new Vector2(12f, -10f), new Vector2(24f, 24f));
        Art("KillsIcon", statistics, 4, new Vector2(12f, -40f), new Vector2(24f, 24f));
        timeText = Label("Time", statistics, "生存 00:00", ZombieHudTheme.Body, new Vector2(46f, -8f), new Vector2(158f, 28f));
        killsText = Label("Kills", statistics, "击杀 0", ZombieHudTheme.Body, new Vector2(46f, -38f), new Vector2(158f, 28f));
        pauseButton = Button("PauseButton", hud, "", new Vector2(1f, 1f), new Vector2(-edge, -edge), new Vector2(48f, 48f));
        Art("PauseIcon", pauseButton.transform, 6, new Vector2(10f, -10f), new Vector2(28f, 28f));
        Label("PauseHint", hud, "暂停\nESC", 12, new Vector2(1f, 1f), new Vector2(-edge, -edge - 52f), new Vector2(48f, 40f), TextAnchor.UpperCenter).color = ZombieHudTheme.Muted;

        upgradeRow = Rect("UpgradeRow", hud);
        Place(upgradeRow, Vector2.zero, new Vector2(edge, edge), new Vector2(560f, 154f));
        Label("Header", upgradeRow, "本局升级", ZombieHudTheme.Small, new Vector2(0f, -2f), new Vector2(85f, 26f)).color = ZombieHudTheme.Muted;
        branchSummary = Label("Branches", upgradeRow, "快攻 0   冰冻 0   燃烧 0", ZombieHudTheme.Small, new Vector2(92f, -2f), new Vector2(430f, 26f));
        branchSummary.color = ZombieHudTheme.Muted;
        upgradeScrollHint = Label("ScrollHint", upgradeRow, "← 拖动或滚轮 →", 11, new Vector2(406f, -2f), new Vector2(154f, 26f), TextAnchor.MiddleRight);
        upgradeScrollHint.color = ZombieHudTheme.Muted;
        upgradeHint = Label("EmptyHint", upgradeRow, "击杀丧尸 · 拾取经验升级", ZombieHudTheme.Body, new Vector2(0f, -38f), new Vector2(400f, 30f));
        upgradeHint.color = ZombieHudTheme.Muted;
        var viewport = Rect("UpgradeViewport", upgradeRow);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(0,-32);
        viewport.gameObject.AddComponent<RectMask2D>();
        Paint(viewport, Color.clear).raycastTarget = true;
        upgradeContent = Rect("UpgradeContent", viewport);
        Place(upgradeContent, new Vector2(0,1), Vector2.zero, new Vector2(560,120));
        upgradeScroll = viewport.gameObject.AddComponent<ScrollRect>();
        upgradeScroll.content = upgradeContent; upgradeScroll.viewport = viewport;
        upgradeScroll.horizontal = true; upgradeScroll.vertical = false;
        upgradeScroll.movementType = ScrollRect.MovementType.Clamped;
        upgradeScroll.scrollSensitivity = 32;
        var slot = Panel("UpgradeTemplate", upgradeContent, new Vector2(0,1), Vector2.zero, Vector2.one * 56f);
        Art("UpgradeIcon", slot, 8, new Vector2(5,-3), new Vector2(30,30));
        Label("Name", slot, "强击", 11, new Vector2(0,-33), new Vector2(56,21), TextAnchor.MiddleCenter);
        Label("StackCount", slot, "×1", 12, new Vector2(32,-3), new Vector2(23,25), TextAnchor.MiddleCenter);
        upgradeTemplate = slot.gameObject; upgradeTemplate.SetActive(false);
        MakeSkill(hud, "Melee", 1, new Vector2(-edge - ZombieHudTheme.SkillSize - 12f, edge), "近战 / 左键", out meleeMask, out meleeStatus);
        MakeSkill(hud, "Dash", 2, new Vector2(-edge, edge), "冲刺 / 空格", out dashMask, out dashStatus);

        RectTransform overlay = Rect("Modal", canvasRect);
        Stretch(overlay);
        modal = overlay.gameObject;
        Paint(overlay, new Color(0.01f, 0.02f, 0.015f, 0.8f)).raycastTarget = true;
        window = Panel("Dialog", overlay, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 600f));
        title = Label("Title", window, "本局记录", ZombieHudTheme.Title, new Vector2(32f, -24f), new Vector2(696f, 46f));
        RectTransform detailViewport = Rect("DetailViewport", window);
        Place(detailViewport, new Vector2(0f, 1f), new Vector2(32f, -86f), new Vector2(696f, 340f));
        detailViewport.gameObject.AddComponent<RectMask2D>();
        Paint(detailViewport, Color.clear).raycastTarget = true;
        detail = Label("Detail", detailViewport, "", ZombieHudTheme.Body, Vector2.zero, new Vector2(696f, 340f));
        detail.alignment = TextAnchor.UpperLeft;
        detail.lineSpacing = 1.25f;
        detailScroll = detailViewport.gameObject.AddComponent<ScrollRect>();
        detailScroll.viewport = detailViewport;
        detailScroll.content = detail.rectTransform;
        detailScroll.horizontal = false;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 30f;
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i] = Button("UpgradeChoice" + i, window, "", new Vector2(0f, 1f), new Vector2(32f, -154f - i * 112f), new Vector2(696f, 96f));
            choiceIcons[i] = Art("Icon", choices[i].transform, 8 + i, new Vector2(16f, -18f), new Vector2(56f, 56f));
            choiceLabels[i] = Label("Description", choices[i].transform, "", ZombieHudTheme.Body, new Vector2(88f, -10f), new Vector2(592f, 76f));
        }
        resume = Button("Resume", window, "继续战斗", new Vector2(0f, 0f), new Vector2(32f, 96f), new Vector2(696f, 52f));
        restart = Button("Restart", window, "再来一局", new Vector2(0f, 0f), new Vector2(32f, 28f), new Vector2(340f, 52f));
        menu = Button("MainMenu", window, "返回主菜单", new Vector2(0f, 0f), new Vector2(388f, 28f), new Vector2(340f, 52f));
        modal.SetActive(false);
    }

    private void BuildUpgradeWidgets()
    {
        foreach (var upgrade in run.Upgrades)
        {
            var root = Instantiate(upgradeTemplate, upgradeContent);
            root.name = "Upgrade_" + upgrade.Id;
            var icon = root.transform.Find("UpgradeIcon").GetComponent<Image>();
            icon.sprite = Icon(RunUpgradePresentation.Icon(upgrade)); icon.color = RunUpgradePresentation.Color(upgrade.Branch);
            root.transform.Find("Name").GetComponent<Text>().text = upgrade.Name;
            root.GetComponent<Outline>().effectColor = RunUpgradePresentation.Color(upgrade.Branch);
            upgradeWidgets.Add(new UpgradeWidget { id = upgrade.Id, rect = root.GetComponent<RectTransform>(), count = root.transform.Find("StackCount").GetComponent<Text>() });
        }
    }

    private void MakeSkill(Transform parent, string name, int iconIndex, Vector2 position, string caption, out Image mask, out Text status)
    {
        RectTransform slot = Panel(name + "Skill", parent, new Vector2(1f, 0f), position, new Vector2(ZombieHudTheme.SkillSize, 108f));
        var skillIcon = Art("Icon", slot, iconIndex, new Vector2(10f, -8f), new Vector2(56f, 56f));
        if (name == "Melee") equippedWeaponIcon = skillIcon;
        RectTransform cooldown = Rect("CooldownMask", slot);
        Place(cooldown, new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(56f, 56f));
        mask = Paint(cooldown, new Color(0f, 0f, 0f, 0.8f));
        mask.sprite = Icon(iconIndex);
        mask.type = Image.Type.Filled;
        mask.fillMethod = Image.FillMethod.Radial360;
        mask.fillOrigin = 2;
        mask.preserveAspect = true;
        mask.fillAmount = 0f;
        status = Label("Cooldown", slot, "就绪", ZombieHudTheme.Small, new Vector2(0f, -60f), new Vector2(ZombieHudTheme.SkillSize, 25f), TextAnchor.MiddleCenter);
        Label("KeyHint", slot, caption, 12, new Vector2(0f, -85f), new Vector2(ZombieHudTheme.SkillSize, 21f), TextAnchor.MiddleCenter).color = ZombieHudTheme.Muted;
    }
    private Sprite Icon(int index) => icons != null && index >= 0 && index < icons.Length ? icons[index] : null;
    private Image Art(string name, Transform parent, int index, Vector2 position, Vector2 size)
    {
        Image image = Paint(Box(name, parent, position, size, Color.white), Color.white);
        image.sprite = Icon(index);
        image.preserveAspect = true;
        return image;
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    private static Image Paint(RectTransform rect, Color color)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    private static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, new Vector2(0f, 1f), position, size);
        Paint(rect, color);
        return rect;
    }
    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, anchor, position, size);
        Paint(rect, ZombieHudTheme.Panel);
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = ZombieHudTheme.Border;
        outline.effectDistance = new Vector2(1f, -1f);
        return rect;
    }
    private Text Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
        => Label(name, parent, value, size, new Vector2(0f, 1f), position, dimensions, alignment);
    private Text Label(string name, Transform parent, string value, int size, Vector2 anchor, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, anchor, position, dimensions);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.text = value;
        label.color = ZombieHudTheme.Text;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }
    private Button Button(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = Panel(name, parent, anchor, position, size);
        Image background = rect.GetComponent<Image>();
        background.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.4f, 1.1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        if (!string.IsNullOrEmpty(text)) Label("Label", rect, text, ZombieHudTheme.Heading, Vector2.zero, size, TextAnchor.MiddleCenter);
        return button;
    }
}
