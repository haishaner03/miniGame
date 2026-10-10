using UnityEngine;

/// <summary>Four cardinal sprite animations. Left uses the right frames with a clip flip curve.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public sealed class ZombieDirectionalAnimator : MonoBehaviour
{
    public enum View { Down, Up, Right, Left }
    public enum Motion { Idle, Walk, Attack, Death }

    [SerializeField, Min(.01f)] private float attackDuration = 8f / 12f;
    [SerializeField, Min(0f)] private float attackHitTime = 3f / 12f;
    [SerializeField, Min(.01f)] private float deathDuration = 4f / 10f;

    private static readonly int[,] StateHashes = CreateStateHashes();
    private Animator animator;
    private int currentHash = int.MinValue;
    private Motion currentMotion;
    public View Facing { get; private set; } = View.Down;
    public float AttackDuration => Mathf.Max(.01f, attackDuration);
    public float AttackHitTime => Mathf.Clamp(attackHitTime, 0f, AttackDuration);
    public float DeathDuration => Mathf.Max(.01f, deathDuration);
    public bool IsConfigured
    {
        get { ResolveAnimator(); return animator != null && animator.runtimeAnimatorController != null; }
    }

    private static int[,] CreateStateHashes()
    {
        var hashes = new int[4, 4];
        for (int motion = 0; motion < 4; motion++)
            for (int view = 0; view < 4; view++)
                hashes[motion, view] = Animator.StringToHash("Base Layer." + (Motion)motion + (View)view);
        return hashes;
    }

    private void ResolveAnimator()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void Awake() { ResolveAnimator(); }
    private void OnEnable()
    {
        currentHash = int.MinValue;
        Play(Motion.Idle);
    }

    public void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude < .001f) return;
        // A little hysteresis avoids alternating views at diagonal path nodes.
        float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y);
        bool horizontal = x > y;
        if (Mathf.Abs(x - y) < .12f * Mathf.Max(x, y))
            horizontal = Facing == View.Left || Facing == View.Right;
        Facing = horizontal ? (direction.x < 0f ? View.Left : View.Right)
                            : (direction.y > 0f ? View.Up : View.Down);
    }

    public void SetFrozen(bool frozen)
    {
        ResolveAnimator();
        if (animator != null) animator.speed = frozen ? 0f : 1f;
    }

    public void ResetVisuals()
    {
        Facing = View.Down;
        currentHash = int.MinValue;
        SetFrozen(false);
        Play(Motion.Idle);
    }

    public void Play(Motion motion)
    {
        if (!IsConfigured || !isActiveAndEnabled || !animator.isActiveAndEnabled) return;
        int hash = StateHashes[(int)motion, (int)Facing];
        if (hash == currentHash) return;
        float phase = motion == Motion.Walk && currentMotion == Motion.Walk && currentHash != int.MinValue
            ? Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) : 0f;
        animator.Play(hash, 0, phase);
        // Apply the first sprite immediately so direction changes never flash the old view.
        animator.Update(0f);
        currentHash = hash;
        currentMotion = motion;
    }
}
