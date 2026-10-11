using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Integration checks restore the user's meta progression in every exit path.</summary>
[InitializeOnLoad]
public static class SurvivorMetaVerification
{
    [Serializable] private class Backup { public bool[] exists; public int[] values; public string settled; public bool hadSettled; }
    private static readonly string[] Keys = { "ZombieSurvivor.Points", "ZombieSurvivor.Unlock.Bow", "ZombieSurvivor.Unlock.Hammer", "ZombieSurvivor.Unlock.BowCards", "ZombieSurvivor.Unlock.Vitality" };
    private const string BackupKey = "ZombieMetaVerification.Backup";
    private static int step, assertions, arrowsCreated;
    private static float ready;
    private static double deadline;
    private static SurvivorHealth player;
    private static SurvivorMeleeAttack weapon;
    private static ZombieChaser front, rear;
    private static GameObject wall;
    public static string Result { get; private set; } = "Not run";

    static SurvivorMetaVerification()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(BackupKey, ""))) Restore();
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) { EditorApplication.update -= Tick; Restore(); } };
    }
    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update -= Tick;
        var backup = new Backup { exists = Keys.Select(PlayerPrefs.HasKey).ToArray(), values = Keys.Select(k => PlayerPrefs.GetInt(k, 0)).ToArray(),
            hadSettled = PlayerPrefs.HasKey("ZombieSurvivor.LastSettledRun"), settled = PlayerPrefs.GetString("ZombieSurvivor.LastSettledRun", "") };
        SessionState.SetString(BackupKey, JsonUtility.ToJson(backup));
        assertions = step = 0;
        Result = "Running";
        deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey("ZombieSurvivor.LastSettledRun");
            Check(!SurvivorMetaProgress.TryUnlockBow(), "insufficient points cannot unlock bow");
            Check(!SurvivorMetaProgress.IsWeaponUnlocked(SurvivorWeaponKind.Bow), "bow initially locked");
            Check(SurvivorMetaProgress.CalculateReward(3, 120, false) == 84, "room and kill payout");
            Check(SurvivorMetaProgress.CalculateReward(5, 300, true) == 210, "victory bonus payout");
            Check(SurvivorMetaProgress.SettleRun("meta-verification", 3, 120, false) == 84, "first payout");
            Check(SurvivorMetaProgress.SettleRun("meta-verification", 3, 120, false) == 0 && SurvivorMetaProgress.Points == 84, "duplicate settlement rejected");
            SurvivorMetaProgress.AddPoints(600);
            Check(!SurvivorMetaProgress.TryUnlockBowCards(), "advanced cards require bow");
            Check(SurvivorMetaProgress.TryUnlockBow() && SurvivorMetaProgress.Points == 564, "unlock spends exact bow cost");
            Check(SurvivorMetaProgress.TryUnlockBow() && SurvivorMetaProgress.Points == 564, "repeat unlock costs nothing");
            Check(SurvivorMetaProgress.TryUnlockHammer(), "hammer unlock");
            Check(SurvivorMetaProgress.TryBuyVitality() && Mathf.Approximately(SurvivorMetaProgress.StartingHealthMultiplier, 1.05f), "permanent health bonus");
            RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel01.unity", 6143, SurvivorWeaponKind.Bow);
            EditorApplication.update += Tick;
        }
        catch (Exception e) { Finish("FAIL: " + e.Message); }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Stopped"); return; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at step " + step);
            var run = RunState.Instance;
            if (run == null || run.Player == null || run.Phase == RunState.RunPhase.Loading) return;
            if (step == 0)
            {
                player = run.Player; weapon = player.GetComponent<SurvivorMeleeAttack>();
                UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>().SetSpawningEnabled(false);
                foreach (var zombie in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None)) zombie.gameObject.SetActive(false);
                Field(typeof(SurvivorHealth), "invulnerableUntil").SetValue(player, float.MaxValue);
                player.transform.position = new Vector3(160f, 160f);
                player.GetComponent<Rigidbody2D>().position = player.transform.position;
                Check(run.UsesBow && weapon.EquippedWeapon.kind == SurvivorWeaponKind.Bow, "bow equipped on player");
                Check(weapon.EquippedWeapon.damage == 200 && weapon.EquippedWeapon.projectilePierce == 1, "200 base damage and one base penetration");
                VerifyHitboxesAndBoss(run);
                Check(player.MaxHealth == 105 && player.CurrentHealth == 105, "permanent bonus applied to starting HP");
                var library = AssetDatabase.LoadAssetAtPath<SurvivorWeaponPoseLibrary>(SurvivorWeaponSetup.CombatFolder + "SurvivorWeaponPoses.asset");
                var visual = player.GetComponent<SurvivorWeaponVisual>();
                var directions = new[] { Vector2.down, Vector2.up, Vector2.right, Vector2.left };
                foreach (SurvivorView view in Enum.GetValues(typeof(SurvivorView)))
                {
                    var frames = library.BowAttack(view);
                    Check(frames.Length == 6 && frames.All(f => f != null) && frames.Distinct().Count() == 6, "six distinct bow frames " + view);
                    visual.BeginAttack(directions[(int)view]);
                    for (int i = 0; i < frames.Length; i++)
                    {
                        visual.SetAttackProgress(new[] { 0f, 0.2f, 0.4f, 0.55f, 0.7f, 0.9f }[i]);
                        Method(typeof(SurvivorWeaponVisual), "LateUpdate").Invoke(visual, null);
                        Check(player.GetComponent<SpriteRenderer>().sprite == frames[i] && !visual.WeaponRenderer.enabled, "bow body frame " + view + " / " + i);
                    }
                    visual.EndAttack();
                    Check(player.GetComponent<Animator>().enabled, "bow attack restores animator " + view);
                }
                Check(run.Upgrades.Count(u => u.Branch == "Bow") == 12, "12 bow cards parsed");
                Check(run.Upgrades.Any(u => u.Effect == "FrostfireBurn") && run.Upgrades.Any(u => u.Effect == "ConvertFreezeToBurn"), "ice-fire bridge cards parsed");
                var bridgeStacks = (Dictionary<int, int>)Field(typeof(RunState), "stacks").GetValue(run);
                bridgeStacks[105] = 1; bridgeStacks[106] = 0; bridgeStacks[51] = 1;
                var frozenBridgeTarget = Target(new Vector2(162f, 174f));
                frozenBridgeTarget.ApplyFreeze(5f);
                Method(typeof(RunCombatEffects), "Ignite").Invoke(run.CombatEffects, new object[] { frozenBridgeTarget });
                int bridgeBurn = (int)Field(typeof(ZombieChaser), "burnDamage").GetValue(frozenBridgeTarget);
                Check(bridgeBurn == Mathf.RoundToInt(weapon.Damage * .25f * 2f), "frozen target burn damage doubles");
                UnityEngine.Object.DestroyImmediate(frozenBridgeTarget.gameObject);
                bridgeStacks[51] = 0; bridgeStacks[52] = 1; bridgeStacks[105] = 4;
                var convertedTarget = Target(new Vector2(162f, 174f));
                bool convertedBurned = false;
                for (int attempt = 0; attempt < 120 && !convertedBurned; attempt++)
                {
                    run.CombatEffects.PrepareHit(convertedTarget, weapon.Damage);
                    convertedBurned = convertedTarget.IsBurning;
                }
                Check(convertedBurned && !convertedTarget.IsFrozen, "freeze chance converts into burn chance");
                UnityEngine.Object.DestroyImmediate(convertedTarget.gameObject);
                Check(run.Upgrades.Where(u => u.Branch == "Quick" || u.Branch == "Frost" || u.Branch == "Fire").All(u => !run.IsUpgradeEligible(u)), "melee-specific branches excluded");
                Check(run.Upgrades.Where(u => u.RequiredUnlock == "BowCards").All(u => !run.IsUpgradeEligible(u)), "locked advanced cards excluded");
                var stacks = (Dictionary<int, int>)Field(typeof(RunState), "stacks").GetValue(run);
                stacks[101] = 1; stacks[102] = 1; stacks[103] = 1; stacks[104] = 1; stacks[105] = 1; stacks[106] = 1;
                Check(SurvivorMetaProgress.TryUnlockBowCards(), "advanced card pack purchased");
                Check(run.Upgrades.Where(u => u.RequiredUnlock == "BowCards").All(run.IsUpgradeEligible), "unlocked prerequisite cards become eligible");
                stacks.Clear();
                arrowsCreated = run.ArrowPool.CreatedCount;
                front = Target(new Vector2(162f, 160f)); rear = Target(new Vector2(163.3f, 160f));
                run.ArrowPool.Fire(new Vector2(160.5f, 160f), Vector2.right, 90, 20f, 2f, 1, 3f, run);
                ready = Time.time + 0.35f; step = 1; return;
            }
            if (Time.time < ready) return;
            if (step == 1)
            {
                Check(front.CurrentHealth == 910 && rear.CurrentHealth == 910, "one arrow penetrates and hits each enemy once");
                run.ArrowPool.Clear();
                front.SetSpawnHealth(1000); rear.SetSpawnHealth(1000);
                wall = new GameObject("ArrowTestWall", typeof(BoxCollider2D));
                wall.transform.position = new Vector2(161f, 160f); wall.GetComponent<BoxCollider2D>().size = new Vector2(0.1f, 1f);
                Physics2D.SyncTransforms();
                run.ArrowPool.Fire(new Vector2(160.5f, 160f), Vector2.right, 90, 50f, 1f, 3, 3f, run);
                ready = Time.time + 0.2f; step = 2; return;
            }
            if (step == 2)
            {
                Check(front.CurrentHealth == 1000 && rear.CurrentHealth == 1000 && run.ArrowPool.ActiveCount == 0, "high-speed arrow blocked by wall");
                UnityEngine.Object.DestroyImmediate(wall); wall = null;
                var stacks = (Dictionary<int, int>)Field(typeof(RunState), "stacks").GetValue(run);
                stacks[110] = 2; stacks[103] = 1; stacks[101] = 1; stacks[102] = 1;
                player.GetComponent<PlayerRunStats>().Apply(run, player.CurrentHealth);
                Physics2D.SyncTransforms();
                weapon.AttackTowards(new Vector2(168f, 160f));
                Check(weapon.IsAttacking, "shot starts with windup");
                ready = Time.time + weapon.ImpactTime + 0.012f; step = 3; return;
            }
            if (step == 3)
            {
                Check(run.ArrowPool.ActiveCount >= 1 && run.ArrowPool.ActiveCount <= 3, "multishot creates pooled projectiles");
                Check(run.ArrowPool.CreatedCount == arrowsCreated, "shooting reuses prewarmed arrows");
                run.ArrowPool.Clear();
                ready = Time.time + 0.5f; step = 4; return;
            }
            if (step == 4)
            {
                int points = SurvivorMetaProgress.Points;
                Method(typeof(RunState), "SettleRun").Invoke(run, new object[] { false });
                int after = SurvivorMetaProgress.Points;
                Method(typeof(RunState), "SettleRun").Invoke(run, new object[] { false });
                Check(SurvivorMetaProgress.Points == after, "run settlement idempotent");
                Finish("PASS: " + assertions + " meta/bow integration assertions");
            }
        }
        catch (Exception e) { Finish("FAIL: " + e.Message); }
    }
    private static ZombieChaser Target(Vector2 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameMain/Entity/Zombie.prefab");
        var root = UnityEngine.Object.Instantiate(prefab); root.name = "ArrowTestTarget"; root.transform.position = position;
        var zombie = root.GetComponent<ZombieChaser>(); zombie.SetSpawnHealth(1000); zombie.enabled = false;
        root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Physics2D.SyncTransforms(); return zombie;
    }
    private static void VerifyHitboxesAndBoss(RunState run)
    {
        var roots = new List<GameObject>();
        try
        {
            var arrowRoot = new GameObject("HitboxVerificationArrow", typeof(SurvivorArrowProjectile));
            roots.Add(arrowRoot);
            var arrow = arrowRoot.GetComponent<SurvivorArrowProjectile>();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/GameMain/Entity" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<ZombieChaser>() == null) continue;
                var root = UnityEngine.Object.Instantiate(prefab); roots.Add(root);
                root.transform.position = new Vector2(162, 175);
                var zombie = root.GetComponent<ZombieChaser>(); zombie.enabled = false; zombie.SetSpawnHealth(1000);
                root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                if (zombie.Champion != null) zombie.Champion.enabled = false;
                var hitbox = root.GetComponentInChildren<ZombieProjectileHitbox>().GetComponent<BoxCollider2D>();
                Physics2D.SyncTransforms();
                float upper = hitbox.bounds.center.y + hitbox.bounds.extents.y * .6f;
                var foot = root.GetComponent<CircleCollider2D>();
                Check(upper > foot.bounds.max.y + .06f, "upper-body coverage " + prefab.name);
                arrowRoot.transform.position = new Vector2(160, upper);
                arrow.Launch(Vector2.right, 200, 20, 2, 0, 0, run);
                Check(arrow.Tick(.2f) && zombie.CurrentHealth == 800, "upper-body arrow hit " + prefab.name);
                zombie.SetSpawnHealth(1000);
                arrowRoot.transform.position = new Vector2(160, foot.bounds.center.y);
                arrow.Launch(Vector2.right, 200, 20, 2, 0, 0, run);
                Check(arrow.Tick(.2f) && zombie.CurrentHealth == 800, "lower-body arrow hit once " + prefab.name);
                zombie.SetSpawnHealth(1000);
                arrowRoot.transform.position = new Vector2(160, hitbox.bounds.max.y + .2f);
                arrow.Launch(Vector2.right, 200, 20, 2, 0, 0, run); arrow.Tick(.2f);
                Check(zombie.CurrentHealth == 1000, "outside body remains a miss " + prefab.name);
                root.SetActive(false);
            }
            var targets = new[] { Target(new Vector2(162, 175)), Target(new Vector2(163.2f, 175)), Target(new Vector2(164.4f, 175)) };
            foreach (var target in targets) roots.Add(target.gameObject);
            var trigger = new GameObject("IgnoredPickupTrigger", typeof(BoxCollider2D)); roots.Add(trigger);
            trigger.transform.position = new Vector2(161, 175.55f); trigger.GetComponent<BoxCollider2D>().isTrigger = true;
            Physics2D.SyncTransforms();
            arrowRoot.transform.position = new Vector2(160, 175.55f);
            arrow.Launch(Vector2.right, 200, 30, 2, weapon.EquippedWeapon.projectilePierce, 0, run);
            Check(arrow.Tick(.2f) && targets[0].CurrentHealth == 800 && targets[1].CurrentHealth == 800 && targets[2].CurrentHealth == 1000, "base penetration hits two targets and ignores unrelated trigger");
            foreach (var target in targets) target.SetSpawnHealth(1000);
            arrowRoot.transform.position = new Vector2(160, 175.55f);
            arrow.Launch(Vector2.right, 200, 30, 2, weapon.EquippedWeapon.projectilePierce + 1, 0, run);
            arrow.Tick(.2f);
            Check(targets.All(t => t.CurrentHealth == 800), "penetration upgrade adds a third target");
            var boss = run.Config.bossEncounterPrefab.GetComponent<ZombieChampion>();
            Check(boss.GetComponent<ZombieChaser>().maxHealth == 15000, "boss has 15000 health");
            Check(boss.actionCooldown == 1.8f && boss.warningDuration == .75f && boss.barrageWarningDuration == .85f, "faster boss attack tells and intervals");
            Check(boss.summonCountMin == 20 && boss.summonCount == 30 && boss.maxSummonedAlive == 90, "boss random 20-30 summon configuration");
        }
        finally { foreach (var root in roots) if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); assertions++; }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        if (RunState.Instance != null) RunState.Instance.ArrowPool.Clear();
        if (front != null) UnityEngine.Object.DestroyImmediate(front.gameObject);
        if (rear != null) UnityEngine.Object.DestroyImmediate(rear.gameObject);
        if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
        Restore(); Result = result; Debug.Log(result); Time.timeScale = 0f;
    }
    private static void Restore()
    {
        string json = SessionState.GetString(BackupKey, ""); if (string.IsNullOrEmpty(json)) return;
        var backup = JsonUtility.FromJson<Backup>(json);
        for (int i = 0; i < Keys.Length; i++)
        { if (backup.exists[i]) PlayerPrefs.SetInt(Keys[i], backup.values[i]); else PlayerPrefs.DeleteKey(Keys[i]); }
        if (backup.hadSettled) PlayerPrefs.SetString("ZombieSurvivor.LastSettledRun", backup.settled); else PlayerPrefs.DeleteKey("ZombieSurvivor.LastSettledRun");
        PlayerPrefs.Save(); SessionState.EraseString(BackupKey);
    }
}
