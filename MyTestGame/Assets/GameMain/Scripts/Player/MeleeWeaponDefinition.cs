using UnityEngine;

/// <summary>Editable artwork and combat timing for one equipped melee weapon.</summary>
[CreateAssetMenu(menuName = "Zombie/Melee Weapon")]
public sealed class MeleeWeaponDefinition : ScriptableObject
{
    public string displayName;
    public Sprite sprite;
    [Min(1)] public int damage = 80;
    [Min(0.05f)] public float cooldown = 0.28f;
    [Min(0.05f)] public float attackDuration = 0.2f;
    [Range(0f, 1f)] public float impactProgress = 0.48f;
    [Min(0.1f)] public float reach = 0.9f;
    [Range(20f, 180f)] public float arcDegrees = 120f;
    [Min(0.1f)] public float knockbackMultiplier = 1f;
    [Min(0.1f)] public float visualLength = 0.72f;
    public Color trailColor = new Color(0.65f, 0.9f, 1f, 0.9f);
    [Min(0.01f)] public float trailDuration = 0.07f;
}
