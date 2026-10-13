#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>Play-mode smoke check using real UGUI raycasts, with a frame between page changes.</summary>
public static class CommandRoomMenuVerification
{
    private static IEnumerator routine;
    private static double nextStep;
    public static bool IsRunning => routine != null;
    public static string LastResult { get; private set; }

    [MenuItem("Tools/Zombie/UI/Validate Command Room Menu (Play Mode)")]
    public static void Start()
    {
        if (!Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
        if (routine != null) throw new System.InvalidOperationException("Verification is already running.");
        LastResult = "Running";
        routine = Verify();
        nextStep = 0;
        EditorApplication.update += Step;
    }

    private static void Step()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        nextStep = EditorApplication.timeSinceStartup + .16;
        try
        {
            if (Application.isPlaying && routine.MoveNext()) return;
            if (!Application.isPlaying) LastResult = "Stopped: Play Mode ended.";
        }
        catch (System.Exception exception) { LastResult = exception.ToString(); }
        routine = null;
        EditorApplication.update -= Step;
        Debug.Log(LastResult);
    }

    private static IEnumerator Verify()
    {
        var flow = UnityEngine.Object.FindFirstObjectByType<ZombieUiFlow>();
        var view = flow.GetComponent<CommandRoomMenuView>();
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var checks = new System.Collections.Generic.List<string>();
        System.Action<bool,string> check = (condition, name) => {
            if (!condition) throw new System.InvalidOperationException("FAIL: " + name);
            checks.Add("PASS: " + name);
        };
        System.Action<UnityEngine.UI.Button> click = button => {
            check(button != null && button.IsInteractable() && button.gameObject.activeInHierarchy, "available " + button.name);
            UnityEngine.Canvas.ForceUpdateCanvases();
            var rect = (UnityEngine.RectTransform)button.transform;
            var data = new UnityEngine.EventSystems.PointerEventData(eventSystem);
            data.position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(data, hits);
            check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "raycast " + button.name);
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        };
        System.Func<object,string,UnityEngine.UI.Button> privateButton = (owner, field) =>
            (UnityEngine.UI.Button)owner.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner);
        int points = SurvivorMetaProgress.Points;
        check(flow.mainPanel.activeInHierarchy && !flow.chapterPanel.activeInHierarchy, "main is the initial page");
        var seven = new UnityEngine.UI.Button[] { flow.startButton, flow.endlessButton, flow.chapterButton, flow.leaderboardButton, flow.growthButton, flow.optionsButton, flow.quitButton };
        foreach(var button in seven) {
            check(button.GetComponent<UiButtonSound>() != null, "audio " + button.name);
            check(button.navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit, "keyboard navigation " + button.name);
        }
        click(flow.chapterButton);
        yield return null;
        check(flow.chapterPanel.activeInHierarchy && !flow.mainPanel.activeInHierarchy, "chapter page opens");
        click(flow.backButton);
        yield return null;
        check(flow.mainPanel.activeInHierarchy && !flow.chapterPanel.activeInHierarchy, "chapter return opens main");
        
        click(flow.startButton);
        yield return null;
        var picker = UnityEngine.Object.FindFirstObjectByType<StartingWeaponPicker>();
        check(picker != null && !view.navigation.interactable, "campaign opens loadout and blocks underlying navigation");
        var sceneField = typeof(StartingWeaponPicker).GetField("scenePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        check((string)sceneField.GetValue(picker) == flow.chapterScenePaths[0], "campaign starts from first chapter");
        click(privateButton(picker,"cancel"));
        yield return null;
        check(view.navigation.interactable && eventSystem.currentSelectedGameObject == flow.startButton.gameObject, "campaign cancel restores focus");
        
        click(flow.endlessButton);
        yield return null;
        picker = UnityEngine.Object.FindFirstObjectByType<StartingWeaponPicker>();
        var config = UnityEngine.Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        check(picker != null && (string)sceneField.GetValue(picker) == config.endlessConfig.scenePath, "endless loadout targets configured level99");
        click(privateButton(picker,"cancel"));
        yield return null;
        check(view.navigation.interactable && eventSystem.currentSelectedGameObject == flow.endlessButton.gameObject, "endless cancel restores focus");
        
        click(flow.growthButton);
        yield return null;
        var growth = UnityEngine.Object.FindFirstObjectByType<SurvivorMetaShop>();
        check(growth != null && !view.navigation.interactable, "growth opens without underlying keyboard clicks");
        click(privateButton(growth,"close"));
        yield return null;
        check(view.navigation.interactable && eventSystem.currentSelectedGameObject == flow.growthButton.gameObject, "growth closes and restores focus");
        
        click(flow.leaderboardButton);
        yield return null;
        var board = UnityEngine.Object.FindFirstObjectByType<EndlessLeaderboardUI>();
        check(board != null && !view.navigation.interactable, "leaderboard opens");
        click(privateButton(board,"close"));
        yield return null;
        check(view.navigation.interactable && eventSystem.currentSelectedGameObject == flow.leaderboardButton.gameObject, "leaderboard closes and restores focus");
        
        click(flow.optionsButton);
        yield return null;
        check(view.settingsPanel.activeInHierarchy && view.dialogLayer.activeInHierarchy && !view.navigation.interactable, "audio settings opens modally");
        check(UnityEngine.Mathf.Approximately(view.master.value, ZombieAudioController.Instance.MasterVolume), "settings reflects live master volume");
        click(view.settingsClose);
        yield return null;
        check(!view.dialogLayer.activeInHierarchy && view.navigation.interactable, "audio settings closes");
        
        click(flow.quitButton);
        yield return null;
        check(view.quitPanel.activeInHierarchy && !view.navigation.interactable, "quit opens confirmation");
        click(view.quitCancel);
        yield return null;
        check(!view.dialogLayer.activeInHierarchy && view.navigation.interactable, "quit cancel restores menu");
        check(SurvivorMetaProgress.Points == points, "survivor balance unchanged");
        check(flow.survivorPointsLabel.text.Contains(points.ToString()), "live points caption");
        int enabledListeners = 0;
        foreach(var listener in UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(UnityEngine.FindObjectsSortMode.None)) if(listener.isActiveAndEnabled) enabledListeners++;
        check(enabledListeners == 1, "exactly one enabled audio listener");
        view.FocusDefault();
        LastResult = string.Join("\n", checks.ToArray());
        yield break;
    }
}
#endif
