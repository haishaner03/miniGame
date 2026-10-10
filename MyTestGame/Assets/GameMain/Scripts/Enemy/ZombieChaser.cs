using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ZombieChaser : MonoBehaviour
{
        public enum ZombieArchetype
    {
        Standard,
        Fast,
        Tank,
        Exploder
    }

    [Header("敌人类型")]
    public ZombieArchetype archetype = ZombieArchetype.Standard;
    public float hitFlashDuration = 0.08f;
    public Color hitFlashColor = Color.white;
    public float fastSpeedMultiplier = 1.75f;
    public float tankSpeedMultiplier = 0.62f;
    public int tankHealthMultiplier = 3;
    public int tankAttackDamageBonus = 20;
    public float exploderSpeedMultiplier = 1.1f;
    public float explosionRadius = 1.45f;
    public int explosionDamage = 40;
    public float explosionKnockback = 3.5f;
    public bool explodeOnContact = true;
public Transform target;
    public float moveSpeed = 1.4f;
    public float attackDistance = 0.75f;
    public float attackCooldown = 1.1f;
    public int attackDamage = 20;
    public float animationFps = 10f;
    public int maxHealth = 200;
    public float knockbackDamping = 12f;
    public bool useGridPathfinding = true;
    public float pathRefreshInterval = 0.35f;
    [Range(0.1f, 1f)] public float freezeDurationMultiplier = 1f;
    [Range(0f, 0.3f)] public float freezeKnockbackDuration = 0.12f;
    [Range(0f, 1f)] public float receivedKnockbackMultiplier = 1f;
    public ZombieChampion Champion { get; private set; }
    public SpriteRenderer spriteRenderer;
    public Sprite[] walkFrames;
    public Sprite[] attackFrames;
    public Sprite[] deathFrames;
    public bool destroyAfterDeath = true;
    public float deathDestroyDelay = 0.15f;
    public float attackScalePulse = 0.12f;
    public Color attackTint = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("感知与游荡")]
    public float aggroRadius = 5.5f;
    public float loseTargetRadius = 8.5f;
    public bool idleWander = true;
    public float idleWanderRadius = 1.8f;
    public float idlePauseMin = 0.7f;
    public float idlePauseMax = 2.0f;
    public float idleSpeedMultiplier = 0.45f;

    [Header("战斗飘字")]
    public GameObject damageTextPrefab;
    public Color damageTextColor = new Color(1f, 0.82f, 0.25f, 1f);


    private Rigidbody2D body;
    private Collider2D hitCollider;
    private Vector2 desiredVelocity;
    private float nextAttackTime;
    private float frameTimer;
    private float attackElapsed;
    private float deathElapsed;
    private int frameIndex;
    private State state;
    private Vector3 baseScale;
    private Color baseColor;
    private int currentHealth;
    private float frozenUntil, burningUntil, nextBurnTick;
    private float freezeKnockbackUntil;
    private bool freezePositionLocked;
    private RigidbodyConstraints2D constraintsBeforeFreeze;
    private int burnDamage;
    private bool lastDamageByPlayer;
    public bool IsFrozen => currentHealth > 0 && Time.time < frozenUntil;
    public bool HasAggro => hasAggro;
    public bool IsBurning => currentHealth > 0 && Time.time < burningUntil;

    public void ApplyFreeze(float duration)
    {
        if (state == State.Death || duration <= 0f) return;
        if (!IsFrozen)
        {
            ReleaseFrozenPosition();
            // PrepareHit applies freeze before TakeDamage adds this hit's knockback.
            // Only a new freeze allows recoil; extending it must not reopen movement.
            freezeKnockbackUntil = Time.time + freezeKnockbackDuration;
        }
        frozenUntil = Mathf.Max(frozenUntil, Time.time + duration * freezeDurationMultiplier);
        desiredVelocity = Vector2.zero;
    }

    private void LockFrozenPosition()
    {
        if (!freezePositionLocked)
        {
            constraintsBeforeFreeze = body.constraints;
            body.constraints |= RigidbodyConstraints2D.FreezePosition;
            freezePositionLocked = true;
        }
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        knockbackVelocity = Vector2.zero;
        desiredVelocity = Vector2.zero;
    }

    private void ReleaseFrozenPosition()
    {
        if (!freezePositionLocked) return;
        if (body != null)
        {
            body.constraints = constraintsBeforeFreeze;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        freezePositionLocked = false;
    }

    private void OnDisable()
    {
        ReleaseFrozenPosition();
    }

    public void ApplyBurn(float duration, int damagePerSecond)
    {
        if (state == State.Death) return;
        if (!IsBurning) nextBurnTick = Time.time + 1f;
        burningUntil = Mathf.Max(burningUntil, Time.time + duration);
        burnDamage = Mathf.Max(burnDamage, damagePerSecond);
    }

    public void ForceAggro() { hasAggro = true; persistentAggro = true; }
        private float runtimeMoveSpeed;
    private float spawnSpeedMultiplier = 1f;
    private int runtimeMaxHealth;
    private int runtimeAttackDamage;
    private Coroutine hitFlashRoutine;
private Vector2 knockbackVelocity;
    private ZombieGridPathfinder pathfinder;
    private readonly List<Vector2> path = new List<Vector2>(32);
    private int pathIndex;
    private float nextPathRefreshTime;
    private Action<ZombieChaser> releaseToPool;
    private Vector2 homePosition;
    private Vector2 wanderTarget;
    private float nextWanderDecision;
    private bool hasAggro;
    private bool persistentAggro;
    private bool waitingAtWanderTarget;
    private ZombieDirectionalAnimator directionalAnimator;
    private bool attackDamageSent;
    private Vector2 attackDirection;
    private readonly RaycastHit2D[] attackSightHits = new RaycastHit2D[32];

        public int CurrentMaxHealth => runtimeMaxHealth;
    public float EffectiveMoveSpeed => runtimeMoveSpeed;
    public float SpawnSpeedMultiplier => spawnSpeedMultiplier;
    public int EffectiveAttackDamage => runtimeAttackDamage;
public int CurrentHealth => currentHealth;
    public event Action<ZombieChaser> Died;


    private enum State
    {
        Idle,
        Walk,
        Attack,
        Death
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        directionalAnimator = GetComponent<ZombieDirectionalAnimator>();
        Champion = GetComponent<ZombieChampion>();
        hitCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        baseScale = transform.localScale;
        baseColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        ApplyArchetypeStats();
        currentHealth = runtimeMaxHealth;
        state = State.Walk;
        homePosition = transform.position;
        ChooseWanderTarget(true);
        SetFrame(0);
    }

    private void Start()
    {
        ResolveTarget();
        pathfinder = FindFirstObjectByType<ZombieGridPathfinder>();
    }

private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (directionalAnimator != null)
            directionalAnimator.SetFrozen(IsFrozen);
        if (state != State.Death && burnDamage > 0 && nextBurnTick <= burningUntil && Time.time >= nextBurnTick)
        {
            nextBurnTick += 1f;
            ApplyDamage(burnDamage, true);
        }
        if (state == State.Death)
        {
            deathElapsed += Time.deltaTime;
            UpdateAnimation();
            float deathDuration = HasDirectionalAnimation ? directionalAnimator.DeathDuration : deathFrames != null && deathFrames.Length > 0
                ? Mathf.Max(deathFrames.Length / Mathf.Max(1f, animationFps), deathDestroyDelay)
                : deathDestroyDelay;
            if (deathElapsed >= deathDuration)
            {
                if (releaseToPool != null)
                    releaseToPool(this);
                else if (destroyAfterDeath)
                    Destroy(gameObject);
            }
            return;
        }

        ResolveTarget();
        if (target == null)
            return;
        if (Champion != null && Champion.enabled && Champion.TickBehaviour(Time.deltaTime))
        {
            UpdateAnimation();
            return;
        }
        if (IsFrozen)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        if (state == State.Attack)
        {
            UpdateAttack();
            return;
        }

        float distance = Vector2.Distance(target.position, transform.position);

        if (!hasAggro && distance <= Mathf.Max(0.1f, aggroRadius))
            hasAggro = true;
        else if (hasAggro && !persistentAggro && distance >= Mathf.Max(aggroRadius, loseTargetRadius))
        {
            hasAggro = false;
            path.Clear();
            pathIndex = 0;
            ChooseWanderTarget(true);
        }

        if (!hasAggro)
        {
            UpdateIdleWander();
            return;
        }

        Vector2 desiredTargetPosition = target.position;
        if (useGridPathfinding && pathfinder != null && distanceNeedsPathRefresh())
        {
            if (pathfinder.TryFindPath(transform.position, target.position, path))
                pathIndex = 0;
            nextPathRefreshTime = Time.time + Mathf.Max(0.1f, pathRefreshInterval);
        }

        if (useGridPathfinding && pathfinder != null && pathIndex < path.Count)
        {
            while (pathIndex < path.Count && Vector2.Distance(transform.position, path[pathIndex]) < 0.2f)
                pathIndex++;
            if (pathIndex < path.Count)
                desiredTargetPosition = path[pathIndex];
        }

        Vector2 toTarget = desiredTargetPosition - (Vector2)transform.position;

        if (distance <= attackDistance)
        {
            desiredVelocity = Vector2.zero;
            if (Champion == null && Time.time >= nextAttackTime)
                BeginAttack();
            UpdateAnimation();
            return;
        }

        desiredVelocity = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized * runtimeMoveSpeed : Vector2.zero;
        FaceDirection(toTarget);
        state = State.Walk;
        UpdateAnimation();
    }

    private bool distanceNeedsPathRefresh()
    {
        return Time.time >= nextPathRefreshTime;
    }

    private void FixedUpdate()
    {
        if (state == State.Death)
            return;
        if (IsFrozen)
        {
            if (freezePositionLocked || Time.time >= freezeKnockbackUntil || knockbackVelocity.sqrMagnitude <= 0.0001f)
            {
                LockFrozenPosition();
                return;
            }
        }
        else
            ReleaseFrozenPosition();
        if (Champion != null && Champion.IsCharging) return;

        Vector2 velocity = knockbackVelocity;
        if (!IsFrozen && (state == State.Walk || state == State.Idle))
            velocity += desiredVelocity;
        if (velocity.sqrMagnitude > 0.0001f)
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);

        knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, knockbackDamping * Time.fixedDeltaTime);
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            player = GameObject.Find("SurvivorPlayer");
        if (player != null)
            target = player.transform;
    }

    private void BeginAttack()
    {
        nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldown);
        if (archetype == ZombieArchetype.Exploder && explodeOnContact)
        {
            ExplodeAndDie();
            return;
        }

        state = State.Attack;
        attackElapsed = 0f;
        attackDamageSent = false;
        attackDirection = target != null ? ((Vector2)target.position - (Vector2)transform.position).normalized : Vector2.down;
        FaceDirection(attackDirection);
        frameTimer = 0f;
        frameIndex = 0;
        if (!HasDirectionalAnimation)
        {
            SendDamageMessage();
            attackDamageSent = true;
        }
        SetFrame(0);
    }

    private bool HasDirectionalAnimation => directionalAnimator != null && directionalAnimator.IsConfigured;

    public void FaceDirection(Vector2 direction)
    {
        if (HasDirectionalAnimation) directionalAnimator.Face(direction);
        else if (spriteRenderer != null && Mathf.Abs(direction.x) > .05f)
            spriteRenderer.flipX = direction.x < 0f;
    }

    private void UpdateAttack()
    {
        desiredVelocity = Vector2.zero;
        attackElapsed += Time.deltaTime;
        float duration = HasDirectionalAnimation ? directionalAnimator.AttackDuration :
            (attackFrames != null && attackFrames.Length > 0 ? attackFrames.Length : 1) / Mathf.Max(1f, animationFps);
        if (HasDirectionalAnimation && !attackDamageSent && attackElapsed >= directionalAnimator.AttackHitTime)
        {
            attackDamageSent = true;
            // Resolve once on the claw impact frame. Dodging the tell makes this attack miss.
            if (CanHitTarget()) SendDamageMessage();
        }
        UpdateAnimation();
        if (!HasDirectionalAnimation)
        {
            float pulse = Mathf.Sin(Mathf.Clamp01(attackElapsed / Mathf.Max(.01f, duration)) * Mathf.PI);
            transform.localScale = baseScale * (1f + attackScalePulse * pulse);
            if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(baseColor, attackTint, pulse);
        }
        if (attackElapsed >= duration)
        {
            state = State.Walk;
            transform.localScale = baseScale;
            if (spriteRenderer != null && hitFlashRoutine == null) spriteRenderer.color = baseColor;
            UpdateAnimation();
        }
    }

    private bool CanHitTarget()
    {
        if (target == null) return false;
        Vector2 delta = (Vector2)target.position - (Vector2)transform.position;
        if (delta.sqrMagnitude > (attackDistance + .08f) * (attackDistance + .08f)) return false;
        if (delta.sqrMagnitude > .01f && Vector2.Dot(delta.normalized, attackDirection) < .25f) return false;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = false;
        int count = Physics2D.Linecast(transform.position, target.position, filter, attackSightHits);
        // A full buffer is ambiguous: do not let a crowded attack hit through a wall.
        if (count >= attackSightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var collider = attackSightHits[i].collider;
            if (collider == null || collider.GetComponentInParent<ZombieChaser>() != null ||
                collider.GetComponentInParent<SurvivorHealth>() != null || collider.transform == target) continue;
            if (collider.attachedRigidbody == null || collider.attachedRigidbody.bodyType == RigidbodyType2D.Static)
                return false;
        }
        return true;
    }

    private void SendDamageMessage()
    {
        if (target == null)
            return;

        SurvivorHealth player = target.GetComponentInParent<SurvivorHealth>();
        if (player != null)
        {
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            player.TakeDamage(runtimeAttackDamage, direction, 1.8f);
            return;
        }

        target.gameObject.SendMessage("TakeDamage", runtimeAttackDamage, SendMessageOptions.DontRequireReceiver);
    }

    private void UpdateAnimation()
    {
        if (HasDirectionalAnimation)
        {
            var motion = state == State.Death ? ZombieDirectionalAnimator.Motion.Death :
                state == State.Attack ? ZombieDirectionalAnimator.Motion.Attack :
                desiredVelocity.sqrMagnitude > .001f ? ZombieDirectionalAnimator.Motion.Walk :
                ZombieDirectionalAnimator.Motion.Idle;
            directionalAnimator.Play(motion);
            return;
        }
        Sprite[] frames = state == State.Attack ? attackFrames : state == State.Death ? deathFrames : walkFrames;
        if (frames == null || frames.Length == 0 || spriteRenderer == null)
            return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, animationFps);
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
        }
        SetFrame(frameIndex);
    }

    private void SetFrame(int index)
    {
        if (HasDirectionalAnimation) { UpdateAnimation(); return; }
        if (spriteRenderer == null)
            return;
        Sprite[] frames = state == State.Attack ? attackFrames : state == State.Death ? deathFrames : walkFrames;
        if (frames == null || frames.Length == 0)
            frames = walkFrames;
        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasAggro = false;
        persistentAggro = false;
        waitingAtWanderTarget = false;
        ChooseWanderTarget(true);
    }

    public void HoldForSpecial(bool attacking)
    {
        desiredVelocity = Vector2.zero;
        var next = attacking ? State.Attack : State.Idle;
        if (state != next)
        {
            frameIndex = 0; frameTimer = 0f;
            if (attacking && target != null) FaceDirection((Vector2)target.position - (Vector2)transform.position);
        }
        state = next;
    }

    /// <summary>
    /// 设置对象池回收回调。设置后死亡动画结束时会回收到池，而不是销毁 GameObject。
    /// </summary>
    public void SetPoolReleaseCallback(Action<ZombieChaser> callback)
    {
        releaseToPool = callback;
    }

    /// <summary>
    /// 从对象池重新取出时重置丧尸运行状态。
    /// </summary>
    public void OnSpawnedFromPool(Transform newTarget)
    {
        ReleaseFrozenPosition();
        freezeKnockbackUntil = 0f;
        lastDamageByPlayer = false;
        frozenUntil = burningUntil = nextBurnTick = 0f;
        burnDamage = 0;
        if (hitFlashRoutine != null) { StopCoroutine(hitFlashRoutine); hitFlashRoutine = null; }
        pathfinder = FindFirstObjectByType<ZombieGridPathfinder>();
        target = newTarget;
        spawnSpeedMultiplier = 1f;
        ApplyArchetypeStats();
        currentHealth = runtimeMaxHealth;
        desiredVelocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        nextAttackTime = 0f;
        frameTimer = 0f;
        attackElapsed = 0f;
        attackDamageSent = false;
        deathElapsed = 0f;
        frameIndex = 0;
        pathIndex = 0;
        nextPathRefreshTime = Time.time;
        path.Clear();
        homePosition = transform.position;
        hasAggro = false;
        persistentAggro = false;
        ChooseWanderTarget(true);
        state = State.Walk;
        transform.localScale = baseScale;

        if (hitCollider != null)
            hitCollider.enabled = true;
        if (body != null)
        {
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = baseColor;
            spriteRenderer.flipX = false;
        }
        if (directionalAnimator != null) directionalAnimator.ResetVisuals();
        SetFrame(0);
    }

    public void TakeDamage(int damage)
    {
        ApplyDamage(damage, false);
    }

    private void ApplyDamage(int damage, bool causedByPlayer)
    {
        if (damage <= 0 || state == State.Death)
            return;

        int actualDamage = Mathf.Min(currentHealth, damage);
        lastDamageByPlayer = causedByPlayer;
        currentHealth -= actualDamage;
        if (causedByPlayer && RunState.Instance != null)
            RunState.Instance.RecordDamageDealt(actualDamage);
        FloatingCombatText.Spawn(damageTextPrefab, transform.position + Vector3.up * 0.45f, "-" + damage.ToString(), damageTextColor);
        StartHitFlash();
        if (currentHealth == 0)
            Die();
    }

    public void TakeDamage(int damage, Vector2 hitDirection, float knockbackForce, bool causedByPlayer = false)
    {
        if (state == State.Death)
            return;

        Vector2 direction = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector2.down;
        knockbackForce *= receivedKnockbackMultiplier;
        if (!freezePositionLocked)
            knockbackVelocity = Vector2.ClampMagnitude(knockbackVelocity + direction * Mathf.Max(0f, knockbackForce), Mathf.Max(5f, knockbackForce));
        ApplyDamage(damage, causedByPlayer);
    }

    public void Die()
    {
        if (state == State.Death)
            return;
        bool frozenOnDeath = Time.time < frozenUntil;
        bool burningOnDeath = Time.time < burningUntil;
        Vector2 burstOrigin = hitCollider != null ? (Vector2)hitCollider.bounds.center : (Vector2)transform.position;
        currentHealth = 0;
        // Notify before returning this instance to its pool or clearing status effects.
        state = State.Death;
        Died?.Invoke(this);
        if (RunState.Instance != null)
        {
            bool elite = Champion != null || archetype != ZombieArchetype.Standard;
            RunState.Instance.RecordKill(elite);
            RunState.Instance.DropExperience(transform.position, elite);
            RunState.Instance.CombatEffects.EnemyKilled(burstOrigin, frozenOnDeath, burningOnDeath, lastDamageByPlayer);
        }
        frozenUntil = burningUntil = 0f;
        ReleaseFrozenPosition();
        freezeKnockbackUntil = 0f;
        burnDamage = 0;
        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }
        state = State.Death;
        desiredVelocity = Vector2.zero;
        frameTimer = 0f;
        deathElapsed = 0f;
        frameIndex = 0;
        transform.localScale = baseScale;
        if (spriteRenderer != null)
            spriteRenderer.color = baseColor;
        if (hitCollider != null)
            hitCollider.enabled = false;
        if (body != null)
            body.simulated = false;
        SetFrame(0);
    }

    public void ApplyRunDifficulty(float healthMultiplier)
    {
        // Round integral HP so float error cannot turn 200 * 1.2 into 241 HP.
        runtimeMaxHealth = Mathf.Max(1, Mathf.RoundToInt(runtimeMaxHealth * healthMultiplier));
        currentHealth = runtimeMaxHealth;
        if (spriteRenderer != null)
        {
            baseColor = archetype == ZombieArchetype.Fast ? new Color(0.72f, 1f, 0.72f) :
                archetype == ZombieArchetype.Tank ? new Color(0.7f, 0.78f, 0.9f) :
                archetype == ZombieArchetype.Exploder ? new Color(1f, 0.62f, 0.62f) : Color.white;
            if (HasDirectionalAnimation)
                baseColor = archetype == ZombieArchetype.Fast ? new Color(.9f, 1f, .9f) :
                    archetype == ZombieArchetype.Tank ? new Color(.84f, .89f, .96f) :
                    archetype == ZombieArchetype.Exploder ? new Color(1f, .84f, .78f) : Color.white;
            spriteRenderer.color = baseColor;
        }
    }

    public void SetSpawnMoveSpeedMultiplier(float multiplier)
    {
        float next = Mathf.Max(.1f, multiplier);
        runtimeMoveSpeed = runtimeMoveSpeed / spawnSpeedMultiplier * next;
        spawnSpeedMultiplier = next;
    }

    public void SetSpawnHealth(int maximum)
    {
        runtimeMaxHealth = Mathf.Max(1, maximum);
        currentHealth = runtimeMaxHealth;
    }

    private void UpdateIdleWander()
    {
        if (!idleWander)
        {
            desiredVelocity = Vector2.zero;
            state = State.Idle;
            UpdateAnimation();
            return;
        }

        if (Time.time >= nextWanderDecision)
        {
            if (waitingAtWanderTarget)
            {
                waitingAtWanderTarget = false;
                ChooseWanderTarget(false);
            }
            else if (Vector2.Distance(transform.position, wanderTarget) <= 0.18f)
            {
                desiredVelocity = Vector2.zero;
                state = State.Idle;
                waitingAtWanderTarget = true;
                nextWanderDecision = Time.time + UnityEngine.Random.Range(Mathf.Max(0.05f, idlePauseMin), Mathf.Max(idlePauseMin, idlePauseMax));
            }
            else
            {
                desiredVelocity = (wanderTarget - (Vector2)transform.position).normalized * runtimeMoveSpeed * Mathf.Clamp01(idleSpeedMultiplier);
                state = State.Idle;
            }
        }

        if (state == State.Idle && desiredVelocity.sqrMagnitude > 0.001f)
        {
            state = State.Idle;
            FaceDirection(desiredVelocity);
        }
        UpdateAnimation();
    }

        private void ApplyArchetypeStats()
    {
        runtimeMoveSpeed = Mathf.Max(0.1f, moveSpeed);
        runtimeMaxHealth = Mathf.Max(1, maxHealth);
        runtimeAttackDamage = Mathf.Max(1, attackDamage);

        switch (archetype)
        {
            case ZombieArchetype.Fast:
                runtimeMoveSpeed *= Mathf.Max(1f, fastSpeedMultiplier);
                break;
            case ZombieArchetype.Tank:
                runtimeMoveSpeed *= Mathf.Clamp(tankSpeedMultiplier, 0.2f, 1f);
                runtimeMaxHealth *= Mathf.Max(1, tankHealthMultiplier);
                runtimeAttackDamage += Mathf.Max(0, tankAttackDamageBonus);
                break;
            case ZombieArchetype.Exploder:
                runtimeMoveSpeed *= Mathf.Max(1f, exploderSpeedMultiplier);
                runtimeMaxHealth = Mathf.RoundToInt(runtimeMaxHealth * 1.25f);
                break;
        }
    }

    private void StartHitFlash()
    {
        if (spriteRenderer == null || hitFlashDuration <= 0f)
            return;
        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);
        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || state == State.Death || hitFlashRoutine != null) return;
        if (IsFrozen) spriteRenderer.color = new Color(0.35f, 0.82f, 1f);
        else if (IsBurning) spriteRenderer.color = Color.Lerp(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.8f, 0.2f), (Mathf.Sin(Time.time * 14f) + 1f) * 0.5f);
        else if (state != State.Attack || HasDirectionalAnimation) spriteRenderer.color = baseColor;
    }

    private System.Collections.IEnumerator HitFlashRoutine()
    {
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        if (state != State.Death && spriteRenderer != null)
            spriteRenderer.color = baseColor;
        hitFlashRoutine = null;
    }

    private void ExplodeAndDie()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.2f, explosionRadius));
        HashSet<int> affected = new HashSet<int>();
        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;
            Vector2 direction = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            SurvivorHealth player = hit.GetComponentInParent<SurvivorHealth>();
            if (player != null && affected.Add(player.gameObject.GetInstanceID()))
                player.TakeDamage(explosionDamage, direction, explosionKnockback);

            ZombieChaser other = hit.GetComponentInParent<ZombieChaser>();
            if (other != null && other != this && affected.Add(other.gameObject.GetInstanceID()))
                other.TakeDamage(explosionDamage, direction, explosionKnockback * 0.6f);
        }
        Die();
    }
private void ChooseWanderTarget(bool resetPause)
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.2f, idleWanderRadius);
        wanderTarget = homePosition + offset;
        if (resetPause)
            nextWanderDecision = Time.time + UnityEngine.Random.Range(0.1f, 0.55f);
    }
}
