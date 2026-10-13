from pathlib import Path
import re
p=Path('D:/UnityProject/miniGame/MyTestGame/Assets/GameMain/Scripts/UI/Battle/ZombieRunUI.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('using UnityEngine.UI;', 'using UnityEngine.UI;\nusing TMPro;\nusing B = ZombieHudTheme.Battle;')
s=re.sub(r'(?<![.\w])Text\b','TextMeshProUGUI',s)
s=s.replace('private Sprite[] icons = new Sprite[17];', 'private Sprite[] icons = new Sprite[24];\n    [SerializeField] private TMP_FontAsset textFont;\n    [SerializeField] private Sprite[] chrome = new Sprite[5];\n    [SerializeField] private TextMeshProUGUI weaponCaption;')
s=s.replace('idleRoomOpacity = 0.28f','idleRoomOpacity = 0.78f')
s=s.replace('BuildLayout(run.Config.uiFont, icons);','BuildLayout(run.Config.uiFont, icons, textFont, chrome);')
s=s.replace('ZombieHudTheme.Critical : ZombieHudTheme.Accent;', 'ZombieHudTheme.Critical : B.Health;')
s=s.replace('equippedWeaponIcon.sprite = melee.EquippedWeapon.sprite;', 'equippedWeaponIcon.sprite = WeaponIcon(melee.EquippedWeapon.kind);\n            if (weaponCaption != null) weaponCaption.text = run.WeaponName;')
s=s.replace('            meleeMask.sprite = equippedWeaponIcon.sprite;\n            meleeMask.preserveAspect = true;\n','')
s=s.replace('"出口已解锁 · 前往安全门" : encounterActive', '"出口已解锁" : encounterActive',1)
s=s.replace('"向下一个房间进发 · 残骸 "','"前往安全门 · 残骸 "')
s=s.replace('choiceIcons[i].color = RunUpgradePresentation.Color(upgrade.Branch);', 'choiceIcons[i].color = Color.white;\n            TintChoice(choices[i], upgrade.Branch);')
s=s.replace('widget.rect.anchoredPosition = new Vector2((visibleSlot / 2) * 64f, -(visibleSlot % 2) * 64f);','widget.rect.anchoredPosition = new Vector2(visibleSlot * B.SlotStep, 0f);')
start=s.index('        float upgradeHeight = visibleSlot == 0')
end=s.index('        if (modal.activeSelf)', start)
s=s[:start]+'''        float maxWidth = Mathf.Min(B.UpgradeMaxWidth, canvasRect.rect.width - B.SkillSize.x * 2f - B.Edge * 4f - B.Gap);
        float contentWidth = visibleSlot > 0 ? visibleSlot * B.SlotStep - B.Gap : 0f;
        float width = Mathf.Clamp(contentWidth + B.Padding * 2f, B.UpgradeMinWidth, Mathf.Max(B.UpgradeMinWidth, maxWidth));
        upgradeRow.sizeDelta = new Vector2(width, B.UpgradeHeight);
        upgradeContent.sizeDelta = new Vector2(Mathf.Max(width - B.Padding * 2f, contentWidth), B.Slot);
        upgradeScroll.horizontal = contentWidth > width - B.Padding * 2f + 1f;
        if (!upgradeScroll.horizontal) upgradeContent.anchoredPosition = Vector2.zero;
        if (upgradeScrollHint != null) upgradeScrollHint.gameObject.SetActive(upgradeScroll.horizontal);
''' + s[end:]
s=s.replace('ZombieHudTheme.Text : ZombieHudTheme.Accent;', 'ZombieHudTheme.Text : B.Ready;')
start=s.index('    public void BuildLayout(')
end=s.index('    private void ResolvePolishReferences()',start)
s=s[:start]+'''    public void BuildLayout(Font uiFont, Sprite[] artwork, TMP_FontAsset uiTextFont = null, Sprite[] chromeArtwork = null)
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
        Art("WeaponIcon",player,1,new Vector2(12,-98),new Vector2(20,20));
        weaponText = Label("Weapon",player,"武器 · 砍刀",B.Small,new Vector2(38,-97),new Vector2(218,22));
        weaponText.color = B.Muted;

        RectTransform objective = Panel("RoomObjective",hud,new Vector2(.5f,1),new Vector2(0,-edge),B.ObjectiveSize);
        roomObjectivePanel=objective;
        roomObjectiveGroup=objective.gameObject.AddComponent<CanvasGroup>();
        roomObjectiveGroup.alpha=1;
        Art("RoomIcon",objective,5,new Vector2(12,-24),new Vector2(42,46));
        roomText=Label("Room",objective,"房间 1 / 5",B.Heading,new Vector2(64,-5),new Vector2(224,28));
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
        Box("StackBadge",slot,new Vector2(B.Slot-23,-B.Slot+17),new Vector2(22,16),new Color(.03f,.04f,.035f,.96f));
        Label("StackCount",slot,"×1",B.Small,new Vector2(B.Slot-24,-B.Slot+18),new Vector2(22,17),TextAnchor.MiddleRight);
        upgradeTemplate=slot.gameObject;upgradeTemplate.SetActive(false);
        MakeSkill(hud,"Melee",1,new Vector2(-edge-B.SkillSize.x-B.Gap,edge+B.SkillCaption),"砍刀",out meleeMask,out meleeStatus);
        MakeSkill(hud,"Dash",2,new Vector2(-edge,edge+B.SkillCaption),"冲刺",out dashMask,out dashStatus);

        RectTransform overlay=Rect("Modal",canvasRect);Stretch(overlay);modal=overlay.gameObject;
        Paint(overlay,new Color(.01f,.02f,.015f,.8f)).raycastTarget=true;
        window=Panel("Dialog",overlay,new Vector2(.5f,.5f),Vector2.zero,new Vector2(760,600));
        title=Label("Title",window,"本局记录",ZombieHudTheme.Title,new Vector2(32,-24),new Vector2(696,46));
        var detailViewport=Rect("DetailViewport",window);
        Place(detailViewport,new Vector2(0,1),new Vector2(32,-86),new Vector2(696,340));
        detailViewport.gameObject.AddComponent<RectMask2D>();Paint(detailViewport,Color.clear).raycastTarget=true;
        detail=Label("Detail",detailViewport,"",ZombieHudTheme.Body,Vector2.zero,new Vector2(696,340));
        detail.alignment=TextAlignmentOptions.TopLeft;detail.lineSpacing=8;
        detailScroll=detailViewport.gameObject.AddComponent<ScrollRect>();
        detailScroll.viewport=detailViewport;detailScroll.content=detail.rectTransform;
        detailScroll.horizontal=false;detailScroll.movementType=ScrollRect.MovementType.Clamped;detailScroll.scrollSensitivity=30;
        for(int i=0;i<choices.Length;i++)
        {
            choices[i]=Button("UpgradeChoice"+i,window,"",new Vector2(0,1),new Vector2(32,-154-i*112),new Vector2(696,96));
            choiceIcons[i]=Art("Icon",choices[i].transform,8+i,new Vector2(16,-18),new Vector2(56,56));
            choiceLabels[i]=Label("Description",choices[i].transform,"",ZombieHudTheme.Body,new Vector2(88,-10),new Vector2(592,76));
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

''' + s[end:]
start=s.index('    private void ResolvePolishReferences()')
end=s.index('    private Sprite Icon(',start)
s=s[:start]+'''    private void ResolvePolishReferences()
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
''' + s[end:]
s=s.replace('private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)', 'private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, bool compact = false)')
s=s.replace('''        Paint(rect, ZombieHudTheme.Panel);
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = ZombieHudTheme.Border;
        outline.effectDistance = new Vector2(1f, -1f);''', '''        var image = Paint(rect, Color.white);
        image.sprite = Chrome(compact ? 1 : 0);
        image.type = Image.Type.Sliced;
        if (image.sprite == null) image.color = ZombieHudTheme.Panel;''')
s=s.replace('label.font = font;','label.font = textFont;')
s=s.replace('label.alignment = alignment;','label.alignment = Alignment(alignment);')
s=s.replace('label.horizontalOverflow = HorizontalWrapMode.Wrap;\n        label.verticalOverflow = VerticalWrapMode.Truncate;', 'label.enableWordWrapping = true;\n        label.overflowMode = TextOverflowModes.Ellipsis;\n        label.extraPadding = true;')
s=s.replace('colors.highlightedColor = new Color(1.2f, 1.4f, 1.1f);','colors.highlightedColor = new Color(1f, .90f, .68f);')
idx=s.index('    private Button Button(')
s=s[:idx]+'''    private static TextAlignmentOptions Alignment(TextAnchor value)
    {
        switch(value)
        {
            case TextAnchor.UpperLeft:return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleCenter:return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft:return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:return TextAlignmentOptions.BottomRight;
            default:return TextAlignmentOptions.MidlineLeft;
        }
    }
''' +s[idx:]
p.write_text(s,encoding='utf-8')
print('Updated battle layout and TMP labels')
