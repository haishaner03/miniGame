using UnityEngine;

/// <summary>Small persistent meta-progression store for the main menu.</summary>
public static class SurvivorMetaProgress
{
    private const string PointsKey = "ZombieSurvivor.Points";
    private const string BowKey = "ZombieSurvivor.Unlock.Bow";
    private const string HammerKey = "ZombieSurvivor.Unlock.Hammer";
    private const string BowCardsKey = "ZombieSurvivor.Unlock.BowCards";
    private const string VitalityKey = "ZombieSurvivor.Unlock.Vitality";

    public const int BowCost = 120;
    public const int HammerCost = 90;
    public const int BowCardsCost = 140;
    public const int VitalityCost = 80;

    public static int Points => PlayerPrefs.GetInt(PointsKey, 0);
    public static bool BowUnlocked => PlayerPrefs.GetInt(BowKey, 0) == 1;
    public static bool HammerUnlocked => PlayerPrefs.GetInt(HammerKey, 0) == 1;
    public static bool BowCardsUnlocked => PlayerPrefs.GetInt(BowCardsKey, 0) == 1;
    public static bool VitalityUnlocked => PlayerPrefs.GetInt(VitalityKey, 0) == 1;
    public static float StartingHealthMultiplier => VitalityUnlocked ? 1.05f : 1f;

    public static int AddPoints(int amount)
    {
        if (amount <= 0) return 0;
        int total = Points + amount;
        PlayerPrefs.SetInt(PointsKey, total);
        PlayerPrefs.Save();
        return total;
    }

    public static bool TryUnlockBow() { return Spend(BowCost, BowKey); }
    public static bool TryUnlockHammer() { return Spend(HammerCost, HammerKey); }
    public static bool TryUnlockBowCards() { return BowUnlocked && Spend(BowCardsCost, BowCardsKey); }
    public static bool TryBuyVitality() { return Spend(VitalityCost, VitalityKey); }

    public static bool IsWeaponUnlocked(SurvivorWeaponKind kind) =>
        kind != SurvivorWeaponKind.Bow && kind != SurvivorWeaponKind.Hammer ||
        kind == SurvivorWeaponKind.Bow && BowUnlocked || kind == SurvivorWeaponKind.Hammer && HammerUnlocked;

    public static int CalculateReward(int clearedRooms, int kills, bool victory) =>
        Mathf.Max(0, clearedRooms) * 20 + Mathf.Max(0, kills) / 5 + (victory ? 50 : 0);

    public static int SettleRun(string runId, int clearedRooms, int kills, bool victory)
    {
        if (string.IsNullOrEmpty(runId) || PlayerPrefs.GetString("ZombieSurvivor.LastSettledRun", "") == runId) return 0;
        int reward = CalculateReward(clearedRooms, kills, victory);
        PlayerPrefs.SetInt(PointsKey, Points + reward);
        PlayerPrefs.SetString("ZombieSurvivor.LastSettledRun", runId);
        PlayerPrefs.Save();
        return reward;
    }

    private static bool Spend(int cost, string key)
    {
        if (PlayerPrefs.GetInt(key, 0) == 1) return true;
        if (Points < cost) return false;
        PlayerPrefs.SetInt(PointsKey, Points - cost);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        return true;
    }
}
