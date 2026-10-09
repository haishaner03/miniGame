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
    public int tankAttackDamageBonus = 1;
    public float exploderSpeedMultiplier = 1.1f;
    public float explosionRadius = 1.45f;
    public int explosionDamage = 2;
    public float explosionKnockback = 3.5f;
    public bool explodeOnContact = true;
public Transform target;
    public float moveSpeed = 1.4f;
    public float attackDistance = 0.75f;
    public float attackCooldown = 1.1f;
    public int attackDamage = 1;
    public float animationFps = 10f;
    public int maxHealth = 8;
    public float knockbackDamping = 12f;
    public bool useGridPathfinding = true;
    public float pathRefreshInterval = 0.35f;
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
        private float runtimeMoveSpeed;
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
    private bool waitingAtWanderTarget;

        public int CurrentMaxHealth => runtimeMaxHealth;
    public float EffectiveMoveSpeed => runtimeMoveSpeed;
    public int EffectiveAttackDamage => runtimeAttackDamage;
public int CurrentHealth => currentHealth;


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
        if (state == State.Death)
        {
            deathElapsed += Time.deltaTime;
            UpdateAnimation();
            float deathDuration = deathFrames != null && deathFrames.Length > 0
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

        float distance = Vector2.Distance(target.position, transform.position);

        if (!hasAggro && distance <= Mathf.Max(0.1f, aggroRadius))
            hasAggro = true;
        else if (hasAggro && distance >= Mathf.Max(aggroRadius, loseTargetRadius))
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

        if (state == State.Attack)
        {
            desiredVelocity = Vector2.zero;
            attackElapsed += Time.deltaTime;
            UpdateAnimation();
            float attackDuration = (attackFrames != null && attackFrames.Length > 0 ? attackFrames.Length : 1) / Mathf.Max(1f, animationFps);
            float attackProgress = Mathf.Clamp01(attackElapsed / Mathf.Max(0.01f, attackDuration));
            float pulse = Mathf.Sin(attackProgress * Mathf.PI);
            transform.localScale = baseScale * (1f + attackScalePulse * pulse);
            if (spriteRenderer != null)
                spriteRenderer.color = Color.Lerp(baseColor, attackTint, pulse);
            if (attackElapsed >= attackDuration)
            {
                state = State.Walk;
                transform.localScale = baseScale;
                if (spriteRenderer != null)
                    spriteRenderer.color = baseColor;
            }
            return;
        }

        if (distance <= attackDistance)
        {
            desiredVelocity = Vector2.zero;
            if (Time.time >= nextAttackTime)
                BeginAttack();
            UpdateAnimation();
            return;
        }

        desiredVelocity = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized * runtimeMoveSpeed : Vector2.zero;
        if (Mathf.Abs(toTarget.x) > 0.05f)
            spriteRenderer.flipX = toTarget.x < 0f;
        state = State.Walk;
        UpdateAnimation();
    }

    private bool distanceNeedsPathRefresh()
    {
        return Time.time >= nextPathRefreshTime || pathIndex >= path.Count;
    }

    private void FixedUpdate()
    {
        if (state == State.Death)
            return;

        Vector2 velocity = knockbackVelocity;
        if (state == State.Walk || state == State.Idle)
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
        frameTimer = 0f;
        frameIndex = 0;
        SendDamageMessage();
        SetFrame(0);
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
        waitingAtWanderTarget = false;
        ChooseWanderTarget(true);
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
        target = newTarget;
        ApplyArchetypeStats();
        currentHealth = runtimeMaxHealth;
        desiredVelocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        nextAttackTime = 0f;
        frameTimer = 0f;
        attackElapsed = 0f;
        deathElapsed = 0f;
        frameIndex = 0;
        pathIndex = 0;
        nextPathRefreshTime = Time.time;
        path.Clear();
        homePosition = transform.position;
        hasAggro = false;
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
        SetFrame(0);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || state == State.Death)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        FloatingCombatText.Spawn(damageTextPrefab, transform.position + Vector3.up * 0.45f, "-" + damage.ToString(), damageTextColor);
        StartHitFlash();
        if (currentHealth == 0)
            Die();
    }

    public void TakeDamage(int damage, Vector2 hitDirection, float knockbackForce)
    {
        if (state == State.Death)
            return;

        Vector2 direction = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector2.down;
        knockbackVelocity = Vector2.ClampMagnitude(knockbackVelocity + direction * Mathf.Max(0f, knockbackForce), 5f);
        TakeDamage(damage);
    }

    public void Die()
    {
        if (state == State.Death)
            return;
        currentHealth = 0;
        if (RunState.Instance != null) RunState.Instance.RecordKill();
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
        runtimeMaxHealth = Mathf.Max(1, Mathf.CeilToInt(runtimeMaxHealth * healthMultiplier));
        currentHealth = runtimeMaxHealth;
        if (spriteRenderer != null)
        {
            baseColor = archetype == ZombieArchetype.Fast ? new Color(0.72f, 1f, 0.72f) :
                archetype == ZombieArchetype.Tank ? new Color(0.7f, 0.78f, 0.9f) :
                archetype == ZombieArchetype.Exploder ? new Color(1f, 0.62f, 0.62f) : Color.white;
            spriteRenderer.color = baseColor;
        }
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
            if (spriteRenderer != null && Mathf.Abs(desiredVelocity.x) > 0.05f)
                spriteRenderer.flipX = desiredVelocity.x < 0f;
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
