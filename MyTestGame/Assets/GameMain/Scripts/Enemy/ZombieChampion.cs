using UnityEngine;

/// <summary>Telegraphed champion attacks; boss skills seek their sprite frames to gameplay timing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ZombieChaser))]
public sealed class ZombieChampion : MonoBehaviour
{
    public enum ActionState { Arrival, Pursuit, SlamWarning, ChargeWarning, Charging, Recovery, Roar, Dead, SlamImpact, BarrageWarning, BarrageImpact }
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
    [Min(0)] public int summonCountMin;
    public int maxSummonedAlive = 6;
    [Min(.1f)] public float summonInterval = 12f;
    [Min(.1f)] public float bossRecoveryDuration = .65f;
    [Min(.1f)] public float roarDuration = .9f;
    [Header("Boss · 碎石轰击")]
    public float barrageRadius = .65f;
    public float barrageWarningDuration = 1.1f;
    public int barrageDamage = 20;
    public Sprite fallingDebrisSprite;
    public ActionState State { get; private set; } = ActionState.Arrival;
    public bool IsEnraged { get; private set; }
    public bool IsAmbientElite { get; private set; }
    public bool IsCharging => State == ActionState.Charging && Health != null && !Health.IsFrozen;
    public bool IsArriving => State == ActionState.Arrival;
    public bool TelegraphVisible => (warning != null && warning.enabled) ||
        (debrisWarnings[0] != null && debrisWarnings[0].enabled);
    public float WarningProgress => Mathf.Clamp01(elapsed / Mathf.Max(.1f, WarningTime));
    public ZombieChaser Health { get; private set; }
    public string ActionLabel => State == ActionState.Arrival ? "强敌出现 · 准备战斗" :
        State == ActionState.SlamWarning ? "双拳砸地 · 离开红圈" :
        State == ActionState.ChargeWarning ? "肩撞蓄力 · 向侧面闪避" :
        State == ActionState.Charging ? "重装冲撞！" :
        State == ActionState.BarrageWarning ? "碎石轰击 · 离开标记位置" :
        State == ActionState.BarrageImpact ? "碎石落下 · 保持移动" :
        State == ActionState.SlamImpact ? "震地重击！" :
        State == ActionState.Recovery ? "攻击后摇 · 趁机反击" :
        State == ActionState.Roar ? "暴怒嘶吼 · 召唤增援" :
        State == ActionState.Dead ? "已击败" : "寻找攻击机会";
    private SurvivorHealth player;
    private ZombieSpawner spawner;
    private Rigidbody2D body;
    private ZombieGridPathfinder nav;
    private float elapsed, nextAction, nextSummon, traveled;
    private bool chargeHit, barrageHit;
    private int actionNumber;
    private Vector2 lockedDirection, warningOrigin;
    private LineRenderer warning, pulse, halo;
    private Material material;
    private string recoveryAnimation;
    private readonly LineRenderer[] debrisWarnings = new LineRenderer[3];
    private readonly SpriteRenderer[] debrisVisuals = new SpriteRenderer[3];
    private readonly Vector2[] debrisTargets = new Vector2[3];
    private readonly bool[] debrisLanded = new bool[3];
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[32];
    private float WarningTime => State == ActionState.BarrageWarning ? barrageWarningDuration :
        warningDuration * (IsEnraged ? .85f : 1f);
    private float Cooldown => actionCooldown * (IsEnraged ? .75f : 1f);
    private float RecoveryDuration => isBoss ? bossRecoveryDuration : .8f;

    private void Awake()
    {
        Health = GetComponent<ZombieChaser>(); body = GetComponent<Rigidbody2D>();
        Health.Died += OnDied;
        material = new Material(Shader.Find("Sprites/Default"));
        warning = MakeLine("AttackWarning", .065f);
        pulse = MakeLine("AttackProgress", .04f);
        halo = MakeLine("ChampionMarker", .045f);
        halo.enabled = true;
        if (isBoss)
            for (int i = 0; i < debrisWarnings.Length; i++)
            {
                debrisWarnings[i] = MakeLine("DebrisWarning" + i, .055f);
                var root = new GameObject("FallingDebris" + i);
                root.transform.SetParent(transform, false);
                debrisVisuals[i] = root.AddComponent<SpriteRenderer>();
                debrisVisuals[i].sprite = fallingDebrisSprite;
                debrisVisuals[i].sortingOrder = 6100;
                debrisVisuals[i].enabled = false;
            }
    }

    public void Initialize(SurvivorHealth target, ZombieSpawner source)
    {
        ResetBehaviour(target, source, false);
        Health.OnSpawnedFromPool(target.transform); Health.ForceAggro();
    }

    public void InitializeAmbient(SurvivorHealth target, ZombieSpawner source)
    {
        ResetBehaviour(target, source, true);
    }

    private void ResetBehaviour(SurvivorHealth target, ZombieSpawner source, bool ambient)
    {
        player = target; spawner = source; nav = FindFirstObjectByType<ZombieGridPathfinder>();
        IsAmbientElite = ambient;
        IsEnraged = false;
        State = ambient ? ActionState.Pursuit : ActionState.Arrival;
        elapsed = traveled = 0f;
        chargeHit = barrageHit = false;
        actionNumber = 0;
        recoveryAnimation = null;
        lockedDirection = warningOrigin = Vector2.zero;
        nextAction = Time.time + (isBoss ? 1.2f : 1.8f);
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
        DrawCircle(halo, transform.position, isBoss ? .48f : .44f, new Color(1f, isBoss ? .22f : .7f, .12f, .7f));
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
        if (isBoss && !IsEnraged && Health.CurrentHealth * 2 <= Health.CurrentMaxHealth &&
            (State == ActionState.Pursuit || State == ActionState.Recovery))
        {
            IsEnraged = true; BeginRoar();
            nextSummon = Time.time + summonInterval;
        }
        if (State == ActionState.Roar)
        {
            Health.HoldForSpecial(true);
            if (elapsed >= roarDuration) { Summon(); Recover("Roar"); }
            return true;
        }
        if (State == ActionState.Recovery)
        {
            Health.HoldForSpecial(false);
            if (elapsed >= RecoveryDuration) { HideWarning(); SetState(ActionState.Pursuit); }
            return true;
        }
        if (State == ActionState.SlamImpact)
        {
            Health.HoldForSpecial(true);
            DrawCircle(pulse, warningOrigin, slamRadius * Mathf.Clamp01(elapsed / .28f),
                new Color(1f, .55f, .1f, 1f - Mathf.Clamp01(elapsed / .32f)));
            pulse.enabled = true;
            if (elapsed >= .32f) Recover("Slam");
            return true;
        }
        if (State == ActionState.BarrageWarning)
        {
            Health.HoldForSpecial(true);
            DrawDebrisWarnings();
            if (elapsed >= WarningTime)
            {
                SetState(ActionState.BarrageImpact);
                barrageHit = false;
                for (int i = 0; i < debrisLanded.Length; i++) debrisLanded[i] = false;
            }
            return true;
        }
        if (State == ActionState.BarrageImpact)
        {
            Health.HoldForSpecial(true);
            TickDebris();
            if (elapsed >= .9f) Recover("Throw");
            return true;
        }
        if (State == ActionState.SlamWarning || State == ActionState.ChargeWarning)
        {
            Health.HoldForSpecial(true);
            // Lock direction halfway through the tell. Nothing follows the player after lock.
            if (elapsed < WarningTime * .5f && State == ActionState.ChargeWarning) Aim();
            DrawWarning();
            if (elapsed < WarningTime) return true;
            if (State == ActionState.SlamWarning)
            {
                HitSlam();
                if (isBoss) { HideWarning(); SetState(ActionState.SlamImpact); }
                else Recover(null);
            }
            else { chargeHit = false; traveled = 0; SetState(ActionState.Charging); HideWarning(); }
            return true;
        }
        if (State == ActionState.Charging) { Health.HoldForSpecial(true); return true; }
        if (isBoss && IsEnraged && Time.time >= nextSummon)
        {
            nextSummon = Time.time + summonInterval; BeginRoar(); return true;
        }
        if (Time.time < nextAction) return false;
        float distance = Vector2.Distance(transform.position, player.transform.position);
        if (isBoss && CanSeePlayer(transform.position) &&
            (actionNumber % 3 == 2 || distance > 6f))
        {
            BeginBarrage(); actionNumber++; return true;
        }
        if (distance <= slamRadius + .25f && (isBoss || actionNumber % 2 == 1))
        {
            warningOrigin = transform.position; Aim(); SetState(ActionState.SlamWarning);
            actionNumber++; DrawWarning(); Health.HoldForSpecial(true); return true;
        }
        if (distance <= 6.5f && CanSeePlayer(transform.position))
        {
            warningOrigin = transform.position; Aim(); SetState(ActionState.ChargeWarning);
            actionNumber++; DrawWarning(); Health.HoldForSpecial(true); return true;
        }
        return false;
    }

    private void FixedUpdate()
    {
        var run = RunState.Instance;
        if (!IsCharging || player == null || player.IsDead || Time.timeScale <= 0 ||
            (run != null && run.Phase != RunState.RunPhase.Playing)) return;
        float distance = Mathf.Min(chargeSpeed * (IsEnraged ? 1.15f : 1f) * Time.fixedDeltaTime, chargeDistance - traveled);
        int count = body.Cast(lockedDirection, Filter(), castHits, distance + .03f);
        for (int i = 0; i < count; i++)
        {
            var c = castHits[i].collider;
            if (c == null || c.GetComponentInParent<SurvivorHealth>() != null ||
                c.GetComponentInParent<ZombieChaser>() != null) continue;
            distance = Mathf.Min(distance, Mathf.Max(0, castHits[i].distance - .03f));
        }
        Vector2 next = body.position + lockedDirection * distance;
        if (nav != null && !nav.IsWalkablePosition(next)) distance = 0;
        for (int i = 0; i < count && !chargeHit; i++)
        {
            var hit = castHits[i];
            if (hit.collider != null && hit.collider.GetComponentInParent<SurvivorHealth>() == player &&
                hit.distance <= distance + .001f)
            {
                player.TakeDamage(chargeDamage, lockedDirection, 3.5f);
                chargeHit = true;
                body.linearVelocity = Vector2.zero;
                Recover(isBoss ? "Charge" : null);
                Health.HoldForSpecial(false);
                return;
            }
        }
        body.MovePosition(body.position + lockedDirection * distance);
        traveled += distance;
        if (distance < .01f || traveled >= chargeDistance - .01f || elapsed > 1.2f)
            Recover(isBoss ? "Charge" : null);
    }

    private Vector2 PlayerCenter => player.GetComponent<Collider2D>().bounds.center;
    private void HitSlam()
    {
        Vector2 center = PlayerCenter;
        if (Vector2.Distance(warningOrigin, center) > slamRadius || !CanSeePlayer(warningOrigin)) return;
        player.TakeDamage(slamDamage + (IsEnraged ? 4 : 0), center - warningOrigin, 3f);
    }
    private bool CanSeePlayer(Vector2 origin)
    {
        int count = Physics2D.Linecast(origin, PlayerCenter, Filter(), sightHits);
        for (int i = 0; i < count; i++)
        {
            var c = sightHits[i].collider;
            if (c == null || c.GetComponentInParent<ZombieChaser>() != null ||
                c.GetComponentInParent<SurvivorHealth>() != null) continue;
            if (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) return false;
        }
        return true;
    }
    private static ContactFilter2D Filter()
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers); filter.useTriggers = false; return filter;
    }
    private void Aim()
    {
        lockedDirection = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        if (lockedDirection.sqrMagnitude < .01f) lockedDirection = Vector2.down;
        Health.FaceDirection(lockedDirection);
    }
    private void BeginRoar()
    {
        HideWarning(); body.linearVelocity = Vector2.zero; Aim();
        SetState(ActionState.Roar); Health.HoldForSpecial(true);
    }
    private void Summon()
    {
        if (spawner != null)
        {
            int count = summonCountMin > 0
                ? Random.Range(Mathf.Min(summonCountMin, summonCount), Mathf.Max(summonCountMin, summonCount) + 1)
                : summonCount;
            spawner.SpawnEncounterAdds(Mathf.Min(count, Mathf.Max(0, maxSummonedAlive - spawner.ActiveCount)));
        }
    }
    private void BeginBarrage()
    {
        HideWarning(); Aim(); SetState(ActionState.BarrageWarning); Health.HoldForSpecial(true);
        Vector2 center = PlayerCenter;
        Vector2 side = new Vector2(-lockedDirection.y, lockedDirection.x);
        for (int i = 0; i < debrisTargets.Length; i++)
            debrisTargets[i] = center + side * (i - 1) * 1.1f;
        DrawDebrisWarnings();
    }
    private void DrawDebrisWarnings()
    {
        float progress = WarningProgress;
        for (int i = 0; i < debrisWarnings.Length; i++)
        {
            debrisWarnings[i].enabled = true;
            DrawCircle(debrisWarnings[i], debrisTargets[i], barrageRadius,
                Color.Lerp(new Color(1f, .6f, .1f, .7f), new Color(1f, .12f, .08f, 1f), progress));
        }
    }
    private void TickDebris()
    {
        for (int i = 0; i < debrisTargets.Length; i++)
        {
            float local = elapsed - i * .22f;
            if (local < 0f) continue;
            var visual = debrisVisuals[i];
            visual.enabled = local < .27f && visual.sprite != null;
            visual.transform.position = debrisTargets[i] + Vector2.up * Mathf.Lerp(2.2f, 0f, Mathf.Clamp01(local / .22f));
            if (local < .22f || debrisLanded[i]) continue;
            debrisLanded[i] = true;
            // At most one damage application per volley, even where red circles overlap.
            if (!barrageHit && Vector2.Distance(PlayerCenter, debrisTargets[i]) <= barrageRadius &&
                CanSeePlayer(debrisTargets[i]))
            {
                barrageHit = true;
                player.TakeDamage(barrageDamage, PlayerCenter - debrisTargets[i], 2.2f);
            }
            debrisWarnings[i].enabled = false;
        }
    }

    // Generated 8-frame skill clips: first four frames tell, last four impact/recovery.
    public bool TryGetAnimation(out string motion, out float phase)
    {
        motion = null; phase = 0f;
        switch (State)
        {
            case ActionState.SlamWarning: motion = "Slam"; phase = WarningProgress * .499f; break;
            case ActionState.SlamImpact: motion = "Slam"; phase = .5f + Mathf.Clamp01(elapsed / .32f) * .49f; break;
            case ActionState.ChargeWarning: motion = "Charge"; phase = 0; break;
            case ActionState.Charging: motion = "Charge"; phase = Mathf.Repeat(elapsed * 14f / 8f, 1f); break;
            case ActionState.BarrageWarning: motion = "Throw"; phase = WarningProgress * .499f; break;
            case ActionState.BarrageImpact: motion = "Throw"; phase = .5f + Mathf.Clamp01(elapsed / .65f) * .49f; break;
            case ActionState.Roar: motion = "Roar"; phase = Mathf.Clamp01(elapsed / roarDuration) * .999f; break;
            case ActionState.Recovery:
                motion = recoveryAnimation;
                phase = motion == "Charge" ? 0f : .75f + Mathf.Clamp01(elapsed / RecoveryDuration) * .249f;
                break;
        }
        return isBoss && motion != null;
    }

    private void SetState(ActionState state) { State = state; elapsed = 0; }
    private void Recover(string animation)
    {
        recoveryAnimation = animation;
        SetState(ActionState.Recovery); nextAction = Time.time + Cooldown;
        HideWarning(); body.linearVelocity = Vector2.zero;
    }
    private void OnDied(ZombieChaser unused)
    { State = ActionState.Dead; HideWarning(); halo.enabled = false; body.linearVelocity = Vector2.zero; }
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
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2 / 32;
            line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
    private void DrawWarning()
    {
        warning.enabled = pulse.enabled = true;
        Color color = Color.Lerp(new Color(1f, .62f, .12f, .8f), new Color(1f, .16f, .08f, 1f), WarningProgress);
        if (State == ActionState.SlamWarning)
        {
            DrawCircle(warning, warningOrigin, slamRadius, color);
            DrawCircle(pulse, warningOrigin, Mathf.Max(.08f, slamRadius * WarningProgress), color);
        }
        else
        {
            Vector2 side = new Vector2(-lockedDirection.y, lockedDirection.x) * (isBoss ? .3f : .4f);
            warning.loop = true; warning.positionCount = 4; warning.startColor = warning.endColor = color;
            warning.SetPosition(0, warningOrigin + side);
            warning.SetPosition(1, warningOrigin + lockedDirection * chargeDistance + side);
            warning.SetPosition(2, warningOrigin + lockedDirection * chargeDistance - side);
            warning.SetPosition(3, warningOrigin - side);
            pulse.loop = false; pulse.positionCount = 2; pulse.startColor = pulse.endColor = color;
            pulse.SetPosition(0, warningOrigin);
            pulse.SetPosition(1, warningOrigin + lockedDirection * chargeDistance * WarningProgress);
        }
    }
    private void HideWarning()
    {
        if (warning != null) warning.enabled = false;
        if (pulse != null) pulse.enabled = false;
        for (int i = 0; i < debrisWarnings.Length; i++)
        {
            if (debrisWarnings[i] != null) debrisWarnings[i].enabled = false;
            if (debrisVisuals[i] != null) debrisVisuals[i].enabled = false;
        }
    }
    private void OnDisable()
    {
        HideWarning();
        if (halo != null) halo.enabled = false;
        if (body != null) body.linearVelocity = Vector2.zero;
    }
    private void OnDestroy()
    { if (Health != null) Health.Died -= OnDied; if (material != null) Destroy(material); }
}
