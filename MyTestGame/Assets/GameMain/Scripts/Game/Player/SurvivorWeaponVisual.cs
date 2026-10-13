using System.Collections.Generic;
using UnityEngine;

/// <summary>Attaches the independent weapon to the current sprite's fist and plays shared attacks.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class SurvivorWeaponVisual : MonoBehaviour
{
    private static readonly float[] FrameTimes = { 0f, 0.2f, 0.36f, 0.48f, 0.68f, 0.88f };
    [SerializeField] private SurvivorWeaponPoseLibrary poseLibrary;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private Transform weaponPivot;
    private SpriteRenderer body;
    private Animator animator;
    private SurvivorMovement movement;
    private SurvivorHealth health;
    private MeleeWeaponDefinition definition;
    private readonly Dictionary<Sprite, SurvivorWeaponPoseLibrary.Pose> poses = new Dictionary<Sprite, SurvivorWeaponPoseLibrary.Pose>();
    private SurvivorWeaponPoseLibrary.Pose[] attack;
    private Sprite[] bowAttack;
    private bool attacking, animatorWasEnabled;
    private float progress;
    private SurvivorView attackView;
    public bool IsPlayingAttack => attacking;
    public SpriteRenderer WeaponRenderer => weaponRenderer;
    public Transform WeaponPivot => weaponPivot;
    public MeleeWeaponDefinition EquippedWeapon => definition;
    public int AttackFrameIndex { get; private set; }

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        movement = GetComponent<SurvivorMovement>();
        health = GetComponent<SurvivorHealth>();
        if (weaponPivot == null)
        {
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);
            var visual = new GameObject("WeaponSprite");
            visual.transform.SetParent(weaponPivot, false);
            weaponRenderer = visual.AddComponent<SpriteRenderer>();
        }
        if (poseLibrary != null)
            foreach (var pose in poseLibrary.poses)
                if (pose.sprite != null) poses[pose.sprite] = pose;
    }

    public void Equip(MeleeWeaponDefinition weapon)
    {
        definition = weapon;
        if (weaponRenderer == null || definition == null) return;
        weaponRenderer.sprite = definition.sprite;
        weaponRenderer.color = Color.white;
        float length = definition.sprite != null ? definition.sprite.rect.height / definition.sprite.pixelsPerUnit : 1f;
        weaponRenderer.transform.localScale = Vector3.one * definition.visualLength / Mathf.Max(0.01f, length);
    }

    public void BeginAttack(Vector2 direction)
    {
        if (poseLibrary == null || definition == null) return;
        attackView = SurvivorWeaponPoseLibrary.View(direction);
        attack = poseLibrary.Attack(attackView);
        bowAttack = definition.kind == SurvivorWeaponKind.Bow ? poseLibrary.BowAttack(attackView) : null;
        if (attack == null || attack.Length == 0) return;
        animatorWasEnabled = animator != null && animator.enabled;
        if (animator != null) animator.enabled = false;
        attacking = true;
        progress = 0f;
        ApplyAttack();
    }

    public void SetAttackProgress(float value) { progress = Mathf.Clamp01(value); }

    public void EndAttack()
    {
        if (!attacking) return;
        attacking = false;
        if (animator != null) animator.enabled = animatorWasEnabled;
        if (movement != null) movement.RefreshAnimation();
    }

    private void LateUpdate()
    {
        if (body == null || definition == null || weaponRenderer == null) return;
        weaponRenderer.enabled = body.enabled && (health == null || !health.IsDead);
        if (attacking && definition.kind == SurvivorWeaponKind.Bow && bowAttack != null && bowAttack.Length > 0)
            weaponRenderer.enabled = false;
        if (attacking) ApplyAttack();
        else if (body.sprite != null && poses.TryGetValue(body.sprite, out var pose)) ApplyPose(pose);
        else ApplyFallback();
    }

    private void ApplyAttack()
    {
        if (definition.kind == SurvivorWeaponKind.Bow && bowAttack != null && bowAttack.Length > 0)
        {
            int bowIndex = progress < 0.16f ? 0 : progress < 0.32f ? 1 : progress < 0.48f ? 2 : progress < definition.impactProgress ? 3 : progress < 0.88f ? 4 : 5;
            AttackFrameIndex = Mathf.Min(bowIndex, bowAttack.Length - 1);
            body.sprite = bowAttack[AttackFrameIndex];
            body.flipX = false;
            return;
        }
        // Anticipation, impact and recovery land on the same normalized timeline as damage.
        float adjusted = progress <= definition.impactProgress
            ? progress / Mathf.Max(0.01f, definition.impactProgress) * 0.48f
            : 0.48f + (progress - definition.impactProgress) / Mathf.Max(0.01f, 1f - definition.impactProgress) * 0.52f;
        int index = 0;
        for (int i = 1; i < attack.Length && i < FrameTimes.Length; i++) if (adjusted >= FrameTimes[i]) index = i;
        AttackFrameIndex = index;
        var pose = attack[index];
        body.sprite = pose.sprite;
        body.flipX = false;
        ApplyPose(pose);
    }

    private void ApplyPose(SurvivorWeaponPoseLibrary.Pose pose)
    {
        Sprite sprite = pose.sprite;
        if (sprite == null) return;
        // Grip coordinates use image top-left; Unity sprite pivots use bottom-left.
        Vector2 grip = new Vector2(pose.gripPixel.x - sprite.pivot.x, sprite.rect.height - pose.gripPixel.y - sprite.pivot.y) / sprite.pixelsPerUnit;
        weaponPivot.localPosition = new Vector3(grip.x, grip.y, 0f);
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, pose.weaponAngle);
        if (definition.kind == SurvivorWeaponKind.Bow)
        {
            // The bow's grip is centered; while walking it is carried upright at the actual hand.
            weaponPivot.localRotation = Quaternion.identity;
        }
        weaponRenderer.sortingLayerID = body.sortingLayerID;
        weaponRenderer.sortingOrder = body.sortingOrder + (pose.view == SurvivorView.Up ? -1 : 1);
        weaponRenderer.flipX = pose.view == SurvivorView.Left;
        Color color = body.color;
        weaponRenderer.color = new Color(color.r, color.g, color.b, color.a);
    }

    private void ApplyFallback()
    {
        SurvivorView view = SurvivorWeaponPoseLibrary.View(movement != null ? movement.FacingDirection : Vector2.down);
        var pose = new SurvivorWeaponPoseLibrary.Pose { sprite = body.sprite, view = view, gripPixel = view == SurvivorView.Left ? new Vector2(19, 34) : new Vector2(43, 40), weaponAngle = view == SurvivorView.Left ? 25f : -25f };
        ApplyPose(pose);
    }

    private void OnDisable() { EndAttack(); }
}
