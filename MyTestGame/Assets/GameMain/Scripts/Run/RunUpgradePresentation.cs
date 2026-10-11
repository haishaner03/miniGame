using Flower;
using UnityEngine;

public static class RunUpgradePresentation
{
    public static string Description(DRRunUpgrade upgrade, bool bow, bool freezeConverted = false)
    {
        string description = upgrade.Description;
        if (freezeConverted)
        {
            switch (upgrade.Effect)
            {
                case "FreezeChance": description = "冰冻概率已转为点燃概率；每层额外提高 15% 点燃概率。"; break;
                case "FreezeDuration": description = "转换后的点燃持续时间额外 +0.4 秒，最多 3 层。"; break;
                case "FrozenDamage": description = "对燃烧目标的攻击伤害 +30%，最多 2 层。"; break;
                case "Shatter": description = "击杀燃烧目标时触发火焰碎裂，对附近敌人造成近战伤害的 40%。"; break;
                case "FrostSpread": description = "攻击燃烧目标时，有 25% 概率点燃附近最多 2 个敌人。"; break;
                case "FrostAura": description = "每个燃烧敌人使移动速度 +4%。"; break;
                case "FrostShield": description = "场上有燃烧敌人时，受到伤害减少 25%。"; break;
                case "FrostKnockback": description = "对燃烧目标的击退力度 +50%。"; break;
                case "FrostMomentum": description = "冰冻概率转为点燃并触发时，攻速 +8%，持续 6 秒，最多叠 5 层。"; break;
                case "FrostRangeBoost": description = "点燃 3 个或更多丧尸时，近战范围 +30%。"; break;
                case "HasteFreeze": description = "移速加成达到 +20% 后，转换点燃概率额外 +15%。"; break;
            }
        }
        return bow ? description.Replace("近战", "攻击").Replace("挥砍", "射击") : description;
    }
    public static string BranchName(string branch) => branch == "Bow" ? "弓箭" : branch == "Quick" ? "快攻" : branch == "Frost" ? "冰冻" : branch == "Fire" ? "燃烧" : "通用";
    public static Color Color(string branch) => branch == "Bow" ? new Color(1f,.82f,.36f) : branch == "Quick" ? new Color(.66f,.93f,.42f) : branch == "Frost" ? new Color(.40f,.80f,1f) : branch == "Fire" ? new Color(1f,.56f,.28f) : ZombieHudTheme.Text;
    public static int Icon(DRRunUpgrade upgrade)
    {
        switch(upgrade.Effect)
        {
            case "DamagePercent": case "ComboDamage": case "BowDamage": return 8;
            case "Range": case "BowPierce": case "BowMultishot": case "BowRange": return 9;
            case "AttackSpeed": case "BowDrawSpeed": return 10;
            case "MoveSpeed": return 11;
            case "MaxHealth": return 12;
            case "DashCooldown": return 13;
            case "DashDistance": return 14;
            case "LifeSteal": return 15;
            case "Knockback": return 9;
            case "FrostfireBurn": case "ThermalShock": case "FrostfireBurst": return 8;
            case "ConvertFreezeToBurn": return 10;
            default: return 1;
        }
    }
}
