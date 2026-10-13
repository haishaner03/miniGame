using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScrapPickupPool : MonoBehaviour
{
    private RunState run;
    private Transform root;
    private readonly Queue<ScrapPickup> idle = new Queue<ScrapPickup>();
    private readonly List<ScrapPickup> active = new List<ScrapPickup>();
    private const int MaxDrops = 96;

    public void Initialize(RunState state)
    {
        run = state;
        root = new GameObject("ScrapPool").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < run.Config.scrapPoolPrewarm; i++)
        {
            var pickup = Create();
            if (pickup != null) idle.Enqueue(pickup);
        }
    }

    private ScrapPickup Create()
    {
        if (run.Config.scrapPickupPrefab == null) return null;
        var go = Instantiate(run.Config.scrapPickupPrefab, root);
        go.SetActive(false);
        return go.GetComponent<ScrapPickup>();
    }

    public void Drop(Vector3 position, int amount)
    {
        if (amount <= 0 || run.Config.scrapPickupPrefab == null) return;
        if (active.Count >= MaxDrops)
        {
            if (active.Count > 0) active[0].AddValue(amount);
            return;
        }
        var pickup = idle.Count > 0 ? idle.Dequeue() : Create();
        if (pickup == null) return;
        pickup.ResetDrop(position, amount);
        pickup.gameObject.SetActive(true);
        active.Add(pickup);
    }

    private void Update()
    {
        if (run == null || run.Phase != RunState.RunPhase.Playing || run.Player == null || run.Player.IsDead) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (!active[i].Tick(run.Player.transform.position, Time.deltaTime)) continue;
            int value = active[i].Value;
            Release(i);
            run.GainScrap(value);
        }
    }

    private void Release(int index)
    {
        var pickup = active[index];
        active.RemoveAt(index);
        pickup.gameObject.SetActive(false);
        idle.Enqueue(pickup);
    }

    public void CollectAll()
    {
        int value = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            value += active[i].Value;
            Release(i);
        }
        run.GainScrap(value);
    }

    public void ClearDrops()
    {
        for (int i = active.Count - 1; i >= 0; i--) Release(i);
    }
}
