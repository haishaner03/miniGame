#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises actual menu entries and async scene changes without awarding progression.</summary>
public static class CommandRoomLoadingVerification
{
    public static string LastResult { get; private set; }
    public static bool IsRunning => routine != null;
    private static IEnumerator routine;
    private static List<string> checks;
    private static double deadline, nextStep;

    [MenuItem("Tools/Zombie/UI/Validate Scene Loading (Play Mode)")]
    public static void Start()
    {
        if (!Application.isPlaying || routine != null) throw new InvalidOperationException("Enter Play Mode with no verification running.");
        checks = new List<string>(); LastResult = "Running";
        routine = Verify(); deadline = EditorApplication.timeSinceStartup + 90; nextStep = 0;
        EditorApplication.update += Step;
    }
    private static void Step()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        nextStep = EditorApplication.timeSinceStartup + .16;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Scene loading verification timed out.");
            if (Application.isPlaying && routine.MoveNext()) return;
            if (!Application.isPlaying) LastResult = "Stopped";
        }
        catch (Exception exception) { LastResult = exception.ToString(); Time.timeScale = 0f; }
        routine = null; EditorApplication.update -= Step;
        Debug.Log(LastResult);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        checks.Add("PASS: " + message);
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var data = new PointerEventData(EventSystem.current);
        var rect = (RectTransform)button.transform;
        data.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(button.IsInteractable() && hits.Count > 0 &&
            (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "pointer target " + button.name);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    private static Button WeaponButton(StartingWeaponPicker picker) => (Button)typeof(StartingWeaponPicker)
        .GetField("machete", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(picker);

    private static IEnumerator AwaitLoad(string screenshot)
    {
        float start = Time.realtimeSinceStartup, previous = 0;
        bool sawBoundRoom = false, sawEarlyBoundRoom = false, captured = false, hadLoading = false;
        var loader = Object.FindFirstObjectByType<ZombieSceneLoadingScreen>();
        if (loader == null) throw new InvalidOperationException("No active loading screen.");
        while (ZombieSceneLoadingScreen.IsLoading)
        {
            hadLoading = true;
            if (Time.timeScale != 0f) throw new InvalidOperationException("Combat resumed under loading screen.");
            var run = RunState.Instance;
            if (run == null || run.Phase != RunState.RunPhase.Loading) throw new InvalidOperationException("Run left loading phase too early.");
            if (loader.DisplayedProgress < previous) throw new InvalidOperationException("Progress moved backwards.");
            previous = loader.DisplayedProgress;
            string targetPath = run.IsEndless ? run.EndlessConfig.scenePath : run.Rooms[run.RoomIndex];
            if (run.Player != null && run.CurrentRoom != null &&
                SceneManager.GetActiveScene().path == targetPath && run.Player.gameObject.scene == SceneManager.GetActiveScene())
            {
                sawBoundRoom = true;
                if (loader.DisplayedProgress < .9f) sawEarlyBoundRoom = true;
            }
            if (!captured && Time.realtimeSinceStartup - start > .65f)
            {
                System.IO.Directory.CreateDirectory("Captures/CommandRoom");
                ScreenCapture.CaptureScreenshot("Captures/CommandRoom/" + screenshot + ".png");
                captured = true;
            }
            yield return null;
        }
        Check(hadLoading && captured, "loading rendered " + screenshot);
        Check(sawBoundRoom, "room initialized behind loading overlay " + screenshot);
        Check(sawEarlyBoundRoom, "destination player and map are active before slider reaches 90% " + screenshot);
        Check(loader.LastDisplaySeconds >= 1.49f, "minimum 1.5-second display " + screenshot + " (" + loader.LastDisplaySeconds.ToString("0.00") + "s)");
        Check(loader.DisplayedProgress == 1f && !loader.gameObject.activeSelf, "reaches 100% then hides " + screenshot);
        Check(RunState.Instance.Phase == RunState.RunPhase.Playing && Mathf.Approximately(Time.timeScale, 1f), "combat released after loading " + screenshot);
        Time.timeScale = 0f;
    }
    private static IEnumerator Verify()
    {
        var config = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        int points = SurvivorMetaProgress.Points;
        string leaderboard = PlayerPrefs.GetString("ZombieSurvivor.EndlessLeaderboard.v1", "");
        Check(config.sceneLoadingPrefab != null && Mathf.Approximately(config.minimumLoadingSeconds, 1.5f), "prefab and loading duration configured");
        var flow = Object.FindFirstObjectByType<ZombieUiFlow>();
        Check(flow != null && flow.mainPanel.activeInHierarchy, "main menu ready");
        Click(flow.startButton); yield return null;
        var picker = Object.FindFirstObjectByType<StartingWeaponPicker>();
        Click(WeaponButton(picker));
        Check(ZombieSceneLoadingScreen.IsLoading, "campaign selection starts loader immediately");
        int seed = RunState.Instance.Seed;
        RunState.StartNewRun(null, 999);
        Check(RunState.Instance.Seed == seed, "duplicate launch is ignored while loading");
        var wait = AwaitLoad("SceneLoading_Campaign_Runtime");
        while (wait.MoveNext()) yield return wait.Current;
        var run = RunState.Instance;
        Check(!run.IsEndless && run.RoomNumber == 1 && SceneManager.GetActiveScene().path == run.Rooms[0], "campaign loads first route room");
        Check(run.ElapsedSeconds < .2f, "loading excluded from run survival time");
        float elapsed = run.ElapsedSeconds;
        int health = run.CurrentHealth;
        run.RoomCleared(); run.CloseRoomRewardShop();
        bool advanced = run.AdvanceRoom();
        Check(advanced && run.RoomNumber == 2 && ZombieSceneLoadingScreen.IsLoading, "room one exit starts room two loading");
        wait = AwaitLoad("SceneLoading_Room02_Runtime");
        while (wait.MoveNext()) yield return wait.Current;
        Check(run.RoomNumber == 2 && SceneManager.GetActiveScene().path == run.Rooms[1], "room two follows the route");
        Check(run.CurrentHealth == health && run.StartingWeapon == SurvivorWeaponKind.Machete, "room transition preserves health and weapon");
        Check(run.ElapsedSeconds - elapsed < .3f, "inter-room loading excluded from survival time");
        run.ReturnToMenu();
        while (Object.FindFirstObjectByType<ZombieUiFlow>() == null) yield return null;
        yield return null;
        flow = Object.FindFirstObjectByType<ZombieUiFlow>();
        Click(flow.endlessButton); yield return null;
        picker = Object.FindFirstObjectByType<StartingWeaponPicker>();
        Click(WeaponButton(picker));
        wait = AwaitLoad("SceneLoading_Endless_Runtime");
        while (wait.MoveNext()) yield return wait.Current;
        Check(RunState.Instance.IsEndless && SceneManager.GetActiveScene().path == config.endlessConfig.scenePath, "endless loads configured level99");
        Check(RunState.Instance.Endless != null, "endless director initialized");
        // No endless settlement: verification must not add a leaderboard row or spend/earn points.
        Object.Destroy(RunState.Instance.gameObject); yield return null;
        var back = SceneManager.LoadSceneAsync(config.menuScenePath);
        while (!back.isDone) yield return null;
        yield return null;
        Check(SurvivorMetaProgress.Points == points && PlayerPrefs.GetString("ZombieSurvivor.EndlessLeaderboard.v1", "") == leaderboard,
            "progression points and endless leaderboard untouched");
        Time.timeScale = 1f;
        LastResult = string.Join("\n", checks.ToArray());
    }
}
#endif
