using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable arrow with swept collision, penetration, elemental hits and distance bonuses.</summary>
[DisallowMultipleComponent]
public sealed class SurvivorArrowProjectile : MonoBehaviour
{
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[128];
    private readonly HashSet<int> hitTargets = new HashSet<int>();
    private Vector2 direction;
    private float speed;
    private float expiresAt;
    private int damage;
    private int remainingPierce;
    private float knockback;
    private RunState run;
    private float traveled;
    private float distanceBonus;
    private ContactFilter2D hitFilter;

    public void Launch(Vector2 launchDirection, int launchDamage, float launchSpeed, float lifetime, int pierce, float launchKnockback, RunState ownerRun)
    {
        direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : Vector2.right;
        damage = Mathf.Max(1, launchDamage);
        speed = Mathf.Max(0.1f, launchSpeed);
        expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        remainingPierce = Mathf.Max(0, pierce);
        knockback = launchKnockback;
        run = ownerRun;
        traveled = 0f;
        distanceBonus = run != null ? run.EffectTotal("BowDistanceDamage") : 0f;
        hitTargets.Clear();
        hitFilter = new ContactFilter2D();
        hitFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        hitFilter.useTriggers = true;
        transform.right = direction;
    }

    public bool Tick(float deltaTime)
    {
        if (run == null || run.Phase == RunState.RunPhase.Loading || run.Phase == RunState.RunPhase.Lost || run.Phase == RunState.RunPhase.Won || Time.time >= expiresAt) return true;
        if (run.Phase != RunState.RunPhase.Playing) return false;
        float step = speed * deltaTime;
        Vector2 origin = transform.position;
        int count = Physics2D.CircleCast(origin, 0.06f, direction, hitFilter, hitBuffer, step);
        for (int i = 0; i < count; i++)
        {
            var collider = hitBuffer[i].collider;
            if (collider == null) continue;
            var hitbox = collider.GetComponent<ZombieProjectileHitbox>();
            if (collider.isTrigger && hitbox == null) continue;
            if (collider.GetComponentInParent<SurvivorHealth>() != null) continue;
            ZombieChaser zombie = hitbox != null ? hitbox.Owner : collider.GetComponentInParent<ZombieChaser>();
            if (zombie == null)
            {
                var body = collider.attachedRigidbody;
                if (body == null || body.bodyType == RigidbodyType2D.Static) return true;
                continue;
            }
            if (zombie.CurrentHealth <= 0 || hitTargets.Contains(zombie.gameObject.GetInstanceID())) continue;
            hitTargets.Add(zombie.gameObject.GetInstanceID());
            RunCombatEffects effects = run != null ? run.CombatEffects : null;
            int distanceDamage = Mathf.RoundToInt(damage * (1f + distanceBonus * Mathf.Clamp01((traveled + hitBuffer[i].distance) / 4f)));
            int targetDamage = effects != null ? effects.PrepareHit(zombie, distanceDamage) : distanceDamage;
            int before = zombie.CurrentHealth;
            zombie.TakeDamage(targetDamage, direction, knockback, true);
            if (effects != null) effects.CompleteSwing(Mathf.Max(0, before - zombie.CurrentHealth));
            if (remainingPierce <= 0) return true;
            remainingPierce--;
        }
        traveled += step;
        transform.position = origin + direction * step;
        return false;
    }
}
