using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises actual runtime drops, XP choices, combat, scene transitions and one-life runs.
public static class ZombieRunVerification
{
    private static int step, assertions, initialCount, damage, maximum, level, totalXP, combatHP;
    private static float speed;
    private static double nextTick, deadline;
    private static string[] route;
    private static int[] firstChoices;
    private static ZombieChaser combatTarget;
    private const int Seed = 47219;
    public static string Result { get; private set; }

    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter play mode first.");
        EditorApplication.update -= Tick;
        step = assertions = 0;
        nextTick = 0;
        deadline = EditorApplication.timeSinceStartup + 90;
        Result = "Running progression checks";
        RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity", Seed);
        EditorApplication.update += Tick;
    }

    public static void StartLifeSteal() { Start(); }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Stopped"); return; }
        if (EditorApplication.timeSinceStartup < nextTick) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at " + step);
            var run = RunState.Instance;
            if (run == null || run.Player == null || run.Phase == RunState.RunPhase.Loading) return;
            var hp = run.Player;
            var spawner = UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>();
            var pool = run.GetComponent<ExperiencePickupPool>();
            var melee = hp.GetComponent<SurvivorMeleeAttack>();
            if (step == 0)
            {
                Check(run.Level == 1 && run.Experience == 0 && hp.CurrentHealth == 100 && melee.Damage == 80, "initial level/HP/damage");
                Check(spawner.ActiveCount == Mathf.RoundToInt(run.Config.initialWanderers * run.Config.firstRoomPopulationMultiplier) && spawner.maxAlive == 100, "reduced first-room population");
                var initialZombies = UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None);
                Check(initialZombies.All(z => z.CurrentMaxHealth >= 80 && z.CurrentMaxHealth <= 140), "first-room zombies take one or two base hits");
                Check(initialZombies.All(z => z.GetComponentsInChildren<UnityEngine.UI.Image>().First(image => image.name == "Fill").rectTransform.rect.height <= 0.0351f), "thin zombie UGUI bars");
                initialCount = spawner.ActiveCount;
                route = run.Rooms.ToArray();
                Check(route.Zip(route.Skip(1), (a, b) => a != b).All(x => x), "no adjacent duplicate rooms");
                Protect(hp);
                step++;
                Wait(5); return;
            }
            if (step == 1)
            {
                Check(spawner.ActiveCount > initialCount && spawner.ActiveCount <= 100, "timed reinforcements");
                var zombies = UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None);
                Check(zombies.Any(z => (bool)Field("persistentAggro", typeof(ZombieChaser)).GetValue(z)), "reinforcements retain aggro");
                spawner.SetSpawningEnabled(false);
                foreach (var z in zombies) { z.enabled = false; z.GetComponent<Rigidbody2D>().simulated = false; }
                int created = pool.CreatedCount;
                zombies.First(z => z.archetype == ZombieChaser.ZombieArchetype.Standard).TakeDamage(9999);
                Check(pool.ActiveCount == 1 && run.Experience == 0 && pool.CreatedCount == created, "kill drops pooled XP without instant collection");
                var pickup = pool.GetComponentsInChildren<ExperiencePickup>().Single();
                Check(pickup.Value == 10, "normal XP value");
                Move(hp, pickup.transform.position);
                step++;
                Wait(0.7); return;
            }
            if (step == 2)
            {
                Check(pool.ActiveCount == 0 && run.Experience == 10 && run.Level == 1, "physical proximity collects");
                int created = pool.CreatedCount;
                for (int i = 0; i < 7; i++) run.DropExperience(hp.transform.position, false);
                Check(pool.ActiveCount == 7 && pool.CreatedCount == created, "drop reuse");
                Capture("ExperienceDrops_Runtime");
                step++;
                Wait(0.8); return;
            }
            if (step == 3)
            {
                Check(run.Level == 2 && run.Experience == 0 && run.Phase == RunState.RunPhase.ChoosingUpgrade && Time.timeScale == 0, "eight drops level up");
                Check(run.OfferedUpgrades.Count == 3 && run.OfferedUpgrades.Select(u => u.Id).Distinct().Count() == 3, "three distinct choices");
                firstChoices = run.OfferedUpgrades.Select(u => u.Id).ToArray();
                Check(!run.ChooseUpgrade(-1) && !run.AdvanceRoom(), "invalid choice and premature exit rejected");
                int oldHP = hp.CurrentHealth;
                hp.TakeDamage(9999);
                Check(hp.CurrentHealth == oldHP, "paused damage blocked");
                Capture("LevelUp_Runtime");
                step = 30;
                Wait(0.4); return;
            }
            if (step == 30)
            {
                Choose(run, hp, run.OfferedUpgrades[0].Id);
                for (int i = 0; i < 150 && (run.Upgrades.Any(u => run.StackCount(u.Id) == 0) || run.StackCount(6) < 4 || run.StackCount(7) < 4); i++)
                {
                    run.GainExperience(run.ExperienceToNextLevel - run.Experience);
                    var reward = run.OfferedUpgrades.OrderBy(u => run.StackCount(u.Id) == 0 ? 0 : (u.Id == 6 || u.Id == 7 ? 1 : 2)).First();
                    Choose(run, hp, reward.Id);
                }
                Check(run.Upgrades.All(u => run.StackCount(u.Id) > 0), "all reward types selected");
                Check(run.StackCount(6) == 4 && run.StackCount(7) == 4, "elemental stack caps");
                int before = run.Level;
                run.GainExperience(run.ExperienceToNextLevel - run.Experience + run.ExperienceToNextLevel + run.Config.experienceGrowthPerLevel);
                Check(run.Level == before + 2 && run.Experience == 0, "overflow two levels");
                run.ChooseUpgrade(run.OfferedUpgrades[0].Id);
                Check(run.Phase == RunState.RunPhase.ChoosingUpgrade && Time.timeScale == 0, "second pending choice paused");
                run.ChooseUpgrade(run.OfferedUpgrades[0].Id);
                Check(run.Phase == RunState.RunPhase.Playing && Time.timeScale == 1, "pending rewards resume");
                // Keep the combat fixture outside authored map walls; wall blocking is
                // exercised separately by SurvivorWeaponVerification.
                Move(hp, new Vector3(160, 160));
                hp.GetComponent<Rigidbody2D>().simulated = false;
                hp.GetComponent<Collider2D>().enabled = false;
                combatTarget = UnityEngine.Object.Instantiate(spawner.zombiePrefab).GetComponent<ZombieChaser>();
                combatTarget.archetype = ZombieChaser.ZombieArchetype.Standard;
                combatTarget.OnSpawnedFromPool(hp.transform);
                combatTarget.ApplyRunDifficulty(100);
                combatTarget.transform.position = hp.transform.position + Vector3.up * 0.62f;
                var body = combatTarget.GetComponent<Rigidbody2D>();
                body.position = combatTarget.transform.position;
                body.bodyType = RigidbodyType2D.Static;
                Physics2D.SyncTransforms();
                combatHP = combatTarget.CurrentHealth;
                // Choose a deterministic RNG draw that procs the configured 80% burn chance.
                for (int seed = 1; seed < 100; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    // Two slash draws, one knockback draw and one freeze draw precede burn.
                    for (int draw = 0; draw < 4; draw++) UnityEngine.Random.value.ToString();
                    if (UnityEngine.Random.value < .8f) { UnityEngine.Random.InitState(seed); break; }
                }
                melee.AttackTowards(combatTarget.transform.position);
                step = 4;
                Wait(0.2); return;
            }
            if (step == 4)
            {
                Check(combatTarget.CurrentHealth < combatHP && combatTarget.IsBurning, "actual melee damage and burn proc");
                Check(((Vector2)Field("knockbackVelocity", typeof(ZombieChaser)).GetValue(combatTarget)).magnitude > 0f, "melee applies knockback");
                combatTarget.ApplyFreeze(1.2f);
                Check(combatTarget.IsFrozen, "freeze active");
                combatHP = combatTarget.CurrentHealth;
                step++;
                Wait(run.Config.baseFreezeDuration + .25f + run.EffectTotal("FreezeDuration")); return;
            }
            if (step == 5)
            {
                Check(!combatTarget.IsFrozen && combatTarget.CurrentHealth < combatHP, "freeze expires and burn ticks");
                combatTarget.OnSpawnedFromPool(hp.transform);
                Check(!combatTarget.IsFrozen && !combatTarget.IsBurning, "enemy status reset on reuse");
                UnityEngine.Object.Destroy(combatTarget.gameObject);
                pool.ClearDrops();
                for (int i = 0; i < 520; i++) run.DropExperience(new Vector3(30, 30), false);
                Check(pool.ActiveCount == 512 && pool.GetComponentsInChildren<ExperiencePickup>().Sum(p => p.Value) == 5200, "cap merges without XP loss");
                pool.ClearDrops();
                Check(pool.ActiveCount == 0 && pool.PooledCount == pool.CreatedCount, "full recycle");
                ClearObjective(run, spawner);
                step++;
                Wait(0.2); return;
            }
            if (step == 6)
            {
                Check(run.RewardClaimed && run.CurrentRoom.ExitUnlocked && !spawner.SpawningEnabled, "objective unlock stops spawning");
                Check(run.Phase == RunState.RunPhase.Playing && spawner.SpawnOne() == null, "clear does not grant upgrade or spawn");
                damage = melee.Damage; speed = hp.GetComponent<SurvivorMovement>().MoveSpeed;
                maximum = hp.MaxHealth; level = run.Level; totalXP = run.TotalExperience;
                run.DropExperience(hp.transform.position, true);
                Check(run.AdvanceRoom(), "exit banks ground XP");
                step++;
                return;
            }
            if (step == 7)
            {
                Check(SceneManager.GetActiveScene().path == route[run.RoomIndex], "route scene loaded");
                Check(run.Level == level && run.TotalExperience == totalXP + 30 && pool.ActiveCount == 0, "XP persists across rooms");
                Check(melee.Damage == damage && Mathf.Approximately(hp.GetComponent<SurvivorMovement>().MoveSpeed, speed) && hp.MaxHealth == maximum, "stats persist without compounding");
                Protect(hp);
                ClearObjective(run, spawner);
                step++;
                Wait(0.2); return;
            }
            if (step == 8)
            {
                if (run.Encounter != null && run.Encounter.IsRequired && !run.Encounter.IsResolved)
                {
                    if (run.Encounter.Enemy != null)
                    {
                        run.Encounter.Enemy.Health.TakeDamage(999999);
                        pool.ClearDrops();
                        run.GetComponent<HealthPickupPool>().Clear();
                    }
                    Wait(0.2); return;
                }
                Check(run.RewardClaimed && run.Phase == RunState.RunPhase.Playing, "clear awaits exit even in final room");
                if (run.RoomNumber < run.RoomCount) { run.AdvanceRoom(); step = 7; return; }
                Check(run.AdvanceRoom() && run.Phase == RunState.RunPhase.Won && Time.timeScale == 0, "final exit wins");
                RunState.StartNewRun(route[0], Seed);
                step = 9;
                return;
            }
            if (step == 9)
            {
                Check(run.Level == 1 && run.TotalExperience == 0 && run.UpgradeStacks.Count == 0 && hp.CurrentHealth == 100, "new run reset");
                run.GainExperience(80);
                Check(firstChoices.SequenceEqual(run.OfferedUpgrades.Select(u => u.Id)), "reproducible rewards");
                run.ChooseUpgrade(run.OfferedUpgrades[0].Id);
                Field("invulnerableUntil", typeof(SurvivorHealth)).SetValue(hp, 0f);
                hp.TakeDamage(9999);
                Check(run.Phase == RunState.RunPhase.Lost && hp.IsGameOver && hp.DeathCount == 1 && Time.timeScale == 0, "one death ends run");
                step++;
                Wait(1); return;
            }
            if (step == 10) { Check(hp.IsDead && hp.CurrentHealth == 0, "no respawn"); Finish("PASS: " + assertions + " progression integration assertions"); }
            Wait(0.1);
        }
        catch (Exception exception) { Finish("FAIL at step " + step + ": " + exception); }
    }

    private static void Choose(RunState run, SurvivorHealth hp, int id)
    {
        var reward = run.GetUpgrade(id);
        int previous = hp.CurrentHealth, maximum = hp.MaxHealth;
        float oldSpeed = hp.GetComponent<SurvivorMovement>().MoveSpeed;
        Check(run.ChooseUpgrade(id), "reward accepted " + id);
        Check(hp.GetComponent<SurvivorMeleeAttack>().Damage == Mathf.RoundToInt(80 * (1 + run.EffectTotal("DamagePercent")) * (run.UsesHeavyWeapon ? 1.25f : 1)), "percentage and weapon damage");
        Check(Mathf.Approximately(hp.GetComponent<SurvivorMeleeAttack>().KnockbackMultiplier, 1f + run.EffectTotal("Knockback")), "knockback upgrade multiplier");
        if (reward.Effect == "MaxHealth") Check(hp.MaxHealth == maximum + 20 && hp.CurrentHealth == previous + 20, "max HP and heal");
        if (reward.Effect == "Heal") Check(hp.CurrentHealth == Mathf.Min(maximum, previous + 35), "immediate heal");
        if (reward.Effect == "MoveSpeed") Check(hp.GetComponent<SurvivorMovement>().MoveSpeed > oldSpeed, "speed upgrade");
    }

    private static void ClearObjective(RunState run, ZombieSpawner spawner)
    {
        // Combat runs in an isolated off-map fixture; objective spawns need a
        // reachable player position on the authored floor, just like normal play.
        var start = UnityEngine.Object.FindObjectsByType<SafeDoor>(FindObjectsSortMode.None).First(d => d.Role == SafeDoor.DoorRole.Start);
        Move(run.Player, start.transform.position);
        spawner.SetSpawningEnabled(true);
        spawner.maxAlive = 1000;
        foreach (var wave in UnityEngine.Object.FindObjectsByType<ZombieWaveTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) wave.Trigger();
        foreach (var zombie in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))
            if (zombie.CurrentHealth > 0) zombie.TakeDamage(999999);
        run.GetComponent<ExperiencePickupPool>().ClearDrops();
        while (run.RoomKills < run.CurrentRoom.RequiredKills) run.RecordKill();
    }

    private static FieldInfo Field(string name, Type type) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Protect(SurvivorHealth hp) { Field("invulnerableUntil", typeof(SurvivorHealth)).SetValue(hp, float.MaxValue); }
    private static void Move(SurvivorHealth hp, Vector3 p) { hp.transform.position = p; hp.GetComponent<Rigidbody2D>().position = p; Physics2D.SyncTransforms(); }
    private static void Wait(double delay) { nextTick = EditorApplication.timeSinceStartup + delay; }
    private static void Capture(string name) { ScreenCapture.CaptureScreenshot("D:/UnityProject/miniGame/MyTestGame/output/imagegen/" + name + ".png"); }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); assertions++; }
    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        Result = result;
        if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log("ZombieRunVerification " + result);
        else Debug.LogError("ZombieRunVerification " + result);
    }
}
