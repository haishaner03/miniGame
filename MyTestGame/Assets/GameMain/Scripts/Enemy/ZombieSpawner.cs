using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
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
    private readonly Queue<GameObject> pooledZombies = new Queue<GameObject>();
    private float nextSpawnTime;
    private float nextWaveTime;
    private readonly HashSet<string> triggeredWaves = new HashSet<string>();

    public int ActiveCount => alive.Count;
    public int PooledCount => pooledZombies.Count;
    private float runWaveMultiplier = 1f;
    private float runHealthMultiplier = 1f;
    private float runVariantChance;
    private bool isRunRoom;

    public void ConfigureForRun(RunState run)
    {
        isRunRoom = true;
        runWaveMultiplier = 1f + run.RoomIndex * run.Config.enemiesPerRoom;
        runHealthMultiplier = 1f + run.RoomIndex * run.Config.healthPerRoom;
        runVariantChance = Mathf.Clamp(run.Config.initialVariantChance +
            run.RoomIndex * run.Config.variantChancePerRoom, 0f, 0.65f);
        initialSpawnCount = Mathf.CeilToInt(initialSpawnCount * runWaveMultiplier);
        maxAlive = Mathf.Max(initialSpawnCount, Mathf.CeilToInt(maxAlive * runWaveMultiplier));
        respawnAfterDeath = false;
        enableTimedWaves = false;
    }

    private void Start()
    {
        ResolvePlayer();
        if (useObjectPool)
            PrewarmPool();
        if (spawnOnStart)
        {
            int count = Mathf.Min(initialSpawnCount, maxAlive);
            for (int i = 0; i < count; i++)
                SpawnOne();
        }
        nextSpawnTime = Time.time + spawnInterval;
        ScheduleNextWave();
    }

    private void Update()
    {
        CleanupDeadEntries();
        if (!spawningEnabled || zombiePrefab == null)
            return;

        // 普通补怪可以由关卡流程单独关闭，但不影响定时大波。
        if (respawnAfterDeath && alive.Count < maxAlive && Time.time >= nextSpawnTime)
        {
            SpawnOne();
            nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
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
        if (zombiePrefab == null || alive.Count >= maxAlive)
            return null;

        ResolvePlayer();
        Vector3 position = overridePoints != null && overridePoints.Length > 0
            ? ChooseSpawnPositionFrom(overridePoints)
            : ChooseSpawnPosition();
        GameObject instance = GetZombieFromPool();
        if (instance == null)
            return null;

        instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        instance.name = "Zombie_" + alive.Count.ToString("00");
        ZombieChaser chaser = instance.GetComponent<ZombieChaser>();
        if (chaser != null)
        {
            chaser.SetPoolReleaseCallback(useObjectPool ? ReleaseZombie : null);
            if (isRunRoom)
            {
                chaser.archetype = Random.value < runVariantChance
                    ? (ZombieChaser.ZombieArchetype)Random.Range(1, 4)
                    : ZombieChaser.ZombieArchetype.Standard;
            }
            chaser.OnSpawnedFromPool(player);
            if (isRunRoom) chaser.ApplyRunDifficulty(runHealthMultiplier);
        }
        instance.SetActive(true);
        alive.Add(instance);
        return instance;
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
        if (!triggeredWaves.Add(id))
            return false;

        SpawnWave(Mathf.Max(1, Mathf.CeilToInt(count * runWaveMultiplier)), overridePoints);
        Debug.Log("Zombie wave triggered: " + id + " (" + count + ")");
        return true;
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
        for (int i = 0; i < count; i++)
        {
            GameObject instance = CreatePooledZombie();
            if (instance != null)
                pooledZombies.Enqueue(instance);
        }
    }

    private void ScheduleNextWave()
    {
        float min = Mathf.Max(1f, waveDelayMin);
        float max = Mathf.Max(min, waveDelayMax);
        nextWaveTime = Time.time + Random.Range(min, max);
    }

    private GameObject GetZombieFromPool()
    {
        if (!useObjectPool)
        {
            GameObject created = Instantiate(zombiePrefab, transform);
            created.SetActive(false);
            return created;
        }

        while (pooledZombies.Count > 0)
        {
            GameObject pooled = pooledZombies.Dequeue();
            if (pooled != null)
                return pooled;
        }

        return CreatePooledZombie();
    }

    private GameObject CreatePooledZombie()
    {
        GameObject instance = Instantiate(zombiePrefab, transform);
        instance.SetActive(false);
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
        pooledZombies.Enqueue(instance);
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
