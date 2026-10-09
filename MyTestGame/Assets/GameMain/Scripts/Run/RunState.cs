using System;
using System.Collections.Generic;
using Flower;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RunState : MonoBehaviour
{
    public enum RunPhase { Playing, Paused, ChoosingUpgrade, Loading, Lost, Won }
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
    public int RoomKills { get; private set; }
    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public int ExperienceToNextLevel => Config.firstLevelExperience + (Level - 1) * Config.experienceGrowthPerLevel;
    public int TotalExperience { get; private set; }
    public bool UsesHeavyWeapon { get; private set; }
    public string WeaponName => UsesHeavyWeapon ? "铁棍" : "砍刀";
    public bool IsChoosingStartingWeapon => restartWeaponPicker != null && restartWeaponPicker.isActiveAndEnabled;
    public bool SuppressPauseInput => IsChoosingStartingWeapon || (restartWeaponPicker != null && restartWeaponPicker.ClosedThisFrame);
    public int EliteKills { get; private set; }
    public int DamageDealt { get; private set; }
    public int DamageTaken { get; private set; }
    public int HealingReceived { get; private set; }
    public SurvivorHealth Player => player;
    public LevelFlowController CurrentRoom => flow;
    public int LifeStealHits { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public bool RewardClaimed { get; private set; }
    public IReadOnlyList<string> Rooms => rooms;
    public IReadOnlyList<DRRunUpgrade> OfferedUpgrades => offered;
    public IReadOnlyDictionary<int, int> UpgradeStacks => stacks;
    public IReadOnlyList<DRRunUpgrade> Upgrades => upgrades;
    public RunCombatEffects CombatEffects { get; private set; }
    public RoomEncounterController Encounter { get; private set; }
    public int ChampionKills { get; private set; }
    public int BossKills { get; private set; }

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
    private int pendingLevelRewards;
    private ExperiencePickupPool pickupPool;
    private HealthPickupPool healthPool;
    private StartingWeaponPicker restartWeaponPicker;

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

    public static void StartNewRun(string firstRoom = null, int? seed = null, bool heavyWeapon = false)
    {
        RunState run = Instance;
        if (run == null)
            run = new GameObject("ZombieRun").AddComponent<RunState>();
        run.Begin(firstRoom, seed);
        run.UsesHeavyWeapon = heavyWeapon;
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
        ui = Config.battleUiPrefab != null
            ? Instantiate(Config.battleUiPrefab, transform).GetComponent<ZombieRunUI>()
            : gameObject.AddComponent<ZombieRunUI>();
        ui.Initialize(this);
        pickupPool = gameObject.AddComponent<ExperiencePickupPool>();
        pickupPool.Initialize(this);
        if (Config.healthPickupPrefab != null)
        {
            healthPool = gameObject.AddComponent<HealthPickupPool>();
            healthPool.Initialize(this);
        }
        CombatEffects = gameObject.AddComponent<RunCombatEffects>();
        CombatEffects.Initialize(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Begin(string firstRoom, int? seed)
    {
        if (restartWeaponPicker != null) restartWeaponPicker.Close();
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
        RoomKills = 0;
        Level = 1;
        Experience = TotalExperience = pendingLevelRewards = 0;
        pickupPool.ClearDrops();
        if (healthPool != null) healthPool.Clear();
        EliteKills = DamageDealt = DamageTaken = HealingReceived = 0;
        LifeStealHits = 0;
        ChampionKills = BossKills = 0;
        UsesHeavyWeapon = false;
        if (CombatEffects != null) CombatEffects.Clear();
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
        if (healthPool != null) healthPool.Clear();
        RoomKills = 0;
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
        CombatEffects.Clear();
        Encounter = controller.GetComponent<RoomEncounterController>();
        if (Encounter == null) Encounter = controller.gameObject.AddComponent<RoomEncounterController>();
        Encounter.Initialize(this, spawner);
    }

    public void RewardEncounter(bool boss, Vector3 position)
    {
        if (Phase != RunPhase.Playing || player == null || player.IsDead) return;
        ChampionKills++;
        if (boss) BossKills++;
        // ZombieChaser also drops its normal elite XP after its Died event.
        int total = boss ? Config.bossEncounterExperience : Config.eliteEncounterExperience;
        pickupPool.Drop(position, Mathf.Max(0, total - Config.eliteExperience));
        if (healthPool != null) healthPool.Drop(position);
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
        RewardClaimed = true;
        flow.ReleaseRunReward();
    }

    public void DropExperience(Vector3 position, bool elite)
    {
        if (Phase != RunPhase.Playing || player == null || player.IsDead) return;
        pickupPool.Drop(position, elite ? Config.eliteExperience : Config.normalExperience);
        if (healthPool != null && UnityEngine.Random.value < Config.healthDropChance) healthPool.Drop(position);
    }

    public void GainExperience(int amount)
    {
        if (amount <= 0 || Phase != RunPhase.Playing || player == null || player.IsDead) return;
        Experience += amount;
        TotalExperience += amount;
        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            Level++;
            pendingLevelRewards++;
        }
        if (pendingLevelRewards > 0) OfferLevelReward();
    }

    private void OfferLevelReward()
    {
        var eligible = new List<DRRunUpgrade>();
        foreach (DRRunUpgrade upgrade in upgrades)
            if (IsUpgradeEligible(upgrade)) eligible.Add(upgrade);
        offered.Clear();
        if (stacks.Count == 0)
        {
            foreach (string effect in new[] { "AttackSpeed", "FreezeChance", "BurnChance" })
            {
                var entry = eligible.Find(u => u.Effect == effect);
                if (entry != null) { offered.Add(entry); eligible.Remove(entry); }
            }
        }
        else
        {
            var focused = eligible.FindAll(u => u.Branch != "Common" && BranchStacks(u.Branch) > 0);
            if (focused.Count > 0)
            {
                var chosen = focused[rewardRandom.Next(focused.Count)];
                offered.Add(chosen); eligible.Remove(chosen);
            }
        }
        while (offered.Count < 3 && eligible.Count > 0)
        {
            double total = 0;
            foreach (var candidate in eligible) total += CandidateWeight(candidate);
            double pick = rewardRandom.NextDouble() * total;
            int index = 0;
            while (index < eligible.Count - 1 && (pick -= CandidateWeight(eligible[index])) >= 0) index++;
            offered.Add(eligible[index]);
            eligible.RemoveAt(index);
        }
        if (offered.Count == 0)
        {
            pendingLevelRewards = 0;
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
        if (upgrade == null || !IsUpgradeEligible(upgrade))
            return false;
        stacks[id] = StackCount(id) + 1;
        int health = CurrentHealth + (upgrade.Effect == "MaxHealth" ? Mathf.RoundToInt(upgrade.Value) : 0);
        stats.Apply(this, health);
        if (upgrade.Effect == "Heal") player.Heal(Mathf.RoundToInt(upgrade.Value));
        offered.Clear();
        pendingLevelRewards = Mathf.Max(0, pendingLevelRewards - 1);
        Phase = RunPhase.Playing;
        ui.HideModal();
        Time.timeScale = resumeTimeScale;
        if (pendingLevelRewards > 0) OfferLevelReward();
        return true;
    }

    public bool AdvanceRoom()
    {
        if (Phase != RunPhase.Playing || !RewardClaimed || player == null || player.IsDead)
            return false;
        // Bank uncollected ground XP before leaving; overflow rewards are chosen first.
        pickupPool.CollectAll();
        if (Phase != RunPhase.Playing) return false;
        if (RoomNumber == RoomCount)
        {
            Phase = RunPhase.Won;
            Pause();
            ui.ShowResult(true);
            return true;
        }
        SyncHealth(player.CurrentHealth, player.MaxHealth);
        Phase = RunPhase.Loading;
        RoomIndex++;
        SceneManager.LoadScene(rooms[RoomIndex]);
        return true;
    }

    public void RecordKill(bool elite = false)
    {
        if (Phase != RunPhase.Playing) return;
        Kills++;
        RoomKills++;
        if (elite) EliteKills++;
    }

    public void RecordDamageDealt(int amount)
    {
        if (Phase == RunPhase.Playing) DamageDealt += Mathf.Max(0, amount);
    }

    public void RecordDamageTaken(int amount)
    {
        if (Phase == RunPhase.Playing) DamageTaken += Mathf.Max(0, amount);
    }

    public void RecordHealing(int amount)
    {
        if (Phase == RunPhase.Playing || Phase == RunPhase.ChoosingUpgrade)
            HealingReceived += Mathf.Max(0, amount);
    }

    public void TogglePause()
    {
        if (IsChoosingStartingWeapon) return;
        if (Phase == RunPhase.Playing)
        {
            Phase = RunPhase.Paused;
            Pause();
            ui.ShowPause();
        }
        else if (Phase == RunPhase.Paused)
        {
            Phase = RunPhase.Playing;
            Time.timeScale = resumeTimeScale;
            ui.HideModal();
        }
    }

    public void RecordMeleeHit()
    {
        LifeStealHits++;
    }

    public bool IsUpgradeEligible(DRRunUpgrade upgrade) => upgrade != null &&
        StackCount(upgrade.Id) < upgrade.MaxStacks &&
        (string.IsNullOrEmpty(upgrade.RequiredEffect) || EffectTotal(upgrade.RequiredEffect) > 0);
    public int BranchStacks(string branch)
    {
        int total = 0;
        foreach (var upgrade in upgrades) if (upgrade.Branch == branch) total += StackCount(upgrade.Id);
        return total;
    }
    private double CandidateWeight(DRRunUpgrade upgrade) =>
        (BranchStacks(upgrade.Branch) > 0 && upgrade.Branch != "Common" ? 2.0 : 1.0) *
        (StackCount(upgrade.Id) == 0 ? 1.2 : .9);

    public void RestartRun()
    {
        if (Phase == RunPhase.Loading || Phase == RunPhase.ChoosingUpgrade || IsChoosingStartingWeapon) return;
        if (Config.startingWeaponPickerPrefab == null)
        {
            Debug.LogError("Run config is missing the starting weapon picker prefab.");
            return;
        }
        bool resumeOnCancel = Phase == RunPhase.Playing;
        if (resumeOnCancel) TogglePause();
        ui.EnsureEventSystem();
        if (restartWeaponPicker == null)
            restartWeaponPicker = Instantiate(Config.startingWeaponPickerPrefab, transform).GetComponent<StartingWeaponPicker>();
        restartWeaponPicker.Show(Config.roomScenePaths[0],
            heavy => StartNewRun(Config.roomScenePaths[0], null, heavy),
            () => { if (resumeOnCancel) TogglePause(); });
    }

    public void ReturnToMenu()
    {
        if (SceneUtility.GetBuildIndexByScenePath(Config.menuScenePath) < 0)
        {
            Debug.LogError("Run menu is not in build settings.");
            return;
        }
        Time.timeScale = 1f;
        if (restartWeaponPicker != null) restartWeaponPicker.Close();
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
        Encounter = null;
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
