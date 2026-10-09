using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Real runtime flow, telegraphs, collision, status effects and retry checks.</summary>
public static class ZombieEncounterVerification
{
    public static string Result { get; private set; }
    private static IEnumerator routine;
    private static int assertions;
    private static double deadline;
    private static float oldDropChance;
    private static GameObject wall;
    private static RunState Run => RunState.Instance;
    private static SurvivorHealth Player => Run.Player;
    private static ZombieSpawner Spawner => UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>();
    private static ZombieChampion Enemy => Run.Encounter.Enemy;
    private const string Output = "Tools/Encounters/Verification.txt";

    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update -= Tick;
        assertions = 0; deadline = EditorApplication.timeSinceStartup + 120;
        oldDropChance = Resources.Load<ZombieRunConfig>("ZombieRunConfig").healthDropChance;
        Resources.Load<ZombieRunConfig>("ZombieRunConfig").healthDropChance = 0;
        Result = "Running encounter integration checks";
        routine = Exercise(); EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish("Stopped"); return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (!routine.MoveNext()) Finish("PASS: " + assertions + " encounter integration assertions");
        }
        catch (Exception e) { Finish("FAIL: " + e); }
    }
    private static IEnumerator Exercise()
    {
        RunState.StartNewRun(Resources.Load<ZombieRunConfig>("ZombieRunConfig").roomScenePaths[0], 83107);
        yield return null;
        while (!Loaded(1)) yield return null;
        for (int room = 1; room <= 2; room++)
        {
            Protect();
            Check(!Run.Encounter.IsRequired && Run.Encounter.Stage == RoomEncounterController.EncounterStage.None, "ordinary room " + room);
            CompletePreparation();
            while (!Run.RewardClaimed) yield return null;
            Check(!Spawner.SpawningEnabled, "ordinary clear stops timers " + room);
            Check(Run.AdvanceRoom(), "ordinary exit advances " + room);
            yield return null;
            while (!Loaded(room + 1)) yield return null;
        }
        Protect();
        Check(Run.Encounter.IsRequired && !Run.Encounter.IsBoss && Run.CurrentRoom.RequiredKills == 35, "room 3 elite preparation");
        Run.CurrentRoom.UnlockExit();
        Check(!Run.CurrentRoom.ExitUnlocked && !Run.AdvanceRoom(), "cannot bypass elite gate");
        CompletePreparation();
        while (Enemy == null) yield return null;
        Check(Enemy.IsArriving && Enemy.Health.CurrentHealth == 1000, "elite arrival and health");
        Check(Spawner.ActiveCount == 0 && !Spawner.SpawningEnabled && !Spawner.enableTimedWaves, "arena retires ordinary population and timers");
        var path = new List<Vector2>();
        Check(UnityEngine.Object.FindFirstObjectByType<ZombieGridPathfinder>().TryFindPath(Enemy.transform.position, Player.transform.position, path), "elite spawn reachable");
        Check(Spawner.SpawnOne() == null && !Run.RewardClaimed, "ordinary spawning and exit blocked during elite");
        var original = Enemy.transform.position;
        float until = Time.time + .35f;
        while (Time.time < until) yield return null;
        Check(Vector2.Distance(original, Enemy.transform.position) < .01f, "arrival gives preparation time");
        Check(UnityEngine.Object.FindObjectsByType<ZombieChampion>(FindObjectsSortMode.None).Length == 1, "only one elite spawned");
        var encounterUi = Run.GetComponentInChildren<ZombieRunUI>();
        Invoke(encounterUi,"RefreshHud"); Canvas.ForceUpdateCanvases();
        Check(((UnityEngine.UI.Text)Get(encounterUi,"encounterName")).cachedTextGenerator.vertexCount > 0,"Chinese encounter title fits actual UGUI rectangle");
        while (Enemy.IsArriving) yield return null;

        // Isolated off-map combat fixture avoids unrelated map walls. Only the
        // floor lookup is removed; real cast physics and TakeDamage still run.
        ConfigureFixture(Enemy);
        Vulnerable(); ResetAttack(ZombieChampion.ActionState.ChargeWarning, new Vector2(162,160));
        int hp = Player.CurrentHealth;
        until = Time.time + .22f; while (Time.time < until) yield return null;
        Check(Enemy.TelegraphVisible && Player.CurrentHealth == hp, "charge windup visible and harmless");
        MovePlayer(new Vector2(162,161));
        until = Time.time + .25f; while (Time.time < until) yield return null;
        var aim = (Vector2)Get(Enemy,"lockedDirection");
        MovePlayer(new Vector2(162,159));
        until = Time.time + .12f; while (Time.time < until) yield return null;
        Check(Vector2.Distance(aim, (Vector2)Get(Enemy,"lockedDirection")) < .001f, "charge aim locks halfway through tell");
        MovePlayer(new Vector2(170,170));
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(!Enemy.TelegraphVisible && (float)Get(Enemy,"traveled") >= Enemy.chargeDistance - .02f, "dodge succeeds and charge ends in punish window");
        Check(Player.CurrentHealth == hp, "dodged charge deals no damage");

        Vulnerable(); ResetAttack(ZombieChampion.ActionState.ChargeWarning, new Vector2(162,160));
        hp = Player.CurrentHealth;
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(Player.CurrentHealth == hp - Enemy.chargeDamage, "charge damages once across fixed ticks");
        var stopped = Enemy.transform.position;
        until = Time.time + .3f; while (Time.time < until) yield return null;
        Check(Vector2.Distance(stopped,Enemy.transform.position) < .01f, "recovery stays still");

        Vulnerable(); ResetAttack(ZombieChampion.ActionState.ChargeWarning, new Vector2(162,160));
        wall = new GameObject("EncounterVerificationWall"); wall.transform.position = new Vector3(161,160);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.15f,2f); Physics2D.SyncTransforms();
        hp = Player.CurrentHealth;
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(Enemy.transform.position.x < 160.8f && Player.CurrentHealth == hp, "charge stops at wall and cannot damage through it");
        UnityEngine.Object.DestroyImmediate(wall); wall = null;

        Vulnerable(); ResetAttack(ZombieChampion.ActionState.SlamWarning, new Vector2(160.7f,160)); hp = Player.CurrentHealth;
        until = Time.time + .22f; while (Time.time < until) yield return null;
        Check(Enemy.TelegraphVisible && Player.CurrentHealth == hp, "slam windup visible and harmless");
        MovePlayer(new Vector2(163,160));
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(Player.CurrentHealth == hp, "leaving marked slam radius avoids damage");
        Vulnerable(); ResetAttack(ZombieChampion.ActionState.SlamWarning,new Vector2(160.7f,160)); hp = Player.CurrentHealth;
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(Player.CurrentHealth == hp - Enemy.slamDamage, "slam deals one hit in marked area");
        Vulnerable(); ResetAttack(ZombieChampion.ActionState.SlamWarning,new Vector2(160.7f,160));
        wall = new GameObject("EncounterVerificationWall"); wall.transform.position = new Vector3(160.35f,160);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.08f,2f); Physics2D.SyncTransforms(); hp = Player.CurrentHealth;
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(Player.CurrentHealth == hp, "slam blocked by solid wall");
        UnityEngine.Object.DestroyImmediate(wall); wall = null;

        ResetAttack(ZombieChampion.ActionState.Pursuit,new Vector2(161,160)); Protect();
        Enemy.Health.ApplyFreeze(1);
        float remaining = (float)Get(Enemy.Health,"frozenUntil") - Time.time;
        Check(remaining > .50f && remaining <= .56f,"elite freeze reduced but effective");
        until = Time.time + .6f; while (Time.time < until) yield return null;
        Check(!Enemy.Health.IsFrozen,"elite thaw");
        Enemy.Health.TakeDamage(1,Vector2.right,5,true);
        Check(Mathf.Abs(((Vector2)Get(Enemy.Health,"knockbackVelocity")).magnitude - 2) < .01f,"elite partial knockback");
        Set(Enemy.Health,"knockbackVelocity",Vector2.zero);
        Enemy.Health.ApplyBurn(1.2f,10); hp = Enemy.Health.CurrentHealth;
        until = Time.time + 1.1f; while (Time.time < until) yield return null;
        Check(Enemy.Health.CurrentHealth == hp - 10,"elite burn tick");
        MoveEnemy(new Vector2(160,160)); MovePlayer(new Vector2(160,159.3f));
        Player.GetComponent<SurvivorMovement>().enabled = true;
        hp = Enemy.Health.CurrentHealth; Player.GetComponent<SurvivorMeleeAttack>().AttackTowards(Enemy.transform.position);
        until = Time.time + .25f; while (Time.time < until) yield return null;
        Check(Enemy.Health.CurrentHealth == hp - 80,"actual player melee damages elite");
        Player.GetComponent<SurvivorMovement>().enabled = false;
        int aliveHP = Enemy.Health.CurrentHealth;
        Run.TogglePause(); Enemy.Health.ApplyBurn(2,10);
        double realUntil = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < realUntil) yield return null;
        Check(Enemy.Health.CurrentHealth == aliveHP && Time.timeScale == 0,"pause freezes encounter damage"); Run.TogglePause();
        MovePlayer(new Vector2(170,170));
        ClearDrops(); Enemy.Health.TakeDamage(999999);
        Check(Run.Encounter.IsResolved && Run.ChampionKills == 1 && Run.BossKills == 0,"elite defeat recorded once");
        Check(GroundXP() == Run.Config.eliteEncounterExperience && Run.GetComponent<HealthPickupPool>().ActiveCount == 1,"elite guaranteed XP and healing drop");
        Enemy.Health.TakeDamage(999999); Check(Run.ChampionKills == 1,"duplicate hit cannot repeat encounter reward");
        ClearDrops(); while (!Run.RewardClaimed) yield return null;
        Check(Run.RewardClaimed && Run.CurrentRoom.ExitUnlocked,"elite death unlocks exit");
        Check(Run.AdvanceRoom(),"elite room advances"); yield return null;
        while (!Loaded(4)) yield return null;
        Protect(); Check(!Run.Encounter.IsRequired,"room 4 returns to normal combat"); CompletePreparation();
        while (!Run.RewardClaimed) yield return null;
        Check(Run.AdvanceRoom(),"room 4 advances"); yield return null;
        while (!Loaded(5)) yield return null;
        Protect(); Check(Run.Encounter.IsBoss && Run.CurrentRoom.RequiredKills == 25,"final room has boss preparation");
        CompletePreparation(); while (Enemy == null) yield return null;
        Check(Enemy.isBoss && Enemy.Health.CurrentHealth == 3600 && !Run.RewardClaimed,"final boss blocks completion");
        ConfigureFixture(Enemy);
        MovePlayer(UnityEngine.Object.FindObjectsByType<SafeDoor>(FindObjectsSortMode.None).First(d=>d.Role==SafeDoor.DoorRole.Start).transform.position);
        Enemy.Health.TakeDamage(1800);
        while (!Enemy.IsEnraged) yield return null;
        Check(Enemy.IsEnraged && Enemy.State == ZombieChampion.ActionState.Roar,"half HP enters enrage with roar");
        Check(Spawner.ActiveCount == 3 && !Spawner.SpawningEnabled,"enrage summons without enabling ordinary waves");
        Enemy.Health.TakeDamage(1); until=Time.time+.05f;while(Time.time<until)yield return null;
        Check(Spawner.ActiveCount == 3,"enrage triggers only once");
        Set(Enemy,"nextSummon",Time.time - 1); Invoke(Enemy,"SetState",ZombieChampion.ActionState.Pursuit);
        while (Spawner.ActiveCount < 6) yield return null;
        Check(Spawner.ActiveCount == 6,"periodic boss summon");
        Set(Enemy,"nextSummon",Time.time - 1); Invoke(Enemy,"SetState",ZombieChampion.ActionState.Pursuit);
        until=Time.time+.05f;while(Time.time<until)yield return null;
        Check(Spawner.ActiveCount == 6,"boss summons capped at six alive");
        foreach (var z in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))
            if (z.Champion == null) { z.enabled=false; z.GetComponent<Rigidbody2D>().simulated=false; }
        Enemy.Health.ApplyFreeze(1);
        remaining = (float)Get(Enemy.Health,"frozenUntil") - Time.time;
        Check(remaining > .25f && remaining <= .31f,"boss freeze resistance still allows brief control");
        until = Time.time + .4f; while (Time.time < until) yield return null;
        Vulnerable(); ResetAttack(ZombieChampion.ActionState.SlamWarning,new Vector2(160.8f,160)); hp=Player.CurrentHealth;
        until = Time.time + .2f; while (Time.time < until) yield return null;
        Check(Enemy.WarningProgress > .25f,"enrage accelerates windup");
        while (Enemy.State != ZombieChampion.ActionState.Recovery) yield return null;
        Check(Player.CurrentHealth == hp - Enemy.slamDamage - 4,"enraged slam damage");
        MovePlayer(new Vector2(170,170)); Protect(); ClearDrops(); Enemy.Health.TakeDamage(999999);
        Check(Spawner.ActiveCount == 0 && Run.ChampionKills == 2 && Run.BossKills == 1,"boss defeat cleans summons and records victory");
        Check(GroundXP() == Run.Config.bossEncounterExperience && Run.GetComponent<HealthPickupPool>().ActiveCount == 1,"boss encounter rewards");
        ClearDrops(); while (!Run.RewardClaimed) yield return null;
        Check(Run.RewardClaimed && Run.Phase == RunState.RunPhase.Playing,"defeating boss awaits actual final exit");
        Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        MovePlayer(UnityEngine.Object.FindObjectsByType<SafeDoor>(FindObjectsSortMode.None).First(d=>d.Role==SafeDoor.DoorRole.Exit).transform.position);
        while (Run.Phase != RunState.RunPhase.Won) yield return null;
        Check(Time.timeScale == 0,"physical exit wins and pauses result");

        RestartWithMachete(); yield return null; while (!Loaded(1)) yield return null;
        Check(Run.Rooms[0] == Run.Config.roomScenePaths[0] && Run.ChampionKills == 0 && Run.BossKills == 0 && Run.Level == 1 && Player.CurrentHealth == 100,"retry resets entire run to first map");
        // Die while a real encounter is active, then ensure no special enemy or
        // stale encounter HUD survives into the next run.
        for (int room=1;room<=2;room++)
        {
            Protect(); CompletePreparation(); while (!Run.RewardClaimed)yield return null;
            Run.AdvanceRoom();yield return null;while(!Loaded(room+1))yield return null;
        }
        Protect(); CompletePreparation();while(Enemy==null)yield return null;
        Vulnerable(); Player.TakeDamage(999999);
        Check(Run.Phase==RunState.RunPhase.Lost && Time.timeScale==0,"death during encounter ends single-life run");
        RestartWithMachete();yield return null;while(!Loaded(1))yield return null;
        Check(UnityEngine.Object.FindObjectsByType<ZombieChampion>(FindObjectsSortMode.None).Length==0 && !Run.Encounter.IsRequired,"retry clears special enemy and encounter state");
        var ui = Run.GetComponentInChildren<ZombieRunUI>();
        Invoke(ui,"RefreshHud");
        Check(!((GameObject)Get(ui,"encounterPanel")).activeSelf,"retry hides encounter UGUI");
    }
    private static void RestartWithMachete()
    {
        Run.RestartRun();
        var picker = Run.GetComponentInChildren<StartingWeaponPicker>();
        ((UnityEngine.UI.Button)Get(picker,"machete")).onClick.Invoke();
    }
    private static bool Loaded(int room) => Run != null && Run.Player != null && Run.RoomNumber == room && Run.Phase == RunState.RunPhase.Playing;
    private static void CompletePreparation()
    {
        var start=UnityEngine.Object.FindObjectsByType<SafeDoor>(FindObjectsSortMode.None).First(d=>d.Role==SafeDoor.DoorRole.Start);
        MovePlayer(start.transform.position); Spawner.SetSpawningEnabled(true); Spawner.respawnAfterDeath=Spawner.enableTimedWaves=false;Spawner.maxAlive=1000;
        foreach(var wave in UnityEngine.Object.FindObjectsByType<ZombieWaveTrigger>(FindObjectsInactive.Include,FindObjectsSortMode.None))wave.Trigger();
        foreach(var z in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))if(z.CurrentHealth>0 && z.Champion==null)z.TakeDamage(999999);
        while(Run.RoomKills<Run.CurrentRoom.RequiredKills)Run.RecordKill(); ClearDrops();
    }
    private static void ConfigureFixture(ZombieChampion champion)
    {
        Set(champion,"nav",null);champion.Health.useGridPathfinding=false;Set(champion.Health,"runtimeMoveSpeed",0f);
        Player.ConfigureForRun(10000,10000);Player.GetComponent<SurvivorMovement>().enabled=false;
        Player.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
        Set(champion,"nextAction",float.MaxValue);Set(champion,"nextSummon",float.MaxValue);
        MovePlayer(new Vector2(170,170));MoveEnemy(new Vector2(160,160));
    }
    private static void ResetAttack(ZombieChampion.ActionState state,Vector2 p)
    {
        MoveEnemy(new Vector2(160,160));MovePlayer(p);Set(Enemy.Health,"knockbackVelocity",Vector2.zero);
        Set(Enemy,"warningOrigin",new Vector2(160,160));Set(Enemy,"nextAction",float.MaxValue);
        Invoke(Enemy,"Aim");Invoke(Enemy,"SetState",state);
    }
    private static void MovePlayer(Vector2 p){Player.transform.position=p;Player.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();}
    private static void MoveEnemy(Vector2 p){Enemy.transform.position=p;Enemy.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();}
    private static void Protect()=>Set(Player,"invulnerableUntil",float.MaxValue);
    private static void Vulnerable()=>Set(Player,"invulnerableUntil",0f);
    private static void ClearDrops(){Run.GetComponent<ExperiencePickupPool>().ClearDrops();Run.GetComponent<HealthPickupPool>().Clear();}
    private static int GroundXP()=>Run.GetComponentsInChildren<ExperiencePickup>().Sum(p=>p.Value);
    private static FieldInfo Field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
    private static object Get(object obj,string name)=>Field(obj,name).GetValue(obj);
    private static void Set(object obj,string name,object value)=>Field(obj,name).SetValue(obj,value);
    private static void Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
    private static void Check(bool condition,string name){if(!condition)throw new Exception(name);assertions++;}
    private static void Finish(string result)
    {
        EditorApplication.update-=Tick;Result=result;
        var config=Resources.Load<ZombieRunConfig>("ZombieRunConfig");if(config!=null)config.healthDropChance=oldDropChance;
        if(wall!=null)UnityEngine.Object.DestroyImmediate(wall);
        Directory.CreateDirectory("Tools/Encounters");File.WriteAllText(Output,result+Environment.NewLine);
        if(result.StartsWith("PASS"))Debug.Log("ZombieEncounterVerification "+result);else Debug.LogError("ZombieEncounterVerification "+result);
    }
}
