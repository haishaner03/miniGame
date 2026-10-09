using System;
using System.Linq;
using Flower;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Play-mode integration checks exercise the real scene loading and room-clear flow.
public static class ZombieRunVerification
{
    private const int Seed = 47219;
    private static int step;
    private static int assertions;
    private static double deadline;
    private static double nextTick;
    private static string[] route;
    private static int[] firstChoices;
    private static int expectedHealth;
    private static int expectedMaximum;
    private static int expectedRoom;
    private static int expectedDamage;
    private static float expectedMoveSpeed;
    private static ZombieChaser combatTarget;
    private static int swings;
    public static string Result { get; private set; }

    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter play mode first.");
        EditorApplication.update -= Tick;
        step = 0;
        assertions = 0;
        expectedRoom = 1;
        Result = "Running";
        deadline = EditorApplication.timeSinceStartup + 70f;
        nextTick = 0;
        RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity", Seed);
        EditorApplication.update += Tick;
    }

    public static void StartLifeSteal()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter play mode first.");
        EditorApplication.update -= Tick;
        step = 10;
        assertions = 0;
        swings = 0;
        Result = "Running combat checks";
        deadline = EditorApplication.timeSinceStartup + 30f;
        nextTick = 0;
        int seed = 0;
        for (; seed < 100; seed++)
        {
            var random = new System.Random(unchecked(seed ^ 0x51A7BEEF));
            var ids = Enumerable.Range(1, 8).ToList();
            bool offersBloodReturn = false;
            for (int i = 0; i < 3; i++)
            {
                int index = random.Next(ids.Count);
                offersBloodReturn |= ids[index] == 8;
                ids.RemoveAt(index);
            }
            if (offersBloodReturn) break;
        }
        RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity", seed);
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("Stopped before completion");
            return;
        }
        if (EditorApplication.timeSinceStartup < nextTick) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Integration check timed out at step " + step);
            RunState run = RunState.Instance;
            if (run == null || run.Phase == RunState.RunPhase.Loading) return;
            SurvivorHealth health = UnityEngine.Object.FindFirstObjectByType<SurvivorHealth>();
            LevelFlowController flow = UnityEngine.Object.FindFirstObjectByType<LevelFlowController>();
            if (health == null || flow == null) return;

            if (step == 10)
            {
                ClearRoom();
                step = 11;
            }
            else if (step == 11)
            {
                if (run.Phase != RunState.RunPhase.ChoosingUpgrade) return;
                Check(run.ChooseUpgrade(8), "blood return can be chosen");
                health.TakeDamage(1);
                expectedHealth = health.CurrentHealth;
                combatTarget = UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>().SpawnOne().GetComponent<ZombieChaser>();
                combatTarget.enabled = false;
                combatTarget.ApplyRunDifficulty(20f);
                Vector2 position = (Vector2)health.transform.position + Vector2.up * 0.62f;
                combatTarget.transform.position = position;
                combatTarget.GetComponent<Rigidbody2D>().position = position;
                Physics2D.SyncTransforms();
                step = 12;
            }
            else if (step == 12)
            {
                health.GetComponent<SurvivorMeleeAttack>().AttackTowards(combatTarget.transform.position);
                step = 13;
                nextTick = EditorApplication.timeSinceStartup + 0.5f;
                return;
            }
            else if (step == 13)
            {
                swings++;
                Check(run.LifeStealHits == swings % 5, "actual melee hit advances lifesteal counter");
                Check(health.CurrentHealth == expectedHealth + (swings == 5 ? 1 : 0), "heal occurs only on fifth hit");
                if (swings < 5) step = 12;
                else Finish("PASS: " + assertions + " combat assertions (5 actual melee attacks)");
            }

            else if (step == 0)
            {
                Check(run.RoomNumber == 1 && run.RoomCount == 5, "five-room start");
                route = run.Rooms.ToArray();
                Check(route.Zip(route.Skip(1), (a, b) => a != b).All(value => value), "no adjacent duplicate room");
                Check(UnityEngine.Object.FindObjectsByType<RunState>(FindObjectsSortMode.None).Length == 1, "single persistent run");
                health.TakeDamage(1);
                expectedHealth = health.CurrentHealth;
                Check(expectedHealth == health.MaxHealth - 1, "initial damage applied");
                ClearRoom();
                step = 1;
            }
            else if (step == 1)
            {
                if (run.Phase == RunState.RunPhase.Playing) return;
                if (expectedRoom == 5)
                {
                    Check(run.Phase == RunState.RunPhase.Won && Time.timeScale == 0f, "final-room victory pauses");
                    Check(run.UpgradeStacks.Values.Sum() == 4, "one reward per non-final room");
                    RunState.StartNewRun(route[0], Seed);
                    step = 3;
                    return;
                }
                Check(run.Phase == RunState.RunPhase.ChoosingUpgrade && Time.timeScale == 0f, "clear opens paused reward");
                Check(run.OfferedUpgrades.Count == 3 && run.OfferedUpgrades.Select(row => row.Id).Distinct().Count() == 3, "three distinct choices");
                Check(run.OfferedUpgrades.All(row => run.StackCount(row.Id) < row.MaxStacks), "stack caps respected");
                Check(!run.AdvanceRoom(), "cannot advance before choice");
                Check(!run.ChooseUpgrade(-1), "invalid choice rejected");
                int oldHealth = health.CurrentHealth;
                health.TakeDamage(999);
                Check(health.CurrentHealth == oldHealth, "paused damage blocked");
                var melee = health.GetComponent<SurvivorMeleeAttack>();
                melee.Attack();
                Check(!melee.IsAttacking, "paused attack blocked");
                int[] ids = run.OfferedUpgrades.Select(row => row.Id).ToArray();
                flow.UnlockExit();
                Check(ids.SequenceEqual(run.OfferedUpgrades.Select(row => row.Id)), "reward cannot reroll");
                if (expectedRoom == 1) firstChoices = ids;
                DRRunUpgrade chosen = run.OfferedUpgrades[0];
                expectedMaximum = health.MaxHealth + (chosen.Effect == "MaxHealth" ? Mathf.RoundToInt(chosen.Value) : 0);
                expectedHealth = Mathf.Min(expectedMaximum, oldHealth + (chosen.Effect == "MaxHealth" ? Mathf.RoundToInt(chosen.Value) : 0));
                Check(run.ChooseUpgrade(chosen.Id), "valid choice accepted");
                Check(!run.ChooseUpgrade(chosen.Id), "double choice rejected");
                Check(run.RewardClaimed && Time.timeScale == 1f, "choice resumes play");
                Check(health.CurrentHealth == expectedHealth && health.MaxHealth == expectedMaximum, "choice health correct");
                expectedDamage = melee.Damage;
                expectedMoveSpeed = health.GetComponent<SurvivorMovement>().MoveSpeed;
                expectedRoom++;
                Check(run.AdvanceRoom(), "room advance accepted");
                step = 2;
            }
            else if (step == 2)
            {
                Check(run.RoomNumber == expectedRoom && SceneManager.GetActiveScene().path == route[expectedRoom - 1], "route followed");
                Check(health.CurrentHealth == expectedHealth && health.MaxHealth == expectedMaximum, "health persists across scene");
                Check(health.GetComponent<SurvivorMeleeAttack>().Damage == expectedDamage, "damage persists without compounding");
                Check(Mathf.Approximately(health.GetComponent<SurvivorMovement>().MoveSpeed, expectedMoveSpeed), "speed persists without compounding");
                ZombieSpawner spawner = UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>();
                Check(!spawner.enableTimedWaves && !spawner.respawnAfterDeath, "finite room spawning");
                ClearRoom();
                step = 1;
            }
            else if (step == 3)
            {
                Check(route.SequenceEqual(run.Rooms), "seed reproduces route");
                Check(run.UpgradeStacks.Count == 0 && run.Kills == 0 && run.LifeStealHits == 0, "restart clears run progression");
                Check(health.CurrentHealth == health.MaxHealth, "restart restores starting health");
                ClearRoom();
                step = 4;
            }
            else if (step == 4)
            {
                if (run.Phase != RunState.RunPhase.ChoosingUpgrade) return;
                Check(firstChoices.SequenceEqual(run.OfferedUpgrades.Select(row => row.Id)), "seed reproduces choices");
                run.ChooseUpgrade(run.OfferedUpgrades[0].Id);
                health.TakeDamage(999);
                Check(run.Phase == RunState.RunPhase.Lost && health.IsGameOver && health.DeathCount == 1, "one death ends entire run");
                Check(Time.timeScale == 0f, "death result pauses play");
                step = 5;
                nextTick = EditorApplication.timeSinceStartup + 1f;
            }
            else if (step == 5)
            {
                Check(health.IsDead && health.CurrentHealth == 0 && health.DeathCount == 1, "no per-level respawn");
                Button button = run.GetComponentsInChildren<Button>(true).First(item => item.name == "Restart");
                button.onClick.Invoke();
                step = 6;
            }
            else if (step == 6)
            {
                Check(run.Phase == RunState.RunPhase.Playing && run.RoomNumber == 1 && Time.timeScale == 1f, "restart button starts new run");
                Check(run.UpgradeStacks.Count == 0 && health.CurrentHealth == health.MaxHealth, "new run is clean");
                Finish("PASS: " + assertions + " integration assertions");
            }
            nextTick = EditorApplication.timeSinceStartup + 0.15f;
        }
        catch (Exception exception)
        {
            Finish("FAIL at step " + step + ": " + exception);
        }
    }

    private static void ClearRoom()
    {
        foreach (ZombieWaveTrigger wave in UnityEngine.Object.FindObjectsByType<ZombieWaveTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            wave.Trigger();
        foreach (ZombieChaser zombie in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))
            zombie.TakeDamage(9999);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        assertions++;
    }

    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        Result = result;
        if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log("ZombieRunVerification " + result);
        else Debug.LogError("ZombieRunVerification " + result);
    }
}
