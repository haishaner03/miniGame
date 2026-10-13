using System.Collections.Generic;
using UnityEngine;

/// <summary>Run-owned pool; inactive instances are reused and room transitions bank remaining XP.</summary>
[DisallowMultipleComponent]
public sealed class ExperiencePickupPool : MonoBehaviour
{
    private RunState run;
    private Transform root;
    private readonly Queue<ExperiencePickup> idle = new Queue<ExperiencePickup>();
    private readonly List<ExperiencePickup> active = new List<ExperiencePickup>();
    public int ActiveCount => active.Count;
    public int PooledCount => idle.Count;
    public int CreatedCount { get; private set; }
    private const int MaximumGroundDrops = 512;

    public void Initialize(RunState state)
    {
        run = state;
        root = new GameObject("ExperiencePool").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < run.Config.experiencePoolPrewarm; i++) idle.Enqueue(Create());
    }

    private ExperiencePickup Create()
    {
        if (run.Config.experiencePickupPrefab == null)
            throw new System.InvalidOperationException("ExperiencePickup prefab is missing from ZombieRunConfig.");
        GameObject go = Instantiate(run.Config.experiencePickupPrefab, root);
        go.SetActive(false);
        CreatedCount++;
        return go.GetComponent<ExperiencePickup>();
    }

    public void Drop(Vector3 position, int amount)
    {
        if (amount <= 0) return;
        if (active.Count >= MaximumGroundDrops)
        {
            ExperiencePickup nearest = active[0];
            float distance = (nearest.transform.position - position).sqrMagnitude;
            for (int i = 1; i < active.Count; i++)
            {
                float candidate = (active[i].transform.position - position).sqrMagnitude;
                if (candidate < distance) { nearest = active[i]; distance = candidate; }
            }
            nearest.AddValue(amount);
            return;
        }
        ExperiencePickup pickup = idle.Count > 0 ? idle.Dequeue() : Create();
        pickup.ResetDrop(position, amount);
        active.Add(pickup);
        pickup.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (run == null || run.Phase != RunState.RunPhase.Playing || run.Player == null || run.Player.IsDead) return;
        Vector3 target = run.Player.transform.position;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ExperiencePickup pickup = active[i];
            if (!pickup.Tick(target, Time.deltaTime)) continue;
            int value = pickup.Value;
            ReleaseAt(i);
            run.GainExperience(value);
            // The next pickup must wait until all pending level choices have been resolved.
            if (run.Phase != RunState.RunPhase.Playing) break;
        }
    }

    private void ReleaseAt(int index)
    {
        ExperiencePickup pickup = active[index];
        active.RemoveAt(index);
        pickup.gameObject.SetActive(false);
        idle.Enqueue(pickup);
    }

    public void CollectAll()
    {
        int value = 0;
        for (int i = active.Count - 1; i >= 0; i--) { value += active[i].Value; ReleaseAt(i); }
        run.GainExperience(value);
    }

    public void ClearDrops()
    {
        for (int i = active.Count - 1; i >= 0; i--) ReleaseAt(i);
    }
}
