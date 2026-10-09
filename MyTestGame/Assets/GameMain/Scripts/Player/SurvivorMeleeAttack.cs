using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家第一版近战攻击：用程序化挥砍弧光表现动作，并在命中帧检测丧尸。
/// 后续可以把 DrawSlash 替换成真正的攻击序列帧，伤害接口保持不变。
/// </summary>
[DisallowMultipleComponent]
public sealed class SurvivorMeleeAttack : MonoBehaviour
{
    [Header("攻击参数")]
    [SerializeField] private int damage = 2;
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
    public float AttackRadius => attackRadius;
    public float AttackCooldown => cooldown;
    private float slashToHitRadius;
    private float offsetToHitRadius;

    public void SetRunStats(int attackDamage, float attackCooldown, float radius)
    {
        damage = Mathf.Max(1, attackDamage);
        cooldown = Mathf.Max(0.05f, attackCooldown);
        attackRadius = Mathf.Max(0.1f, radius);
        slashRadius = attackRadius * slashToHitRadius;
        attackOffset = attackRadius * offsetToHitRadius;
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
        slashSegments = Mathf.Clamp(slashSegments, 4, 32);
        attackDuration = Mathf.Max(0.05f, attackDuration);
        hitTime = Mathf.Clamp(hitTime, 0.01f, attackDuration);
        slashToHitRadius = slashRadius / Mathf.Max(0.1f, attackRadius);
        offsetToHitRadius = attackOffset / Mathf.Max(0.1f, attackRadius);
        CreateSlashRenderer();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (health != null && (health.IsDead || health.IsGameOver))
        {
            if (IsAttacking)
                EndAttack();
            return;
        }

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
        if (allowMouseLeftButton && Input.GetMouseButtonDown(0))
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
        nextAttackTime = Time.time + Mathf.Max(0.05f, cooldown);
        randomSwingOffset = Random.Range(-14f, 14f);
        randomSweep = Random.Range(112f, 150f);
        randomRadiusScale = Random.Range(0.94f, 1.08f);

        Vector2 attackDirection = pendingAttackDirection;
        if (attackDirection.sqrMagnitude <= 0.001f)
            attackDirection = GetMouseWorldDirection();
        if (attackDirection.sqrMagnitude <= 0.001f && movement != null)
            attackDirection = movement.FacingDirection;
        if (attackDirection.sqrMagnitude <= 0.001f)
            attackDirection = Vector2.down;

        facingDirection = attackDirection.normalized;

        if (movement != null)
        {
            movementWasEnabled = movement.enabled;
            movement.enabled = false;
        }

        pendingAttackDirection = Vector2.zero;
        ApplyAttackPose();

        if (slash != null)
            slash.enabled = true;
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
    }

    private bool healthIsDead()
    {
        return health != null && (health.IsDead || health.IsGameOver);
    }

    private void ApplyHit()
    {
        Vector2 hitCenter = (Vector2)transform.position + facingDirection * attackOffset;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(hitCenter, attackRadius);
        HashSet<int> hitTargets = new HashSet<int>();

        foreach (Collider2D collider in colliders)
        {
            if (collider == null)
                continue;

            ZombieChaser zombie = collider.GetComponentInParent<ZombieChaser>();
            if (zombie == null || zombie.CurrentHealth <= 0 || !hitTargets.Add(zombie.gameObject.GetInstanceID()))
                continue;

            float knockbackForce = Random.Range(2.2f, 3.1f);
            zombie.TakeDamage(damage, facingDirection, knockbackForce);
            if (RunState.Instance != null) RunState.Instance.RecordMeleeHit();
        }
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

        float centerAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        float sweep = randomSweep > 0f ? randomSweep : 138f;
        float swingOffset = randomSwingOffset + Mathf.Lerp(-32f, 32f, progress);
        Vector3 center = transform.position;

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
        Vector2 direction = facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.down;
        Gizmos.DrawWireSphere((Vector2)transform.position + direction * attackOffset, attackRadius);
    }


private void ApplyAttackPose()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = GetComponentInChildren<SpriteRenderer>();

        if (renderer != null)
            renderer.flipX = false;

        if (animator == null)
            return;

        string animationName;
        if (Mathf.Abs(facingDirection.x) > Mathf.Abs(facingDirection.y))
            animationName = facingDirection.x < 0f ? "RunLeft" : "RunRight";
        else
            animationName = facingDirection.y > 0f ? "RunUp" : "RunDown";

        animator.Play(animationName, 0, 0f);
    }
}
