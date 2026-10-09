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
    [Range(0f, 0.5f)] public float enemiesPerRoom = 0.2f;
    [Range(0f, 0.3f)] public float healthPerRoom = 0.1f;
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
