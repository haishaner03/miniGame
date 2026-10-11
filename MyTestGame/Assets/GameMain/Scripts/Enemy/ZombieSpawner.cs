using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
    public GameObject[] ordinaryZombiePrefabs;
    public Transform player;
    public Transform[] spawnPoints;
    public int initialSpawnCount = 6;
    public int maxAlive = 12;
    public float spawnInterval = 2.5f;
    public float minSpawnDistance = 5f;
    public float spawnJitter = 0.35f;
    public bool spawnOnStart = true;
    public bool respawnAfterDeath = true;
    [Header("随机大波次")]
    public bool enableTimedWaves = true;
    public float waveDelayMin = 10f;
    public float waveDelayMax = 20f;
    public int waveCountMin = 4;
    public int waveCountMax = 8;
    public bool waveSpawnsRequireCapacity = true;
    [Header("对象池")]
    public bool useObjectPool = true;
    public int poolPrewarm = 12;
    [SerializeField] private bool spawningEnabled = true;

    private readonly List<GameObject> alive = new List<GameObject>();
    private readonly Dictionary<GameObject, Queue<GameObject>> ordinaryPools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> ordinarySources = new Dictionary<GameObject, GameObject>();
    private readonly Queue<GameObject> pooledElites = new Queue<GameObject>();
    private GameObject elitePrefab;
    private float eliteChance;
    private float eliteHealthMultiplier = 2.5f;
    private int ordinaryBaseHealth = 200;
    private float nextSpawnTime;
    private float nextWaveTime;
    private readonly HashSet<string> triggeredWaves = new HashSet<string>();
    private sealed class ObjectiveWave
    {
        public int pending;
        public Transform[] points;
        public readonly HashSet<ZombieChaser> living = new HashSet<ZombieChaser>();
    }
    private readonly Dictionary<string, ObjectiveWave> objectiveWaves = new Dictionary<string, ObjectiveWave>();
    private readonly List<Vector2> reachableSpawnPath = new List<Vector2>();
    public int WaveRemaining(string id)
    {
        ObjectiveWave wave;
        return objectiveWaves.TryGetValue(id, out wave) ? wave.pending + wave.living.Count : 0;
    }
    public int WavePending(string id)
    {
        ObjectiveWave wave;
        return objectiveWaves.TryGetValue(id, out wave) ? wave.pending : 0;
    }
    public bool WaveCleared(string id) => objectiveWaves.ContainsKey(id) && WaveRemaining(id) == 0;
    public bool BelongsToWave(ZombieChaser zombie, string id)
    {
        ObjectiveWave wave;
        return objectiveWaves.TryGetValue(id, out wave) && wave.living.Contains(zombie);
    }

    public int ActiveCount => alive.Count;
    public int PooledCount
    {
        get
        {
            int total = pooledElites.Count;
            foreach (var pool in ordinaryPools.Values) total += pool.Count;
            return total;
        }
    }
    private float runWaveMultiplier = 1f;
    private float runHealthMultiplier = 1f;
    private float runVariantChance;
    private bool isRunRoom;
    private ZombieGridPathfinder pathfinder;
    private bool generatingWanderers;
    private bool isFirstRoom;
    private Vector2Int firstRoomHealth;
    private Vector2 reinforcementDelay;
    private Vector2Int reinforcementCount;
    private Vector2 moveSpeedMultiplier = Vector2.one;
    public bool SpawningEnabled => spawningEnabled;

    public void ConfigureForRun(RunState run)
    {
        isRunRoom = true;
        if (run.Config.ordinaryZombiePrefabs != null && run.Config.ordinaryZombiePrefabs.Length > 0)
            ordinaryZombiePrefabs = run.Config.ordinaryZombiePrefabs;
        isFirstRoom = run.RoomIndex == 0;
        elitePrefab = run.Config.eliteEncounterPrefab;
        var champion = elitePrefab != null ? elitePrefab.GetComponent<ZombieChampion>() : null;
        eliteChance = champion != null && !champion.isBoss ? Mathf.Clamp01(run.Config.ambientEliteChance) : 0f;
        eliteHealthMultiplier = Mathf.Max(1f, run.Config.ambientEliteHealthMultiplier);
        var ordinary = zombiePrefab != null ? zombiePrefab.GetComponent<ZombieChaser>() : null;
        ordinaryBaseHealth = ordinary != null ? Mathf.Max(1, ordinary.maxHealth) : 200;
        float roomDensity = run.RoomIndex == 1 ? Mathf.Clamp(run.Config.secondRoomPopulationMultiplier, .1f, 1f) : 1f;
        firstRoomHealth = run.Config.firstRoomZombieHealth;
        runWaveMultiplier = (1f + run.RoomIndex * run.Config.enemiesPerRoom) * roomDensity;
        runHealthMultiplier = 1f + run.RoomIndex * run.Config.healthPerRoom;
        runVariantChance = Mathf.Clamp(run.Config.initialVariantChance +
            run.RoomIndex * run.Config.variantChancePerRoom, 0f, 0.65f);
        float population = isFirstRoom ? run.Config.firstRoomPopulationMultiplier : runWaveMultiplier;
        initialSpawnCount = Mathf.Max(1, Mathf.RoundToInt(run.Config.initialWanderers * population));
        moveSpeedMultiplier = run.Config.zombieMoveSpeedMultiplier;
        maxAlive = Mathf.Max(1, Mathf.RoundToInt(run.Config.maxLivingZombies * roomDensity));
        poolPrewarm = Mathf.Max(poolPrewarm, Mathf.Min(maxAlive, initialSpawnCount + run.Config.hordeCount.y));
        reinforcementDelay = run.Config.reinforcementDelay;
        reinforcementCount = run.Config.reinforcementCount;
        if (isFirstRoom)
            reinforcementCount = new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(reinforcementCount.x * population)),
                Mathf.Max(1, Mathf.RoundToInt(reinforcementCount.y * population)));
        else if (roomDensity < 1f)
            reinforcementCount = new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(reinforcementCount.x * roomDensity)),
                Mathf.Max(1, Mathf.CeilToInt(reinforcementCount.y * roomDensity)));
        waveDelayMin = run.Config.hordeDelay.x;
        waveDelayMax = run.Config.hordeDelay.y;
        waveCountMin = Mathf.Max(1, Mathf.CeilToInt(run.Config.hordeCount.x * population));
        waveCountMax = Mathf.Max(waveCountMin, Mathf.CeilToInt(run.Config.hordeCount.y * population));
        minSpawnDistance = Mathf.Max(7f, minSpawnDistance);
        spawnJitter = Mathf.Max(1f, spawnJitter);
        respawnAfterDeath = true;
        enableTimedWaves = true;
        pathfinder = FindFirstObjectByType<ZombieGridPathfinder>();
    }

    private void Start()
    {
        ResolvePlayer();
        if (useObjectPool)
            PrewarmPool();
        if (spawnOnStart)
        {
            generatingWanderers = true;
            int count = Mathf.Min(initialSpawnCount, maxAlive);
            for (int i = 0; i < count; i++)
                SpawnOne();
            generatingWanderers = false;
        }
        ScheduleNextReinforcements();
        ScheduleNextWave();
    }

    private void Update()
    {
        CleanupDeadEntries();
        if (Time.timeScale <= 0f || (RunState.Instance != null && RunState.Instance.Phase != RunState.RunPhase.Playing)) return;
        if (!spawningEnabled || zombiePrefab == null)
            return;

        // Objective waves get capacity before ambient reinforcements. A full pool
        // queues the missing members instead of silently accepting an empty wave.
        SpawnPendingObjectiveWaves();

        // 普通补怪可以由关卡流程单独关闭，但不影响定时大波。
        if (respawnAfterDeath && alive.Count < maxAlive && Time.time >= nextSpawnTime)
        {
            if (isRunRoom) SpawnWave(Random.Range(reinforcementCount.x, reinforcementCount.y + 1));
            else SpawnOne();
            ScheduleNextReinforcements();
        }

        // 定时大波独立于普通补怪规则，按随机时间生成一次。
        if (enableTimedWaves && Time.time >= nextWaveTime)
        {
            int minCount = Mathf.Max(1, waveCountMin);
            int maxCount = Mathf.Max(minCount, waveCountMax);
            SpawnWave(Random.Range(minCount, maxCount + 1));
            ScheduleNextWave();
        }
    }

    public GameObject SpawnOne()
    {
        return SpawnOne(null);
    }

    public GameObject SpawnOne(Transform[] overridePoints)
    {
        return SpawnInternal(overridePoints, false);
    }

    private GameObject SpawnInternal(Transform[] overridePoints, bool encounterAdd)
    {
        if ((!spawningEnabled && !encounterAdd) || zombiePrefab == null || alive.Count >= maxAlive)
            return null;

        ResolvePlayer();
        Vector3 position;
        if (!TrySpawnPosition(overridePoints, out position)) return null;
        // Boss summons remain ordinary adds; all other sources can mix in elites.
        bool spawnElite = isRunRoom && !encounterAdd && eliteChance > 0f && Random.value < eliteChance;
        GameObject instance = GetZombieFromPool(spawnElite);
        if (instance == null)
            return null;

        instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        var appearance = instance.GetComponent<ZombieDirectionalAnimator>();
        instance.name = (spawnElite ? "EliteHunter_" : "Zombie_Type" + (appearance != null ? appearance.OrdinaryTypeId : 1) + "_") + alive.Count.ToString("00");
        ZombieChaser chaser = instance.GetComponent<ZombieChaser>();
        if (chaser != null)
        {
            chaser.Died -= OnZombieDied;
            chaser.Died += OnZombieDied;
            chaser.SetPoolReleaseCallback(useObjectPool ? ReleaseZombie : null);
            if (isRunRoom && !spawnElite)
            {
                chaser.archetype = Random.value < runVariantChance
                    ? (ZombieChaser.ZombieArchetype)Random.Range(1, 4)
                    : ZombieChaser.ZombieArchetype.Standard;
            }
            chaser.OnSpawnedFromPool(player);
            if (isRunRoom) chaser.ApplyRunDifficulty(runHealthMultiplier);
            if (isRunRoom)
            {
                float minimum = Mathf.Max(.1f, Mathf.Min(moveSpeedMultiplier.x, moveSpeedMultiplier.y));
                float maximum = Mathf.Max(minimum, Mathf.Max(moveSpeedMultiplier.x, moveSpeedMultiplier.y));
                chaser.SetSpawnMoveSpeedMultiplier(Random.Range(minimum, maximum));
            }
            if (isRunRoom && isFirstRoom)
            {
                int minimum = Mathf.Max(1, firstRoomHealth.x);
                chaser.SetSpawnHealth(Random.Range(minimum, Mathf.Max(minimum, firstRoomHealth.y) + 1));
            }
            if (spawnElite)
            {
                int ordinaryHealth = isFirstRoom ? chaser.CurrentMaxHealth : Mathf.RoundToInt(ordinaryBaseHealth * runHealthMultiplier);
                chaser.SetSpawnHealth(Mathf.RoundToInt(ordinaryHealth * eliteHealthMultiplier));
                chaser.Champion.InitializeAmbient(player != null ? player.GetComponentInParent<SurvivorHealth>() : null, this);
            }
            if (isRunRoom && !generatingWanderers) chaser.ForceAggro();
        }
        instance.SetActive(true);
        alive.Add(instance);
        return instance;
    }

    // Explicit boss summons use the regular enemy pool without restarting timers.
    public int SpawnEncounterAdds(int count)
    {
        int spawned = 0;
        for (int i = 0; i < count; i++) if (SpawnInternal(null, true) != null) spawned++;
        return spawned;
    }

    public void RetireAmbientForEncounter()
    {
        SetSpawningEnabled(false);
        respawnAfterDeath = enableTimedWaves = false;
        // Clear the arena without granting kills, XP, or death-triggered effects.
        while (alive.Count > 0)
        {
            var instance = alive[alive.Count - 1];
            alive.RemoveAt(alive.Count - 1);
            if (instance == null) continue;
            var zombie = instance.GetComponent<ZombieChaser>();
            if (zombie != null) ReleaseZombie(zombie); else Destroy(instance);
        }
    }

    public void SetSpawningEnabled(bool enabled)
    {
        spawningEnabled = enabled;
    }

    /// <summary>
    /// 由区域触发器调用。同一个 ID 在本关卡只会成功一次。
    /// </summary>
    public bool TriggerWaveOnce(string waveId, int count)
    {
        return TriggerWaveOnce(waveId, count, null);
    }

    /// <summary>
    /// 区域波次：可传入专属刷怪点，丧尸只从这些点出现（例如前方小巷），为空时使用全局刷怪点。
    /// </summary>
    public bool TriggerWaveOnce(string waveId, int count, Transform[] overridePoints)
    {
        string id = string.IsNullOrWhiteSpace(waveId) ? "Wave_Default" : waveId;
        if (!spawningEnabled) return false;
        if (!triggeredWaves.Add(id))
            return false;

        objectiveWaves.Add(id, new ObjectiveWave {
            pending = Mathf.Max(1, Mathf.CeilToInt(count * runWaveMultiplier)), points = overridePoints
        });
        SpawnPendingObjectiveWaves();
        Debug.Log("Zombie wave triggered: " + id + " (" + count + ")");
        return true;
    }

    private void SpawnPendingObjectiveWaves()
    {
        foreach (var wave in objectiveWaves.Values)
        {
            while (wave.pending > 0 && alive.Count < maxAlive)
            {
                GameObject instance = SpawnOne(wave.points);
                if (instance == null) break;
                var zombie = instance.GetComponent<ZombieChaser>();
                if (zombie == null) break;
                wave.living.Add(zombie);
                wave.pending--;
            }
        }
    }

    private void OnZombieDied(ZombieChaser zombie)
    {
        // Death animation may still be playing, but this member is already defeated.
        alive.Remove(zombie.gameObject);
        foreach (var wave in objectiveWaves.Values) wave.living.Remove(zombie);
    }

    public int SpawnWave(int count)
    {
        return SpawnWave(count, null);
    }

    public int SpawnWave(int count, Transform[] overridePoints)
    {
        if (zombiePrefab == null || !spawningEnabled)
            return 0;

        int spawned = 0;
        int capacity = Mathf.Max(0, maxAlive - alive.Count);
        int targetCount = waveSpawnsRequireCapacity ? Mathf.Min(count, capacity) : count;
        for (int i = 0; i < targetCount; i++)
        {
            if (SpawnOne(overridePoints) != null)
                spawned++;
        }
        return spawned;
    }

    private void ScheduleNextReinforcements()
    {
        nextSpawnTime = Time.time + (isRunRoom
            ? Random.Range(Mathf.Max(0.5f, reinforcementDelay.x), Mathf.Max(reinforcementDelay.x, reinforcementDelay.y))
            : Mathf.Max(0.1f, spawnInterval));
    }

    private bool TrySpawnPosition(Transform[] overrides, out Vector3 position)
    {
        if (pathfinder == null) pathfinder = FindFirstObjectByType<ZombieGridPathfinder>();
        Vector2 center = player != null ? (Vector2)player.position : Vector2.zero;
        if (generatingWanderers && pathfinder != null)
            return pathfinder.TryRandomWalkable(center, minSpawnDistance, out position);
        Transform[] points = overrides != null && overrides.Length > 0 ? overrides : spawnPoints;
        for (int i = 0; i < 64 && points != null && points.Length > 0; i++)
        {
            Transform point = points[Random.Range(0, points.Length)];
            if (point == null) continue;
            Vector2 candidate = (Vector2)point.position + Random.insideUnitCircle * spawnJitter;
            if ((candidate - center).sqrMagnitude < minSpawnDistance * minSpawnDistance) continue;
            if (pathfinder != null && !pathfinder.IsWalkablePosition(candidate)) continue;
            if (pathfinder != null && !pathfinder.TryFindPath(candidate, center, reachableSpawnPath)) continue;
            position = candidate;
            return true;
        }
        if (pathfinder != null) return pathfinder.TryRandomWalkable(center, minSpawnDistance, out position);
        position = ChooseSpawnPosition();
        return true;
    }

    /// <summary>
    /// 当前小关卡重试时清理旧丧尸并重新生成初始波次。
    /// </summary>
    public void ResetForLevelRestart()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] != null)
            {
                if (useObjectPool)
                    ReleaseZombie(alive[i].GetComponent<ZombieChaser>());
                else
                    Destroy(alive[i]);
            }
        }
        alive.Clear();

        if (zombiePrefab == null)
            return;

        int count = Mathf.Min(initialSpawnCount, maxAlive);
        for (int i = 0; i < count; i++)
            SpawnOne();
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
        ScheduleNextWave();
        triggeredWaves.Clear();
        objectiveWaves.Clear();
    }

    private Vector3 ChooseSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (point == null)
                    continue;
                Vector2 candidate = (Vector2)point.position + Random.insideUnitCircle * spawnJitter;
                if (player == null || Vector2.Distance(candidate, player.position) >= minSpawnDistance)
                    return new Vector3(candidate.x, candidate.y, 0f);
            }
        }

        Vector2 center = player != null ? (Vector2)player.position : Vector2.zero;
        Vector2 fallback = center + Random.insideUnitCircle.normalized * Mathf.Max(minSpawnDistance, 4f);
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    /// <summary>
    /// 只在给定点位里挑选。全部离玩家太近时取最远的一个，避免回退到随机圆周而刷进墙里。
    /// </summary>
    private Vector3 ChooseSpawnPositionFrom(Transform[] points)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Transform point = points[Random.Range(0, points.Length)];
            if (point == null)
                continue;
            Vector2 candidate = (Vector2)point.position + Random.insideUnitCircle * spawnJitter;
            if (player == null || Vector2.Distance(candidate, player.position) >= minSpawnDistance)
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Transform farthest = null;
        float best = -1f;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
                continue;
            float d = player != null ? Vector2.Distance(points[i].position, player.position) : 0f;
            if (d > best)
            {
                best = d;
                farthest = points[i];
            }
        }

        if (farthest != null)
        {
            Vector2 p = (Vector2)farthest.position + Random.insideUnitCircle * spawnJitter;
            return new Vector3(p.x, p.y, 0f);
        }

        return ChooseSpawnPosition();
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null)
            found = GameObject.Find("SurvivorPlayer");
        if (found != null)
            player = found.transform;
    }

    private void CleanupDeadEntries()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null)
                alive.RemoveAt(i);
        }
    }

    private void PrewarmPool()
    {
        if (zombiePrefab == null)
            return;

        int count = Mathf.Max(0, Mathf.Min(poolPrewarm, Mathf.Max(maxAlive, initialSpawnCount)));
        int eliteCount = Mathf.RoundToInt(count * eliteChance);
        for (int i = 0; i < count; i++)
        {
            bool elite = i < eliteCount;
            var prefab = elite ? elitePrefab : OrdinaryPrefab(i - eliteCount);
            GameObject instance = CreatePooledZombie(elite, prefab);
            if (instance != null)
                (elite ? pooledElites : OrdinaryPool(prefab)).Enqueue(instance);
        }
    }

    private void ScheduleNextWave()
    {
        float min = Mathf.Max(1f, waveDelayMin);
        float max = Mathf.Max(min, waveDelayMax);
        nextWaveTime = Time.time + Random.Range(min, max);
    }

    private GameObject GetZombieFromPool(bool elite)
    {
        var prefab = elite ? elitePrefab : OrdinaryPrefab(Random.Range(0, OrdinaryVariantCount));
        if (!useObjectPool)
        {
            GameObject created = Instantiate(prefab, transform);
            created.SetActive(false);
            return created;
        }

        var pool = elite ? pooledElites : OrdinaryPool(prefab);
        while (pool.Count > 0)
        {
            GameObject pooled = pool.Dequeue();
            if (pooled != null)
                return pooled;
        }

        return CreatePooledZombie(elite, prefab);
    }

    private int OrdinaryVariantCount => ordinaryZombiePrefabs != null && ordinaryZombiePrefabs.Length > 0 ? ordinaryZombiePrefabs.Length : 1;

    private GameObject OrdinaryPrefab(int index)
    {
        if (ordinaryZombiePrefabs == null || ordinaryZombiePrefabs.Length == 0) return zombiePrefab;
        var prefab = ordinaryZombiePrefabs[Mathf.Abs(index) % ordinaryZombiePrefabs.Length];
        return prefab != null ? prefab : zombiePrefab;
    }

    private Queue<GameObject> OrdinaryPool(GameObject prefab)
    {
        Queue<GameObject> pool;
        if (!ordinaryPools.TryGetValue(prefab, out pool))
        {
            pool = new Queue<GameObject>();
            ordinaryPools.Add(prefab, pool);
        }
        return pool;
    }

    private GameObject CreatePooledZombie(bool elite, GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.SetActive(false);
        if (!elite) ordinarySources[instance] = prefab;
        return instance;
    }

    private void ReleaseZombie(ZombieChaser chaser)
    {
        if (chaser == null)
            return;

        GameObject instance = chaser.gameObject;
        alive.Remove(instance);
        if (!useObjectPool)
        {
            Destroy(instance);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform, false);
        if (chaser.Champion != null) pooledElites.Enqueue(instance);
        else
        {
            GameObject source;
            if (!ordinarySources.TryGetValue(instance, out source)) source = zombiePrefab;
            OrdinaryPool(source).Enqueue(instance);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.85f, 0.15f, 0.1f, 0.85f);
        if (spawnPoints == null)
            return;
        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;
            Gizmos.DrawWireSphere(point.position, 0.35f);
            Gizmos.DrawWireSphere(point.position, spawnJitter);
        }
    }
}
