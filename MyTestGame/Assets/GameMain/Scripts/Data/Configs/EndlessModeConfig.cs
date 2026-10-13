using UnityEngine;

[CreateAssetMenu(menuName = "Zombie/Endless Mode Config")]
public sealed class EndlessModeConfig : ScriptableObject
{
    public string scenePath = "Assets/GameMain/Scenes/Level/ZombieLevel99/ZombieLevel99.unity";
    [Header("持续增援：小批次补充，不一次塞满")]
    [Min(0)] public int initialWanderers = 16;
    [Min(1)] public int startingPopulation = 32;
    [Min(0)] public int populationPerMinute = 12;
    [Min(1)] public int maximumPopulation = 220;
    [Min(1)] public int livingLimit = 240;
    public Vector2 reinforcementInterval = new Vector2(3f, .65f);
    [Min(.01f)] public float intervalReductionPerMinute = .18f;
    public Vector2Int batchSize = new Vector2Int(4, 10);
    [Min(1f)] public float hordeInterval = 40f;
    public Vector2Int hordeSize = new Vector2Int(8, 40);
    [Min(0)] public int hordeGrowthPerMinute = 2;
    [Header("普通敌人成长：m 为有效战斗分钟")]
    [Min(1)] public int baseHealth = 110;
    [Min(0f)] public float healthLinear = .12f;
    [Min(0f)] public float healthQuadratic = .012f;
    [Min(0f)] public float speedPerMinute = .025f;
    [Min(1f)] public float maximumSpeedMultiplier = 1.35f;
    [Min(0f)] public float damagePerMinute = .025f;
    [Min(1f)] public float maximumDamageMultiplier = 2f;
    [Range(0f, 1f)] public float initialEliteChance = .05f;
    [Range(0f, 1f)] public float maximumEliteChance = .16f;
    [Min(0f)] public float eliteChancePerMinute = .005f;
    [Min(1f)] public float eliteHealthMultiplier = 2.5f;
    [Range(0f, 1f)] public float initialVariantChance = .1f;
    [Min(0f)] public float variantChancePerMinute = .02f;
    [Min(1f)] public float livingStatsRefreshInterval = 30f;
    [Header("Boss：5 分钟首批，之后每 2 分钟一批")]
    [Min(1f)] public float firstBossSeconds = 300f;
    [Min(1f)] public float bossWaveInterval = 120f;
    [Min(1f)] public float extraBossEverySeconds = 300f;
    [Min(1)] public int maximumBossesPerWave = 4;
    [Min(1)] public int maximumLivingBosses = 6;
    [Min(1)] public int bossBaseHealth = 15000;
    [Min(0f)] public float bossHealthPerMinute = .06f;

    public EndlessDifficulty Evaluate(float elapsedSeconds)
    {
        float minutes = Mathf.Max(0f, elapsedSeconds) / 60f;
        return new EndlessDifficulty
        {
            population = Mathf.Min(Mathf.Max(1, livingLimit), Mathf.Min(Mathf.Max(1, maximumPopulation),
                Mathf.Max(1, startingPopulation) + Mathf.FloorToInt(minutes * populationPerMinute))),
            healthMultiplier = 1f + minutes * healthLinear + minutes * minutes * healthQuadratic,
            speedMultiplier = Mathf.Min(maximumSpeedMultiplier, 1f + minutes * speedPerMinute),
            damageMultiplier = Mathf.Min(maximumDamageMultiplier, 1f + minutes * damagePerMinute),
            eliteChance = Mathf.Min(maximumEliteChance, initialEliteChance + minutes * eliteChancePerMinute),
            variantChance = Mathf.Min(.65f, initialVariantChance + minutes * variantChancePerMinute),
            batch = Mathf.Min(Mathf.Max(1, batchSize.y), Mathf.Max(1, batchSize.x) + Mathf.FloorToInt(minutes / 2f)),
            interval = Mathf.Max(.1f, Mathf.Max(reinforcementInterval.y, reinforcementInterval.x - minutes * intervalReductionPerMinute)),
            hordeCount = Mathf.Min(hordeSize.y, hordeSize.x + Mathf.FloorToInt(minutes * hordeGrowthPerMinute)),
            bossCount = elapsedSeconds < firstBossSeconds ? 0 : Mathf.Min(maximumBossesPerWave,
                1 + Mathf.FloorToInt((elapsedSeconds - firstBossSeconds) / Mathf.Max(1f, extraBossEverySeconds))),
            bossHealthMultiplier = 1f + Mathf.Max(0f, minutes - firstBossSeconds / 60f) * bossHealthPerMinute
        };
    }
}

public struct EndlessDifficulty
{
    public int population, batch, hordeCount, bossCount;
    public float healthMultiplier, speedMultiplier, damageMultiplier, eliteChance, variantChance, interval, bossHealthMultiplier;
}
