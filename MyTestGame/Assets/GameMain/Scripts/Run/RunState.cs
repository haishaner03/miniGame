using System;
using System.Collections.Generic;
using Flower;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RunState : MonoBehaviour
{
    public enum RunPhase { Playing, ChoosingUpgrade, Loading, Lost, Won }
    public static RunState Instance { get; private set; }
    public ZombieRunConfig Config { get; private set; }
    public RunPhase Phase { get; private set; }
    public int Seed { get; private set; }
    public int RoomIndex { get; private set; }
    public int RoomNumber => RoomIndex + 1;
    public int RoomCount => rooms.Count;
    public int CurrentHealth { get; private set; } = -1;
    public int MaxHealth { get; private set; }
    public int Kills { get; private set; }
    public int LifeStealHits { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public bool RewardClaimed { get; private set; }
    public IReadOnlyList<string> Rooms => rooms;
    public IReadOnlyList<DRRunUpgrade> OfferedUpgrades => offered;
    public IReadOnlyDictionary<int, int> UpgradeStacks => stacks;

    private readonly List<string> rooms = new List<string>();
    private readonly Dictionary<int, int> stacks = new Dictionary<int, int>();
    private readonly List<DRRunUpgrade> offered = new List<DRRunUpgrade>();
    private DRRunUpgrade[] upgrades;
    private System.Random rewardRandom;
    private SurvivorHealth player;
    private PlayerRunStats stats;
    private LevelFlowController flow;
    private ZombieRunUI ui;
    private float resumeTimeScale = 1f;

    public static RunState EnsureForRoom(string scenePath)
    {
        if (Instance == null)
        {
            var root = new GameObject("ZombieRun");
            root.AddComponent<RunState>();
            Instance.Begin(scenePath, null);
        }
        return Instance;
    }

    public static void StartNewRun(string firstRoom = null, int? seed = null)
    {
        RunState run = Instance;
        if (run == null)
            run = new GameObject("ZombieRun").AddComponent<RunState>();
        run.Begin(firstRoom, seed);
        run.Phase = RunPhase.Loading;
        SceneManager.LoadScene(run.rooms[0]);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Config = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        if (Config == null)
            throw new InvalidOperationException("Missing Resources/ZombieRunConfig asset.");
        upgrades = Config.LoadUpgrades();
        ui = gameObject.AddComponent<ZombieRunUI>();
        ui.Initialize(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Begin(string firstRoom, int? seed)
    {
        UnbindPlayer();
        stacks.Clear();
        offered.Clear();
        rooms.Clear();
        var pool = new List<string>();
        foreach (string path in Config.roomScenePaths)
        {
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
                throw new InvalidOperationException("Run room not enabled in build settings: " + path);
            if (!pool.Contains(path)) pool.Add(path);
        }
        if (pool.Count < 2 || Config.roomCount < 2)
            throw new InvalidOperationException("A run needs two room templates and at least two rooms.");
        if (firstRoom != null && !pool.Contains(firstRoom))
            throw new InvalidOperationException("Unknown starting room: " + firstRoom);
        Seed = seed ?? Guid.NewGuid().GetHashCode();
        var routeRandom = new System.Random(Seed);
        rewardRandom = new System.Random(unchecked(Seed ^ 0x51A7BEEF));
        rooms.Add(firstRoom ?? pool[routeRandom.Next(pool.Count)]);
        while (rooms.Count < Config.roomCount)
        {
            var candidates = pool.FindAll(path => path != rooms[rooms.Count - 1]);
            rooms.Add(candidates[routeRandom.Next(candidates.Count)]);
        }
        RoomIndex = 0;
        CurrentHealth = -1;
        MaxHealth = 0;
        Kills = 0;
        LifeStealHits = 0;
        ElapsedSeconds = 0f;
        RewardClaimed = false;
        Phase = RunPhase.Playing;
        Time.timeScale = 1f;
        ui.HideModal();
        Debug.Log("Zombie run started. Seed=" + Seed + "; rooms=" + string.Join(",", rooms));
    }

    public void BindRoom(LevelFlowController controller, SurvivorHealth health, ZombieSpawner spawner)
    {
        UnbindPlayer();
        if (rooms[RoomIndex] != controller.gameObject.scene.path)
            throw new InvalidOperationException("Loaded room does not match run route.");
        flow = controller;
        player = health;
        stats = player.GetComponent<PlayerRunStats>();
        if (stats == null) stats = player.gameObject.AddComponent<PlayerRunStats>();
        stats.Apply(this, CurrentHealth);
        SyncHealth(player.CurrentHealth, player.MaxHealth);
        player.HealthChanged += SyncHealth;
        player.GameOver += OnPlayerGameOver;
        UnityEngine.Random.InitState(unchecked(Seed + RoomIndex * 7919));
        if (spawner != null) spawner.ConfigureForRun(this);
        RewardClaimed = false;
        offered.Clear();
        Phase = RunPhase.Playing;
        ui.HideModal();
        ui.EnsureEventSystem();
    }

    private void Update()
    {
        if (Phase == RunPhase.Playing && player != null && !player.IsDead)
            ElapsedSeconds += Time.deltaTime;
    }

    public float EffectTotal(string effect)
    {
        float value = 0f;
        foreach (DRRunUpgrade upgrade in upgrades)
            if (upgrade.Effect == effect && stacks.TryGetValue(upgrade.Id, out int count))
                value += upgrade.Value * count;
        return value;
    }

    public DRRunUpgrade GetUpgrade(int id)
    {
        return Array.Find(upgrades, upgrade => upgrade.Id == id);
    }

    public int StackCount(int id)
    {
        return stacks.TryGetValue(id, out int count) ? count : 0;
    }

    public void RoomCleared()
    {
        if (Phase != RunPhase.Playing || RewardClaimed || player == null || player.IsDead)
            return;
        if (RoomNumber == RoomCount)
        {
            Phase = RunPhase.Won;
            Pause();
            ui.ShowResult(true);
            return;
        }
        var eligible = new List<DRRunUpgrade>();
        foreach (DRRunUpgrade upgrade in upgrades)
            if (StackCount(upgrade.Id) < upgrade.MaxStacks) eligible.Add(upgrade);
        offered.Clear();
        while (offered.Count < 3 && eligible.Count > 0)
        {
            int index = rewardRandom.Next(eligible.Count);
            offered.Add(eligible[index]);
            eligible.RemoveAt(index);
        }
        if (offered.Count == 0)
        {
            RewardClaimed = true;
            flow.ReleaseRunReward();
            return;
        }
        Phase = RunPhase.ChoosingUpgrade;
        Pause();
        ui.ShowChoices();
    }

    public bool ChooseUpgrade(int id)
    {
        if (Phase != RunPhase.ChoosingUpgrade || player == null || player.IsDead)
            return false;
        DRRunUpgrade upgrade = offered.Find(choice => choice.Id == id);
        if (upgrade == null || StackCount(id) >= upgrade.MaxStacks)
            return false;
        stacks[id] = StackCount(id) + 1;
        int health = CurrentHealth + (upgrade.Effect == "MaxHealth" ? Mathf.RoundToInt(upgrade.Value) : 0);
        stats.Apply(this, health);
        offered.Clear();
        RewardClaimed = true;
        Phase = RunPhase.Playing;
        ui.HideModal();
        Time.timeScale = resumeTimeScale;
        flow.ReleaseRunReward();
        return true;
    }

    public bool AdvanceRoom()
    {
        if (Phase != RunPhase.Playing || !RewardClaimed || RoomNumber >= RoomCount || player.IsDead)
            return false;
        SyncHealth(player.CurrentHealth, player.MaxHealth);
        Phase = RunPhase.Loading;
        RoomIndex++;
        SceneManager.LoadScene(rooms[RoomIndex]);
        return true;
    }

    public void RecordKill()
    {
        if (Phase == RunPhase.Playing) Kills++;
    }

    public void RecordMeleeHit()
    {
        int threshold = Mathf.RoundToInt(EffectTotal("LifeSteal"));
        if (Phase != RunPhase.Playing || threshold <= 0 || player == null || player.IsDead)
            return;
        LifeStealHits++;
        if (LifeStealHits >= threshold)
        {
            LifeStealHits = 0;
            player.Heal(1);
        }
    }

    public void RestartRun()
    {
        StartNewRun();
    }

    public void ReturnToMenu()
    {
        if (SceneUtility.GetBuildIndexByScenePath(Config.menuScenePath) < 0)
        {
            Debug.LogError("Run menu is not in build settings.");
            return;
        }
        Time.timeScale = 1f;
        ui.DisableOwnedEventSystem();
        SceneManager.LoadScene(Config.menuScenePath);
    }

    private void SyncHealth(int current, int maximum)
    {
        CurrentHealth = current;
        MaxHealth = maximum;
    }

    private void OnPlayerGameOver()
    {
        if (Phase == RunPhase.Lost || Phase == RunPhase.Won) return;
        Phase = RunPhase.Lost;
        offered.Clear();
        Pause();
        ui.ShowResult(false);
    }

    private void Pause()
    {
        resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && Config != null &&
            Array.IndexOf(Config.roomScenePaths, scene.path) < 0)
            Destroy(gameObject);
    }

    private void UnbindPlayer()
    {
        if (player != null)
        {
            player.HealthChanged -= SyncHealth;
            player.GameOver -= OnPlayerGameOver;
        }
        player = null;
        stats = null;
        flow = null;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        UnbindPlayer();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Time.timeScale = 1f;
        Instance = null;
    }
}
