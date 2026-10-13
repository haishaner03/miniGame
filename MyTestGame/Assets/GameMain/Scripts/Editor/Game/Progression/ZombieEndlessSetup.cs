#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bakes editable menu buttons, leaderboard prefab and the endless config.</summary>
public static class ZombieEndlessSetup
{
    public static void Apply()
    {
        const string configPath = "Assets/GameMain/Resources/EndlessModeConfig.asset";
        var endless = AssetDatabase.LoadAssetAtPath<EndlessModeConfig>(configPath);
        if (endless == null)
        {
            endless = ScriptableObject.CreateInstance<EndlessModeConfig>();
            AssetDatabase.CreateAsset(endless, configPath);
        }
        var run = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        run.endlessConfig = endless; EditorUtility.SetDirty(run);
        const string boardPath = "Assets/GameMain/Prefabs/UI/Screens/EndlessLeaderboard.prefab";
        GameObject board = AssetDatabase.LoadAssetAtPath<GameObject>(boardPath);
        // Rebuild this generated panel while preserving the prefab asset's GUID.
        {
            var root = new GameObject("EndlessLeaderboard", typeof(RectTransform));
            root.AddComponent<EndlessLeaderboardUI>().Build(run.uiFont);
            root.SetActive(false);
            board = PrefabUtility.SaveAsPrefabAsset(root, boardPath);
            Object.DestroyImmediate(root);
        }
        string menuPath = "Assets/GameMain/Prefabs/UI/Screens/ZombieMainMenu.prefab";
        GameObject menu = PrefabUtility.LoadPrefabContents(menuPath);
        try
        {
            var flow = menu.GetComponent<ZombieUiFlow>();
            if (flow.endlessButton == null) flow.endlessButton = Duplicate(flow.startButton, "EndlessButton", "无尽模式");
            if (flow.leaderboardButton == null) flow.leaderboardButton = Duplicate(flow.chapterButton, "LeaderboardButton", "无尽生存榜");
            flow.leaderboardPrefab = board;
            Button[] buttons = { flow.startButton, flow.endlessButton, flow.chapterButton, flow.leaderboardButton,
                flow.growthButton, flow.optionsButton, flow.quitButton };
            flow.startButton.GetComponentInChildren<Text>().text = "闯关模式";
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].transform as RectTransform;
                rect.anchoredPosition = new Vector2(70, -292 - i * 82);
                rect.sizeDelta = new Vector2(540, 66);
                var label = buttons[i].GetComponentInChildren<Text>(); label.fontSize = 27;
            }
            PrefabUtility.SaveAsPrefabAsset(menu, menuPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(menu); }
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(s => s.path == endless.scenePath);
        if (index < 0) scenes.Add(new EditorBuildSettingsScene(endless.scenePath, true));
        else scenes[index] = new EditorBuildSettingsScene(endless.scenePath, true);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }
    private static Button Duplicate(Button source, string name, string caption)
    {
        var button = Object.Instantiate(source, source.transform.parent);
        button.name = name;
        button.GetComponentInChildren<Text>().text = caption;
        return button;
    }
}
#endif
