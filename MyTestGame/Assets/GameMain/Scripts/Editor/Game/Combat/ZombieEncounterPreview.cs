using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only shortcuts that use the real run flow to reach a fight.</summary>
[InitializeOnLoad]
public static class ZombieEncounterPreview
{
    private const string PendingKey = "ZombieEncounterPreview.TargetRoom";
    private static int targetRoom, preparedRoom;
    private static double deadline;
    public static string Status { get; private set; }
    static ZombieEncounterPreview() { EditorApplication.playModeStateChanged += OnPlayState; }

    [MenuItem("Tools/Zombie/Preview Elite Encounter")]
    public static void PreviewElite() { Request(3); }
    [MenuItem("Tools/Zombie/Preview Final Boss")]
    public static void PreviewBoss() { Request(Resources.Load<ZombieRunConfig>("ZombieRunConfig").roomCount); }
    private static void Request(int room)
    {
        SessionState.SetInt(PendingKey, room);
        if (EditorApplication.isPlaying) Begin(); else EditorApplication.isPlaying = true;
    }
    private static void OnPlayState(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(PendingKey, 0) > 0) Begin();
        if (change == PlayModeStateChange.ExitingPlayMode) { EditorApplication.update -= Tick; SessionState.EraseInt(PendingKey); }
    }
    private static void Begin()
    {
        targetRoom = SessionState.GetInt(PendingKey, 0); SessionState.EraseInt(PendingKey);
        if (targetRoom <= 0) return;
        preparedRoom = 0; deadline = EditorApplication.timeSinceStartup + 60;
        Status = "Preparing encounter preview";
        var config = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        RunState.StartNewRun(config.roomScenePaths[0], 83107);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish("Stopped"); return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Encounter preview timeout.");
            var run = RunState.Instance;
            if (run == null || run.Player == null || run.Phase == RunState.RunPhase.Loading) return;
            var invulnerability = typeof(SurvivorHealth).GetField("invulnerableUntil",BindingFlags.Instance|BindingFlags.NonPublic);
            invulnerability.SetValue(run.Player,float.MaxValue);
            var spawner = UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>();
            if (preparedRoom != run.RoomNumber)
            {
                preparedRoom = run.RoomNumber;
                var start = UnityEngine.Object.FindObjectsByType<SafeDoor>(FindObjectsSortMode.None).First(d=>d.Role==SafeDoor.DoorRole.Start);
                run.Player.transform.position = start.transform.position;
                run.Player.GetComponent<Rigidbody2D>().position = start.transform.position; Physics2D.SyncTransforms();
                spawner.SetSpawningEnabled(true); spawner.respawnAfterDeath = spawner.enableTimedWaves = false;
                spawner.maxAlive = 1000;
                foreach (var wave in UnityEngine.Object.FindObjectsByType<ZombieWaveTrigger>(FindObjectsInactive.Include,FindObjectsSortMode.None)) wave.Trigger();
                foreach (var zombie in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))
                    if (zombie.CurrentHealth > 0 && zombie.Champion == null) zombie.TakeDamage(999999);
                while (run.RoomKills < run.CurrentRoom.RequiredKills) run.RecordKill();
                ClearDrops(run); return;
            }
            if (run.RoomNumber < targetRoom)
            {
                if (run.Encounter.Enemy != null && !run.Encounter.IsResolved)
                { run.Encounter.Enemy.Health.TakeDamage(999999); ClearDrops(run); }
                if (run.RewardClaimed) { ClearDrops(run); run.AdvanceRoom(); }
                return;
            }
            if (run.Encounter.Enemy == null) return;
            spawner.maxAlive = run.Config.maxLivingZombies;
            ClearDrops(run);
            if (Camera.main != null)
                Camera.main.transform.position = new Vector3(run.Player.transform.position.x,run.Player.transform.position.y,-10f);
            invulnerability.SetValue(run.Player,0f);
            Finish("Ready: " + run.Encounter.EnemyName + " (room " + run.RoomNumber + ", preview skipped earlier combat)");
        }
        catch (Exception e) { Debug.LogError(e); Finish("Failed: " + e.Message); }
    }
    private static void ClearDrops(RunState run)
    { run.GetComponent<ExperiencePickupPool>().ClearDrops();run.GetComponent<HealthPickupPool>().Clear(); }
    private static void Finish(string value)
    { EditorApplication.update -= Tick; Status=value;Debug.Log("Encounter preview: "+value); }
}
