using System.Collections.Generic;
using UnityEngine;

/// <summary>Actual branch mechanics, with bounded non-recursive death chains.</summary>
[DisallowMultipleComponent]
public sealed class RunCombatEffects : MonoBehaviour
{
    private RunState run;
    private int combo;
    private float lastHit = -100f;
    private int rhythmHits; // 战斗节奏计数
    private int comboHits; // 连斩狂热计数
    private float comboFrenzyUntil = -100f;
    private float lastDashTime = -100f;
    private bool dashArmorActive;
    private struct Burst { public Vector2 origin; public bool ice, fire, frostfire; public int depth; }
    private readonly Queue<Burst> bursts = new Queue<Burst>();
    private readonly Collider2D[] hits = new Collider2D[128];
    private readonly RaycastHit2D[] walls = new RaycastHit2D[32];
    private readonly HashSet<int> affected = new HashSet<int>();
    private int processingDepth;
    private sealed class BurstVisual { public LineRenderer line; public float age; public Color color; }
    private readonly Queue<LineRenderer> idleVisuals = new Queue<LineRenderer>();
    private readonly List<BurstVisual> activeVisuals = new List<BurstVisual>();
    private Material burstMaterial;
    private int frozenCount; // 当前冰冻敌人数
    private float frostMomentumUntil = -100f;
    private int frostMomentumStacks;
    private float lowHealthRegenAccumulator; // 低血回复累积器
    public int PendingBursts => bursts.Count;
    public void Initialize(RunState state) { run = state; }
    public void Clear()
    {
        bursts.Clear(); combo = 0; lastHit = -100f; processingDepth = 0;
        rhythmHits = 0; comboHits = 0; comboFrenzyUntil = -100f;
        dashArmorActive = false; lastDashTime = -100f;
        frozenCount = 0; frostMomentumStacks = 0; frostMomentumUntil = -100f;
        lowHealthRegenAccumulator = 0f;
        burnZones.Clear();
        statusScanFrame = -1; burningEnemyCount = 0; frozenEnemyCount = 0;
        foreach (var visual in activeVisuals) { visual.line.gameObject.SetActive(false); idleVisuals.Enqueue(visual.line); }
        activeVisuals.Clear();
    }
    public int SwingDamage(int damage)
    {
        if (Time.time - lastHit > 2f) combo = 0;
        float multiplier = 1f;

        // 三连斩：第3次 +45%
        if (combo % 3 == 2) multiplier += run.EffectTotal("ComboDamage");

        // 战斗节奏：每5次命中，下一次 +40%
        if (rhythmHits >= 5 && run.EffectTotal("RhythmBoost") > 0)
        {
            multiplier += run.EffectTotal("RhythmBoost");
            rhythmHits = 0;
        }

        // 低血量伤害：生命 <25% 时 +40%
        if (run.Player != null && run.CurrentHealth < run.MaxHealth * 0.25f)
            multiplier += run.EffectTotal("LowHealthDamage");

        // 超速暴击：攻速 +100% 后，20% 概率双倍伤害
        if (run.EffectTotal("AttackSpeed") >= 1f && run.EffectTotal("HyperStrike") > 0)
            if (Random.value < run.EffectTotal("HyperStrike")) multiplier += 1f;

        return Mathf.RoundToInt(damage * multiplier);
    }
    public void CompleteSwing(int actualDamage)
    {
        if (actualDamage <= 0) return;
        combo++; lastHit = Time.time;
        rhythmHits++; comboHits++;
        run.RecordMeleeHit();

        // 血之契约：每次命中扣2点生命
        if (run.EffectTotal("BloodPact") > 0 && run.Player != null && run.CurrentHealth > 2)
        {
            int bloodCost = Mathf.RoundToInt(2 * run.EffectTotal("BloodPact"));
            run.Player.TakeDamage(bloodCost, Vector2.zero, 0f);
        }

        // 连斩狂热：连续命中5次后，攻速 +25%，持续4秒
        if (comboHits >= 5 && run.EffectTotal("ComboFrenzy") > 0)
        {
            comboFrenzyUntil = Time.time + 4f;
            comboHits = 0;
        }

        if (run.EffectTotal("LifeSteal") > 0 && run.Player != null)
            run.Player.Heal(Mathf.Min(6, Mathf.RoundToInt(actualDamage * run.EffectTotal("LifeSteal"))));
    }
    public int PrepareHit(ZombieChaser target, int swingDamage)
    {
        bool frozen = target.IsFrozen, burning = target.IsBurning;
        int damage = swingDamage;
        bool convertFreezeToBurn = run.EffectTotal("ConvertFreezeToBurn") > 0;

        // 冻伤加成：冰冻敌人 +45% 伤害
        if (frozen) damage = Mathf.RoundToInt(damage * (1f + run.EffectTotal("FrozenDamage")));

        // 寒冰猛击：冰冻敌人额外击退（近战武器自带击退，这里只加强效果）
        // 注意：ZombieChaser 没有 ApplyKnockback 方法，击退通过近战武器的 knockbackMultiplier 实现

        // 燃烧暴击：燃烧敌人25%概率暴击（双倍伤害）
        if (burning && run.EffectTotal("BurnCrit") > 0 && Random.value < run.EffectTotal("BurnCrit"))
            damage = Mathf.RoundToInt(damage * 2f);

        // 热能冲击：同时冰冻+燃烧的敌人，受到伤害 +100%
        if (frozen && burning && run.EffectTotal("ThermalShock") > 0)
            damage = Mathf.RoundToInt(damage * (1f + run.EffectTotal("ThermalShock")));

        // 燃烧处决：燃烧敌人生命 <30% 时，立即击杀
        if (burning && run.EffectTotal("BurnExecute") > 0 && target.CurrentHealth < target.CurrentMaxHealth * 0.3f)
            damage = target.CurrentHealth + 100;

        // 应用冰冻概率（急冻加速联动：移动速度 ≥ +20% 时生效）
        float freezeChance = convertFreezeToBurn ? 0f : run.EffectTotal("FreezeChance");
        if (!convertFreezeToBurn && run.EffectTotal("MoveSpeed") >= 0.2f) freezeChance += run.EffectTotal("HasteFreeze");
        if (Random.value < Mathf.Clamp01(freezeChance))
        {
            target.ApplyFreeze(FreezeDuration);
            // 寒霜动能：冰冻敌人时，攻速 +8%，可叠加至40%
            if (run.EffectTotal("FrostMomentum") > 0)
            {
                frostMomentumStacks = Mathf.Min(5, frostMomentumStacks + 1);
                frostMomentumUntil = Time.time + 6f;
            }
        }

        // 应用燃烧概率（焚心加速联动：移动速度 ≥ +20% 时生效）
        float burnChance = run.EffectTotal("BurnChance") + (convertFreezeToBurn ? run.EffectTotal("FreezeChance") : 0f);
        if (run.EffectTotal("MoveSpeed") >= 0.2f) burnChance += run.EffectTotal("HasteBurn");
        if (Random.value < Mathf.Clamp01(burnChance)) Ignite(target);

        // 冰霜扩散（击中冰冻敌人时）
        if (frozen && Random.value < Mathf.Clamp01(run.EffectTotal("FrostSpread"))) Spread(target, !convertFreezeToBurn);

        // 燃烧蔓延（击中燃烧敌人时）
        if (burning && Random.value < Mathf.Clamp01(run.EffectTotal("BurnSpread"))) Spread(target, false);

        return damage;
    }
    private float FreezeDuration => run.Config.baseFreezeDuration + run.EffectTotal("FreezeDuration");
    private void Ignite(ZombieChaser target)
    {
        var melee = run.Player != null ? run.Player.GetComponent<SurvivorMeleeAttack>() : null;
        int baseDamage = melee != null ? melee.Damage : 80;
        float burnMultiplier = 1f + run.EffectTotal("BurnDamage");
        // Bridge card: igniting an already frozen target makes its DoT stronger.
        if (target.IsFrozen) burnMultiplier *= 1f + run.EffectTotal("FrostfireBurn");
        target.ApplyBurn(3f + run.EffectTotal("BurnDuration"), Mathf.Max(1, Mathf.RoundToInt(baseDamage * .25f * burnMultiplier)));
    }
    private int Nearby(Vector2 origin, float radius)
    {
        affected.Clear();
        var filter = new ContactFilter2D(); filter.SetLayerMask(Physics2D.DefaultRaycastLayers); filter.useTriggers = false;
        return Physics2D.OverlapCircle(origin, radius, filter, hits);
    }
    private bool Visible(Vector2 origin, ZombieChaser target)
    {
        var filter = new ContactFilter2D(); filter.SetLayerMask(Physics2D.DefaultRaycastLayers); filter.useTriggers = false;
        int count = Physics2D.Linecast(origin, target.GetComponent<Collider2D>().bounds.center, filter, walls);
        for (int i = 0; i < count; i++)
        {
            var c = walls[i].collider;
            if (c == null || c.GetComponentInParent<ZombieChaser>() != null || c.GetComponentInParent<SurvivorHealth>() != null) continue;
            if (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) return false;
        }
        return true;
    }
    private void Spread(ZombieChaser source, bool ice)
    {
        Vector2 origin = source.GetComponent<Collider2D>().bounds.center;
        int count = Nearby(origin, 1.5f), spread = 0;
        for (int i = 0; i < count && spread < 2; i++)
        {
            var other = hits[i] != null ? hits[i].GetComponentInParent<ZombieChaser>() : null;
            if (other == null || other == source || other.CurrentHealth <= 0 || !affected.Add(other.GetInstanceID()) || !Visible(origin, other)) continue;
            if (ice) other.ApplyFreeze(FreezeDuration); else Ignite(other);
            spread++;
        }
    }
    public void EnemyKilled(Vector2 origin, bool frozen, bool burning, bool causedByPlayer)
    {
        if (!causedByPlayer || run == null || run.Phase != RunState.RunPhase.Playing || processingDepth >= 3 || bursts.Count >= 64) return;

        bool ice = frozen && run.EffectTotal("Shatter") > 0;
        bool fire = burning && run.EffectTotal("BurnExplosion") > 0;

        // 霜火爆发：同时拥有冰冻和燃烧概率，击杀触发双爆炸
        bool frostfire = run.EffectTotal("FreezeChance") > 0 && run.EffectTotal("BurnChance") > 0
                         && run.EffectTotal("FrostfireBurst") > 0 && (frozen || burning);

        if (ice || fire || frostfire)
            bursts.Enqueue(new Burst { origin = origin, ice = ice, fire = fire, frostfire = frostfire, depth = processingDepth + 1 });

        // 炼狱步伐：燃烧敌人死亡时留下火焰区域
        if (burning && run.EffectTotal("BurnZone") > 0)
            SpawnBurnZone(origin);
    }

    private struct BurnZone { public Vector2 center; public float until; public float nextTick; }
    private readonly List<BurnZone> burnZones = new List<BurnZone>();

    private void SpawnBurnZone(Vector2 center)
    {
        if (burnZones.Count >= 12) burnZones.RemoveAt(0);
        burnZones.Add(new BurnZone { center = center, until = Time.time + 3f, nextTick = Time.time + 1f });
        ShowBurst(center, new Color(1f, .4f, .1f, .5f));
    }

    private void TickBurnZones()
    {
        for (int i = burnZones.Count - 1; i >= 0; i--)
        {
            var zone = burnZones[i];
            if (Time.time > zone.until) { burnZones.RemoveAt(i); continue; }
            if (Time.time < zone.nextTick) continue;
            zone.nextTick = Time.time + 1f;
            burnZones[i] = zone;

            var melee = run.Player != null ? run.Player.GetComponent<SurvivorMeleeAttack>() : null;
            int baseDamage = melee != null ? melee.Damage : 80;
            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * run.EffectTotal("BurnZone")));

            int count = Nearby(zone.center, 1.8f);
            for (int h = 0; h < count; h++)
            {
                var target = hits[h] != null ? hits[h].GetComponentInParent<ZombieChaser>() : null;
                if (target == null || target.CurrentHealth <= 0) continue;
                Ignite(target);
                target.TakeDamage(tickDamage, Vector2.zero, 0.5f, true);
            }
        }
    }
    private void Update()
    {
        if (run == null || run.Phase != RunState.RunPhase.Playing || run.Player == null || run.Player.IsDead) return;

        TickVisuals();
        TickPassiveEffects();
        TickBurnZones();

        int budget = 12;
        while (bursts.Count > 0 && budget-- > 0)
        {
            var burst = bursts.Dequeue(); processingDepth = burst.depth;

            // 霜火爆发：紫色爆炸
            if (burst.frostfire)
                ShowBurst(burst.origin, new Color(0.8f, 0.3f, 1f, 0.9f));
            else
                ShowBurst(burst.origin, burst.fire ? new Color(1,.5f,.15f,.8f) : new Color(.35f,.8f,1,.8f));

            int count = Nearby(burst.origin, 1.6f);
            int baseDamage = run.Player.GetComponent<SurvivorMeleeAttack>().Damage;

            float iceMulti = burst.ice ? run.EffectTotal("Shatter") : 0f;
            float fireMulti = burst.fire ? run.EffectTotal("BurnExplosion") : 0f;
            float frostfireMulti = burst.frostfire ? run.EffectTotal("FrostfireBurst") * 2f : 0f;

            int damage = Mathf.RoundToInt(baseDamage * (iceMulti + fireMulti + frostfireMulti));

            for (int i = 0; i < count; i++)
            {
                var target = hits[i] != null ? hits[i].GetComponentInParent<ZombieChaser>() : null;
                if (target == null || target.CurrentHealth <= 0 || !affected.Add(target.GetInstanceID()) || !Visible(burst.origin, target)) continue;

                if (burst.fire || burst.frostfire) Ignite(target);
                if (burst.ice || burst.frostfire) target.ApplyFreeze(FreezeDuration);

                target.TakeDamage(damage, (Vector2)target.transform.position - burst.origin, 1f, true);
            }
        }
        processingDepth = 0;
    }

    private void TickPassiveEffects()
    {
        // 低血量回复：生命 <30% 时每秒回2点（累积到整数再治疗）
        if (run.EffectTotal("LowHealthRegen") > 0 && run.CurrentHealth < run.MaxHealth * 0.30f)
        {
            lowHealthRegenAccumulator += 2f * run.EffectTotal("LowHealthRegen") * Time.deltaTime;
            if (lowHealthRegenAccumulator >= 1f)
            {
                int heal = Mathf.FloorToInt(lowHealthRegenAccumulator);
                run.Player.Heal(heal);
                lowHealthRegenAccumulator -= heal;
            }
        }
        else
        {
            lowHealthRegenAccumulator = 0f;
        }

        // 连斩狂热：攻速加成过期不影响计数器，等待下次触发
        // （计数器由 CompleteSwing 累积，只在命中时重置）

        // 寒霜动能：过期清零
        if (Time.time > frostMomentumUntil) frostMomentumStacks = 0;

        // 冲刺护甲：持续0.8秒
        if (dashArmorActive && Time.time > lastDashTime + 0.8f) dashArmorActive = false;
    }

    // 被 SurvivorDash 调用
    public void OnDashStart()
    {
        lastDashTime = Time.time;
        if (run.EffectTotal("DashArmor") > 0) dashArmorActive = true;

        // 冲刺回声：残影伤害周围敌人
        if (run.EffectTotal("DashEcho") > 0 && run.Player != null)
        {
            Vector2 origin = run.Player.transform.position;
            int count = Nearby(origin, 1.8f);
            int baseDamage = run.Player.GetComponent<SurvivorMeleeAttack>().Damage;
            int echoDamage = Mathf.RoundToInt(baseDamage * run.EffectTotal("DashEcho"));

            for (int i = 0; i < count; i++)
            {
                var target = hits[i] != null ? hits[i].GetComponentInParent<ZombieChaser>() : null;
                if (target == null || target.CurrentHealth <= 0 || !affected.Add(target.GetInstanceID())) continue;
                target.TakeDamage(echoDamage, Vector2.zero, 0f, true);
            }
            affected.Clear();
        }
    }

    // 被 RunState 调用
    public void OnEliteKill()
    {
        if (run.EffectTotal("EliteKillHeal") > 0 && run.Player != null)
            run.Player.Heal(Mathf.RoundToInt(run.EffectTotal("EliteKillHeal")));
    }

    // 获取当前攻速加成（供 SurvivorMeleeAttack 调用）
    public float GetAttackSpeedBonus()
    {
        float bonus = 0f;
        if (Time.time <= comboFrenzyUntil) bonus += run.EffectTotal("ComboFrenzy");
        if (frostMomentumStacks > 0) bonus += frostMomentumStacks * run.EffectTotal("FrostMomentum");
        // 业火缠身：每个燃烧敌人 +8% 攻速
        if (run.EffectTotal("BurnHaste") > 0)
        {
            int burningCount = CountBurningEnemies();
            bonus += burningCount * run.EffectTotal("BurnHaste");
        }
        return bonus;
    }

    // 获取当前减伤（供 SurvivorHealth 调用）
    public float GetDamageReduction()
    {
        // 玻璃大炮：完全放弃减伤，换取近战伤害 +80%
        if (run.EffectTotal("GlassCannon") > 0) return 0f;

        float reduction = 0f;
        if (dashArmorActive) reduction += run.EffectTotal("DashArmor");
        // 燃烧护甲：每个燃烧敌人减伤 4%
        if (run.EffectTotal("BurnArmor") > 0)
        {
            int burningCount = CountBurningEnemies();
            reduction += Mathf.Min(0.4f, burningCount * run.EffectTotal("BurnArmor"));
        }
        // 冰盾：场上有冰冻敌人时 -20% 伤害
        if (run.EffectTotal("FrostShield") > 0 && CountFrozenEnemies() > 0)
            reduction += run.EffectTotal("FrostShield");
        return Mathf.Clamp01(reduction);
    }

    // 获取当前移速加成（供 SurvivorMovement 调用）
    public float GetMoveSpeedBonus()
    {
        float bonus = 0f;
        // 极寒领域：每个冰冻敌人 +4% 移速
        if (run.EffectTotal("FrostAura") > 0)
        {
            int frozenCount = CountFrozenEnemies();
            bonus += frozenCount * run.EffectTotal("FrostAura");
        }
        return bonus;
    }

    // 获取当前攻击范围加成（供 SurvivorMeleeAttack 调用）
    public float GetMeleeRangeBonus()
    {
        float bonus = 0f;
        // 凛冬之怒：冰冻 ≥3 个敌人时 +30% 攻击范围
        if (run.EffectTotal("FrostRangeBoost") > 0 && CountFrozenEnemies() >= 3)
            bonus += run.EffectTotal("FrostRangeBoost");
        return bonus;
    }

    private void ShowBurst(Vector2 position, Color color)
    {
        if (activeVisuals.Count >= 16) return;
        LineRenderer line;
        if (idleVisuals.Count > 0) line=idleVisuals.Dequeue();
        else
        {
            if(burstMaterial==null)burstMaterial=new Material(Shader.Find("Sprites/Default"));
            var go=new GameObject("BranchBurst");go.transform.SetParent(transform,false);
            line=go.AddComponent<LineRenderer>();line.sharedMaterial=burstMaterial;
            line.useWorldSpace=false;line.loop=true;line.positionCount=16;line.widthMultiplier=.05f;line.sortingOrder=5600;
        }
        line.transform.position=position;line.gameObject.SetActive(true);
        activeVisuals.Add(new BurstVisual {line=line,color=color});
    }
    private void TickVisuals()
    {
        for(int i=activeVisuals.Count-1;i>=0;i--)
        {
            var visual=activeVisuals[i];visual.age+=Time.deltaTime;float t=visual.age/.18f;
            if(t>=1){visual.line.gameObject.SetActive(false);idleVisuals.Enqueue(visual.line);activeVisuals.RemoveAt(i);continue;}
            float radius=Mathf.Lerp(.25f,1.6f,t);
            for(int p=0;p<16;p++){float angle=p*Mathf.PI*2/16;visual.line.SetPosition(p,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius);}
            var color=visual.color;color.a*=1-t;visual.line.startColor=visual.line.endColor=color;
        }
    }
    private void OnDestroy(){if(burstMaterial!=null)Destroy(burstMaterial);}

    // 状态计数按帧缓存：同一帧内多个系统查询只扫描一次
    private int statusScanFrame = -1;
    private int burningEnemyCount;
    private int frozenEnemyCount;

    private void ScanStatusCounts()
    {
        if (statusScanFrame == Time.frameCount) return;
        statusScanFrame = Time.frameCount;
        burningEnemyCount = 0;
        frozenEnemyCount = 0;
        var enemies = FindObjectsOfType<ZombieChaser>();
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.CurrentHealth <= 0) continue;
            if (enemy.IsBurning) burningEnemyCount++;
            if (enemy.IsFrozen) frozenEnemyCount++;
        }
    }

    private int CountBurningEnemies()
    {
        ScanStatusCounts();
        return burningEnemyCount;
    }

    private int CountFrozenEnemies()
    {
        ScanStatusCounts();
        return frozenEnemyCount;
    }
}
