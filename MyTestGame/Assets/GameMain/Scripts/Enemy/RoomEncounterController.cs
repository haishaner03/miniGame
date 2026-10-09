using System.Collections.Generic;
using UnityEngine;

/// <summary>Room milestones are tied to run position, independent of the random map template.</summary>
[DisallowMultipleComponent]
public sealed class RoomEncounterController : MonoBehaviour
{
    public enum EncounterStage { None, Waiting, Arrival, Fighting, Defeated }
    public EncounterStage Stage { get; private set; }
    public bool IsBoss { get; private set; }
    public bool IsRequired { get; private set; }
    public bool IsResolved => !IsRequired || Stage == EncounterStage.Defeated;
    public ZombieChampion Enemy { get; private set; }
    public float DefeatedAt { get; private set; }
    public string EnemyName => IsBoss ? "街区暴君" : "猎食者";
    private RunState run;
    private ZombieSpawner spawner;
    private ZombieGridPathfinder nav;
    private float retryAt;
    private bool loggedFailure;
    private readonly List<Vector2> spawnRoute = new List<Vector2>();

    public void Initialize(RunState state, ZombieSpawner roomSpawner)
    {
        run = state; spawner = roomSpawner;
        IsBoss = run.RoomNumber == run.RoomCount;
        IsRequired = IsBoss || run.RoomNumber == run.Config.eliteRoomNumber;
        Stage = IsRequired ? EncounterStage.Waiting : EncounterStage.None;
        nav = FindFirstObjectByType<ZombieGridPathfinder>();
    }

    // Called only after both the kill quota and all objective waves are complete.
    public bool TryResolveObjective()
    {
        if (IsResolved) return true;
        if (Stage == EncounterStage.Waiting && Time.time >= retryAt) SpawnChampion();
        return IsResolved;
    }

    private void SpawnChampion()
    {
        retryAt = Time.time + 1f;
        var prefab = IsBoss ? run.Config.bossEncounterPrefab : run.Config.eliteEncounterPrefab;
        Vector3 position;
        if (prefab == null || prefab.GetComponent<ZombieChampion>() == null || spawner == null || !TryPosition(out position))
        {
            if (!loggedFailure) Debug.LogError("Encounter requires a configured prefab and reachable floor: " + EnemyName);
            loggedFailure = true;
            return;
        }
        spawner.RetireAmbientForEncounter();
        var root = Instantiate(prefab, position, Quaternion.identity);
        Enemy = root.GetComponent<ZombieChampion>();
        Enemy.Initialize(run.Player, spawner);
        Enemy.Health.Died += OnDefeated;
        Stage = EncounterStage.Arrival;
    }

    private bool TryPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (run.Player == null || nav == null) return false;
        nav.Rebuild();
        Vector2 center = run.Player.transform.position;
        // Search near the player, rather than spawning a mandatory enemy in an
        // unreachable courtyard. Validate both floor and path through open gates.
        for (float radius = 4f; radius >= 2f; radius -= .5f)
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!nav.IsWalkablePosition(candidate) || !nav.TryFindPath(candidate, center, spawnRoute)) continue;
                position = candidate;
                return true;
            }
        for (int attempt = 0; attempt < 16; attempt++)
            if (nav.TryRandomWalkable(center, 2f, out position) && nav.TryFindPath(position, center, spawnRoute)) return true;
        return false;
    }

    private void Update()
    {
        if (run == null || run.Phase != RunState.RunPhase.Playing) return;
        if (Stage == EncounterStage.Arrival && Enemy != null && !Enemy.IsArriving)
            Stage = EncounterStage.Fighting;
    }

    private void OnDefeated(ZombieChaser zombie)
    {
        if (Stage == EncounterStage.Defeated) return;
        zombie.Died -= OnDefeated;
        Stage = EncounterStage.Defeated;
        DefeatedAt = Time.time;
        spawner.RetireAmbientForEncounter();
        run.RewardEncounter(IsBoss, zombie.transform.position);
    }

    private void OnDestroy()
    {
        if (Enemy != null) Enemy.Health.Died -= OnDefeated;
    }
}
