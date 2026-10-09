using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ZombieRunTuningVerification
{
    public static string Result { get; private set; }
    private static IEnumerator routine;
    private static int assertions;
    private static double deadline;
    private static ZombieChaser target;
    private static RunState Run => RunState.Instance;
    private static SurvivorHealth Player => Run.Player;
    private static StartingWeaponPicker Picker => Run.GetComponentInChildren<StartingWeaponPicker>();
    private static ZombieSpawner Spawner => UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>();
    private const string First = "Assets/GameMain/Scenes/Level/ZombieLevel01.unity";
    public static void Start()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update-=Tick;assertions=0;deadline=EditorApplication.timeSinceStartup+60;
        Result="Running restart and spawn tuning verification";routine=Exercise();EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        try
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout");
            if(!routine.MoveNext())Finish("PASS: "+assertions+" restart and spawn tuning assertions");
        }
        catch(Exception e){Finish("FAIL: "+e);}
    }
    private static IEnumerator Exercise()
    {
        RunState.StartNewRun(First,68421);yield return null;while(!Loaded())yield return null;
        Protect();
        Check(Spawner.initialSpawnCount==28 && Spawner.ActiveCount==28,"28 initial first-room wanderers");
        Check(Spawner.waveCountMin==11 && Spawner.waveCountMax==18,"smaller first-room timed horde");
        var reinforcement=(Vector2Int)Get(Spawner,"reinforcementCount");
        Check(reinforcement==new Vector2Int(2,4),"smaller first-room reinforcements");
        var zombies=UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None);
        var normals=zombies.Where(z=>z.archetype==ZombieChaser.ZombieArchetype.Standard).ToArray();
        Check(normals.Length>10 && normals.Max(z=>z.EffectiveMoveSpeed)-normals.Min(z=>z.EffectiveMoveSpeed)>.15f,"same-type zombies have noticeably different speeds");
        Check(zombies.All(z=>z.SpawnSpeedMultiplier>=.8f && z.SpawnSpeedMultiplier<=1.2f),"all spawn speeds within configured range");
        var speeds=zombies.ToDictionary(z=>z,z=>z.EffectiveMoveSpeed);
        float until=Time.time+.25f;while(Time.time<until)yield return null;
        Check(speeds.All(pair=>Mathf.Approximately(pair.Key.EffectiveMoveSpeed,pair.Value)),"speed stays stable during a life");
        Spawner.RetireAmbientForEncounter();
        var seen=new Dictionary<int,float>();int reused=0,changed=0;
        for(int i=0;i<80;i++)
        {
            Spawner.SetSpawningEnabled(true);var z=Spawner.SpawnOne().GetComponent<ZombieChaser>();
            int id=z.GetInstanceID();float previous;
            if(seen.TryGetValue(id,out previous)){reused++;if(Mathf.Abs(previous-z.SpawnSpeedMultiplier)>.001f)changed++;}
            seen[id]=z.SpawnSpeedMultiplier;
            Check(z.EffectiveMoveSpeed>=.1f && z.SpawnSpeedMultiplier>=.8f && z.SpawnSpeedMultiplier<=1.2f,"pooled spawn remains in range");
            float archetypeSpeed=z.moveSpeed;
            if(z.archetype==ZombieChaser.ZombieArchetype.Fast)archetypeSpeed*=z.fastSpeedMultiplier;
            if(z.archetype==ZombieChaser.ZombieArchetype.Tank)archetypeSpeed*=z.tankSpeedMultiplier;
            if(z.archetype==ZombieChaser.ZombieArchetype.Exploder)archetypeSpeed*=z.exploderSpeedMultiplier;
            Check(Mathf.Abs(z.EffectiveMoveSpeed-archetypeSpeed*z.SpawnSpeedMultiplier)<.001f,"pool reuse does not accumulate multiplier");
            Spawner.RetireAmbientForEncounter();
        }
        Check(reused>10 && changed>10,"actual pooled instances reroll speed");

        Move(new Vector2(160,160));
        target=UnityEngine.Object.Instantiate(Spawner.zombiePrefab).GetComponent<ZombieChaser>();
        target.OnSpawnedFromPool(Player.transform);target.enabled=false;target.GetComponent<Rigidbody2D>().simulated=false;
        target.transform.position=new Vector3(161,160);
        var stacks=(Dictionary<int,int>)Get(Run,"stacks");stacks[6]=4;
        FreezeProc();float remaining=(float)Get(target,"frozenUntil")-Time.time;
        Check(remaining>1.75f && remaining<=1.81f,"upgrade freeze base duration 1.8 seconds");
        until=Time.time+1.4f;while(Time.time<until)yield return null;
        Check(target.IsFrozen,"freeze lasts beyond old 1.2-second duration");
        until=Time.time+.5f;while(Time.time<until)yield return null;
        Check(!target.IsFrozen,"freeze expires normally");
        stacks[15]=1;FreezeProc();remaining=(float)Get(target,"frozenUntil")-Time.time;
        Check(remaining>2.15f && remaining<=2.21f,"deep freeze still adds .4 seconds");
        target.SetSpawnMoveSpeedMultiplier(.8f);target.OnSpawnedFromPool(Player.transform);
        Check(!target.IsFrozen && target.SpawnSpeedMultiplier==1f,"reuse resets freeze and prior speed before new roll");
        UnityEngine.Object.DestroyImmediate(target.gameObject);target=null;stacks.Clear();

        Run.GainExperience(80);Run.ChooseUpgrade(Run.OfferedUpgrades[0].Id);Run.TogglePause();
        var ui=Run.GetComponentInChildren<ZombieRunUI>();
        ((Button)Get(ui,"restart")).onClick.Invoke();
        Check(Run.IsChoosingStartingWeapon && Run.Phase==RunState.RunPhase.Paused && Time.timeScale==0,"actual pause retry button opens picker and keeps world paused");
        var picker=Picker;Run.RestartRun();
        Check(Picker==picker && Run.GetComponentsInChildren<StartingWeaponPicker>(true).Length==1,"repeat retry cannot duplicate picker");
        Click("cancel");
        Check(!Run.IsChoosingStartingWeapon && Run.SuppressPauseInput && Run.Phase==RunState.RunPhase.Paused && Run.Level==2,"cancel retains paused run and consumes Escape frame");
        Run.TogglePause();Run.RestartRun();Click("cancel");
        Check(Run.Phase==RunState.RunPhase.Playing && Time.timeScale==1,"cancel from active run restores play");
        Run.RestartRun();Click("ironBar");yield return null;while(!Loaded())yield return null;
        Protect();
        Check(Run.UsesHeavyWeapon && Player.GetComponent<SurvivorMeleeAttack>().Damage==100,"retry applies newly chosen iron bar");
        Check(SceneManager.GetActiveScene().path==First && Run.Level==1 && Run.UpgradeStacks.Count==0 && Run.TotalExperience==0 && Player.CurrentHealth==100,"retry fully resets progress at first map");
        Check(!Run.IsChoosingStartingWeapon && Time.timeScale==1,"confirmed picker closes and resumes simulation");

        RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel03/ZombieLevel03.unity",68422,true);
        yield return null;while(!Loaded())yield return null;
        Set(Player,"invulnerableUntil",0f);Player.TakeDamage(999999);
        Check(Run.Phase==RunState.RunPhase.Lost,"real death reaches result");
        ui=Run.GetComponentInChildren<ZombieRunUI>();((Button)Get(ui,"restart")).onClick.Invoke();
        Check(Run.IsChoosingStartingWeapon && SceneManager.GetActiveScene().path!=First,"death retry waits for weapon before loading");
        Click("cancel");Check(Run.Phase==RunState.RunPhase.Lost && Time.timeScale==0,"cancel returns to death result");
        Run.RestartRun();
        Directory.CreateDirectory("output/Tuning");ScreenCapture.CaptureScreenshot("D:/UnityProject/miniGame/MyTestGame/output/Tuning/RetryWeaponPicker.png");
        double realUntil=EditorApplication.timeSinceStartup+.15;while(EditorApplication.timeSinceStartup<realUntil)yield return null;
        Click("machete");yield return null;while(!Loaded())yield return null;
        Protect();
        Check(!Run.UsesHeavyWeapon && Player.GetComponent<SurvivorMeleeAttack>().Damage==80 && Player.CurrentHealth==100 && Run.RoomNumber==1 && SceneManager.GetActiveScene().path==First,"death retry can switch back to machete and always starts first map");
        Check(Run.GetComponentsInChildren<StartingWeaponPicker>(true).Length==1 && !Run.IsChoosingStartingWeapon,"repeated death retries reuse hidden picker");
        var menuPicker=UnityEngine.Object.Instantiate(Run.Config.startingWeaponPickerPrefab).GetComponent<StartingWeaponPicker>();
        menuPicker.Show(First);((Button)Get(menuPicker,"ironBar")).onClick.Invoke();
        yield return null;while(!Loaded())yield return null;
        Protect();Check(Run.UsesHeavyWeapon && Player.GetComponent<SurvivorMeleeAttack>().Damage==100,"original menu picker without callback still starts chosen weapon");
        if(menuPicker!=null)UnityEngine.Object.DestroyImmediate(menuPicker.gameObject);
    }
    private static void FreezeProc()
    {
        for(int seed=1;seed<100;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.value<.6f){UnityEngine.Random.InitState(seed);break;}}
        Run.CombatEffects.PrepareHit(target,80);
    }
    private static bool Loaded()=>Run!=null && Run.Player!=null && Run.Phase==RunState.RunPhase.Playing;
    private static void Click(string name)=>((Button)Get(Picker,name)).onClick.Invoke();
    private static void Protect()=>Set(Player,"invulnerableUntil",float.MaxValue);
    private static void Move(Vector2 p){Player.transform.position=p;Player.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();}
    private static FieldInfo Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);
    private static object Get(object o,string n)=>Field(o,n).GetValue(o);
    private static void Set(object o,string n,object value)=>Field(o,n).SetValue(o,value);
    private static void Check(bool pass,string name){if(!pass)throw new Exception(name);assertions++;}
    private static void Finish(string result)
    {
        EditorApplication.update-=Tick;Result=result;if(target!=null)UnityEngine.Object.DestroyImmediate(target.gameObject);
        Directory.CreateDirectory("Tools/Tuning");File.WriteAllText("Tools/Tuning/Verification.txt",result+Environment.NewLine);
        if(result.StartsWith("PASS"))Debug.Log("ZombieRunTuningVerification "+result);else Debug.LogError("ZombieRunTuningVerification "+result);
    }
}
