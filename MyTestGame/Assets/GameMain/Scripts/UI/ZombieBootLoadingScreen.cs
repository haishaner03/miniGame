using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ZombieBootLoadingScreen : MonoBehaviour
{
    public Slider progressSlider;
    public Text statusLabel;
    public Text percentLabel;
    [Min(0f)] public float minimumDisplaySeconds = 4f;
    [Min(0f)] public float completionHoldSeconds = 0.4f;

    public static ZombieBootLoadingScreen Instance { get; private set; }
    public static bool CanContinue
    {
        get { return Instance == null || !Instance.gameObject.activeInHierarchy || Instance.IsReady; }
    }

    public float DisplayedProgress { get; private set; }
    public float TargetProgress { get; private set; }
    public bool IsReady { get { return !failed && completed && reachedFullAt >= 0f && Time.unscaledTime - reachedFullAt >= completionHoldSeconds; } }

    private float startedAt;
    private float reachedFullAt = -1f;
    private bool completed;
    private bool failed;

    private void Awake()
    {
        Instance = this;
        startedAt = Time.unscaledTime;
        TargetProgress = 0.04f;
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.interactable = false;
            progressSlider.SetValueWithoutNotify(0f);
        }
        SetStatus("\u6b63\u5728\u542f\u52a8\u2026");
        if (percentLabel != null) percentLabel.text = "0%";
    }

    private void Update()
    {
        if (failed) return;
        // The presentation never outruns actual loading, even when the demo timer finishes.
        float presentationLimit = minimumDisplaySeconds <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - startedAt) / minimumDisplaySeconds);
        float next = Mathf.Min(TargetProgress, presentationLimit);
        DisplayedProgress = Mathf.MoveTowards(DisplayedProgress, next, Time.unscaledDeltaTime * 0.65f);
        if (progressSlider != null) progressSlider.SetValueWithoutNotify(DisplayedProgress);
        if (percentLabel != null) percentLabel.text = Mathf.FloorToInt(DisplayedProgress * 100f) + "%";
        if (completed && DisplayedProgress >= 1f && reachedFullAt < 0f)
        {
            reachedFullAt = Time.unscaledTime;
            SetStatus("\u52a0\u8f7d\u5b8c\u6210");
        }
    }

    public static void Report(float progress, string status)
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy || Instance.failed || Instance.completed) return;
        Instance.TargetProgress = Mathf.Max(Instance.TargetProgress, Mathf.Clamp(progress, 0f, 0.95f));
        Instance.SetStatus(status);
    }

    public static void Complete()
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy || Instance.failed) return;
        Instance.completed = true;
        Instance.TargetProgress = 1f;
        Instance.SetStatus("\u6b63\u5728\u8fdb\u5165\u907f\u96be\u6240\u2026");
    }

    public static void Fail()
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy) return;
        Instance.failed = true;
        Instance.SetStatus("\u52a0\u8f7d\u5931\u8d25\uff0c\u8bf7\u91cd\u65b0\u542f\u52a8\u6e38\u620f");
    }

    public static void Hide()
    {
        if (Instance != null) Instance.gameObject.SetActive(false);
    }

    private void SetStatus(string status)
    {
        if (statusLabel != null) statusLabel.text = status;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
