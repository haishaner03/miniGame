using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 独立武器、四方向攻击帧和与命中帧同步的扇形伤害检测。
/// </summary>
[DisallowMultipleComponent]
public sealed class SurvivorMeleeAttack : MonoBehaviour
{
    [Header("可装备武器")]
    [SerializeField] private MeleeWeaponDefinition machete;
    [SerializeField] private MeleeWeaponDefinition ironBar;
    private MeleeWeaponDefinition equipped;
    private SurvivorWeaponVisual weaponVisual;
    private bool heavyWeapon;
    private readonly Collider2D[] hitBuffer = new Collider2D[128];
    private readonly RaycastHit2D[] obstacleBuffer = new RaycastHit2D[32];
    private readonly HashSet<int> hitTargets = new HashSet<int>();
    public MeleeWeaponDefinition EquippedWeapon => equipped;
    public float AttackDuration => attackDuration;
    public float ImpactTime => hitTime;
    public float AttackProgress => Mathf.Clamp01(attackElapsed / Mathf.Max(0.01f, attackDuration));
    public float WeaponKnockbackMultiplier => equipped != null ? equipped.knockbackMultiplier : 1f;
    public MeleeWeaponDefinition GetWeaponDefinition(bool heavy) => heavy ? ironBar : machete;

    public void EquipWeapon(bool heavy)
    {
        if (IsAttacking) EndAttack();
        heavyWeapon = heavy;
        equipped = GetWeaponDefinition(heavy);
        if (equipped != null)
        {
            damage = equipped.damage;
            cooldown = equipped.cooldown;
            attackRadius = equipped.reach;
            attackDuration = equipped.attackDuration;
            hitTime = attackDuration * equipped.impactProgress;
            slashColor = equipped.trailColor;
            slashRadius = attackRadius;
            if (weaponVisual == null) weaponVisual = GetComponent<SurvivorWeaponVisual>();
            if (weaponVisual != null) weaponVisual.Equip(equipped);
        }
    }
    [Header("攻击参数")]
    [SerializeField] private int damage = 80;
    [Tooltip("每次挥击在伤害基础上随机加减的点数；同一次挥击共用一个随机结果。")]
    [SerializeField, Min(0)] private int damageVariance = 5;
    [SerializeField] private float attackDuration = 0.16f;
    [SerializeField] private float hitTime = 0.055f;
    [SerializeField] private float cooldown = 0.28f;
    [SerializeField] private float attackOffset = 0.62f;
    [SerializeField] private float attackRadius = 0.72f;
    [SerializeField] private KeyCode attackKey = KeyCode.None;
    [SerializeField] private bool allowMouseLeftButton = true;

    [Header("挥砍表现")]
    [SerializeField] private Color slashColor = new Color(0.72f, 0.95f, 1f, 1f);
    [SerializeField] private float slashRadius = 0.92f;
    [SerializeField] private float slashWidth = 0.11f;
    [SerializeField] private int slashSegments = 9;

    public bool IsAttacking { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
    public Vector2 CurrentAttackDirection => facingDirection;
    public int Damage => damage;
    public int DamageVariance => Mathf.Max(0, damageVariance);
    public float AttackRadius => attackRadius;
    public float AttackCooldown => cooldown;
    public float KnockbackMultiplier { get; private set; } = 1f;
    public void SetKnockbackMultiplier(float multiplier) { KnockbackMultiplier = Mathf.Max(1f, multiplier); }
    public void SetWeaponAppearance(bool heavy)
    {
        if (heavyWeapon != heavy || equipped == null) EquipWeapon(heavy);
    }
    private float offsetToHitRadius;

    public void SetRunStats(int attackDamage, float attackCooldown, float radius)
    {
        damage = Mathf.Max(1, attackDamage);
        cooldown = Mathf.Max(0.05f, attackCooldown);
        attackRadius = Mathf.Max(0.1f, radius);
        slashRadius = attackRadius;
        attackOffset = attackRadius * offsetToHitRadius;
        if (equipped != null)
        {
            attackDuration = Mathf.Max(0.05f, equipped.attackDuration * cooldown / equipped.cooldown);
            hitTime = attackDuration * equipped.impactProgress;
        }
        if (attackKey == KeyCode.Space) attackKey = KeyCode.None;
    }

    private SurvivorMovement movement;
    private SurvivorHealth health;
    private Animator animator;
    private LineRenderer slash;
    private Material slashMaterial;
    private Vector2 facingDirection = Vector2.down;
    private bool movementWasEnabled;
    private bool hitApplied;
    private float attackElapsed;
    private float nextAttackTime;
    private float randomSwingOffset;
    private float randomSweep;
    private float randomRadiusScale;
    private Vector2 pendingAttackDirection;

    private void Awake()
    {
        movement = GetComponent<SurvivorMovement>();
        health = GetComponent<SurvivorHealth>();
        animator = GetComponent<Animator>();
        weaponVisual = GetComponent<SurvivorWeaponVisual>();
        slashSegments = Mathf.Clamp(slashSegments, 4, 32);
        attackDuration = Mathf.Max(0.05f, attackDuration);
        hitTime = Mathf.Clamp(hitTime, 0.01f, attackDuration);
        offsetToHitRadius = attackOffset / Mathf.Max(0.1f, attackRadius);
        CreateSlashRenderer();
        EquipWeapon(false);
    }

    private void Update()
    {
        if (health != null && (health.IsDead || health.IsGameOver))
        {
            if (IsAttacking)
                EndAttack();
            return;
        }
        if (Time.timeScale <= 0f) return;

        if (!IsAttacking)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            if (Time.time >= nextAttackTime && IsAttackPressed())
                BeginAttack();
            return;
        }

        attackElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(attackElapsed / attackDuration);
        if (weaponVisual != null) weaponVisual.SetAttackProgress(progress);
        DrawSlash(progress);

        if (!hitApplied && attackElapsed >= hitTime)
        {
            hitApplied = true;
            ApplyHit();
        }

        if (attackElapsed >= attackDuration)
            EndAttack();
    }

    public void Attack()
    {
        if (Time.timeScale > 0f && !healthIsDead() && !IsAttacking &&
            (GetComponent<SurvivorDash>() == null || !GetComponent<SurvivorDash>().IsDashing) && Time.time >= nextAttackTime)
            BeginAttack();
    }

    public void AttackTowards(Vector2 worldPosition)
    {
        Vector2 direction = worldPosition - (Vector2)transform.position;
        pendingAttackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
        Attack();
    }

    private bool IsAttackPressed()
    {
        if (allowMouseLeftButton && Input.GetMouseButton(0))
        {
            pendingAttackDirection = GetMouseWorldDirection();
            return true;
        }

        if (attackKey != KeyCode.None && Input.GetKeyDown(attackKey))
        {
            // 空格攻击也以鼠标所在的世界方向为准，鼠标不可用时才回退到移动朝向。
            pendingAttackDirection = GetMouseWorldDirection();
            if (pendingAttackDirection.sqrMagnitude <= 0.001f && movement != null)
                pendingAttackDirection = movement.FacingDirection;
            return true;
        }

        return false;
    }

    private void BeginAttack()
    {
        SurvivorDash dash = GetComponent<SurvivorDash>();
        if (dash != null && dash.IsDashing) return;
        IsAttacking = true;
        attackElapsed = 0f;
        hitApplied = false;

        // 应用动态攻速加成（连斩狂热、寒霜动能）
        float dynamicCooldown = cooldown;
        var combatEffects = RunState.Instance != null ? RunState.Instance.GetComponent<RunCombatEffects>() : null;
        if (combatEffects != null)
        {
            float bonus = combatEffects.GetAttackSpeedBonus();
            dynamicCooldown = cooldown / (1f + bonus);
        }

        nextAttackTime = Time.time + Mathf.Max(0.05f, dynamicCooldown);
        randomSwingOffset = Random.Range(-14f, 14f);
        randomSweep = equipped != null ? equipped.arcDegrees : 120f;
        randomRadiusScale = Random.Range(0.94f, 1.08f);

        Vector2 attackDirection = pendingAttackDirection;
        if (attackDirection.sqrMagnitude <= 0.001f)
            attackDirection = GetMouseWorldDirection();
        if (attackDirection.sqrMagnitude <= 0.001f && movement != null)
            attackDirection = movement.FacingDirection;
        if (attackDirection.sqrMagnitude <= 0.001f)
            attackDirection = Vector2.down;

        facingDirection = attackDirection.normalized;
        if (movement != null) movement.SetFacingDirection(facingDirection);

        if (movement != null)
        {
            movementWasEnabled = movement.enabled;
            movement.enabled = false;
        }

        pendingAttackDirection = Vector2.zero;
        ApplyAttackPose();
        if (weaponVisual != null) weaponVisual.BeginAttack(facingDirection);

        if (slash != null)
            slash.enabled = false;
    }

    private Vector2 GetMouseWorldDirection()
    {
        Camera attackCamera = Camera.main;
        if (attackCamera == null)
            return Vector2.zero;

        float cameraDistance = Mathf.Abs(attackCamera.transform.position.z - transform.position.z);
        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = cameraDistance;
        Vector3 mouseWorldPosition = attackCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 direction = (Vector2)(mouseWorldPosition - transform.position);
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
    }

    private void EndAttack()
    {
        IsAttacking = false;
        attackElapsed = 0f;
        hitApplied = false;
        if (slash != null)
            slash.enabled = false;
        if (movement != null && !healthIsDead())
            movement.enabled = movementWasEnabled;
        if (weaponVisual != null) weaponVisual.EndAttack();
    }

    private bool healthIsDead()
    {
        return health != null && (health.IsDead || health.IsGameOver);
    }

    private void ApplyHit()
    {
        Vector2 origin = AttackOrigin;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = true;
        int count = Physics2D.OverlapCircle(origin, attackRadius, filter, hitBuffer);
        hitTargets.Clear();
        float halfArc = (equipped != null ? equipped.arcDegrees : 120f) * 0.5f;
        var run = RunState.Instance;
        var effects = run != null ? run.CombatEffects : null;
        int swingDamage = effects != null ? effects.SwingDamage(damage) : damage;
        swingDamage = Mathf.Max(1, swingDamage + Random.Range(-DamageVariance, DamageVariance + 1));
        int actualSwingDamage = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = hitBuffer[i];
            if (collider == null)
                continue;

            ZombieChaser zombie = collider.GetComponentInParent<ZombieChaser>();
            if (zombie == null || zombie.CurrentHealth <= 0 || hitTargets.Contains(zombie.gameObject.GetInstanceID()))
                continue;
            Vector2 point = collider.ClosestPoint(origin);
            Vector2 toHit = point - origin;
            if (toHit.sqrMagnitude > 0.01f && Vector2.Angle(facingDirection, toHit) > halfArc) continue;
            if (HasObstacleBetween(origin, point, zombie)) continue;
            hitTargets.Add(zombie.gameObject.GetInstanceID());

            float knockbackForce = Random.Range(2.2f, 3.1f) * KnockbackMultiplier * WeaponKnockbackMultiplier;
            int targetDamage = effects != null ? effects.PrepareHit(zombie, swingDamage) : swingDamage;
            int healthBefore = zombie.CurrentHealth;
            zombie.TakeDamage(targetDamage, facingDirection, knockbackForce, true);
            actualSwingDamage += healthBefore - zombie.CurrentHealth;
        }
        if (effects != null) effects.CompleteSwing(actualSwingDamage);
    }

    private bool HasObstacleBetween(Vector2 origin, Vector2 point, ZombieChaser targetZombie)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = false;
        int count = Physics2D.Linecast(origin, point, filter, obstacleBuffer);
        for (int i = 0; i < count; i++)
        {
            var collider = obstacleBuffer[i].collider;
            if (collider == null || collider.transform.IsChildOf(transform) || collider.transform.IsChildOf(targetZombie.transform)) continue;
            var attached = collider.attachedRigidbody;
            if (attached != null && attached.bodyType != RigidbodyType2D.Static) continue;
            if (collider.GetComponentInParent<ZombieChaser>() != null) continue;
            return true;
        }
        return false;
    }

    private void CreateSlashRenderer()
    {
        GameObject slashObject = new GameObject("MeleeSlashVisual");
        slashObject.transform.SetParent(transform, false);
        slash = slashObject.AddComponent<LineRenderer>();
        slash.useWorldSpace = true;
        slash.loop = false;
        slash.alignment = LineAlignment.View;
        slash.textureMode = LineTextureMode.Stretch;
        slash.numCapVertices = 0;
        slash.numCornerVertices = 0;
        slash.startWidth = slashWidth;
        slash.endWidth = slashWidth * 0.15f;
        slash.positionCount = slashSegments + 1;
        slash.sortingLayerName = "Default";
        slash.sortingOrder = 5600;
        slash.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            slashMaterial = new Material(shader)
            {
                color = slashColor
            };
            slash.material = slashMaterial;
        }
    }

    private void DrawSlash(float progress)
    {
        if (slash == null)
            return;
        float duration = equipped != null ? equipped.trailDuration : 0.07f;
        float local = (attackElapsed - hitTime + duration * 0.25f) / duration;
        slash.enabled = local >= 0f && local < 1f;
        if (!slash.enabled) return;
        progress = Mathf.Clamp01(local);
        SpriteRenderer bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer != null) slash.sortingOrder = bodyRenderer.sortingOrder + 3;

        float centerAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        float sweep = randomSweep > 0f ? randomSweep : 138f;
        float swingOffset = randomSwingOffset + Mathf.Lerp(-32f, 32f, progress);
        Vector3 center = new Vector3(AttackOrigin.x, AttackOrigin.y, transform.position.z);

        for (int i = 0; i <= slashSegments; i++)
        {
            float t = i / (float)slashSegments;
            float angle = (centerAngle - sweep * 0.5f + swingOffset) + sweep * t;
            float radians = angle * Mathf.Deg2Rad;
            float radius = slashRadius * randomRadiusScale * Mathf.Lerp(0.88f, 1.02f, Mathf.Sin(t * Mathf.PI));
            slash.SetPosition(i, center + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius);
        }

        if (slashMaterial != null)
        {
            float alpha = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            Color color = slashColor;
            color.a = Mathf.Max(0.2f, alpha);
            slashMaterial.color = color;
        }
    }

    private void OnDestroy()
    {
        if (slashMaterial != null)
            Destroy(slashMaterial);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.65f);
        Gizmos.DrawWireSphere(AttackOrigin, attackRadius);
    }

    // Both the damage sector and its arc use the same point in front of the feet.
    private Vector2 AttackOrigin => (Vector2)transform.position +
        (facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.down) * 0.2f;


    private void ApplyAttackPose()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = GetComponentInChildren<SpriteRenderer>();

        if (renderer != null)
            renderer.flipX = false;

        if (animator == null)
            return;
        if (weaponVisual != null) return;

        string animationName;
        if (Mathf.Abs(facingDirection.x) > Mathf.Abs(facingDirection.y))
            animationName = facingDirection.x < 0f ? "RunLeft" : "RunRight";
        else
            animationName = facingDirection.y > 0f ? "RunUp" : "RunDown";

        animator.Play(animationName, 0, 0f);
    }

    private void OnDisable()
    {
        if (IsAttacking) EndAttack();
    }
}
