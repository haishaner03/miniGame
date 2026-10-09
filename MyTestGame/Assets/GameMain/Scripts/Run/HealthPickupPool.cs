using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HealthPickupPool : MonoBehaviour
{
    private RunState run;
    private readonly Queue<HealthPickup> idle = new Queue<HealthPickup>();
    private readonly List<HealthPickup> active = new List<HealthPickup>();
    public int ActiveCount => active.Count;
    public int CreatedCount { get; private set; }
    public void Initialize(RunState state) { run = state; for (int i = 0; i < 8; i++) idle.Enqueue(Create()); }
    private HealthPickup Create()
    {
        var go = Instantiate(run.Config.healthPickupPrefab, transform); go.SetActive(false); CreatedCount++;
        return go.GetComponent<HealthPickup>();
    }
    public void Drop(Vector3 position)
    {
        if (active.Count >= 32 || run.Config.healthPickupPrefab == null) return;
        var pickup = idle.Count > 0 ? idle.Dequeue() : Create();
        pickup.ResetDrop(position, 25); pickup.gameObject.SetActive(true); active.Add(pickup);
    }
    private void Update()
    {
        if (run == null || run.Phase != RunState.RunPhase.Playing || run.Player == null || run.Player.IsDead) return;
        for (int i = active.Count - 1; i >= 0; i--) if (active[i].Tick(run.Player, Time.deltaTime)) Release(i);
    }
    private void Release(int i) { var p = active[i]; active.RemoveAt(i); p.gameObject.SetActive(false); idle.Enqueue(p); }
    public void Clear() { for (int i = active.Count - 1; i >= 0; i--) Release(i); }
}
