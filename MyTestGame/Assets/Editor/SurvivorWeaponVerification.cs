using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SurvivorWeaponVerification
{
    private static int step, attackCase, assertions;
    private static float deadline, ready;
    private static SurvivorHealth player;
    private static SurvivorMeleeAttack melee;
    private static SurvivorWeaponVisual visual;
    private static ZombieChaser front, rear, far;
    private static GameObject wall;
    private static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.right, Vector2.left };
    public static string Result { get; private set; }

    public static void Start()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter play mode first.");
        EditorApplication.update -= Tick;
        step = attackCase = assertions = 0;
        Result = "Running weapon integration checks";
        deadline = (float)EditorApplication.timeSinceStartup + 45;
        RunState.StartNewRun("Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity", 19383);
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("Stopped"); return; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at " + step);
            var run = RunState.Instance;
            if (run == null || run.Player == null || run.Phase == RunState.RunPhase.Loading) return;
            if (step == 0)
            {
                player = run.Player;
                melee = player.GetComponent<SurvivorMeleeAttack>();
                visual = player.GetComponent<SurvivorWeaponVisual>();
                Check(visual != null && visual.WeaponRenderer.sprite == melee.EquippedWeapon.sprite, "default machete rendered");
                Check(melee.Damage == 80 && player.CurrentHealth == 100, "base balance preserved");
                UnityEngine.Object.FindFirstObjectByType<ZombieSpawner>().SetSpawningEnabled(false);
                foreach (var zombie in UnityEngine.Object.FindObjectsByType<ZombieChaser>(FindObjectsSortMode.None))
                { zombie.enabled = false; zombie.GetComponent<Rigidbody2D>().simulated = false; }
                Field(typeof(SurvivorHealth), "invulnerableUntil").SetValue(player, float.MaxValue);
                player.transform.position = new Vector3(160, 160);
                player.GetComponent<Rigidbody2D>().position = player.transform.position;
                player.GetComponent<Rigidbody2D>().simulated = false;
                VerifyPoses();
                step = 1;
            }
            if (Time.time < ready) return;
            if (step == 1)
            {
                CleanupTargets();
                bool heavy = attackCase >= 4;
                melee.EquipWeapon(heavy);
                Check(visual.WeaponRenderer.sprite == melee.EquippedWeapon.sprite, "equipped weapon sprite");
                Check(heavy ? melee.Damage == 100 && melee.AttackDuration > .3f && melee.WeaponKnockbackMultiplier > 1.5f : melee.Damage == 80 && melee.AttackDuration < .25f, "distinct weapon handling");
                Vector2 direction = Directions[attackCase % 4];
                Vector2 origin = (Vector2)player.transform.position + direction * .2f;
                front = Target(origin + direction * .65f);
                rear = Target(origin - direction * .65f);
                far = Target(origin + direction * 2f);
                Physics2D.SyncTransforms();
                melee.AttackTowards((Vector2)player.transform.position + direction * 2f);
                Check(melee.IsAttacking && !player.GetComponent<Animator>().enabled && !player.GetComponent<SurvivorMovement>().enabled, "body attack owns animation");
                Check(front.CurrentHealth == 1000, "windup does not deal instant damage");
                ready = Time.time + melee.AttackDuration + .05f;
                step = 2; return;
            }
            if (step == 2)
            {
                int actualDamage = 1000 - front.CurrentHealth;
                Check(actualDamage >= Mathf.Max(1, melee.Damage - melee.DamageVariance) && actualDamage <= melee.Damage + melee.DamageVariance, "one directional hit within damage variance " + attackCase);
                Check(rear.CurrentHealth == 1000 && far.CurrentHealth == 1000, "rear and distant targets excluded");
                Check(((Vector2)Field(typeof(ZombieChaser), "knockbackVelocity").GetValue(front)).magnitude >= 2.2f * melee.WeaponKnockbackMultiplier - .001f, "weapon knockback applied");
                Check(!melee.IsAttacking && player.GetComponent<Animator>().enabled && player.GetComponent<SurvivorMovement>().enabled, "animation and movement restored");
                attackCase++;
                ready = Time.time + .2f;
                step = attackCase < 8 ? 1 : 3; return;
            }
            if (step == 3)
            {
                CleanupTargets();
                melee.EquipWeapon(false);
                Vector2 origin = (Vector2)player.transform.position + Vector2.right * .2f;
                front = Target(origin + Vector2.right * .8f);
                wall = new GameObject("WeaponTestWall");
                wall.transform.position = origin + Vector2.right * .4f;
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.08f, .8f);
                Physics2D.SyncTransforms();
                melee.AttackTowards((Vector2)player.transform.position + Vector2.right * 2f);
                ready = Time.time + .3f;
                step = 4; return;
            }
            if (step == 4)
            {
                Check(front.CurrentHealth == 1000, "static wall blocks melee");
                CleanupTargets();
                // Interrupt an attack by switching equipment, then prove it can attack again.
                ready = Time.time + .2f;
                step = 5; return;
            }
            if (step == 5)
            {
                melee.AttackTowards((Vector2)player.transform.position + Vector2.left);
                melee.EquipWeapon(true);
                Check(!melee.IsAttacking && !visual.IsPlayingAttack && player.GetComponent<Animator>().enabled, "equipment switch restores interrupted animation");
                Finish("PASS: " + assertions + " weapon integration assertions");
            }
        }
        catch (Exception exception) { CleanupTargets(); Finish("FAIL: " + exception.Message); }
    }

    private static void VerifyPoses()
    {
        var library = AssetDatabase.LoadAssetAtPath<SurvivorWeaponPoseLibrary>(SurvivorWeaponSetup.CombatFolder + "SurvivorWeaponPoses.asset");
        var body = player.GetComponent<SpriteRenderer>();
        var animator = player.GetComponent<Animator>();
        var movement = player.GetComponent<SurvivorMovement>();
        foreach (SurvivorView view in Enum.GetValues(typeof(SurvivorView)))
        {
            var poses = library.Attack(view);
            Check(poses.Length == 6 && poses.Select(p => p.sprite).Distinct().Count() == 6, "six distinct attack frames " + view);
            visual.BeginAttack(Directions[(int)view]);
            var times = new[] { 0f, .2f, .36f, .49f, .69f, .89f };
            for (int i = 0; i < poses.Length; i++)
            {
                visual.SetAttackProgress(times[i]);
                Method(typeof(SurvivorWeaponVisual), "LateUpdate").Invoke(visual, null);
                Check(body.sprite == poses[i].sprite && !body.flipX, "directional sprite and no double mirror");
                var pose = poses[i];
                Vector2 grip = new Vector2(pose.gripPixel.x - pose.sprite.pivot.x, pose.sprite.rect.height - pose.gripPixel.y - pose.sprite.pivot.y) / pose.sprite.pixelsPerUnit;
                Check(Vector2.Distance(visual.WeaponPivot.localPosition, grip) < .001f, "weapon attached to frame hand");
                Check(visual.WeaponRenderer.sortingOrder == body.sortingOrder + (view == SurvivorView.Up ? -1 : 1), "directional layering");
            }
            visual.EndAttack();
            Check(animator.enabled, "pose preview restores Animator");
            movement.SetFacingDirection(Directions[(int)view]);
            movement.RefreshAnimation();
            animator.Update(0);
            string idle = view == SurvivorView.Down ? "Idle" : "Idle" + view;
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName(idle), "idle keeps attack direction");
        }
        for (int i = 0; i < 6; i++)
        {
            var left = library.attackLeft[i]; var right = library.attackRight[i];
            Check(Mathf.Abs(left.gripPixel.x + right.gripPixel.x - 64) < .001f && Mathf.Abs(left.weaponAngle + right.weaponAngle) < .001f, "mirrored hand and weapon angle");
        }
    }

    private static ZombieChaser Target(Vector2 center)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameMain/Entity/Zombie.prefab");
        var root = UnityEngine.Object.Instantiate(prefab);
        root.name = "WeaponTestTarget";
        var zombie = root.GetComponent<ZombieChaser>();
        zombie.SetSpawnHealth(1000);
        zombie.enabled = false;
        root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var collider = root.GetComponent<Collider2D>();
        root.transform.position = center - (Vector2)collider.bounds.center;
        root.GetComponent<Rigidbody2D>().position = root.transform.position;
        return zombie;
    }

    private static void CleanupTargets()
    {
        if (front != null) UnityEngine.Object.DestroyImmediate(front.gameObject);
        if (rear != null) UnityEngine.Object.DestroyImmediate(rear.gameObject);
        if (far != null) UnityEngine.Object.DestroyImmediate(far.gameObject);
        if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); assertions++; }
    private static void Finish(string result)
    {
        CleanupTargets();
        EditorApplication.update -= Tick;
        Result = result;
        if (result.StartsWith("PASS")) Debug.Log("SurvivorWeaponVerification " + result);
        else Debug.LogError("SurvivorWeaponVerification " + result);
    }
}
