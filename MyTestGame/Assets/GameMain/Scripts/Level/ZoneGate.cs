using System.Collections;
using UnityEngine;

/// <summary>
/// 区域路障：绑定的波次清理完成后打开，不受其它区域和随机增援影响。
/// 用于线性推进关卡，把地图切成若干段。
/// </summary>
[DisallowMultipleComponent]
public sealed class ZoneGate : MonoBehaviour
{
    [SerializeField] private ZombieWaveTrigger requiredWave;
    [SerializeField] private float openDelay = 0.6f;
    [SerializeField] private float fadeDuration = 0.6f;

    private ZombieSpawner spawner;
    private ZombieGridPathfinder pathfinder;
    private bool opening;

    public bool IsOpen { get; private set; }
    public ZombieWaveTrigger RequiredWave => requiredWave;

    private void Start()
    {
        spawner = FindFirstObjectByType<ZombieSpawner>();
        pathfinder = FindFirstObjectByType<ZombieGridPathfinder>();
    }

    private void Update()
    {
        if (IsOpen || opening || requiredWave == null || spawner == null)
            return;

        if (requiredWave.IsCleared)
            StartCoroutine(OpenRoutine());
    }

    public void OpenImmediately()
    {
        StopAllCoroutines();
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        IsOpen = true;
        opening = false;
    }

    private IEnumerator OpenRoutine()
    {
        opening = true;
        yield return new WaitForSeconds(Mathf.Max(0f, openDelay));

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        // 碰撞体关闭后等一个物理帧再重建，确保 OverlapCircle 不再命中路障。
        yield return new WaitForFixedUpdate();
        if (pathfinder != null)
            pathfinder.Rebuild();

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Color[] start = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            start[i] = renderers[i].color;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                Color c = start[i];
                c.a *= a;
                renderers[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        IsOpen = true;
        opening = false;
        Debug.Log("Zone gate opened: " + name);
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            return;
        Gizmos.color = IsOpen ? new Color(0.3f, 1f, 0.4f, 0.4f) : new Color(1f, 0.85f, 0.1f, 0.5f);
        Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
    }
}
