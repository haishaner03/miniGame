using UnityEngine;

/// <summary>Telegraphed attacks layered over the shared chase, damage and status systems.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ZombieChaser))]
public sealed class ZombieChampion : MonoBehaviour
{
    public enum ActionState { Arrival, Pursuit, SlamWarning, ChargeWarning, Charging, Recovery, Roar, Dead }
    public bool isBoss;
    public string displayName = "猎食者";
    public float slamRadius = 1.25f;
    public float warningDuration = .85f;
    public float chargeSpeed = 7f;
    public float chargeDistance = 4.5f;
    public int slamDamage = 26;
    public int chargeDamage = 28;
    public float actionCooldown = 2.5f;
    public int summonCount = 3;
    public int maxSummonedAlive = 6;
    public ActionState State { get; private set; } = ActionState.Arrival;
    public bool IsEnraged { get; private set; }
    public bool IsAmbientElite { get; private set; }
    public bool IsCharging => State == ActionState.Charging && Health != null && !Health.IsFrozen;
    public bool IsArriving => State == ActionState.Arrival;
    public bool TelegraphVisible => warning != null && warning.enabled;
    public float WarningProgress => Mathf.Clamp01(elapsed / Mathf.Max(.1f, WarningTime));
    public ZombieChaser Health { get; private set; }
    public string ActionLabel => State == ActionState.Arrival ? "强敌出现 · 准备战斗" :
        State == ActionState.SlamWarning ? "重击蓄力 · 离开红圈" :
        State == ActionState.ChargeWarning ? "冲锋蓄力 · 向侧面闪避" :
        State == ActionState.Charging ? "冲锋！" : State == ActionState.Recovery ? "攻击后摇 · 趁机反击" :
        State == ActionState.Roar ? "暴怒 · 召唤丧尸" : State == ActionState.Dead ? "已击败" : "寻找攻击机会";
    private SurvivorHealth player;
    private ZombieSpawner spawner;
    private Rigidbody2D body;
    private ZombieGridPathfinder nav;
    private float elapsed, nextAction, nextSummon, traveled;
    private bool chargeHit;
    private int actionNumber;
    private Vector2 lockedDirection, warningOrigin;
    private LineRenderer warning, pulse, halo;
    private Material material;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[32];
    private float WarningTime => warningDuration * (IsEnraged ? .78f : 1f);
    private float Cooldown => actionCooldown * (IsEnraged ? .7f : 1f);

    private void Awake()
    {
        Health = GetComponent<ZombieChaser>(); body = GetComponent<Rigidbody2D>();
        Health.Died += OnDied;
        material = new Material(Shader.Find("Sprites/Default"));
        warning = MakeLine("AttackWarning", .065f);
        pulse = MakeLine("AttackProgress", .04f);
        halo = MakeLine("ChampionMarker", .045f);
        halo.enabled = true;
    }

    public void Initialize(SurvivorHealth target, ZombieSpawner source)
    {
        ResetBehaviour(target, source, false);
        Health.OnSpawnedFromPool(target.transform); Health.ForceAggro();
    }

    public void InitializeAmbient(SurvivorHealth target, ZombieSpawner source)
    {
        // The spawner already reset health, difficulty and movement speed.
        ResetBehaviour(target, source, true);
    }

    private void ResetBehaviour(SurvivorHealth target, ZombieSpawner source, bool ambient)
    {
        player = target; spawner = source; nav = FindFirstObjectByType<ZombieGridPathfinder>();
        IsAmbientElite = ambient;
        IsEnraged = false;
        State = ambient ? ActionState.Pursuit : ActionState.Arrival;
        elapsed = traveled = 0f;
        chargeHit = false;
        actionNumber = 0;
        lockedDirection = warningOrigin = Vector2.zero;
        nextAction = Time.time + 1.8f;
        nextSummon = Time.time + 9f;
        HideWarning();
        if (halo != null) halo.enabled = true;
    }

    // Called by ZombieChaser after status damage, before normal chase / melee.
    public bool TickBehaviour(float delta)
    {
        var run = RunState.Instance;
        if (player == null || player.IsDead || (run != null && run.Phase != RunState.RunPhase.Playing))
        { HideWarning(); Health.HoldForSpecial(false); return true; }
        DrawCircle(halo, (Vector2)transform.position, isBoss ? .58f : .44f, new Color(1f,isBoss?.22f:.7f,.12f,.7f));
        if (Health.IsFrozen) { Health.HoldForSpecial(false); return true; }
        if (IsAmbientElite && !Health.HasAggro)
        {
            HideWarning();
            if (State != ActionState.Pursuit) SetState(ActionState.Pursuit);
            return false;
        }
        elapsed += delta;
        if (State == ActionState.Arrival)
        {
            Health.HoldForSpecial(false);
            if (elapsed >= 1.4f) SetState(ActionState.Pursuit);
            return true;
        }
        if (isBoss && !IsEnraged && Health.CurrentHealth * 2 <= Health.CurrentMaxHealth)
        {
            IsEnraged = true; HideWarning(); SetState(ActionState.Roar);
            nextSummon = Time.time + 10f;
            Summon();
        }
        if (State == ActionState.Roar)
        {
            Health.HoldForSpecial(true);
            if (elapsed >= 1.1f) Recover();
            return true;
        }
        if (State == ActionState.Recovery)
        {
            Health.HoldForSpecial(false);
            if (elapsed >= .8f) { HideWarning(); SetState(ActionState.Pursuit); }
            return true;
        }
        if (State == ActionState.SlamWarning || State == ActionState.ChargeWarning)
        {
            Health.HoldForSpecial(true);
            // Lock the target halfway through the tell, leaving time to dodge.
            if (elapsed < WarningTime * .5f && State == ActionState.ChargeWarning) Aim();
            DrawWarning();
            if (elapsed < WarningTime) return true;
            if (State == ActionState.SlamWarning) { HitSlam(); Recover(); }
            else { chargeHit = false; traveled = 0; SetState(ActionState.Charging); HideWarning(); }
            return true;
        }
        if (State == ActionState.Charging) { Health.HoldForSpecial(true); return true; }
        if (isBoss && IsEnraged && Time.time >= nextSummon)
        {
            nextSummon = Time.time + 12f; SetState(ActionState.Roar); Summon(); return true;
        }
        if (Time.time < nextAction) return false;
        float distance = Vector2.Distance(transform.position, player.transform.position);
        if (distance <= slamRadius + .25f && (isBoss || actionNumber % 2 == 1))
        {
            warningOrigin = transform.position; Aim(); SetState(ActionState.SlamWarning); actionNumber++; DrawWarning();
            Health.HoldForSpecial(true); return true;
        }
        if (distance <= 6.5f && CanSeePlayer(transform.position))
        {
            warningOrigin = transform.position; Aim(); SetState(ActionState.ChargeWarning); actionNumber++; DrawWarning();
            Health.HoldForSpecial(true); return true;
        }
        return false;
    }

    private void FixedUpdate()
    {
        if (!IsCharging || player == null || player.IsDead || Time.timeScale <= 0) return;
        float distance = Mathf.Min(chargeSpeed * (IsEnraged ? 1.2f : 1f) * Time.fixedDeltaTime, chargeDistance - traveled);
        var filter = Filter();
        int count = body.Cast(lockedDirection, filter, castHits, distance + .03f);
        // Resolve the nearest wall first: cast result order must never let a
        // player behind a wall receive charge damage.
        for (int i = 0; i < count; i++)
        {
            var c = castHits[i].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<SurvivorHealth>() != null) continue;
            if (c.GetComponentInParent<ZombieChaser>() != null) continue;
            distance = Mathf.Min(distance, Mathf.Max(0, castHits[i].distance - .03f));
        }
        Vector2 next = body.position + lockedDirection * distance;
        if (nav != null && !nav.IsWalkablePosition(next)) distance = 0;
        for (int i = 0; i < count && !chargeHit; i++)
        {
            var hit = castHits[i];
            if (hit.collider != null && hit.collider.GetComponentInParent<SurvivorHealth>() == player && hit.distance <= distance + .001f)
            { player.TakeDamage(chargeDamage, lockedDirection, 3.5f); chargeHit = true; }
        }
        body.MovePosition(body.position + lockedDirection * distance);
        traveled += distance;
        if (distance < .01f || traveled >= chargeDistance - .01f || elapsed > 1.2f) Recover();
    }

    private void HitSlam()
    {
        Vector2 center = player.GetComponent<Collider2D>().bounds.center;
        if (Vector2.Distance(warningOrigin, center) > slamRadius || !CanSeePlayer(warningOrigin)) return;
        player.TakeDamage(slamDamage + (IsEnraged ? 4 : 0), center - warningOrigin, 3f);
    }
    private bool CanSeePlayer(Vector2 origin)
    {
        int count = Physics2D.Linecast(origin, player.GetComponent<Collider2D>().bounds.center, Filter(), sightHits);
        for (int i = 0; i < count; i++)
        {
            var c = sightHits[i].collider;
            if (c == null || c.GetComponentInParent<ZombieChaser>() != null || c.GetComponentInParent<SurvivorHealth>() != null) continue;
            if (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) return false;
        }
        return true;
    }
    private static ContactFilter2D Filter()
    { var filter = new ContactFilter2D(); filter.SetLayerMask(Physics2D.DefaultRaycastLayers); filter.useTriggers = false; return filter; }
    private void Aim()
    {
        lockedDirection = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        if (lockedDirection.sqrMagnitude < .01f) lockedDirection = Vector2.down;
        Health.FaceDirection(lockedDirection);
    }
    private void Summon()
    {
        if (spawner != null) spawner.SpawnEncounterAdds(Mathf.Min(summonCount, Mathf.Max(0,maxSummonedAlive - spawner.ActiveCount)));
    }
    private void SetState(ActionState state) { State = state; elapsed = 0; }
    private void Recover() { SetState(ActionState.Recovery); nextAction = Time.time + Cooldown; HideWarning(); }
    private void OnDied(ZombieChaser unused) { State = ActionState.Dead; HideWarning(); halo.enabled = false; }
    private LineRenderer MakeLine(string label, float width)
    {
        var root = new GameObject(label); root.transform.SetParent(transform, false);
        var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
        line.loop = true; line.widthMultiplier = width; line.sortingOrder = 5400; line.enabled = false;
        return line;
    }
    private static void DrawCircle(LineRenderer line, Vector2 center, float radius, Color color)
    {
        line.loop = true; line.positionCount = 32; line.startColor = line.endColor = color;
        for (int i=0;i<32;i++) { float angle=i*Mathf.PI*2/32; line.SetPosition(i,center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius); }
    }
    private void DrawWarning()
    {
        warning.enabled = pulse.enabled = true;
        Color color = Color.Lerp(new Color(1f,.62f,.12f,.8f),new Color(1f,.16f,.08f,1f),WarningProgress);
        if (State == ActionState.SlamWarning)
        {
            DrawCircle(warning,warningOrigin,slamRadius,color);
            DrawCircle(pulse,warningOrigin,Mathf.Max(.08f,slamRadius*WarningProgress),color);
        }
        else
        {
            Vector2 side = new Vector2(-lockedDirection.y,lockedDirection.x)*.4f;
            warning.loop=true; warning.positionCount=4; warning.startColor=warning.endColor=color;
            warning.SetPosition(0,warningOrigin+side);warning.SetPosition(1,warningOrigin+lockedDirection*chargeDistance+side);
            warning.SetPosition(2,warningOrigin+lockedDirection*chargeDistance-side);warning.SetPosition(3,warningOrigin-side);
            pulse.loop=false;pulse.positionCount=2;pulse.startColor=pulse.endColor=color;
            pulse.SetPosition(0,warningOrigin);pulse.SetPosition(1,warningOrigin+lockedDirection*chargeDistance*WarningProgress);
        }
    }
    private void HideWarning() { if(warning!=null)warning.enabled=false;if(pulse!=null)pulse.enabled=false; }
    private void OnDestroy()
    { if(Health!=null)Health.Died-=OnDied;if(material!=null)Destroy(material); }
}
