using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SurvivorMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private bool normalizeDiagonal = true;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private string currentAnimation;

    private Rigidbody2D rb;
        private Vector2 knockbackVelocity;
    [SerializeField] private float knockbackDamping = 18f;
private Vector2 input;
    public Vector2 FacingDirection { get; private set; } = Vector2.down;
    public float MoveSpeed => moveSpeed;
    public Vector2 MoveInput => input;

    public void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f) FacingDirection = direction.normalized;
    }

    public void RefreshAnimation()
    {
        currentAnimation = null;
        UpdateAnimation();
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0.1f, speed);
    }

private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        PlayAnimation("Idle");
    }

private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            input = Vector2.zero;
            return;
        }
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (normalizeDiagonal && input.sqrMagnitude > 1f)
            input.Normalize();

        if (input.sqrMagnitude > 0.0001f)
            FacingDirection = input.normalized;

        UpdateAnimation();
    }

private void UpdateAnimation()
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = false;
            string state = "Idle";
            if (GetComponent<SurvivorWeaponVisual>() != null)
            {
                var view = SurvivorWeaponPoseLibrary.View(FacingDirection);
                state = view == SurvivorView.Up ? "IdleUp" : view == SurvivorView.Left ? "IdleLeft" : view == SurvivorView.Right ? "IdleRight" : "Idle";
            }
            PlayAnimation(state);
            return;
        }

        bool hasHorizontalInput = Mathf.Abs(input.x) > 0.1f;
        bool hasVerticalInput = Mathf.Abs(input.y) > 0.1f;

        if (hasHorizontalInput && hasVerticalInput)
        {
            // 只使用上下左右四组序列帧。斜向移动仍然保留，
            // 但动画选择输入中幅度更大的主方向，不再额外 flipX。
            if (spriteRenderer != null)
                spriteRenderer.flipX = false;

            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
                PlayAnimation(input.x < 0f ? "RunLeft" : "RunRight");
            else
                PlayAnimation(input.y > 0f ? "RunUp" : "RunDown");
            return;
        }

        if (hasHorizontalInput)
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = false;
            PlayAnimation(input.x < 0f ? "RunLeft" : "RunRight");
            return;
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = false;
        PlayAnimation(input.y > 0f ? "RunUp" : "RunDown");
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || currentAnimation == stateName)
            return;

        animator.Play(stateName, 0, 0f);
        currentAnimation = stateName;
    }


        public void ApplyKnockback(Vector2 velocity)
    {
        knockbackVelocity = Vector2.ClampMagnitude(knockbackVelocity + velocity, 8f);
    }

    public void ClearKnockback()
    {
        knockbackVelocity = Vector2.zero;
    }
    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + (input * moveSpeed + knockbackVelocity) * Time.fixedDeltaTime);
        knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, knockbackDamping * Time.fixedDeltaTime);
    }
}
