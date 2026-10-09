using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        AddListener(backButton, ShowMain);

        if (chapterButtons == null)
            return;

        for (int i = 0; i < chapterButtons.Length; i++)
        {
            int chapterIndex = i;
            if (chapterButtons[i] != null)
            {
                chapterButtons[i].onClick.RemoveAllListeners();
                chapterButtons[i].onClick.AddListener(() => LoadChapter(chapterIndex));
            }
        }
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }

    public void ShowMain()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);
        if (chapterPanel != null)
            chapterPanel.SetActive(false);
    }

    public void ShowChapters()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (chapterPanel != null)
            chapterPanel.SetActive(true);
    }

    public void StartFirstChapter()
    {
        LoadChapter(0);
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
        weaponPicker.Show(scenePath);
    }

    private void OnDestroy()
    {
        if (weaponPicker != null) Destroy(weaponPicker.gameObject);
    }

    private void OnOptionsClicked()
    {
        Debug.Log("Options panel is reserved for the next UI pass.");
    }

    private void QuitGame()
    {
        Debug.Log("Quit requested.");
        Application.Quit();
    }
}

