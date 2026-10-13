using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using Flower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using B = ZombieHudTheme.Battle;

/// <summary>Editable UGUI battle prefab. All widget references are baked by ZombieHudSetup.</summary>
[DisallowMultipleComponent]
public sealed class ZombieRunUI : MonoBehaviour
{
    [SerializeField] private Font font;
    [SerializeField] private Sprite[] icons = new Sprite[24];
    [SerializeField] private TMP_FontAsset textFont;
    [SerializeField] private Sprite[] chrome = new Sprite[5];
    [SerializeField] private TextMeshProUGUI weaponCaption;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private TextMeshProUGUI healthText, roomText, objectiveText, waveText, timeText, killsText;
    [SerializeField] private RectTransform roomObjectivePanel;
    [SerializeField] private CanvasGroup roomObjectiveGroup;
    [SerializeField, Range(0f, 1f)] private float idleRoomOpacity = 0.78f;
    [SerializeField, Min(1f)] private float combatDetectionRadius = 7f;
    [SerializeField, Min(0f)] private float combatVisibilityHold = 2.5f;
    [SerializeField] private Image healthFill;
    [SerializeField] private GameObject encounterPanel;
    [SerializeField] private TextMeshProUGUI encounterName, encounterHealth, encounterAction;
    [SerializeField] private Image encounterFill;
    [SerializeField] private TextMeshProUGUI levelText, experienceText, weaponText;
    [SerializeField] private Image experienceFill;
    [SerializeField] private Button pauseButton;
    [SerializeField] private RectTransform upgradeRow, upgradeContent;
    [SerializeField] private Image upgradeBackground;
    [SerializeField] private TextMeshProUGUI upgradeHeader;
    [SerializeField] private GameObject upgradeTemplate;
    [SerializeField] private ScrollRect upgradeScroll;
    [SerializeField] private TextMeshProUGUI branchSummary;
    [SerializeField] private TextMeshProUGUI upgradeScrollHint;
    private sealed class UpgradeWidget { public int id; public RectTransform rect; public TextMeshProUGUI count; }
    private readonly List<UpgradeWidget> upgradeWidgets = new List<UpgradeWidget>();
    [SerializeField] private TextMeshProUGUI upgradeHint;
    [SerializeField] private Image meleeMask, dashMask;
    [SerializeField] private Image equippedWeaponIcon, playerWeaponIcon;
    [SerializeField] private TextMeshProUGUI meleeStatus, dashStatus;
    [SerializeField] private GameObject modal;
    [SerializeField] private RectTransform window;
    private CanvasGroup modalInteractionGroup;
    [SerializeField] private TextMeshProUGUI title, detail;
    [SerializeField] private ScrollRect detailScroll;
    [SerializeField] private Button[] choices = new Button[RunState.UpgradeChoiceCount];
    [SerializeField] private TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[RunState.UpgradeChoiceCount];
    [SerializeField] private Image[] choiceIcons = new Image[RunState.UpgradeChoiceCount];
    [SerializeField] private Button resume, restart, menu;
    private RunState run;
    private SurvivorHealth boundPlayer;
    private SurvivorMeleeAttack melee;
    private SurvivorDash dash;
    private ZombieBattleMiniMap miniMap;
    private GameObject ownedEventSystem;
    private float nextRefresh;
    private float roomCombatUntil;
    private SurvivorHealth roomCombatPlayer;
    private readonly Collider2D[] nearbyEnemies = new Collider2D[48];
    private readonly StringBuilder buffer = new StringBuilder(512);
    private enum ChoiceMode { Upgrade, RoomShop, RoomShopCards, CurseCards }
    private ChoiceMode choiceMode = ChoiceMode.Upgrade;
    private Coroutine modalAnimation;
    private bool modalReady;
    private bool modalClosing;
    private const float ModalAnimationDuration = 0.2f;
    private const float ModalStartScale = 0.86f;

    public void Initialize(RunState state)
    {
        run = state;
        if (canvasRect == null) BuildLayout(run.Config.uiFont, icons, textFont, chrome);
        ResolvePolishReferences();
        EnsureModalInteractionGroup();
        miniMap = GetComponent<ZombieBattleMiniMap>();
        if (miniMap == null) miniMap = gameObject.AddComponent<ZombieBattleMiniMap>();
        miniMap.Initialize(run, canvasRect);
        BuildUpgradeWidgets();
        UiButtonSound.Ensure(pauseButton); UiButtonSound.Ensure(resume);
        UiButtonSound.Ensure(restart); UiButtonSound.Ensure(menu);
        pauseButton.onClick.AddListener(run.TogglePause);
        for (int i = 0; i < choices.Length; i++)
        {
            UiButtonSound.Ensure(choices[i]);
            int index = i;
            choices[i].onClick.AddListener(() =>
            {
                if (!modalReady || modalClosing) return;
                BeginModalClose(() =>
                {
                    if (choiceMode == ChoiceMode.Upgrade && index < run.OfferedUpgrades.Count)
                        run.ChooseUpgrade(run.OfferedUpgrades[index].Id);
                    else if (choiceMode == ChoiceMode.RoomShop)
                    {
                        if (index == 0) run.BuyRoomCard();
                        else if (index == 1) run.BuyRoomHeal();
                        else if (index == 2) run.BuyRoomBuff();
                    }
                    else if (choiceMode == ChoiceMode.RoomShopCards && index < run.ShopOfferedUpgrades.Count)
                        run.ChooseRoomShopCard(run.ShopOfferedUpgrades[index].Id);
                    else if (choiceMode == ChoiceMode.CurseCards && index < run.ShopOfferedUpgrades.Count)
                        run.ChooseCurseCard(run.ShopOfferedUpgrades[index].Id);
                });
            });
        }
        resume.onClick.AddListener(() => { if (modalReady && !modalClosing) BeginModalClose(run.TogglePause); });
        restart.onClick.AddListener(() => { if (modalReady && !modalClosing) BeginModalClose(run.RestartRun); });
        menu.onClick.AddListener(() => { if (modalReady && !modalClosing) BeginModalClose(run.ReturnToMenu); });
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
        RefreshPanelVisibility();
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        CheckRoomCombat();
        RefreshHud();
    }

    private void RefreshHud()
    {
        healthText.text = "生命  " + Mathf.Max(0, run.CurrentHealth) + " / " + run.MaxHealth;
        float ratio = run.MaxHealth > 0 ? Mathf.Clamp01(run.CurrentHealth / (float)run.MaxHealth) : 0f;
        healthFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        healthFill.color = ratio <= 0.3f ? ZombieHudTheme.Critical : B.Health;
        healthText.color = ratio <= 0.3f ? ZombieHudTheme.Critical : ZombieHudTheme.Text;
        levelText.text = "等级 " + run.Level;
        experienceText.text = run.Experience + " / " + run.ExperienceToNextLevel;
        experienceFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(run.Experience / (float)run.ExperienceToNextLevel), 1f);
        weaponText.text = "武器 · " + run.WeaponName;
        if (melee != null && melee.EquippedWeapon != null && equippedWeaponIcon != null)
        {
            equippedWeaponIcon.sprite = WeaponIcon(melee.EquippedWeapon.kind);
            if (playerWeaponIcon != null) playerWeaponIcon.sprite = equippedWeaponIcon.sprite;
            if (weaponCaption != null) weaponCaption.text = run.WeaponName;
            equippedWeaponIcon.preserveAspect = true;
        }
        roomText.text = run.IsEndless ? "无尽生存 · 压力 " + (1 + Mathf.FloorToInt(run.ElapsedSeconds / 60f)) : "房间 " + run.RoomNumber + " / " + run.RoomCount;
        var encounter = run.Encounter;
        bool encounterActive = encounter != null && encounter.IsRequired && encounter.Stage != RoomEncounterController.EncounterStage.Waiting;
        objectiveText.text = run.RewardClaimed ? "出口已解锁" : encounterActive ? "击败" + encounter.EnemyName :
            encounter != null && encounter.IsRequired ? "清理街区 · " + (encounter.IsBoss ? "决战将至" : "精英将至") :
            "清理街区" + (run.RoomLayout != null ? " · " + run.RoomLayout.VariantName : string.Empty);
        LevelFlowController flow = run.CurrentRoom;
        waveText.text = flow != null && !run.RewardClaimed
            ? "清理 " + run.RoomKills + "/" + flow.RequiredKills + (flow.TotalObjectiveWaves > 0 ? " · 区域 " + flow.CompletedObjectiveWaves + "/" + flow.TotalObjectiveWaves : string.Empty) + " · 残骸 " + run.Scrap
            : "前往安全门 · 残骸 " + run.Scrap;
        if (encounterActive && !run.RewardClaimed) waveText.text = encounter.IsBoss ? "最终遭遇 · 留意地面预警" : "精英遭遇 · 留意冲锋预警";
        if (run.IsEndless && run.Endless != null)
        {
            objectiveText.text = "坚持生存 · 存活 " + run.Endless.ActiveEnemyCount;
            waveText.text = run.Endless.ActiveBossCount > 0 ? "Boss ×" + run.Endless.ActiveBossCount + " · 保持移动" :
                "下批 Boss " + FormatTime(Mathf.Max(0, run.Endless.NextBossSeconds - run.ElapsedSeconds));
        }
        RefreshEncounter(encounter);
        timeText.text = "生存 " + FormatTime(run.ElapsedSeconds);
        killsText.text = "击杀 " + run.Kills;
        pauseButton.interactable = run.Phase == RunState.RunPhase.Playing;
        upgradeHint.gameObject.SetActive(run.UpgradeStacks.Count == 0);
        branchSummary.text = run.UsesBow ? "弓箭 " + run.BranchStacks("Bow") + "   通用 " + run.BranchStacks("Common") :
            "快攻 " + run.BranchStacks("Quick") + "   冰冻 " + run.BranchStacks("Frost") + "   燃烧 " + run.BranchStacks("Fire");
        int visibleSlot = 0;
        foreach (var widget in upgradeWidgets)
        {
            int count = run.StackCount(widget.id);
            widget.rect.gameObject.SetActive(count > 0);
            if (count > 0)
            {
                widget.rect.anchoredPosition = new Vector2(visibleSlot * B.SlotStep, 0f);
                visibleSlot++;
            }
            widget.count.text = "×" + count;
        }
        float maxWidth = Mathf.Min(B.UpgradeMaxWidth, canvasRect.rect.width - B.SkillSize.x * 2f - B.Edge * 4f - B.Gap);
        float contentWidth = visibleSlot > 0 ? visibleSlot * B.SlotStep - B.Gap : 0f;
        float width = Mathf.Clamp(contentWidth + B.Padding * 2f, B.UpgradeMinWidth, Mathf.Max(B.UpgradeMinWidth, maxWidth));
        upgradeRow.sizeDelta = new Vector2(width, B.UpgradeHeight);
        upgradeContent.sizeDelta = new Vector2(Mathf.Max(width - B.Padding * 2f, contentWidth), B.Slot);
        upgradeScroll.horizontal = contentWidth > width - B.Padding * 2f + 1f;
        if (!upgradeScroll.horizontal) upgradeContent.anchoredPosition = Vector2.zero;
        if (upgradeScrollHint != null) upgradeScrollHint.gameObject.SetActive(upgradeScroll.horizontal);
        if (modal.activeSelf) window.sizeDelta = new Vector2(Mathf.Min(ZombieHudTheme.UpgradeDialog.Size.x, canvasRect.rect.width - 48f), ZombieHudTheme.UpgradeDialog.Size.y);
    }

    private void RefreshPanelVisibility()
    {
        if (roomObjectiveGroup == null) return;
        bool combat = run.Phase == RunState.RunPhase.Playing && !run.RewardClaimed &&
            !run.IsChoosingStartingWeapon && Time.unscaledTime < roomCombatUntil;
        float target = combat ? 1f : idleRoomOpacity;
        if (run.Phase == RunState.RunPhase.Loading || run.Phase == RunState.RunPhase.Won || run.Phase == RunState.RunPhase.Lost)
            target = 0.12f;
        roomObjectiveGroup.alpha = Mathf.MoveTowards(roomObjectiveGroup.alpha, target, Time.unscaledDeltaTime * 8f);
        roomObjectiveGroup.interactable = false;
        roomObjectiveGroup.blocksRaycasts = false;
    }

    private void CheckRoomCombat()
    {
        // Check at HUD frequency with a reusable buffer, not one scene-wide search per frame.
        if (roomCombatPlayer != run.Player)
        {
            roomCombatPlayer = run.Player;
            roomCombatUntil = 0f;
        }
        if (run.Phase != RunState.RunPhase.Playing || run.RewardClaimed || run.Player == null || run.Player.IsDead)
            return;
        bool fighting = melee != null && (melee.IsAttacking || melee.CooldownRemaining > 0f);
        var encounter = run.Encounter;
        fighting |= encounter != null && encounter.Enemy != null && !encounter.IsResolved;
        if (!fighting)
        {
            var filter = new ContactFilter2D();
            filter.NoFilter();
            int count = Physics2D.OverlapCircle(run.Player.transform.position, combatDetectionRadius, filter, nearbyEnemies);
            for (int i = 0; i < count; i++)
            {
                ZombieChaser enemy = nearbyEnemies[i] != null ? nearbyEnemies[i].GetComponentInParent<ZombieChaser>() : null;
                if (enemy == null || enemy.CurrentHealth <= 0) continue;
                fighting = true;
                break;
            }
        }
        if (fighting) roomCombatUntil = Time.unscaledTime + combatVisibilityHold;
    }

    private void RefreshEncounter(RoomEncounterController encounter)
    {
        if (encounterPanel == null) return;
        if (run.IsEndless)
        {
            var endless = run.Endless;
            bool active = endless != null && endless.ActiveBossCount > 0;
            encounterPanel.SetActive(active);
            if (!active) return;
            encounterName.text = "暴君 ×" + endless.ActiveBossCount + " · 总生命";
            encounterHealth.text = endless.BossHealth + " / " + endless.BossMaxHealth;
            encounterFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(endless.BossHealth / (float)Mathf.Max(1, endless.BossMaxHealth)), 1);
            encounterFill.color = ZombieHudTheme.Critical;
            encounterAction.text = "多重遭遇 · 注意冲撞与落石预警";
            encounterAction.color = ZombieHudTheme.Text;
            return;
        }
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

    private static void UpdateCooldown(Image mask, TextMeshProUGUI label, float remaining, float total)
    {
        float amount = Mathf.Clamp01(remaining / Mathf.Max(0.05f, total));
        mask.gameObject.SetActive(amount > 0f);
        mask.fillAmount = amount;
        string value = remaining > 0f ? (Mathf.Ceil(remaining * 10f) / 10f).ToString("0.0") + "s" : "就绪";
        if (label.text != value) label.text = value;
        label.color = remaining > 0f ? ZombieHudTheme.Text : B.Ready;
    }

    public void ShowChoices()
    {
        choiceMode = ChoiceMode.Upgrade;
        SetModal("升级！四选一", "当前等级 " + run.Level + " · 选择一项奖励继续战斗", true, false);
        detail.fontSize = ZombieHudTheme.Body;
        PopulateCardChoices(run.OfferedUpgrades);
        SelectFirstAvailableChoice();
        StartModalOpen();
    }

    public void ShowRoomRewardShop(string message = null)
    {
        choiceMode = ChoiceMode.RoomShop;
        string curse = string.IsNullOrEmpty(run.ActiveCurseDescription) ? string.Empty : "\n当前诅咒：" + run.ActiveCurseDescription;
        string suffix = string.IsNullOrEmpty(message) ? string.Empty : "\n\n" + message;
        SetModal("房间完成 · 残骸兑换", "精英掉落的残骸：" + run.Scrap + curse + suffix, true, false);
        for (int i = 0; i < choices.Length; i++)
        {
            bool available = i < ZombieHudTheme.UpgradeDialog.ShopProductCount;
            choices[i].gameObject.SetActive(available);
            choices[i].interactable = available;
        }
        choiceLabels[0].text = "额外卡牌   · 6 残骸";
        choiceLabels[1].text = "治疗道具   · 4 残骸";
        choiceLabels[2].text = "临时强化   · 5 残骸\n30 秒伤害 +25%";
        for (int i = 0; i < ZombieHudTheme.UpgradeDialog.ShopProductCount; i++)
        {
            choiceIcons[i].sprite = Icon(i == 0 ? 8 : i == 1 ? 0 : 23);
            choiceIcons[i].color = Color.white;
            TintChoice(choices[i], "Common");
        }
        choices[0].interactable = run.Scrap >= 6;
        choices[1].interactable = run.Scrap >= 4 && run.CurrentHealth < run.MaxHealth;
        choices[2].interactable = run.Scrap >= 5;
        resume.gameObject.SetActive(true);
        SelectFirstAvailableChoice();
        StartModalOpen();
    }

    public void ShowRoomShopCardChoices()
    {
        choiceMode = ChoiceMode.RoomShopCards;
        SetModal("额外卡牌 · 四选一", "花费已扣除 · 选择一张加入本局", true, false);
        PopulateCardChoices(run.ShopOfferedUpgrades);
        resume.gameObject.SetActive(false);
        SelectFirstAvailableChoice();
        StartModalOpen();
    }

    public void ShowCurseCardChoices(string curseDescription)
    {
        choiceMode = ChoiceMode.CurseCards;
        SetModal("诅咒圣坛 · 四选一", curseDescription + "\n接受代价后选择一张稀有卡", true, false);
        PopulateCardChoices(run.ShopOfferedUpgrades);
        resume.gameObject.SetActive(false);
        SelectFirstAvailableChoice();
        StartModalOpen();
    }

    private void PopulateCardChoices(IReadOnlyList<DRRunUpgrade> cards)
    {
        for (int i = 0; i < choices.Length; i++)
        {
            bool available = i < cards.Count && run.IsUpgradeEligible(cards[i]);
            choices[i].gameObject.SetActive(available);
            choices[i].interactable = available;
            if (!available) continue;
            var upgrade = cards[i];
            choiceLabels[i].text = "[" + RunUpgradePresentation.BranchName(upgrade.Branch) + (string.IsNullOrEmpty(upgrade.RequiredEffect) ? "" : " · 联动") + "] " + upgrade.Name + "   " + RunUpgradePresentation.StackProgress(upgrade, run.StackCount(upgrade.Id)) + "\n" + RunUpgradePresentation.Description(upgrade, run.UsesBow, run.FreezeConvertedToBurn);
            choiceIcons[i].sprite = Icon(RunUpgradePresentation.Icon(upgrade));
            choiceIcons[i].color = Color.white;
            TintChoice(choices[i], upgrade.Branch);
        }
    }

    public void ShowPause()
    {
        buffer.Clear();
        if (run.IsEndless) buffer.Append("无尽模式 · ");
        else buffer.Append("房间 ").Append(run.RoomNumber).Append(" / ").Append(run.RoomCount);
        buffer
            .Append("    生存 ").Append(FormatTime(run.ElapsedSeconds)).Append("    击杀 ").Append(run.Kills)
            .Append("\n等级 ").Append(run.Level).Append("    武器 ").Append(run.WeaponName).Append("\n\n已获得升级\n");
        AppendUpgrades(true);
        SetModal("已暂停", buffer.ToString(), false, true);
        detail.fontSize = ZombieHudTheme.Body;
        resume.gameObject.SetActive(true);
        Select(resume);
        StartModalOpen();
    }

    public void ShowResult(bool victory)
    {
        buffer.Clear();
        buffer.Append("幸存者点数  +").Append(run.EarnedSurvivorPoints).Append("    持有 ").Append(SurvivorMetaProgress.Points);
        if (run.IsEndless) buffer.Append("\n模式   无尽生存    初始武器   ").Append(run.WeaponName);
        else buffer.Append("\n通过房间   ").Append(run.ClearedRooms).Append(" / ").Append(run.RoomCount)
            .Append("    到达房间 ").Append(run.RoomNumber);
        buffer.Append("       生存时间   ").Append(FormatTime(run.ElapsedSeconds))
            .Append("\n总击杀数   ").Append(run.Kills).Append("       精英击杀   ").Append(run.EliteKills)
            .Append("\n造成伤害   ").Append(run.DamageDealt).Append("       受到伤害   ").Append(run.DamageTaken)
            .Append("\n恢复生命   ").Append(run.HealingReceived).Append("       等级   ").Append(run.Level)
            .Append("       总经验   ").Append(run.TotalExperience)
            .Append("\n强敌击败   ").Append(run.ChampionKills).Append("       Boss 击败   ").Append(run.BossKills).Append("\n\n本局升级\n");
        AppendUpgrades(false);
        if (run.IsEndless)
            buffer.Append("\n\n本地生存榜   ")
                .Append(run.EndlessRank > 0 ? "第 " + run.EndlessRank + " 名" : "未进入前 6 名");
        buffer.Append("\n\n随机种子   ").Append(run.Seed);
        SetModal(run.IsEndless ? "无尽挑战结束 · 本局记录" : victory ? "成功撤离 · 本局记录" : "本局结束 · 本局记录", buffer.ToString(), false, true);
        detail.fontSize = ZombieHudTheme.Body;
        title.color = victory ? ZombieHudTheme.Accent : ZombieHudTheme.Critical;
        Select(restart);
        StartModalOpen();
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
            if (descriptions) buffer.Append("  ").Append(RunUpgradePresentation.Description(upgrade, run.UsesBow, run.FreezeConvertedToBurn)).Append('\n');
            else buffer.Append(++shown % 4 == 0 ? "\n" : "    ");
        }
    }

    private void SetModal(string heading, string description, bool upgrades, bool actions)
    {
        StopModalAnimation();
        modalReady = false;
        modalClosing = false;
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
            detailScroll.vertical = !upgrades || detail.preferredHeight > height;
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
    private void SelectFirstAvailableChoice()
    {
        foreach (Button button in choices)
        {
            if (button.gameObject.activeInHierarchy && button.interactable)
            {
                Select(button);
                return;
            }
        }
        if (resume.gameObject.activeInHierarchy && resume.interactable)
        {
            Select(resume);
            return;
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void HideModal()
    {
        StopModalAnimation();
        modalReady = false;
        modalClosing = false;
        SetModalInteractable(false);
        if (window != null) window.localScale = Vector3.one;
        if (modal != null) modal.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void StartModalOpen()
    {
        if (window == null) return;
        StopModalAnimation();
        SetModalInteractable(false);
        modalReady = false;
        modalClosing = false;
        window.localScale = Vector3.one * ModalStartScale;
        modalAnimation = StartCoroutine(AnimateModal(true, null));
    }

    private void BeginModalClose(Action completed)
    {
        if (window == null)
        {
            completed?.Invoke();
            return;
        }
        StopModalAnimation();
        SetModalInteractable(false);
        modalReady = false;
        modalClosing = true;
        modalAnimation = StartCoroutine(AnimateModal(false, completed));
    }

    private IEnumerator AnimateModal(bool opening, Action completed)
    {
        float elapsed = 0f;
        Vector3 start = opening ? Vector3.one * ModalStartScale : Vector3.one;
        Vector3 end = opening ? Vector3.one : Vector3.one * ModalStartScale;
        while (elapsed < ModalAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / ModalAnimationDuration);
            t = t * t * (3f - 2f * t);
            window.localScale = Vector3.LerpUnclamped(start, end, t);
            yield return null;
        }
        window.localScale = end;
        modalAnimation = null;
        if (opening)
        {
            modalReady = true;
            modalClosing = false;
            SetModalInteractable(true);
            SelectFirstAvailableChoice();
        }
        else
        {
            modalClosing = false;
            completed?.Invoke();
        }
    }

    private void SetModalInteractable(bool enabled)
    {
        if (modalInteractionGroup == null) EnsureModalInteractionGroup();
        if (modalInteractionGroup == null) return;
        modalInteractionGroup.interactable = enabled;
        // The full-screen modal image continues to block clicks from reaching the game.
        modalInteractionGroup.blocksRaycasts = true;
    }

    private void EnsureModalInteractionGroup()
    {
        if (window == null) return;
        modalInteractionGroup = window.GetComponent<CanvasGroup>();
        if (modalInteractionGroup == null) modalInteractionGroup = window.gameObject.AddComponent<CanvasGroup>();
        modalInteractionGroup.alpha = 1f;
        modalInteractionGroup.blocksRaycasts = true;
    }

    private void StopModalAnimation()
    {
        if (modalAnimation == null) return;
        StopCoroutine(modalAnimation);
        modalAnimation = null;
    }
    public static string FormatTime(float seconds) => Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00");

    /// <summary>Editor builder saves these real Images, Texts and Buttons in the prefab.</summary>
    public void BuildLayout(Font uiFont, Sprite[] artwork, TMP_FontAsset uiTextFont = null, Sprite[] chromeArtwork = null)
    {
        font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textFont = uiTextFont != null ? uiTextFont : TMP_FontAsset.CreateFontAsset(font);
        icons = artwork ?? new Sprite[24];
        chrome = chromeArtwork ?? new Sprite[5];
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
        const float edge = B.Edge;
        RectTransform player = Panel("PlayerStatus", hud, new Vector2(0f, 1f), new Vector2(edge, -edge), B.PlayerSize);
        var portraitFrame = Panel("PortraitFrame", player, new Vector2(0,1), new Vector2(B.Padding,-B.Padding), new Vector2(62,74), true);
        var portrait = Paint(Box("Portrait", portraitFrame, new Vector2(5,-5), new Vector2(52,64), Color.white),Color.white);
        portrait.sprite = Chrome(4); portrait.preserveAspect = true;
        healthText = Label("HealthValue", player, "生命 100 / 100", B.Heading, new Vector2(86,-8),new Vector2(170,32));
        healthFill = ProgressTrack("Health", player, new Vector2(86,-42), new Vector2(170,12), B.Track, B.Health);
        levelText = Label("Level",player,"等级 1", B.Small,new Vector2(86,-61),new Vector2(70,23));
        experienceText = Label("ExperienceValue",player,"0 / 80",B.Small,new Vector2(156,-61),new Vector2(100,23),TextAnchor.MiddleRight);
        experienceText.color = B.Muted;
        experienceFill = ProgressTrack("Experience",player,new Vector2(86,-87),new Vector2(170,6),B.Track,B.Experience);
        playerWeaponIcon = Art("WeaponIcon",player,1,new Vector2(12,-98),new Vector2(20,20));
        weaponText = Label("Weapon",player,"武器 · 砍刀",B.Small,new Vector2(38,-97),new Vector2(218,22));
        weaponText.color = B.Muted;

        RectTransform objective = Panel("RoomObjective",hud,new Vector2(.5f,1),new Vector2(0,-edge),B.ObjectiveSize);
        roomObjectivePanel=objective;
        roomObjectiveGroup=objective.gameObject.AddComponent<CanvasGroup>();
        roomObjectiveGroup.alpha=1;
        Art("RoomIcon",objective,5,new Vector2(12,-24),new Vector2(42,46));
        roomText=Label("Room",objective,"房间 1 / 5",B.Heading,new Vector2(64,-2),new Vector2(224,32));
        objectiveText=Label("Objective",objective,"清理街区",B.Body,new Vector2(64,-35),new Vector2(224,26));
        objectiveText.enableAutoSizing=true; objectiveText.fontSizeMin=B.Small; objectiveText.fontSizeMax=B.Body;
        waveText=Label("WaveProgress",objective,"清理 0/60 · 残骸 0",B.Small,new Vector2(64,-64),new Vector2(224,22));
        waveText.color=B.Muted;
        waveText.enableAutoSizing=true; waveText.fontSizeMin=11; waveText.fontSizeMax=B.Small;

        var encounterRoot=Panel("EncounterStatus",hud,new Vector2(.5f,1),new Vector2(0,-edge-B.ObjectiveSize.y-B.Gap),B.EncounterSize);
        encounterPanel=encounterRoot.gameObject;
        encounterName=Label("EncounterName",encounterRoot,"最终 Boss · 街区暴君",B.Body,new Vector2(12,-4),new Vector2(310,28));
        encounterHealth=Label("EncounterHealth",encounterRoot,"15000 / 15000",B.Small,new Vector2(326,-4),new Vector2(142,28),TextAnchor.MiddleRight);
        encounterFill=ProgressTrack("Encounter",encounterRoot,new Vector2(12,-36),new Vector2(456,9),B.Track,ZombieHudTheme.Critical);
        encounterAction=Label("EncounterAction",encounterRoot,"留意地面预警",B.Small,new Vector2(12,-47),new Vector2(456,23),TextAnchor.MiddleCenter);
        encounterPanel.SetActive(false);

        RectTransform statistics=Panel("RunStatistics",hud,new Vector2(1,1),new Vector2(-edge-56,-edge),B.StatisticsSize);
        Art("TimeIcon",statistics,3,new Vector2(12,-12),new Vector2(24,24));
        Art("KillsIcon",statistics,4,new Vector2(12,-46),new Vector2(24,24));
        timeText=Label("Time",statistics,"生存 00:00",B.Body,new Vector2(46,-10),new Vector2(128,28));
        killsText=Label("Kills",statistics,"击杀 0",B.Body,new Vector2(46,-44),new Vector2(128,28));
        pauseButton=Button("PauseButton",hud,"",new Vector2(1,1),new Vector2(-edge,-edge),new Vector2(48,82));
        Art("PauseIcon",pauseButton.transform,6,new Vector2(12,-14),new Vector2(24,30));
        Label("PauseHint",pauseButton.transform,"ESC",B.Small,new Vector2(0,-52),new Vector2(48,22),TextAnchor.MiddleCenter).color=B.Muted;

        upgradeRow=Panel("UpgradeRow",hud,Vector2.zero,new Vector2(edge,edge),new Vector2(B.UpgradeMinWidth,B.UpgradeHeight));
        upgradeBackground=upgradeRow.GetComponent<Image>();
        upgradeHeader=Label("Header",upgradeRow,"本局升级",B.Small,new Vector2(B.Padding,-4),new Vector2(76,24));
        branchSummary=Label("Branches",upgradeRow,"快攻 0   冰冻 0   燃烧 0",B.Small,new Vector2(92,-4),new Vector2(224,24));
        branchSummary.color=B.Muted;
        upgradeScrollHint=Label("ScrollHint",upgradeRow,"↔",B.Small,new Vector2(1,1),new Vector2(-B.Padding,-4),new Vector2(20,24),TextAnchor.MiddleRight);
        upgradeHint=Label("EmptyHint",upgradeRow,"拾取经验 · 选择升级",B.Small,new Vector2(B.Padding,-40),new Vector2(280,26));
        upgradeHint.color=B.Muted;
        var viewport=Rect("UpgradeViewport",upgradeRow);
        viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(B.Padding,B.Padding);viewport.offsetMax=new Vector2(-B.Padding,-B.UpgradeHeader);
        viewport.gameObject.AddComponent<RectMask2D>();Paint(viewport,Color.clear).raycastTarget=true;
        upgradeContent=Rect("UpgradeContent",viewport);
        Place(upgradeContent,new Vector2(0,1),Vector2.zero,new Vector2(B.UpgradeMinWidth-B.Padding*2,B.Slot));
        upgradeScroll=viewport.gameObject.AddComponent<ScrollRect>();
        upgradeScroll.content=upgradeContent;upgradeScroll.viewport=viewport;
        upgradeScroll.horizontal=true;upgradeScroll.vertical=false;
        upgradeScroll.movementType=ScrollRect.MovementType.Clamped;upgradeScroll.scrollSensitivity=B.SlotStep;
        var slot=Panel("UpgradeTemplate",upgradeContent,new Vector2(0,1),Vector2.zero,Vector2.one*B.Slot,true);
        Art("UpgradeIcon",slot,8,new Vector2(5,-4),new Vector2(B.Slot-10,B.Slot-10));
        var branchFrame=Paint(Box("BranchFrame",slot,Vector2.zero,Vector2.one*B.Slot,Color.white),Color.white);
        branchFrame.sprite=Chrome(1);branchFrame.type=Image.Type.Sliced;branchFrame.fillCenter=false;
        Box("StackBadge",slot,new Vector2(B.Slot-31,-B.Slot+22),new Vector2(30,21),new Color(.03f,.04f,.035f,.96f));
        var stackCount = Label("StackCount",slot,"×1",B.Small,new Vector2(B.Slot-32,-B.Slot+23),new Vector2(30,23),TextAnchor.MiddleRight);
        stackCount.enableAutoSizing = true;
        stackCount.fontSizeMin = 10;
        stackCount.fontSizeMax = B.Small;
        upgradeTemplate=slot.gameObject;upgradeTemplate.SetActive(false);
        MakeSkill(hud,"Melee",1,new Vector2(-edge-B.SkillSize.x-B.Gap,edge+B.SkillCaption),"砍刀",out meleeMask,out meleeStatus);
        MakeSkill(hud,"Dash",2,new Vector2(-edge,edge+B.SkillCaption),"冲刺",out dashMask,out dashStatus);

        RectTransform overlay=Rect("Modal",canvasRect);Stretch(overlay);modal=overlay.gameObject;
        Paint(overlay,new Color(.01f,.02f,.015f,.8f)).raycastTarget=true;
        window=Panel("Dialog",overlay,new Vector2(.5f,.5f),Vector2.zero,ZombieHudTheme.UpgradeDialog.Size);
        title=Label("Title",window,"本局记录",ZombieHudTheme.Title,new Vector2(32,-24),new Vector2(696,46));
        var detailViewport=Rect("DetailViewport",window);
        Place(detailViewport,new Vector2(0,1),new Vector2(32,-86),new Vector2(696,340));
        detailViewport.gameObject.AddComponent<RectMask2D>();Paint(detailViewport,Color.clear).raycastTarget=true;
        detail=Label("Detail",detailViewport,"",ZombieHudTheme.Body,Vector2.zero,new Vector2(696,340));
        detail.alignment=TextAlignmentOptions.TopLeft;detail.lineSpacing=8;
        detailScroll=detailViewport.gameObject.AddComponent<ScrollRect>();
        detailScroll.viewport=detailViewport;detailScroll.content=detail.rectTransform;
        detailScroll.horizontal=false;detailScroll.movementType=ScrollRect.MovementType.Clamped;detailScroll.scrollSensitivity=30;
        choices=new Button[RunState.UpgradeChoiceCount];
        choiceLabels=new TextMeshProUGUI[RunState.UpgradeChoiceCount];
        choiceIcons=new Image[RunState.UpgradeChoiceCount];
        for(int i=0;i<choices.Length;i++)
        {
            var position=ZombieHudTheme.UpgradeDialog.FirstCardPosition+Vector2.down*(i*ZombieHudTheme.UpgradeDialog.CardStep);
            choices[i]=Button("UpgradeChoice"+i,window,"",new Vector2(0,1),position,ZombieHudTheme.UpgradeDialog.CardSize);
            choiceIcons[i]=Art("Icon",choices[i].transform,8+i,new Vector2(16,-15),new Vector2(56,56));
            choiceLabels[i]=Label("Description",choices[i].transform,"",ZombieHudTheme.Body,new Vector2(88,-9),new Vector2(592,68));
            choiceLabels[i].enableAutoSizing=true;
            choiceLabels[i].fontSizeMin=B.Small;
            choiceLabels[i].fontSizeMax=ZombieHudTheme.Body;
        }
        resume=Button("Resume",window,"继续战斗",Vector2.zero,new Vector2(32,96),new Vector2(696,52));
        restart=Button("Restart",window,"再来一局",Vector2.zero,new Vector2(32,28),new Vector2(340,52));
        menu=Button("MainMenu",window,"返回主菜单",Vector2.zero,new Vector2(388,28),new Vector2(340,52));
        modal.SetActive(false);
    }

    private Image ProgressTrack(string name,Transform parent,Vector2 position,Vector2 size,Color trackColor,Color fillColor)
    {
        var track=Box(name+"Track",parent,position,size,trackColor);
        var bar=Rect(name+"Fill",track);Stretch(bar);
        var image=Paint(bar,fillColor);image.sprite=Chrome(3);return image;
    }

    private void ResolvePolishReferences()
    {
        if(roomObjectiveGroup!=null) roomObjectiveGroup.alpha=1f;
        if(upgradeBackground!=null) upgradeBackground.color=Color.white;
    }

    private void BuildUpgradeWidgets()
    {
        foreach(var upgrade in run.Upgrades)
        {
            var root=Instantiate(upgradeTemplate,upgradeContent);root.name="Upgrade_"+upgrade.Id;
            var icon=root.transform.Find("UpgradeIcon").GetComponent<Image>();
            icon.sprite=Icon(RunUpgradePresentation.Icon(upgrade));icon.color=Color.white;
            root.transform.Find("BranchFrame").GetComponent<Image>().color=RunUpgradePresentation.Color(upgrade.Branch);
            upgradeWidgets.Add(new UpgradeWidget {id=upgrade.Id,rect=root.GetComponent<RectTransform>(),count=root.transform.Find("StackCount").GetComponent<TextMeshProUGUI>()});
        }
    }

    private void MakeSkill(Transform parent,string name,int iconIndex,Vector2 position,string caption,out Image mask,out TextMeshProUGUI status)
    {
        var slot=Panel(name+"Skill",parent,new Vector2(1,0),position,B.SkillSize,true);
        var ringPosition=new Vector2((B.SkillSize.x-B.SkillRing)/2,-6);
        var ring=Paint(Box("ReadyRing",slot,ringPosition,Vector2.one*B.SkillRing,Color.white),Color.white);
        ring.sprite=Chrome(2);
        var skillIcon=Art("Icon",slot,iconIndex,new Vector2((B.SkillSize.x-B.SkillIcon)/2,-12),Vector2.one*B.SkillIcon);
        if(name=="Melee") equippedWeaponIcon=skillIcon;
        var cooldown=Rect("CooldownMask",slot);Place(cooldown,new Vector2(0,1),ringPosition,Vector2.one*B.SkillRing);
        mask=Paint(cooldown,B.Brass);mask.sprite=Chrome(2);
        mask.type=Image.Type.Filled;mask.fillMethod=Image.FillMethod.Radial360;mask.fillOrigin=2;mask.fillAmount=0;
        status=Label("Cooldown",slot,"就绪",B.Small,new Vector2(0,-65),new Vector2(B.SkillSize.x,22),TextAnchor.MiddleCenter);
        var nameLabel=Label("SkillName",slot,caption,B.Small,new Vector2(0,-B.SkillSize.y),new Vector2(B.SkillSize.x,B.SkillCaption),TextAnchor.MiddleCenter);
        nameLabel.color=B.Muted;if(name=="Melee") weaponCaption=nameLabel;
    }

    private Sprite Chrome(int index)=>chrome!=null && index>=0 && index<chrome.Length?chrome[index]:null;
    private Sprite WeaponIcon(SurvivorWeaponKind kind)
    {
        switch(kind)
        {
            case SurvivorWeaponKind.Bow:return Icon(19);
            case SurvivorWeaponKind.IronBar:return Icon(20);
            case SurvivorWeaponKind.Hammer:return Icon(21);
            default:return Icon(1);
        }
    }
    private static void TintChoice(Button button,string branch)
    {
        // Tint only the selection state; the painted equipment keeps its original colors.
        var colors=button.colors;colors.highlightedColor=RunUpgradePresentation.Color(branch);
        colors.selectedColor=colors.highlightedColor;button.colors=colors;
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
    private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, bool compact = false)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, anchor, position, size);
        var image = Paint(rect, Color.white);
        image.sprite = Chrome(compact ? 1 : 0);
        image.type = Image.Type.Sliced;
        if (image.sprite == null) image.color = ZombieHudTheme.Panel;
        return rect;
    }
    private TextMeshProUGUI Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
        => Label(name, parent, value, size, new Vector2(0f, 1f), position, dimensions, alignment);
    private TextMeshProUGUI Label(string name, Transform parent, string value, int size, Vector2 anchor, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, anchor, position, dimensions);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = textFont;
        label.fontSize = size;
        label.text = value;
        label.color = ZombieHudTheme.Text;
        label.alignment = Alignment(alignment);
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.extraPadding = true;
        return label;
    }
    private static TextAlignmentOptions Alignment(TextAnchor value)
    {
        switch(value)
        {
            case TextAnchor.UpperLeft:return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleCenter:return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:return TextAlignmentOptions.BottomRight;
            default:return TextAlignmentOptions.Left;
        }
    }
    private Button Button(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = Panel(name, parent, anchor, position, size);
        Image background = rect.GetComponent<Image>();
        background.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        UiButtonSound.Ensure(button);
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, .90f, .68f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        if (!string.IsNullOrEmpty(text)) Label("Label", rect, text, ZombieHudTheme.Heading, Vector2.zero, size, TextAnchor.MiddleCenter);
        return button;
    }
}
