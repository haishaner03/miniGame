using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家短距离冲刺：沿最后朝向移动，使用 Rigidbody2D.Cast 在碰撞前停下，
/// 并用当前 SpriteRenderer 帧生成逐渐透明的残影。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class SurvivorDash : MonoBehaviour
{
    [Header("冲刺")]
    [SerializeField] private KeyCode dashKey = KeyCode.Space;
    [SerializeField] private float dashDistance = 1.8f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.65f;
    [SerializeField] private float collisionSkin = 0.025f;
    [SerializeField] private bool blockDashWhileAttacking = true;

    [Header("残影")]
    [SerializeField] private bool spawnAfterimages = true;
    [SerializeField] private float afterimageInterval = 0.035f;
    [SerializeField] private float afterimageLifetime = 0.22f;
    [SerializeField, Range(0.05f, 1f)] private float afterimageAlpha = 0.42f;
    [SerializeField] private Color afterimageColor = new Color(0.45f, 0.9f, 1f, 1f);
    [SerializeField] private int afterimageSortingOffset = -1;
    [SerializeField] private int afterimagePrewarm = 6;

    public bool IsDashing { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
    public float DashCooldown => dashCooldown;
    public float DashDistance => dashDistance;

    public void SetRunStats(float cooldown, float distance)
    {
        dashCooldown = Mathf.Max(0.05f, cooldown);
        dashDistance = Mathf.Max(0.1f, distance);
    }

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private SurvivorMovement movement;
    private SurvivorHealth health;
    private SurvivorMeleeAttack meleeAttack;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    private readonly Queue<GameObject> afterimagePool = new Queue<GameObject>();
    private readonly Dictionary<GameObject, Coroutine> activeAfterimageCoroutines = new Dictionary<GameObject, Coroutine>();
    private ContactFilter2D castFilter;
    private Vector2 dashDirection;
    private float dashRemainingDistance;
    private float dashRemainingTime;
    private float nextDashTime;
    private float afterimageTimer;
    private bool movementWasEnabled;
    private Transform afterimageRoot;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<SurvivorMovement>();
        health = GetComponent<SurvivorHealth>();
        meleeAttack = GetComponent<SurvivorMeleeAttack>();

        castFilter = ContactFilter2D.noFilter;
        castFilter.useTriggers = false;
        dashDistance = Mathf.Max(0.1f, dashDistance);
        dashDuration = Mathf.Max(0.02f, dashDuration);
        dashCooldown = Mathf.Max(0.05f, dashCooldown);
        collisionSkin = Mathf.Max(0.001f, collisionSkin);
        afterimageInterval = Mathf.Max(0.01f, afterimageInterval);
        afterimageLifetime = Mathf.Max(0.03f, afterimageLifetime);
        afterimagePrewarm = Mathf.Max(0, afterimagePrewarm);
        CreateAfterimagePool();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || IsDashing || healthIsUnavailable())
            return;

        if (Time.time < nextDashTime || !Input.GetKeyDown(dashKey))
            return;

        if (blockDashWhileAttacking && meleeAttack != null && meleeAttack.IsAttacking)
            return;

        BeginDash();
    }

    private void FixedUpdate()
    {
        if (!IsDashing)
            return;

        float step = dashDistance / dashDuration * Time.fixedDeltaTime;
        step = Mathf.Min(step, dashRemainingDistance);

        if (spawnAfterimages && afterimageTimer <= 0f)
        {
            SpawnAfterimage();
            afterimageTimer = afterimageInterval;
        }
        afterimageTimer -= Time.fixedDeltaTime;

        float allowedDistance = GetAllowedDistance(step);
        if (allowedDistance > 0f)
        {
            body.MovePosition(body.position + dashDirection * allowedDistance);
            dashRemainingDistance -= allowedDistance;
        }

        dashRemainingTime -= Time.fixedDeltaTime;
        bool hitObstacle = allowedDistance + collisionSkin < step;
        if (hitObstacle || dashRemainingDistance <= 0.001f || dashRemainingTime <= 0f)
            EndDash();
    }

    private void BeginDash()
    {
        dashDirection = movement != null ? movement.FacingDirection : Vector2.down;
        if (dashDirection.sqrMagnitude < 0.001f)
            dashDirection = Vector2.down;
        dashDirection.Normalize();

        IsDashing = true;
        dashRemainingDistance = dashDistance;
        dashRemainingTime = dashDuration;
        afterimageTimer = 0f;
        nextDashTime = Time.time + dashCooldown;

        if (movement != null)
        {
            movementWasEnabled = movement.enabled;
            movement.enabled = false;
        }
    }

    private void EndDash()
    {
        IsDashing = false;
        dashRemainingDistance = 0f;
        dashRemainingTime = 0f;
        if (movement != null && movementWasEnabled && !healthIsUnavailable())
            movement.enabled = true;
    }

    private float GetAllowedDistance(float requestedDistance)
    {
        if (body == null || bodyCollider == null || requestedDistance <= 0f)
            return requestedDistance;

        int hitCount = body.Cast(dashDirection, castFilter, castHits, requestedDistance + collisionSkin);
        float nearest = requestedDistance + collisionSkin;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider == bodyCollider || hit.collider.isTrigger)
                continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                foundHit = true;
            }
        }

        if (!foundHit)
            return requestedDistance;

        return Mathf.Max(0f, nearest - collisionSkin);
    }

    private void SpawnAfterimage()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        GameObject ghost = afterimagePool.Count > 0 ? afterimagePool.Dequeue() : CreateAfterimage();
        if (ghost == null)
            return;

        ghost.SetActive(true);
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.GetComponent<SpriteRenderer>();
        ghostRenderer.sprite = spriteRenderer.sprite;
        ghostRenderer.flipX = spriteRenderer.flipX;
        ghostRenderer.flipY = spriteRenderer.flipY;
        ghostRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = spriteRenderer.sortingOrder + afterimageSortingOffset;

        Color color = afterimageColor;
        color.a = Mathf.Clamp01(afterimageAlpha);
        ghostRenderer.color = color;

        if (activeAfterimageCoroutines.TryGetValue(ghost, out Coroutine previousCoroutine) && previousCoroutine != null)
            StopCoroutine(previousCoroutine);

        activeAfterimageCoroutines[ghost] = StartCoroutine(FadeAfterimage(ghost, ghostRenderer, afterimageLifetime));
    }

    private IEnumerator FadeAfterimage(GameObject ghost, SpriteRenderer ghostRenderer, float lifetime)
    {
        float elapsed = 0f;
        Color startColor = ghostRenderer.color;

        while (ghost != null && ghostRenderer != null && elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            Color color = startColor;
            color.a = startColor.a * (1f - Mathf.Clamp01(elapsed / lifetime));
            ghostRenderer.color = color;
            yield return null;
        }

        if (ghost != null)
        {
            ghost.SetActive(false);
            afterimagePool.Enqueue(ghost);
            activeAfterimageCoroutines.Remove(ghost);
        }
    }

    private void CreateAfterimagePool()
    {
        GameObject rootObject = new GameObject("SurvivorAfterimagePool");
        afterimageRoot = rootObject.transform;
        afterimageRoot.SetParent(transform.parent, false);

        for (int i = 0; i < afterimagePrewarm; i++)
            afterimagePool.Enqueue(CreateAfterimage());
    }

    private GameObject CreateAfterimage()
    {
        GameObject ghost = new GameObject("SurvivorAfterimage");
        ghost.transform.SetParent(afterimageRoot, false);
        ghost.SetActive(false);
        ghost.AddComponent<SpriteRenderer>();
        return ghost;
    }

    private bool healthIsUnavailable()
    {
        return health != null && (health.IsDead || health.IsGameOver);
    }

    private void OnDisable()
    {
        if (movement != null && movementWasEnabled && !healthIsUnavailable())
            movement.enabled = true;
        IsDashing = false;
    }

    private void OnDestroy()
    {
        if (afterimageRoot != null)
            Destroy(afterimageRoot.gameObject);
    }
}
