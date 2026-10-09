using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ZombieBuildVerification
{
    public static string Result { get; private set; }
    private static int stage, assertions, strike;
    private static float ready;
    private static double deadline;
    private static RunState run;
    private static SurvivorHealth player;
    private static SurvivorMeleeAttack melee;
    private static ZombieChaser target, neighbor;
    private static GameObject wall;
    private static int hp;
    private static float originalDropChance;
    private static readonly List<int[]> offers = new List<int[]>();
    public static void Start()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
        EditorApplication.update-=Tick;stage=assertions=strike=0;deadline=EditorApplication.timeSinceStartup+90;
        originalDropChance=Resources.Load<ZombieRunConfig>("ZombieRunConfig").healthDropChance;
        Result="Running upgrade branch verification";
        RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity",57181);
        EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        try
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout stage "+stage);
            run=RunState.Instance;
            if(run==null || run.Player==null || run.Phase==RunState.RunPhase.Loading)return;
            if(stage==0)
            {
                SetupPlayer();
                Check(run.Upgrades.Count==20 && run.Upgrades.Select(u=>u.Id).Distinct().Count()==20,"20 unique upgrade rows");
                Check(run.GetUpgrade(3)==null && run.GetUpgrade(8)==null,"weapon and healing removed from candidate table");
                Check(run.Upgrades.All(u=>u.MaxStacks<999),"all caps finite");
                foreach(var u in run.Upgrades.Where(u=>!string.IsNullOrEmpty(u.RequiredEffect)))Check(!run.IsUpgradeEligible(u),"prerequisite locks "+u.Name);
                SampleOffers(true);RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity",57181);stage=1;return;
            }
            if(stage==1)
            {
                SetupPlayer();SampleOffers(false);
                Set(10,1,13,3);player.ConfigureForRun(100,50);
                Check(melee.AttackCooldown<.28f && melee.AttackDuration<.2f,"attack speed scales action and cooldown");
                Set(10,1,11,2,12,1,13,3);var dash=player.GetComponent<SurvivorDash>();
                Check(Mathf.Approximately(dash.DashCooldown,.65f*.76f)&&Mathf.Approximately(dash.DashDistance,1.8f*1.15f),"dash stats reachable from table");
                target=Target(new Vector2(160.65f,160),1000);neighbor=Target(new Vector2(160.6f,160.2f),1000);
                hp=player.CurrentHealth;melee.AttackTowards(new Vector2(162,160));ready=Time.time+melee.AttackDuration+.05f;stage=2;return;
            }
            if(Time.time<ready)return;
            if(stage==2)
            {
                int dealt = 1000 - target.CurrentHealth;
                Check(dealt >= 80 - melee.DamageVariance && dealt <= 80 + melee.DamageVariance && target.CurrentHealth == neighbor.CurrentHealth,"real melee shares one damage roll across targets");
                Check(player.CurrentHealth==hp+6,"life steal caps at six per swing, not per enemy");
                Clean();Set(10,1,14,1);target=Target(new Vector2(160.65f,160),1000);run.CombatEffects.Clear();strike=0;ready=Time.time+.3f;stage=3;return;
            }
            if(stage==3)
            {
                hp=target.CurrentHealth;melee.AttackTowards(new Vector2(162,160));ready=Time.time+melee.AttackDuration+.05f;stage=4;return;
            }
            if(stage==4)
            {
                int expected = strike == 2 ? 116 : 80;
                Check(Mathf.Abs(hp-target.CurrentHealth-expected)<=melee.DamageVariance,"third landed swing combo with damage variance "+strike);
                strike++;ready=Time.time+.15f;stage=strike<3?3:5;return;
            }
            if(stage==5)
            {
                Clean();Set(6,4,15,1,16,1,17,1,18,2);target=Target(new Vector2(160.65f,160),1000);neighbor=Target(new Vector2(160.65f,161),1000);
                target.ApplyFreeze(1);Check(run.CombatEffects.PrepareHit(target,80)==104,"already-frozen target gets bonus damage");
                ForceSpreadSeed();run.CombatEffects.PrepareHit(target,80);Check(neighbor.IsFrozen,"freeze spreads to neighbor");
                Check((float)Field(typeof(ZombieChaser),"frozenUntil").GetValue(target)-Time.time>1.5f,"deep freeze duration applies");
                target.TakeDamage(99999,Vector2.right,0,true);ready=Time.time+.15f;stage=6;return;
            }
            if(stage==6)
            {
                Check(neighbor.CurrentHealth==968,"frozen kill deals shatter area damage");
                Clean();Set(7,4,19,1,20,1,21,1,22,2);target=Target(new Vector2(160.65f,160),1000);neighbor=Target(new Vector2(160.65f,161),1000);
                target.ApplyBurn(1,1);ForceSpreadSeed();run.CombatEffects.PrepareHit(target,80);
                Check(neighbor.IsBurning,"fire spreads to neighbor");
                Check((int)Field(typeof(ZombieChaser),"burnDamage").GetValue(target)==27,"burn damage upgrade");
                Check((float)Field(typeof(ZombieChaser),"burningUntil").GetValue(target)-Time.time>3.9f,"burn duration upgrade");
                target.TakeDamage(99999,Vector2.right,0,true);ready=Time.time+.15f;stage=7;return;
            }
            if(stage==7)
            {
                Check(neighbor.CurrentHealth==964 && neighbor.IsBurning,"burning kill explodes and ignites");
                Clean();Set(7,1,21,1);target=Target(new Vector2(160.65f,160),1000);neighbor=Target(new Vector2(160.65f,161),1000);
                wall=new GameObject("BranchTestWall");wall.transform.position=new Vector3(160.65f,160.5f);wall.AddComponent<BoxCollider2D>().size=new Vector2(1,.08f);Physics2D.SyncTransforms();
                target.ApplyBurn(2,20);target.TakeDamage(99999,Vector2.right,0,true);ready=Time.time+.2f;stage=8;return;
            }
            if(stage==8)
            {
                Check(neighbor.CurrentHealth==1000 && !neighbor.IsBurning,"walls stop branch area damage");
                Clean();var pool=run.GetComponent<HealthPickupPool>();player.ConfigureForRun(100,100);int created=pool.CreatedCount;
                pool.Drop(player.transform.position);ready=Time.time+.5f;stage=9;hp=created;return;
            }
            if(stage==9)
            {
                var pool=run.GetComponent<HealthPickupPool>();Check(pool.ActiveCount==1,"full health does not waste ground medkit");player.ConfigureForRun(100,50);ready=Time.time+.15f;stage=10;return;
            }
            if(stage==10)
            {
                var pool=run.GetComponent<HealthPickupPool>();Check(player.CurrentHealth==75&&pool.ActiveCount==0&&pool.CreatedCount==hp,"medkit heals and returns to pool");
                var stacks=Stacks();stacks.Clear();foreach(var u in run.Upgrades)stacks[u.Id]=1;
                player.GetComponent<PlayerRunStats>().Apply(run,player.CurrentHealth);
                var ui=UnityEngine.Object.FindFirstObjectByType<ZombieRunUI>();
                Method(typeof(ZombieRunUI),"RefreshHud").Invoke(ui,null);
                int count=ui.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Upgrade_")&&t.gameObject.activeSelf);
                Check(count==20,"UGUI displays all 20 acquired types with sparse IDs");
                ui.ShowPause();Check(ui.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.name=="Detail"&&t.text.Contains("火种扩散")),"pause lists final upgrade");
                ui.HideModal();ScreenCapture.CaptureScreenshot("D:/UnityProject/miniGame/MyTestGame/output/Builds/AllBuildsHUD.png");
                Finish("PASS: "+assertions+" upgrade and combat branch assertions");
            }
        }
        catch(Exception e){Finish("FAIL stage "+stage+": "+e);}
    }
    private static void SampleOffers(bool record)
    {
        if(record)offers.Clear();
        for(int i=0;i<35;i++)
        {
            run.GainExperience(run.ExperienceToNextLevel-run.Experience);
            var choices=run.OfferedUpgrades.ToArray();
            Check(choices.Length==3 && choices.Select(u=>u.Id).Distinct().Count()==3,"three unique choices "+i);
            Check(choices.All(run.IsUpgradeEligible),"no capped or prerequisite-locked choices");
            if(i==0)Check(choices.Select(u=>u.Branch).SequenceEqual(new[]{"Quick","Frost","Fire"}),"first level introduces three builds");
            if(record)offers.Add(choices.Select(u=>u.Id).ToArray());else Check(offers[i].SequenceEqual(choices.Select(u=>u.Id)),"seeded offers reproducible "+i);
            var pick=choices.OrderBy(u=>run.StackCount(u.Id)).ThenBy(u=>u.Id).First();
            Check(run.ChooseUpgrade(pick.Id),"candidate accepted");
        }
        foreach(var upgrade in run.Upgrades){int old=run.StackCount(upgrade.Id);Stacks()[upgrade.Id]=upgrade.MaxStacks;Check(!run.IsUpgradeEligible(upgrade),"max stack excluded "+upgrade.Name);if(old==0)Stacks().Remove(upgrade.Id);else Stacks()[upgrade.Id]=old;}
    }
    private static void SetupPlayer()
    {
        player=run.Player;melee=player.GetComponent<SurvivorMeleeAttack>();UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>().SetSpawningEnabled(false);
        run.Config.healthDropChance=0;
        foreach(var z in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None)){z.enabled=false;z.GetComponent<Rigidbody2D>().simulated=false;}
        Field(typeof(SurvivorHealth),"invulnerableUntil").SetValue(player,float.MaxValue);
        player.transform.position=new Vector3(160,160);var body=player.GetComponent<Rigidbody2D>();body.position=player.transform.position;body.simulated=false;
    }
    private static void Set(params int[] values){Stacks().Clear();for(int i=0;i<values.Length;i+=2)Stacks()[values[i]]=values[i+1];player.GetComponent<PlayerRunStats>().Apply(run,player.CurrentHealth);run.CombatEffects.Clear();}
    private static Dictionary<int,int> Stacks() => (Dictionary<int,int>)Field(typeof(RunState),"stacks").GetValue(run);
    private static ZombieChaser Target(Vector2 center,int health)
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameMain/Entity/Zombie.prefab"));
        var z=root.GetComponent<ZombieChaser>();z.SetSpawnHealth(health);z.enabled=false;root.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
        var collider=root.GetComponent<Collider2D>();root.transform.position=center-(Vector2)collider.bounds.center;root.GetComponent<Rigidbody2D>().position=root.transform.position;Physics2D.SyncTransforms();return z;
    }
    private static void ForceSpreadSeed(){for(int seed=1;seed<1000;seed++){UnityEngine.Random.InitState(seed);float first=UnityEngine.Random.value,second=UnityEngine.Random.value,third=UnityEngine.Random.value;if(first<.6f&&second<.8f&&third<.5f){UnityEngine.Random.InitState(seed);return;}}}
    private static void Clean(){if(target!=null)UnityEngine.Object.DestroyImmediate(target.gameObject);if(neighbor!=null)UnityEngine.Object.DestroyImmediate(neighbor.gameObject);if(wall!=null)UnityEngine.Object.DestroyImmediate(wall);run.CombatEffects.Clear();run.GetComponent<ExperiencePickupPool>().ClearDrops();}
    private static FieldInfo Field(Type t,string name)=>t.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
    private static MethodInfo Method(Type t,string name)=>t.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance);
    private static void Check(bool condition,string name){if(!condition)throw new Exception(name);assertions++;}
    private static void Finish(string result){EditorApplication.update-=Tick;Result=result;if(run!=null){Clean();run.Config.healthDropChance=originalDropChance;}if(result.StartsWith("PASS"))Debug.Log("ZombieBuildVerification "+result);else Debug.LogError("ZombieBuildVerification "+result);}
}
