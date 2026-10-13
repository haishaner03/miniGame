using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Local survival records. Submit once per run; persist only the top six.</summary>
public static class EndlessLeaderboard
{
    public const int Capacity = 6;
    public const string StorageKey = "ZombieSurvivor.EndlessLeaderboard.v1";
    [Serializable] public sealed class Entry
    {
        public string runId;
        public SurvivorWeaponKind weapon;
        public float seconds;
        public int kills, bossKills, seed;
        public string utc;
    }
    [Serializable] private sealed class SaveData
    {
        public List<Entry> entries = new List<Entry>();
        public string lastSubmittedRun;
    }

    public static IReadOnlyList<Entry> Load() => Read().entries;

    public static int Submit(string runId, SurvivorWeaponKind weapon, float seconds, int kills, int bossKills, int seed)
    {
        if (string.IsNullOrEmpty(runId) || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return 0;
        SaveData data = Read();
        if (data.lastSubmittedRun == runId || data.entries.Exists(e => e.runId == runId))
            return data.entries.FindIndex(e => e.runId == runId) + 1;
        data.lastSubmittedRun = runId;
        data.entries.Add(new Entry { runId = runId, weapon = weapon, seconds = seconds,
            kills = Mathf.Max(0, kills), bossKills = Mathf.Max(0, bossKills), seed = seed,
            utc = DateTime.UtcNow.ToString("o") });
        data.entries.Sort(Compare);
        if (data.entries.Count > Capacity) data.entries.RemoveRange(Capacity, data.entries.Count - Capacity);
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        return data.entries.FindIndex(e => e.runId == runId) + 1;
    }

    private static SaveData Read()
    {
        SaveData data = null;
        try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(StorageKey, "")); }
        catch (ArgumentException) { }
        if (data == null) data = new SaveData();
        if (data.entries == null) data.entries = new List<Entry>();
        data.entries.RemoveAll(e => e == null || float.IsNaN(e.seconds) || float.IsInfinity(e.seconds) || e.seconds <= 0f);
        data.entries.Sort(Compare);
        if (data.entries.Count > Capacity) data.entries.RemoveRange(Capacity, data.entries.Count - Capacity);
        return data;
    }

    private static int Compare(Entry a, Entry b)
    {
        int order = b.seconds.CompareTo(a.seconds);
        if (order == 0) order = b.kills.CompareTo(a.kills);
        return order != 0 ? order : string.CompareOrdinal(a.utc, b.utc);
    }

    public static string WeaponName(SurvivorWeaponKind weapon) => weapon == SurvivorWeaponKind.Bow ? "猎弓" :
        weapon == SurvivorWeaponKind.Hammer ? "重锤" : weapon == SurvivorWeaponKind.IronBar ? "铁棍" : "砍刀";
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0, seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
