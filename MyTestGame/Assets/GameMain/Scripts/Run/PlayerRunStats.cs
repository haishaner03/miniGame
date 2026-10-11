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
        // 赌博牌：狂战士契约牺牲生命上限换取伤害（玻璃大炮的代价是放弃减伤，不改生命）
        int healthMod = Mathf.RoundToInt(run.EffectTotal("MaxHealth") - run.EffectTotal("BerserkerPact") * 30f);
        int maximum = Mathf.RoundToInt(baseHealth * run.StartingHealthMultiplier) + healthMod;
        health.ConfigureForRun(maximum, currentHealth < 0 ? maximum : currentHealth);

        // 移速：固定加成在这里设置；动态加成（极寒领域）由 SurvivorMovement 实时计算
        if (movement != null)
            movement.SetMoveSpeed(baseMoveSpeed * (1f + run.EffectTotal("MoveSpeed")));

        if (melee != null)
        {
            var weapon = melee.GetWeaponDefinition(run.StartingWeapon);
            melee.EquipWeapon(run.StartingWeapon);
            int weaponDamage = weapon != null ? weapon.damage : baseDamage;
            float weaponCooldown = weapon != null ? weapon.cooldown : baseAttackCooldown;
            float weaponRange = weapon != null ? weapon.reach : baseRange;

            // 赌博牌伤害加成：狂战士 +60%、玻璃大炮 +80%、血之契约 +35%
            float gambleDamage = run.EffectTotal("BerserkerPact") * 0.6f + run.EffectTotal("GlassCannon") * 0.8f + run.EffectTotal("BloodPact") * 0.35f;
            // 双元素加成
            float dualBonus = run.EffectTotal("FreezeChance") > 0 && run.EffectTotal("BurnChance") > 0 ? run.EffectTotal("DualElement") : 0f;

            float totalDamagePercent = run.EffectTotal("DamagePercent") + gambleDamage + dualBonus + run.EffectTotal("BowDamage");
            int finalDamage = Mathf.RoundToInt((weaponDamage + run.EffectTotal("Damage")) * (1f + totalDamagePercent));

            // 速度转范围：每 +10% 速度 = +5% 范围
            float speedRangeBonus = run.EffectTotal("MoveSpeed") * run.EffectTotal("SpeedRange");
            float totalRange = run.EffectTotal("Range") + speedRangeBonus;
            // 双元素范围加成
            if (run.EffectTotal("FreezeChance") > 0 && run.EffectTotal("BurnChance") > 0)
                totalRange += run.EffectTotal("DualElement") * 0.2f / 0.15f; // 按描述是 +20%
            // 动态范围加成（凛冬之怒）由 SurvivorMeleeAttack 在命中检测时实时计算

            melee.SetRunStats(finalDamage,
                weaponCooldown / (1f + Mathf.Max(0f, run.EffectTotal("AttackSpeed") + run.EffectTotal("BowDrawSpeed"))),
                weaponRange * (1f + totalRange));
            melee.SetKnockbackMultiplier(1f + run.EffectTotal("Knockback"));
        }

        if (dash != null)
            dash.SetRunStats(baseDashCooldown * Mathf.Max(0.25f, 1f - run.EffectTotal("DashCooldown")),
                baseDashDistance * (1f + run.EffectTotal("DashDistance")));
    }
}
