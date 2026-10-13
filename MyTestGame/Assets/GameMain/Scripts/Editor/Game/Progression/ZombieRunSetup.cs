using UnityEditor;
using UnityEngine;

public static class ZombieRunSetup
{
    [MenuItem("Tools/Zombie/Setup Roguelite Prototype")]
    public static void Setup()
    {
        const string folder = "Assets/GameMain/Resources";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/GameMain", "Resources");
        const string path = folder + "/ZombieRunConfig.asset";
        ZombieRunConfig config = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ZombieRunConfig>();
            config.roomScenePaths = new[]
            {
                "Assets/GameMain/Scenes/Level/ZombieLevel01.unity",
                "Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity",
                "Assets/GameMain/Scenes/Level/ZombieLevel03/ZombieLevel03.unity",
                "Assets/GameMain/Scenes/Level/ZombieLevel04/ZombieLevel04.unity"
            };
            AssetDatabase.CreateAsset(config, path);
        }
        config.upgradeTable = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameMain/DataTables/RunUpgrade.txt");
        config.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf");
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("Roguelite config ready: " + path);
    }
}
