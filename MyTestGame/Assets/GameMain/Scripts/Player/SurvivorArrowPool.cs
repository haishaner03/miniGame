using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent run-owned projectile pool; no instantiate/destroy in steady-state shooting.</summary>
public sealed class SurvivorArrowPool : MonoBehaviour
{
    private readonly Queue<SurvivorArrowProjectile> idle = new Queue<SurvivorArrowProjectile>();
    private readonly List<SurvivorArrowProjectile> active = new List<SurvivorArrowProjectile>();
    private GameObject prefab;
    private bool ticking, clearRequested;
    public int CreatedCount { get; private set; }
    public int ActiveCount => active.Count;

    public void Initialize(GameObject arrowPrefab)
    {
        prefab = arrowPrefab;
        for (int i = 0; i < 24; i++) idle.Enqueue(Create());
    }
    private SurvivorArrowProjectile Create()
    {
        var root = prefab != null ? Instantiate(prefab, transform) : new GameObject("ArrowProjectile");
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        var arrow = root.GetComponent<SurvivorArrowProjectile>();
        if (arrow == null) arrow = root.AddComponent<SurvivorArrowProjectile>();
        CreatedCount++;
        return arrow;
    }
    public void Fire(Vector2 origin, Vector2 direction, int damage, float speed, float lifetime, int pierce, float knockback, RunState run)
    {
        if (active.Count >= 256) return;
        var arrow = idle.Count > 0 ? idle.Dequeue() : Create();
        arrow.transform.position = origin;
        arrow.gameObject.SetActive(true);
        arrow.Launch(direction, damage, speed, lifetime, pierce, knockback, run);
        active.Add(arrow);
    }
    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        ticking = true;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].Tick(Time.deltaTime)) Release(i);
            if (clearRequested) break;
        }
        ticking = false;
        if (clearRequested) { clearRequested = false; Clear(); }
    }
    private void Release(int index)
    {
        var arrow = active[index];
        active.RemoveAt(index);
        arrow.gameObject.SetActive(false);
        idle.Enqueue(arrow);
    }
    public void Clear()
    {
        if (ticking) { clearRequested = true; return; }
        for (int i = active.Count - 1; i >= 0; i--) Release(i);
    }
}
