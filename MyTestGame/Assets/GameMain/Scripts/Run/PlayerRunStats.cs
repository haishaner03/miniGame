using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRunStats : MonoBehaviour
{
    private SurvivorHealth health;
    private SurvivorMovement movement;
    private SurvivorMeleeAttack melee;
    private SurvivorDash dash;
    private int baseHealth;
    private int baseDamage;
    private float baseMoveSpeed;
    private float baseAttackCooldown;
    private float baseRange;
    private float baseDashCooldown;
    private float baseDashDistance;

    private void Awake()
    {
        health = GetComponent<SurvivorHealth>();
        movement = GetComponent<SurvivorMovement>();
        melee = GetComponent<SurvivorMeleeAttack>();
        dash = GetComponent<SurvivorDash>();
        baseHealth = health.MaxHealth;
        if (movement != null) baseMoveSpeed = movement.MoveSpeed;
        if (melee != null)
        {
            baseDamage = melee.Damage;
            baseRange = melee.AttackRadius;
            baseAttackCooldown = melee.AttackCooldown;
        }
        if (dash != null)
        {
            baseDashCooldown = dash.DashCooldown;
            baseDashDistance = dash.DashDistance;
        }
    }

    public void Apply(RunState run, int currentHealth)
    {
        int maximum = baseHealth + Mathf.RoundToInt(run.EffectTotal("MaxHealth"));
        health.ConfigureForRun(maximum, currentHealth < 0 ? maximum : currentHealth);
        if (movement != null)
            movement.SetMoveSpeed(baseMoveSpeed * (1f + run.EffectTotal("MoveSpeed")));
        if (melee != null)
        {
            var weapon = melee.GetWeaponDefinition(run.UsesHeavyWeapon);
            melee.EquipWeapon(run.UsesHeavyWeapon);
            int weaponDamage = weapon != null ? weapon.damage : baseDamage;
            float weaponCooldown = weapon != null ? weapon.cooldown : baseAttackCooldown;
            float weaponRange = weapon != null ? weapon.reach : baseRange;
            melee.SetRunStats(Mathf.RoundToInt((weaponDamage + run.EffectTotal("Damage")) * (1f + run.EffectTotal("DamagePercent"))),
                weaponCooldown * Mathf.Max(0.25f, 1f - run.EffectTotal("AttackSpeed")),
                weaponRange * (1f + run.EffectTotal("Range")));
            melee.SetKnockbackMultiplier(1f + run.EffectTotal("Knockback"));
        }
        if (dash != null)
            dash.SetRunStats(baseDashCooldown * Mathf.Max(0.25f, 1f - run.EffectTotal("DashCooldown")),
                baseDashDistance * (1f + run.EffectTotal("DashDistance")));
    }
}
