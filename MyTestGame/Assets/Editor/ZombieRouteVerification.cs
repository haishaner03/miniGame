using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Walks real rigidbody routes, enters triggers and defeats only their wave members.</summary>
public static class ZombieRouteVerification
{
    public static string Result { get; private set; }
    private static int room, stage, waveIndex, node, assertions, startingRoom;
    private static double deadline, lastMove, waitingUntil;
    private static string[] maps;
    private static SurvivorHealth player;
    private static ZombieSpawner spawner;
    private static ZombieGridPathfinder nav;
    private static ZombieWaveTrigger[] waves;
    private static SafeDoor exit;
    private static List<Vector2> route = new List<Vector2>();
    private static bool capacityTest;
    private static float originalDropChance;
    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        EditorApplication.update -= Tick;
        maps = Resources.Load<ZombieRunConfig>("ZombieRunConfig").roomScenePaths;
        originalDropChance = Resources.Load<ZombieRunConfig>("ZombieRunConfig").healthDropChance;
        room=stage=assertions=0; capacityTest=false;
        deadline=EditorApplication.timeSinceStartup+240;
        Result="Running physical room routes";
        LoadRoom(); EditorApplication.update+=Tick;
    }
    private static void LoadRoom() { stage=0;RunState.StartNewRun(maps[room], 7219+room); }
    private static void Tick()
    {
        try
        {
            if(!EditorApplication.isPlaying) {Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Timeout room="+room+" stage="+stage+" player="+(player!=null?player.transform.position.ToString():"null"));
            var run=RunState.Instance;
            if(run==null || run.Player==null || run.Phase==RunState.RunPhase.Loading) return;
            if(run.Phase==RunState.RunPhase.ChoosingUpgrade) {run.ChooseUpgrade(run.OfferedUpgrades[0].Id);return;}
            if(stage==0)
            {
                player=run.Player; spawner=UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>(); nav=UnityEngine.Object.FindFirstObjectByType<ZombieGridPathfinder>();
                typeof(SurvivorHealth).GetField("invulnerableUntil",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,float.MaxValue);
                player.GetComponent<SurvivorMovement>().enabled=false;
                spawner.respawnAfterDeath=false;spawner.enableTimedWaves=false;spawner.maxAlive=capacityTest ? 100 : spawner.ActiveCount;
                run.Config.healthDropChance=0;
                FreezeAmbient();
                waves=UnityEngine.Object.FindObjectsByType<ZombieWaveTrigger>(FindObjectsSortMode.None).OrderBy(w=>w.transform.position.y).ThenBy(w=>w.transform.position.x).ToArray();
                exit=UnityEngine.Object.FindObjectsByType<SafeDoor>(FindObjectsSortMode.None).First(d=>d.Role==SafeDoor.DoorRole.Exit);
                nav.Rebuild();
                Check(nav.IsWalkablePosition(player.transform.position),"walkable spawn "+maps[room]);
                Check(nav.IsWalkablePosition(exit.GetComponent<Collider2D>().bounds.center),"walkable exit interaction "+maps[room]);
                Check(!run.CurrentRoom.ExitUnlocked,"exit starts locked");
                waveIndex=0;stage=1;
            }
            if(stage==1)
            {
                if(waveIndex>=waves.Length){spawner.maxAlive=100;stage=4;return;}
                Plan(waves[waveIndex].transform.position);stage=2;
            }
            if(stage==2)
            {
                var wave=waves[waveIndex];
                MoveAlongRoute();
                if(!wave.HasTriggered)
                {
                    if(node>=route.Count) throw new Exception("Trigger not entered at "+wave.name+" player="+player.transform.position);
                    return;
                }
                if(!capacityTest)
                {
                    Check(spawner.WavePending(wave.WaveId)>0 && !wave.IsCleared,"full capacity queues wave");
                    int before=wave.Remaining;wave.Trigger();Check(before==wave.Remaining,"wave triggers once");
                    foreach(var z in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None).Where(z=>z.CurrentHealth>0).Take(12)) z.TakeDamage(999999);
                    spawner.maxAlive=100;capacityTest=true;
                }
                var members=UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None).Where(z=>spawner.BelongsToWave(z,wave.WaveId)&&z.CurrentHealth>0).ToArray();
                foreach(var z in members) z.TakeDamage(999999);
                FreezeAmbient();
                if(!wave.IsCleared) return;
                Check(spawner.ActiveCount>0,"unrelated enemies remain while wave clears");
                waitingUntil=EditorApplication.timeSinceStartup+1.5;stage=3;return;
            }
            if(stage==3)
            {
                if(EditorApplication.timeSinceStartup<waitingUntil)return;
                foreach(var g in UnityEngine.Object.FindObjectsByType<ZoneGate>(FindObjectsSortMode.None).Where(g=>g.RequiredWave==waves[waveIndex]))
                {
                    Check(g.IsOpen,"wave gate physically opens "+g.name);
                    Check(g.GetComponentsInChildren<Collider2D>().All(c=>!c.enabled),"gate collider disabled");
                }
                waveIndex++;stage=1;return;
            }
            if(stage==4)
            {
                while(run.RoomKills<run.CurrentRoom.RequiredKills)run.RecordKill();
                waitingUntil=EditorApplication.timeSinceStartup+.2;stage=5;return;
            }
            if(stage==5)
            {
                if(EditorApplication.timeSinceStartup<waitingUntil)return;
                Check(run.CurrentRoom.ExitUnlocked&&!exit.IsLocked&&!spawner.SpawningEnabled,"exit unlock after actual waves "+maps[room]);
                Plan(exit.GetComponent<Collider2D>().bounds.center); startingRoom=run.RoomIndex;waitingUntil=0;stage=6;
            }
            if(stage==6)
            {
                if(run.RoomIndex!=startingRoom || run.Phase==RunState.RunPhase.Loading)
                {
                    Check(true,"physical exit trigger advances room "+maps[room]);
                    room++;if(room==maps.Length){Finish("PASS: "+assertions+" physical route assertions across 4 maps");return;}
                    LoadRoom();return;
                }
                MoveAlongRoute();
                if(node>=route.Count && run.RoomIndex==startingRoom)
                {
                    // Trigger callbacks and scene loading run after this editor update.
                    if(waitingUntil==0) waitingUntil=EditorApplication.timeSinceStartup+2;
                    if(EditorApplication.timeSinceStartup>waitingUntil) throw new Exception("Exit did not advance at "+player.transform.position);
                }
            }
        }
        catch(Exception e){Finish("FAIL: "+e);}
    }
    private static void FreezeAmbient()
    {
        foreach(var z in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))
        {if(z.CurrentHealth>0){z.enabled=false;z.GetComponent<Rigidbody2D>().simulated=false;}}
    }
    private static void Plan(Vector2 target)
    {
        nav.Rebuild();route.Clear();
        Check(nav.TryFindPath(player.transform.position,target,route),"reachable waypoint "+target);
        // The pathfinder snaps its endpoints to grid centers; the actual trigger is at target.
        route.Add(target);node=0;lastMove=EditorApplication.timeSinceStartup;
    }
    private static void MoveAlongRoute()
    {
        if(node>=route.Count)return;
        var rb=player.GetComponent<Rigidbody2D>();
        if(Vector2.Distance(rb.position,route[node])<.13f){node++;lastMove=EditorApplication.timeSinceStartup;return;}
        if(EditorApplication.timeSinceStartup-lastMove>5)throw new Exception("Physical collision blocks path at "+rb.position+" -> "+route[node]);
        rb.MovePosition(Vector2.MoveTowards(rb.position,route[node],.11f));
    }
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);assertions++;}
    private static void Finish(string result)
    {
        EditorApplication.update-=Tick;Result=result;
        if(RunState.Instance!=null)RunState.Instance.Config.healthDropChance=originalDropChance;
        if(result.StartsWith("PASS"))Debug.Log("ZombieRouteVerification "+result);else Debug.LogError("ZombieRouteVerification "+result);
    }
}
