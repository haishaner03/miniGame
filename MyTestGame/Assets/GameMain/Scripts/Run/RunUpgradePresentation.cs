using Flower;
using UnityEngine;

public static class RunUpgradePresentation
{
    public static string BranchName(string branch) => branch == "Quick" ? "快攻" : branch == "Frost" ? "冰冻" : branch == "Fire" ? "燃烧" : "通用";
    public static Color Color(string branch) => branch == "Quick" ? new Color(.66f,.93f,.42f) : branch == "Frost" ? new Color(.40f,.80f,1f) : branch == "Fire" ? new Color(1f,.56f,.28f) : ZombieHudTheme.Text;
    public static int Icon(DRRunUpgrade upgrade)
    {
        switch(upgrade.Effect)
        {
            case "DamagePercent": case "ComboDamage": return 8;
            case "Range": return 9;
            case "AttackSpeed": return 10;
            case "MoveSpeed": return 11;
            case "MaxHealth": return 12;
            case "DashCooldown": return 13;
            case "DashDistance": return 14;
            case "LifeSteal": return 15;
            case "Knockback": return 9;
            default: return 1;
        }
    }
}
