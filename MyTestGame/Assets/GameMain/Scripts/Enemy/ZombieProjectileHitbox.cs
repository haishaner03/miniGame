using UnityEngine;

/// <summary>Body-sized projectile target independent of the foot collider used for movement.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ZombieProjectileHitbox : MonoBehaviour
{
    private ZombieChaser owner;
    public ZombieChaser Owner => owner != null ? owner : (owner = GetComponentInParent<ZombieChaser>());

    public static ZombieProjectileHitbox Ensure(ZombieChaser zombie)
    {
        var existing = zombie.GetComponentInChildren<ZombieProjectileHitbox>(true);
        if (existing != null)
        {
            Configure(existing, zombie);
            return existing;
        }
        var root = new GameObject("ProjectileHitbox", typeof(BoxCollider2D));
        root.layer = zombie.gameObject.layer;
        root.transform.SetParent(zombie.transform, false);
        var hitbox = root.AddComponent<ZombieProjectileHitbox>();
        hitbox.owner = zombie;
        Configure(hitbox, zombie);
        return hitbox;
    }

    private static void Configure(ZombieProjectileHitbox hitbox, ZombieChaser zombie)
    {
        var collider = hitbox.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        var champion = zombie.GetComponent<ZombieChampion>();
        bool boss = champion != null && champion.isBoss;
        // The root circle remains the movement footprint. This box follows the
        // visible body so arrows can connect with the torso and head as well.
        collider.size = boss ? new Vector2(1.65f, 2.15f) : new Vector2(.64f, 1.02f);
        collider.offset = boss ? new Vector2(0f, .95f) : new Vector2(0f, .30f);
    }
}
