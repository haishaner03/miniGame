using System.Collections.Generic;
using UnityEngine;

/// <summary>Time-driven pressure on Level99, using the existing pooled spawner.</summary>
[DisallowMultipleComponent]
public sealed class EndlessModeController : MonoBehaviour
{
    public EndlessDifficulty Difficulty { get; private set; }
    public int ActiveBossCount => livingBosses.Count;
    public int ActiveEnemyCount => (spawner != null ? spawner.ActiveCount : 0) + ActiveBossCount;
    public float NextBossSeconds => nextBossWave;
    public int BossHealth { get { int value = 0; foreach (var boss in livingBosses) value += boss.CurrentHealth; return value; } }
    public int BossMaxHealth { get { int value = 0; foreach (var boss in livingBosses) value += boss.CurrentMaxHealth; return value; } }
    private RunState run;
    private ZombieSpawner spawner;
    private ZombieGridPathfinder nav;
    private float nextReinforcement, nextHorde, nextBossWave, nextBossSpawn, nextGrowth, nextDifficulty;
    private int pendingBosses;
    private readonly List<ZombieChaser> livingBosses = new List<ZombieChaser>(6);
    private readonly Queue<ZombieChaser> bossPool = new Queue<ZombieChaser>();
    private readonly List<Vector2> route = new List<Vector2>(64);

    public void Initialize(RunState state, ZombieSpawner source)
    {
        run = state; spawner = source; nav = FindFirstObjectByType<ZombieGridPathfinder>();
        spawner.ConfigureForEndless(run);
        Difficulty = run.EndlessConfig.Evaluate(0);
        nextReinforcement = 5f;
        nextHorde = run.EndlessConfig.hordeInterval;
        nextBossWave = run.EndlessConfig.firstBossSeconds;
        nextGrowth = nextDifficulty = nextBossSpawn = 0f;
        pendingBosses = 0;
        // Authored region waves belong to the adventure objective, not endless pressure.
        foreach (var trigger in FindObjectsByType<ZombieWaveTrigger>(FindObjectsSortMode.None)) trigger.gameObject.SetActive(false);
        var encounter = GetComponent<RoomEncounterController>();
        if (encounter != null) encounter.enabled = false;
    }

    private void Update()
    {
        if (run == null || !run.IsEndless || run.Phase != RunState.RunPhase.Playing || run.Player == null || run.Player.IsDead) return;
        TickPressure(run.ElapsedSeconds);
    }

    public void TickPressure(float seconds)
    {
        if (run == null || spawner == null || !spawner.SpawningEnabled) return;
        if (seconds >= nextDifficulty)
        {
            Difficulty = run.EndlessConfig.Evaluate(seconds);
            spawner.ApplyEndlessDifficulty(Difficulty);
            nextDifficulty = seconds + .5f;
        }
        if (seconds >= nextReinforcement)
        {
            int deficit = Difficulty.population - spawner.ActiveCount - spawner.PendingAmbientCount;
            if (deficit > 0) spawner.QueueEndlessReinforcements(Mathf.Min(Difficulty.batch, deficit));
            nextReinforcement = seconds + Difficulty.interval;
        }
        if (seconds >= nextHorde)
        {
            int capacity = run.EndlessConfig.livingLimit - spawner.ActiveCount - spawner.PendingAmbientCount;
            spawner.QueueEndlessReinforcements(Mathf.Min(Difficulty.hordeCount, capacity));
            nextHorde = seconds + Mathf.Max(10f, run.EndlessConfig.hordeInterval);
        }
        if (seconds >= nextBossWave)
        {
            int room = run.EndlessConfig.maximumLivingBosses - livingBosses.Count - pendingBosses;
            pendingBosses += Mathf.Max(0, Mathf.Min(Difficulty.bossCount, room));
            nextBossWave = seconds + Mathf.Max(10f, run.EndlessConfig.bossWaveInterval);
        }
        if (pendingBosses > 0 && seconds >= nextBossSpawn)
        {
            if (TrySpawnBoss()) pendingBosses--;
            nextBossSpawn = seconds + 1f;
        }
        if (seconds >= nextGrowth)
        {
            foreach (var boss in livingBosses)
                boss.ApplyEndlessGrowth(Difficulty.bossHealthMultiplier, 1f, Difficulty.damageMultiplier);
            nextGrowth = seconds + run.EndlessConfig.livingStatsRefreshInterval;
        }
    }

    private bool TrySpawnBoss()
    {
        if (nav == null || run.Player == null || run.Config.bossEncounterPrefab == null) return false;
        Vector3 position;
        if (!TryBossPosition(out position)) return false;
        ZombieChaser enemy = null;
        while (bossPool.Count > 0 && enemy == null) enemy = bossPool.Dequeue();
        if (enemy == null)
        {
            enemy = Instantiate(run.Config.bossEncounterPrefab, position, Quaternion.identity, transform).GetComponent<ZombieChaser>();
            enemy.gameObject.SetActive(false);
            enemy.SetPoolReleaseCallback(ReleaseBoss);
            enemy.Died += OnBossDied;
        }
        enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
        enemy.Champion.Initialize(run.Player, spawner);
        enemy.SetSpawnHealth(run.EndlessConfig.bossBaseHealth);
        enemy.ConfigureEndlessGrowth(Difficulty.bossHealthMultiplier, 1f, Difficulty.damageMultiplier);
        // Each boss can call a small group, rather than multiplying 30 adds by six.
        enemy.Champion.summonCountMin = 6;
        enemy.Champion.summonCount = 10;
        enemy.Champion.maxSummonedAlive = run.EndlessConfig.livingLimit;
        enemy.name = "EndlessBoss_" + (run.BossKills + livingBosses.Count + 1);
        enemy.gameObject.SetActive(true);
        livingBosses.Add(enemy);
        return true;
    }

    private bool TryBossPosition(out Vector3 position)
    {
        Vector2 player = run.Player.transform.position;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector2 candidate = player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(7f, 11f);
            if (!nav.IsWalkablePosition(candidate) || !nav.CanTraverseSegment(candidate, candidate, .45f) ||
                !nav.TryFindPath(candidate, player, route, .8f, .4f)) continue;
            bool nearBoss = false;
            foreach (var boss in livingBosses)
                if (((Vector2)boss.transform.position - candidate).sqrMagnitude < 4f) { nearBoss = true; break; }
            if (nearBoss) continue;
            position = candidate; return true;
        }
        position = Vector3.zero; return false;
    }

    private void OnBossDied(ZombieChaser enemy)
    {
        if (!livingBosses.Remove(enemy)) return;
        run.RewardEncounter(true, enemy.transform.position);
    }
    private void ReleaseBoss(ZombieChaser enemy)
    {
        enemy.gameObject.SetActive(false);
        bossPool.Enqueue(enemy);
    }
}
