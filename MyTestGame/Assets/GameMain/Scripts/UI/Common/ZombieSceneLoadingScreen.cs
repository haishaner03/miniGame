using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

    /// <summary>A persistent UGUI cover played over an already initialized battle scene.</summary>
[DisallowMultipleComponent]
public sealed class ZombieSceneLoadingScreen : MonoBehaviour
{
    public Slider progressSlider;
    public TMP_Text destinationLabel, statusLabel, percentLabel;
    public Button failureReturn;
    private static ZombieSceneLoadingScreen instance;
    public static bool IsLoading => instance != null && instance.busy;
    public float DisplayedProgress { get; private set; }
    public float LastDisplaySeconds { get; private set; }
    private bool busy;
    private int lastPercent = -1, lastDots = -1;
    private float previousTimeScale;
    private const float CompletionHold = .1f;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        if (failureReturn != null)
        {
            UiButtonSound.Ensure(failureReturn);
            failureReturn.onClick.AddListener(ReturnAfterFailure);
        }
    }

    public static void Load(GameObject prefab, string scenePath, string destination,
        float minimumSeconds, Action completed)
    {
        if (IsLoading) throw new InvalidOperationException("A scene transition is already in progress.");
        if (!Application.CanStreamedLevelBeLoaded(scenePath))
            throw new InvalidOperationException("Scene is not enabled in Build Settings: " + scenePath);
        if (instance == null)
        {
            if (prefab == null) throw new InvalidOperationException("Run config is missing its scene loading prefab.");
            instance = Instantiate(prefab).GetComponent<ZombieSceneLoadingScreen>();
            if (instance == null) throw new InvalidOperationException("Loading prefab has no ZombieSceneLoadingScreen.");
        }
        instance.gameObject.SetActive(true);
        instance.busy = true;
        instance.previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        instance.StartCoroutine(instance.LoadRoutine(scenePath, destination, Mathf.Max(0f, minimumSeconds), completed));
    }

    private IEnumerator LoadRoutine(string path, string destination, float minimum, Action completed)
    {
        float start = Time.realtimeSinceStartup;
        lastPercent = lastDots = -1;
        if (destinationLabel != null) destinationLabel.text = destination;
        if (failureReturn != null) failureReturn.gameObject.SetActive(false);
        Display(0f);
        SetStatus("正在加载街区", 0);
        AsyncOperation operation = null;
        string error = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Unity returned no scene loading operation.");
            // Enter the destination as soon as Unity is ready. The visual timer never gates activation.
        }
        catch (Exception exception) { error = exception.Message; }
        if (error != null) { ShowFailure(error); yield break; }

        while (!operation.isDone)
        {
            SetStatus("正在加载街区", Time.realtimeSinceStartup - start);
            yield return null;
        }
        // LevelFlowController binds the room in Start; give it a full frame while simulation remains paused.
        yield return null;
        // The map, player, colliders and enemies now exist under the opaque overlay.
        // Play the cosmetic slider for the requested duration, independent of scene loading progress.
        float coverStartedAt = Time.realtimeSinceStartup;
        float presentationSeconds = Mathf.Max(.01f, minimum - CompletionHold);
        while (Time.realtimeSinceStartup - coverStartedAt < presentationSeconds)
        {
            float elapsed = Time.realtimeSinceStartup - coverStartedAt;
            Display(Mathf.Clamp01(elapsed / presentationSeconds));
            SetStatus("正在准备战斗", elapsed);
            yield return null;
        }
        Display(1f);
        if (statusLabel != null) statusLabel.text = "加载完成";
        yield return new WaitForSecondsRealtime(CompletionHold);
        LastDisplaySeconds = Time.realtimeSinceStartup - start;
        try { completed?.Invoke(); }
        catch (Exception exception) { error = exception.Message; }
        if (error != null) { ShowFailure(error); yield break; }
        busy = false;
        gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    private void Display(float progress)
    {
        DisplayedProgress = Mathf.Max(DisplayedProgress, progress);
        if (progress == 0f) DisplayedProgress = 0f;
        if (progressSlider != null) progressSlider.SetValueWithoutNotify(DisplayedProgress);
        int percent = Mathf.FloorToInt(DisplayedProgress * 100f);
        if (percentLabel != null && percent != lastPercent) percentLabel.SetText("{0}%", percent);
        lastPercent = percent;
    }

    private void SetStatus(string caption, float elapsed)
    {
        int dots = Mathf.FloorToInt(elapsed * 3f) % 4;
        if (statusLabel == null) return;
        string suffix = dots == 0 ? "" : dots == 1 ? "." : dots == 2 ? ".." : "...";
        string next = caption + suffix;
        if (dots != lastDots || statusLabel.text != next) statusLabel.text = next;
        lastDots = dots;
    }

    private void ShowFailure(string message)
    {
        Debug.LogError("Scene transition failed: " + message);
        if (statusLabel != null) statusLabel.text = "加载失败，请返回主菜单重试";
        if (failureReturn != null) failureReturn.gameObject.SetActive(true);
    }

    private void ReturnAfterFailure()
    {
        var config = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        if (config == null || !Application.CanStreamedLevelBeLoaded(config.menuScenePath)) return;
        busy = false;
        Time.timeScale = 1f;
        gameObject.SetActive(false);
        SceneManager.LoadSceneAsync(config.menuScenePath);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        if (busy) Time.timeScale = previousTimeScale;
        instance = null;
    }
}
