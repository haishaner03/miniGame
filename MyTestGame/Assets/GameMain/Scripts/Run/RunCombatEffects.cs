using System.Collections.Generic;
using UnityEngine;

/// <summary>Actual branch mechanics, with bounded non-recursive death chains.</summary>
[DisallowMultipleComponent]
public sealed class RunCombatEffects : MonoBehaviour
{
    private RunState run;
    private int combo;
    private float lastHit = -100f;
    private struct Burst { public Vector2 origin; public bool ice, fire; public int depth; }
    private readonly Queue<Burst> bursts = new Queue<Burst>();
    private readonly Collider2D[] hits = new Collider2D[128];
    private readonly RaycastHit2D[] walls = new RaycastHit2D[32];
    private readonly HashSet<int> affected = new HashSet<int>();
    private int processingDepth;
    private sealed class BurstVisual { public LineRenderer line; public float age; public Color color; }
    private readonly Queue<LineRenderer> idleVisuals = new Queue<LineRenderer>();
    private readonly List<BurstVisual> activeVisuals = new List<BurstVisual>();
    private Material burstMaterial;
    public int PendingBursts => bursts.Count;
    public void Initialize(RunState state) { run = state; }
    public void Clear()
    {
        bursts.Clear(); combo = 0; lastHit = -100f; processingDepth = 0;
        foreach (var visual in activeVisuals) { visual.line.gameObject.SetActive(false); idleVisuals.Enqueue(visual.line); }
        activeVisuals.Clear();
    }
    public int SwingDamage(int damage)
    {
        if (Time.time - lastHit > 2f) combo = 0;
        return Mathf.RoundToInt(damage * (combo % 3 == 2 ? 1f + run.EffectTotal("ComboDamage") : 1f));
    }
    public void CompleteSwing(int actualDamage)
    {
        if (actualDamage <= 0) return;
        combo++; lastHit = Time.time;
        run.RecordMeleeHit();
        if (run.EffectTotal("LifeSteal") > 0 && run.Player != null)
            run.Player.Heal(Mathf.Min(6, Mathf.RoundToInt(actualDamage * run.EffectTotal("LifeSteal"))));
    }
    public int PrepareHit(ZombieChaser target, int swingDamage)
    {
        bool frozen = target.IsFrozen, burning = target.IsBurning;
        int damage = Mathf.RoundToInt(swingDamage * (frozen ? 1f + run.EffectTotal("FrozenDamage") : 1f));
        if (Random.value < Mathf.Clamp01(run.EffectTotal("FreezeChance"))) target.ApplyFreeze(FreezeDuration);
        if (Random.value < Mathf.Clamp01(run.EffectTotal("BurnChance"))) Ignite(target);
        if (frozen && Random.value < Mathf.Clamp01(run.EffectTotal("FrostSpread"))) Spread(target, true);
        if (burning && Random.value < Mathf.Clamp01(run.EffectTotal("BurnSpread"))) Spread(target, false);
        return damage;
    }
    private float FreezeDuration => run.Config.baseFreezeDuration + run.EffectTotal("FreezeDuration");
    private void Ignite(ZombieChaser target)
    {
        var melee = run.Player != null ? run.Player.GetComponent<SurvivorMeleeAttack>() : null;
        int baseDamage = melee != null ? melee.Damage : 80;
        target.ApplyBurn(3f + run.EffectTotal("BurnDuration"), Mathf.Max(1, Mathf.RoundToInt(baseDamage * .25f * (1f + run.EffectTotal("BurnDamage")))));
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
        if (ice || fire) bursts.Enqueue(new Burst { origin = origin, ice = ice, fire = fire, depth = processingDepth + 1 });
    }
    private void Update()
    {
        if (run == null || run.Phase != RunState.RunPhase.Playing || run.Player == null || run.Player.IsDead) return;
        TickVisuals();
        int budget = 12;
        while (bursts.Count > 0 && budget-- > 0)
        {
            var burst = bursts.Dequeue(); processingDepth = burst.depth;
            ShowBurst(burst.origin, burst.fire ? new Color(1,.5f,.15f,.8f) : new Color(.35f,.8f,1,.8f));
            int count = Nearby(burst.origin, 1.6f);
            int baseDamage = run.Player.GetComponent<SurvivorMeleeAttack>().Damage;
            int damage = Mathf.RoundToInt(baseDamage * ((burst.ice ? run.EffectTotal("Shatter") : 0) + (burst.fire ? run.EffectTotal("BurnExplosion") : 0)));
            for (int i = 0; i < count; i++)
            {
                var target = hits[i] != null ? hits[i].GetComponentInParent<ZombieChaser>() : null;
                if (target == null || target.CurrentHealth <= 0 || !affected.Add(target.GetInstanceID()) || !Visible(burst.origin, target)) continue;
                if (burst.fire) Ignite(target);
                target.TakeDamage(damage, (Vector2)target.transform.position - burst.origin, 1f, true);
            }
        }
        processingDepth = 0;
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
}
