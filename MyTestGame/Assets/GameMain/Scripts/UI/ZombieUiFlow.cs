using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 丧尸生存游戏的主菜单与章节选择面板控制器。
/// 使用普通 UGUI，不依赖旧的塔防 UI 流程。
/// </summary>
[DisallowMultipleComponent]
public sealed class ZombieUiFlow : MonoBehaviour
{
    [Header("面板")]
    public GameObject mainPanel;
    public GameObject chapterPanel;

    [Header("主菜单按钮")]
    public Button startButton;
    public Button chapterButton;
    public Button optionsButton;
    public Button quitButton;
    public Button growthButton;
    public Button endlessButton;
    public Button leaderboardButton;
    public GameObject leaderboardPrefab;
    private EndlessLeaderboardUI leaderboard;
    public Text survivorPoints;
    public TMP_Text survivorPointsLabel;
    public GameObject growthShopPrefab;
    private SurvivorMetaShop growthShop;

    [Header("章节按钮")]
    public Button backButton;
    public Button[] chapterButtons;
    public string[] chapterScenePaths;
    public bool startOnChapterPanel;
    public GameObject weaponPickerPrefab;
    private StartingWeaponPicker weaponPicker;

    private void Awake()
    {
        WireButtons();
    }

    private void Start()
    {
        // Prefab 实例在 GF 场景切换完成后才会稳定完成序列化引用。
        // 在 Start 再绑定一次，确保首次显示菜单时按钮已经可点击；
        // WireButtons 会先移除旧监听，因此不会产生重复回调。
        WireButtons();
    }

    private void OnEnable()
    {
        if (startOnChapterPanel)
            ShowChapters();
        else
            ShowMain();
    }

    private void WireButtons()
    {
        AddListener(startButton, StartFirstChapter);
        AddListener(chapterButton, ShowChapters);
        AddListener(optionsButton, OnOptionsClicked);
        AddListener(quitButton, QuitGame);
        AddListener(growthButton, ShowGrowth);
        AddListener(endlessButton, StartEndless);
        AddListener(leaderboardButton, ShowLeaderboard);
        AddListener(backButton, ShowMain);

        if (chapterButtons == null)
            return;

        for (int i = 0; i < chapterButtons.Length; i++)
        {
            int chapterIndex = i;
            if (chapterButtons[i] != null)
            {
                UiButtonSound.Ensure(chapterButtons[i]);
                chapterButtons[i].onClick.RemoveAllListeners();
                chapterButtons[i].onClick.AddListener(() => LoadChapter(chapterIndex));
            }
        }
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        UiButtonSound.Ensure(button);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }

    public void ShowMain()
    {
        RefreshPoints();
        if (mainPanel != null)
            mainPanel.SetActive(true);
        if (chapterPanel != null)
            chapterPanel.SetActive(false);
        if (TryGetComponent<CommandRoomMenuView>(out var view)) view.FocusDefault();
    }

    private void RefreshPoints()
    {
        if (survivorPoints != null) survivorPoints.text = "幸存者点数  " + SurvivorMetaProgress.Points;
        if (survivorPointsLabel != null) survivorPointsLabel.SetText("幸存者点数  {0}", SurvivorMetaProgress.Points);
    }

    public void ShowGrowth()
    {
        if (growthShopPrefab == null) return;
        if (growthShop == null) growthShop = Instantiate(growthShopPrefab).GetComponent<SurvivorMetaShop>();
        BlockForModal();
        growthShop.Show(() => { RefreshPoints(); RestoreAfterModal(growthButton); });
        foreach (var button in growthShop.GetComponentsInChildren<Button>())
            if (button.IsInteractable())
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
                break;
            }
    }

    public void ShowChapters()
    {
        if (TryGetComponent<CommandRoomMenuView>(out var view)) view.CloseDialogs();
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (chapterPanel != null)
            chapterPanel.SetActive(true);
        if (backButton != null && UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }

    public void StartFirstChapter()
    {
        LoadChapter(0);
    }

    public void StartEndless()
    {
        var config = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        if (config == null || config.endlessConfig == null) { Debug.LogError("Missing endless mode config."); return; }
        if (SceneUtility.GetBuildIndexByScenePath(config.endlessConfig.scenePath) < 0) { Debug.LogError("Endless scene is not enabled in Build Settings."); return; }
        if (weaponPickerPrefab == null) { RunState.StartEndlessRun(); return; }
        if (weaponPicker == null) weaponPicker = Instantiate(weaponPickerPrefab).GetComponent<StartingWeaponPicker>();
        BlockForModal();
        weaponPicker.Show(config.endlessConfig.scenePath, weapon => RunState.StartEndlessRun(weapon), () => RestoreAfterModal(endlessButton));
    }

    public void ShowLeaderboard()
    {
        if (leaderboardPrefab == null) return;
        if (leaderboard == null) leaderboard = Instantiate(leaderboardPrefab).GetComponent<EndlessLeaderboardUI>();
        BlockForModal();
        leaderboard.Show(() => RestoreAfterModal(leaderboardButton));
    }

    public void LoadChapter(int index)
    {
        if (chapterScenePaths == null || index < 0 || index >= chapterScenePaths.Length)
        {
            Debug.LogWarning("Chapter index is not configured: " + index);
            return;
        }

        string scenePath = chapterScenePaths[index];
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (buildIndex < 0)
        {
            Debug.LogError("Chapter scene is not enabled in Build Settings: " + scenePath);
            return;
        }

        if (weaponPickerPrefab == null) { RunState.StartNewRun(scenePath); return; }
        // This prefab owns a screen-space Canvas and its scaler; keep it a root
        // canvas rather than inheriting the menu's scale and sorting settings.
        if (weaponPicker == null) weaponPicker = Instantiate(weaponPickerPrefab).GetComponent<StartingWeaponPicker>();
        Button returnButton = mainPanel != null && mainPanel.activeInHierarchy ? startButton :
            chapterButtons != null && index < chapterButtons.Length ? chapterButtons[index] : backButton;
        BlockForModal();
        weaponPicker.Show(scenePath, null, () => RestoreAfterModal(returnButton));
    }

    private void BlockForModal()
    {
        if (TryGetComponent<CommandRoomMenuView>(out var view)) view.BlockForExternalModal();
    }

    private void RestoreAfterModal(Button target)
    {
        if (TryGetComponent<CommandRoomMenuView>(out var view)) view.RestoreAfterExternalModal(target);
        else if (target != null && UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    private void OnDestroy()
    {
        if (weaponPicker != null) Destroy(weaponPicker.gameObject);
        if (growthShop != null) Destroy(growthShop.gameObject);
        if (leaderboard != null) Destroy(leaderboard.gameObject);
    }

    private void OnOptionsClicked()
    {
        if (TryGetComponent<CommandRoomMenuView>(out var view)) view.OpenSettings();
    }

    private void QuitGame()
    {
        if (TryGetComponent<CommandRoomMenuView>(out var view)) view.OpenQuit();
        else Application.Quit();
    }
}

