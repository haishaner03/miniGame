using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Presentation and local dialogs; mode loading stays in ZombieUiFlow.</summary>
[DisallowMultipleComponent]
public sealed class CommandRoomMenuView : MonoBehaviour
{
    [Header("菜单")]
    public ZombieUiFlow flow;
    public CanvasGroup navigation;
    public CanvasGroup chapterNavigation;
    public Button defaultButton;
    [Header("设置与退出")]
    public GameObject dialogLayer, settingsPanel, quitPanel;
    public Button settingsClose, quitCancel, quitConfirm;
    public Slider master, music, effects, ui;
    public TMP_Text masterValue, musicValue, effectsValue, uiValue;

    private GameObject previousSelection;
    private bool dialogOpen, settingsOpen, volumesDirty;
    private UnityGameFramework.Runtime.DebuggerComponent debugger;
    private bool debuggerWasEnabled;

    private void Awake()
    {
        settingsClose.onClick.AddListener(CloseDialogs);
        quitCancel.onClick.AddListener(CloseDialogs);
        quitConfirm.onClick.AddListener(ConfirmQuit);
        master.onValueChanged.AddListener(OnVolumeChanged);
        music.onValueChanged.AddListener(OnVolumeChanged);
        effects.onValueChanged.AddListener(OnVolumeChanged);
        ui.onValueChanged.AddListener(OnVolumeChanged);
        debugger = FindFirstObjectByType<UnityGameFramework.Runtime.DebuggerComponent>();
        if (debugger != null) { debuggerWasEnabled = debugger.enabled; debugger.enabled = false; }
    }

    private void Start() { FocusDefault(); }

    public void FocusDefault()
    {
        if (defaultButton != null && defaultButton.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }

    public void BlockForExternalModal()
    {
        SetNavigation(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void RestoreAfterExternalModal(Button target)
    {
        SetNavigation(true);
        if (target != null && target.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    private void SetNavigation(bool available)
    {
        if (navigation != null) { navigation.interactable = available; navigation.blocksRaycasts = available; }
        if (chapterNavigation != null) { chapterNavigation.interactable = available; chapterNavigation.blocksRaycasts = available; }
    }

    public void OpenSettings()
    {
        var audio = ZombieAudioController.Ensure();
        if (audio == null) return;
        master.SetValueWithoutNotify(audio.MasterVolume);
        music.SetValueWithoutNotify(audio.MusicVolume);
        effects.SetValueWithoutNotify(audio.EffectsVolume);
        ui.SetValueWithoutNotify(audio.UiVolume);
        RefreshVolumeLabels();
        OpenDialog(settingsPanel, master.gameObject);
        settingsOpen = true;
    }

    public void OpenQuit() { OpenDialog(quitPanel, quitCancel.gameObject); }

    private void OpenDialog(GameObject panel, GameObject firstControl)
    {
        if (dialogOpen) CloseDialogs();
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        dialogLayer.SetActive(true);
        settingsPanel.SetActive(panel == settingsPanel);
        quitPanel.SetActive(panel == quitPanel);
        SetNavigation(false);
        dialogOpen = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstControl);
    }

    private void OnVolumeChanged(float value)
    {
        RefreshVolumeLabels();
        var audio = ZombieAudioController.Instance;
        if (audio == null || !settingsOpen) return;
        // Preview immediately, then persist once when the window closes.
        audio.SetVolumes(master.value, music.value, effects.value, ui.value, audio.Muted, false);
        volumesDirty = true;
    }

    private void RefreshVolumeLabels()
    {
        masterValue.SetText("{0:0}%", master.value * 100f);
        musicValue.SetText("{0:0}%", music.value * 100f);
        effectsValue.SetText("{0:0}%", effects.value * 100f);
        uiValue.SetText("{0:0}%", ui.value * 100f);
    }

    public void CloseDialogs()
    {
        if (volumesDirty && ZombieAudioController.Instance != null)
            ZombieAudioController.Instance.SaveVolumes();
        volumesDirty = settingsOpen = dialogOpen = false;
        if (dialogLayer != null) dialogLayer.SetActive(false);
        SetNavigation(true);
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void ConfirmQuit()
    {
        CloseDialogs();
        Application.Quit();
#if UNITY_EDITOR
        Debug.Log("退出游戏：独立构建会退出应用；编辑器中保留运行。", this);
#endif
    }

    private void Update()
    {
        if (dialogOpen && Input.GetKeyDown(KeyCode.Escape)) CloseDialogs();
    }

    private void OnDestroy()
    {
        if (volumesDirty && ZombieAudioController.Instance != null) ZombieAudioController.Instance.SaveVolumes();
        if (debugger != null) debugger.enabled = debuggerWasEnabled;
    }
}
