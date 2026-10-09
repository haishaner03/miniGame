using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ZombieRunUI : MonoBehaviour
{
    private RunState run;
    private Font font;
    private RectTransform canvasRect;
    private GameObject modal;
    private RectTransform window;
    private Text hud;
    private Text title;
    private Text detail;
    private readonly Button[] choices = new Button[3];
    private readonly Text[] choiceLabels = new Text[3];
    private Button restart;
    private Button menu;
    private GameObject ownedEventSystem;

    public void Initialize(RunState state)
    {
        run = state;
        font = run.Config.uiFont != null ? run.Config.uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvasRect = Rect("RunCanvas", transform);
        Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform hudRect = Rect("HUD", canvasRect);
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(1f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.offsetMin = new Vector2(20f, -135f);
        hudRect.offsetMax = new Vector2(-20f, -16f);
        hud = Label("RunStatus", hudRect, 20, TextAnchor.UpperLeft);
        var shadow = hud.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);

        RectTransform modalRect = Rect("Modal", canvasRect);
        Stretch(modalRect);
        modal = modalRect.gameObject;
        Image blocker = modal.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.78f);
        window = Rect("Dialog", modalRect);
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        Image background = window.gameObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.12f, 0.12f, 1f);

        title = PositionedLabel("Title", window, 28, -18f, 44f);
        detail = PositionedLabel("Detail", window, 17, -64f, 48f);
        for (int i = 0; i < choices.Length; i++)
        {
            int choiceIndex = i;
            choices[i] = MakeButton("Upgrade" + i, window, -125f - i * 102f, 90f);
            choiceLabels[i] = Label("Description", choices[i].transform, 19, TextAnchor.MiddleLeft);
            RectTransform labelRect = choiceLabels[i].rectTransform;
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);
            choices[i].onClick.AddListener(() =>
            {
                if (choiceIndex < run.OfferedUpgrades.Count)
                    run.ChooseUpgrade(run.OfferedUpgrades[choiceIndex].Id);
            });
        }
        restart = MakeButton("Restart", window, -260f, 56f);
        Label("Label", restart.transform, 22, TextAnchor.MiddleCenter).text = "NEW RUN";
        restart.onClick.AddListener(run.RestartRun);
        menu = MakeButton("Menu", window, -330f, 56f);
        Label("Label", menu.transform, 22, TextAnchor.MiddleCenter).text = "MAIN MENU";
        menu.onClick.AddListener(run.ReturnToMenu);
        HideModal();
    }

    public void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            ownedEventSystem = new GameObject("RunEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            ownedEventSystem.transform.SetParent(transform, false);
        }
    }

    public void DisableOwnedEventSystem()
    {
        if (ownedEventSystem != null) ownedEventSystem.SetActive(false);
    }

    private void Update()
    {
        if (run == null || hud == null) return;
        string objective = run.RewardClaimed ? "EXIT OPEN" : "CLEAR THE STREET";
        hud.text = "ROOM " + run.RoomNumber + " / " + run.RoomCount + "    HP " +
            Mathf.Max(0, run.CurrentHealth) + " / " + run.MaxHealth + "    KILLS " + run.Kills +
            "    " + Mathf.FloorToInt(run.ElapsedSeconds / 60f).ToString("00") + ":" +
            Mathf.FloorToInt(run.ElapsedSeconds % 60f).ToString("00") + "\n" + objective;
        var build = new StringBuilder();
        foreach (var stack in run.UpgradeStacks)
        {
            if (build.Length > 0) build.Append("  |  ");
            build.Append(run.GetUpgrade(stack.Key).Name).Append(" x").Append(stack.Value);
        }
        if (build.Length > 0) hud.text += "\n" + build;
        if (modal.activeSelf)
        {
            window.sizeDelta = new Vector2(Mathf.Min(640f, canvasRect.rect.width - 32f),
                Mathf.Min(460f, canvasRect.rect.height - 32f));
            float buttonHeight = Mathf.Min(90f, (window.sizeDelta.y - 149f) / 3f);
            for (int i = 0; i < choices.Length; i++)
                PlaceTop(choices[i].GetComponent<RectTransform>(), -125f - i * (buttonHeight + 12f), buttonHeight);
        }
    }

    public void ShowChoices()
    {
        modal.SetActive(true);
        title.text = "CHOOSE AN UPGRADE";
        detail.text = "Room " + run.RoomNumber + " cleared";
        restart.gameObject.SetActive(false);
        menu.gameObject.SetActive(false);
        for (int i = 0; i < choices.Length; i++)
        {
            bool available = i < run.OfferedUpgrades.Count;
            choices[i].gameObject.SetActive(available);
            if (!available) continue;
            var upgrade = run.OfferedUpgrades[i];
            choiceLabels[i].text = upgrade.Name + "  [" + (run.StackCount(upgrade.Id) + 1) +
                "/" + upgrade.MaxStacks + "]\n" + upgrade.Description;
        }
        EnsureEventSystem();
        EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
    }

    public void ShowResult(bool victory)
    {
        modal.SetActive(true);
        title.text = victory ? "RUN COMPLETE" : "RUN ENDED";
        detail.text = "Room " + run.RoomNumber + " / " + run.RoomCount + "    Kills " + run.Kills +
            "\nSeed " + run.Seed;
        foreach (Button button in choices) button.gameObject.SetActive(false);
        restart.gameObject.SetActive(true);
        menu.gameObject.SetActive(true);
        EnsureEventSystem();
        EventSystem.current.SetSelectedGameObject(restart.gameObject);
    }

    public void HideModal()
    {
        if (modal != null) modal.SetActive(false);
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

    private Text Label(string name, Transform parent, int size, TextAnchor alignment)
    {
        RectTransform rect = Rect(name, parent);
        Stretch(rect);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Text PositionedLabel(string name, Transform parent, int size, float y, float height)
    {
        Text text = Label(name, parent, size, TextAnchor.MiddleCenter);
        PlaceTop(text.rectTransform, y, height);
        return text;
    }

    private Button MakeButton(string name, Transform parent, float y, float height)
    {
        RectTransform rect = Rect(name, parent);
        PlaceTop(rect, y, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.26f, 0.24f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.75f, 1f, 0.88f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        return button;
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(24f, y - height);
        rect.offsetMax = new Vector2(-24f, y);
    }
}
