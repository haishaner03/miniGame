using System;
using System.Collections.Generic;
using System.IO;
using Flower;
using GameFramework.DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

[CreateAssetMenu(menuName = "Zombie/Run Config")]
public sealed class ZombieRunConfig : ScriptableObject
{
    [Min(2)] public int roomCount = 5;
    public string[] roomScenePaths;
    public string menuScenePath = "Assets/GameMain/Scenes/Menu/Menu.unity";
    public TextAsset upgradeTable;
    public Font uiFont;
    public GameObject battleUiPrefab;
    public GameObject startingWeaponPickerPrefab;
    [Header("精英与最终 Boss")]
    public GameObject eliteEncounterPrefab;
    public GameObject bossEncounterPrefab;
    [Min(2)] public int eliteRoomNumber = 3;
    [Min(1)] public int elitePreparationKills = 35;
    [Min(1)] public int bossPreparationKills = 25;
    [Min(1)] public int eliteEncounterExperience = 120;
    [Min(1)] public int bossEncounterExperience = 240;
    [Header("经验与升级")]
    public GameObject experiencePickupPrefab;
    public GameObject healthPickupPrefab;
    [Range(0f, 1f)] public float healthDropChance = .05f;
    [Min(1)] public int firstLevelExperience = 80;
    [Min(1)] public int experienceGrowthPerLevel = 30;
    [Min(1)] public int normalExperience = 10;
    [Min(1)] public int eliteExperience = 30;
    [Min(0)] public int experiencePoolPrewarm = 64;
    [Min(0.1f)] public float baseFreezeDuration = 1.8f;
    [Header("街区人口与增援")]
    [Min(1)] public int initialWanderers = 40;
    [Tooltip("第一房间游荡敌人、普通增援和定时大波的人数倍率；区域目标波次不受影响。")]
    [Range(0.1f, 1f)] public float firstRoomPopulationMultiplier = .7f;
    [Tooltip("每次生成普通丧尸时，在其类型速度基础上随机一次；对象池复用会重新抽取。")]
    public Vector2 zombieMoveSpeedMultiplier = new Vector2(.8f, 1.2f);
    [Min(1)] public int maxLivingZombies = 100;
    public Vector2 reinforcementDelay = new Vector2(2f, 4f);
    public Vector2Int reinforcementCount = new Vector2Int(3, 6);
    public Vector2 hordeDelay = new Vector2(12f, 22f);
    public Vector2Int hordeCount = new Vector2Int(15, 25);
    [Min(1)] public int roomKillTarget = 60;
    [Tooltip("第一房间所有丧尸的血量范围，基础攻击 80 可在 1–2 刀击杀。")]
    public Vector2Int firstRoomZombieHealth = new Vector2Int(80, 140);
    [Range(0f, 0.5f)] public float enemiesPerRoom = 0.2f;
    [Range(0f, 0.3f)] public float healthPerRoom = 0.05f;
    [Range(0f, 0.5f)] public float initialVariantChance = 0.1f;
    [Range(0f, 0.2f)] public float variantChancePerRoom = 0.1f;

    public DRRunUpgrade[] LoadUpgrades()
    {
        if (upgradeTable == null)
            throw new InvalidOperationException("Run config has no upgrade table.");
        DataTableComponent component = UnityGameFramework.Runtime.GameEntry.GetComponent<DataTableComponent>();
        IDataTable<DRRunUpgrade> table = component != null ? component.GetDataTable<DRRunUpgrade>() : null;
        if (table != null && table.Count > 0)
            return table.GetAllDataRows();
        if (component != null && table == null)
            table = component.CreateDataTable<DRRunUpgrade>();

        // Direct scene play has no GF bootstrap; use the same DataRow parser there.
        var rows = new List<DRRunUpgrade>();
        var ids = new HashSet<int>();
        using (var reader = new StringReader(upgradeTable.text))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                var row = new DRRunUpgrade();
                row.ParseDataRow(line, null);
                if (!ids.Add(row.Id))
                    throw new FormatException("Duplicate run upgrade: " + row.Id);
                rows.Add(row);
                if (table != null && !((DataTableBase)table).AddDataRow(line, null))
                    throw new FormatException("Could not load run upgrade: " + row.Id);
            }
        }
        if (rows.Count < 3)
            throw new InvalidOperationException("A run needs at least three upgrades.");
        return rows.ToArray();
    }
}
