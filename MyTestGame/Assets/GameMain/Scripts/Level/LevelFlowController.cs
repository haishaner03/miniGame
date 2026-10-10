using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 当前小关卡的最小可玩流程：清理本关丧尸后解锁出口，进入出口加载下一关。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class LevelFlowController : MonoBehaviour
{
    public enum FlowState
    {
        Waiting,
        Playing,
        Completed,
        GameOver
    }

    [Header("引用")]
    [SerializeField] private SurvivorHealth playerHealth;
    [SerializeField] private ZombieSpawner zombieSpawner;
    [SerializeField] private SafeDoor startDoor;
    [SerializeField] private SafeDoor exitDoor;

    [Header("关卡流程")]
    [SerializeField] private string nextScenePath;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool requireAllZombiesDefeated = true;
    [SerializeField] private bool stopSpawningWhenExitUnlocked = true;
    [SerializeField] private bool disableSpawnerRespawn = true;
    [SerializeField] private float nextSceneDelay = 0.75f;
    [SerializeField] private bool showDebugOverlay = true;
    [Tooltip("勾选时，区域波次也属于出口解锁条件；关闭后仅判断击杀目标与精英/Boss遭遇。")]
    [SerializeField] private bool requireRegionalWaves = true;
    [Tooltip("击杀目标和这些区域波次全部完成后解锁出口。为空时自动查找本场景区域触发器。")]
    [SerializeField] private ZombieWaveTrigger[] requiredWaves;

    public FlowState State { get; private set; } = FlowState.Waiting;
    public bool ExitUnlocked { get; private set; }
    public string NextScenePath => nextScenePath;
    public int TotalObjectiveWaves => !requireRegionalWaves || requiredWaves == null ? 0 : requiredWaves.Length;
    public int CompletedObjectiveWaves => ClearedWaveCount();
    public int RemainingEnemies => zombieSpawner != null ? zombieSpawner.ActiveCount : 0;
    public int RequiredKills
    {
        get
        {
            var run = RunState.Instance;
            if (run == null) return 0;
            if (run.RoomNumber == run.RoomCount) return run.Config.bossPreparationKills;
            if (run.Config.requireEliteEncounter && run.RoomNumber == run.Config.eliteRoomNumber) return run.Config.elitePreparationKills;
            return run.Config.roomKillTarget + run.RoomIndex * 15;
        }
    }

    private bool hadEnemies;
    private bool loadRequested;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.PlayerDied += OnPlayerDied;
            playerHealth.GameOver += OnPlayerGameOver;
        }
    }

    private void Start()
    {
        ResolveReferences();

        if (playerHealth != null)
        {
            playerHealth.PlayerDied -= OnPlayerDied;
            playerHealth.GameOver -= OnPlayerGameOver;
            playerHealth.PlayerDied += OnPlayerDied;
            playerHealth.GameOver += OnPlayerGameOver;
        }

        if (startDoor != null && playerHealth != null)
        {
            playerHealth.SetSpawnPosition(startDoor.transform.position);
            playerHealth.transform.position = startDoor.transform.position;
        }

        if (zombieSpawner != null && disableSpawnerRespawn)
            zombieSpawner.respawnAfterDeath = false;

        if (exitDoor != null)
        {
            exitDoor.Initialize(this, SafeDoor.DoorRole.Exit);
            exitDoor.SetLocked(true);
        }

        if (startDoor != null)
            startDoor.Initialize(this, SafeDoor.DoorRole.Start);

        State = autoStart ? FlowState.Playing : FlowState.Waiting;
        if (playerHealth != null)
            RunState.EnsureForRoom(gameObject.scene.path).BindRoom(this, playerHealth, zombieSpawner);
    }

    private void Update()
    {
        if (State != FlowState.Playing)
            return;
        if (RunState.Instance != null && RunState.Instance.Phase != RunState.RunPhase.Playing)
            return;

        if (playerHealth != null && playerHealth.IsGameOver)
        {
            OnPlayerGameOver();
            return;
        }

        if (zombieSpawner == null)
            return;

        if (zombieSpawner.ActiveCount > 0)
            hadEnemies = true;

        bool objectiveMet = RunState.Instance != null
            ? RunState.Instance.RoomKills >= RequiredKills
            : hadEnemies && zombieSpawner.ActiveCount == 0;
        if (requireAllZombiesDefeated && objectiveMet && AllRequiredWavesCleared())
        {
            var encounter = RunState.Instance != null ? RunState.Instance.Encounter : null;
            if (encounter == null || encounter.TryResolveObjective()) UnlockExit();
        }
    }

    private int ClearedWaveCount()
    {
        int count = 0;
        if (!requireRegionalWaves || requiredWaves == null)
            return 0;
        for (int i = 0; i < requiredWaves.Length; i++)
        {
            if (requiredWaves[i] != null && requiredWaves[i].IsCleared)
                count++;
        }
        return count;
    }

    private bool AllRequiredWavesCleared()
    {
        if (!requireRegionalWaves || requiredWaves == null || requiredWaves.Length == 0)
            return true;
        for (int i = 0; i < requiredWaves.Length; i++)
        {
            if (requiredWaves[i] != null && !requiredWaves[i].IsCleared)
                return false;
        }
        return true;
    }

    public void HandleStartDoorReached(SafeDoor door, SurvivorHealth player)
    {
        if (playerHealth == null && player != null)
            playerHealth = player;

        if (player != null)
            player.SetSpawnPosition(door.transform.position);
    }

    public void HandleExitDoorReached(SafeDoor door, SurvivorHealth player)
    {
        if (State != FlowState.Playing || door == null || door.IsLocked)
            return;

        CompleteLevel();
    }

    public void UnlockExit()
    {
        if (ExitUnlocked)
            return;
        if (RunState.Instance != null && RunState.Instance.Encounter != null && !RunState.Instance.Encounter.IsResolved)
            return;

        ExitUnlocked = true;
        if (stopSpawningWhenExitUnlocked && zombieSpawner != null)
            zombieSpawner.SetSpawningEnabled(false);
        if (RunState.Instance != null)
        {
            RunState.Instance.RoomCleared();
            return;
        }
        if (exitDoor != null) exitDoor.SetLocked(false);
        Debug.Log("Level objective complete: exit door unlocked; zombie spawning stopped.");
    }

    public void CompleteLevel()
    {
        if (State != FlowState.Playing || loadRequested)
            return;

        if (RunState.Instance != null)
        {
            if (RunState.Instance.AdvanceRoom())
            {
                State = FlowState.Completed;
                loadRequested = true;
            }
            return;
        }

        State = FlowState.Completed;
        loadRequested = true;
        StartCoroutine(LoadNextSceneRoutine());
    }

    public void ReleaseRunReward()
    {
        if (ExitUnlocked && exitDoor != null) exitDoor.SetLocked(false);
    }

    private IEnumerator LoadNextSceneRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, nextSceneDelay));

        if (string.IsNullOrWhiteSpace(nextScenePath))
        {
            Debug.Log("Level complete: no next scene configured.");
            yield break;
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(nextScenePath);
        if (buildIndex < 0)
        {
            Debug.LogError("Next scene is not enabled in Build Settings: " + nextScenePath);
            yield break;
        }

        SceneManager.LoadScene(buildIndex);
    }

    private void OnPlayerDied(int deathCount)
    {
        if (State == FlowState.Playing)
        {
            hadEnemies = false;
            ExitUnlocked = false;
            if (exitDoor != null)
                exitDoor.SetLocked(true);
        }
    }

    private void OnPlayerGameOver()
    {
        State = FlowState.GameOver;
        if (exitDoor != null)
            exitDoor.SetLocked(true);
    }

    private void ResolveReferences()
    {
        if (requiredWaves == null || requiredWaves.Length == 0)
            requiredWaves = FindObjectsByType<ZombieWaveTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<SurvivorHealth>();
        if (zombieSpawner == null)
            zombieSpawner = FindFirstObjectByType<ZombieSpawner>();

        SafeDoor[] doors = FindObjectsByType<SafeDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
                continue;
            if (doors[i].Role == SafeDoor.DoorRole.Start && startDoor == null)
                startDoor = doors[i];
            else if (doors[i].Role == SafeDoor.DoorRole.Exit && exitDoor == null)
                exitDoor = doors[i];
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.PlayerDied -= OnPlayerDied;
            playerHealth.GameOver -= OnPlayerGameOver;
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || RunState.Instance != null)
            return;

        string objective = ExitUnlocked ? "出口已解锁，前往出口门" : "清理街区中的丧尸";
        if (!ExitUnlocked && TotalObjectiveWaves > 0)
            objective = "推进街区 " + ClearedWaveCount() + "/" + requiredWaves.Length + "，清理区域波次";
        GUI.Label(new Rect(18f, 18f, 500f, 24f), "关卡状态: " + State + "  |  " + objective);
    }
}
