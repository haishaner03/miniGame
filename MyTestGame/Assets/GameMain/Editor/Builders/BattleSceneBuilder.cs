using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameMain.Editor
{
    [InitializeOnLoad]
    public static class BattleSceneBuilder
    {
        private const string SourceScenePath = "Assets/GameMain/LearningDemos/WaveOfTheFist/Scenes/SampleScene.unity";
        private const string BattleScenePath = "Assets/GameMain/Scenes/Battle.unity";
        private const string LaunchScenePath = "Assets/GameMain/Scenes/Launch.unity";

        static BattleSceneBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            EditorApplication.delayCall -= BuildIfMissing;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(BattleScenePath))
            {
                Build();
            }

            AddSceneToBuildSettings(LaunchScenePath);
            AddSceneToBuildSettings(BattleScenePath);
        }

        [MenuItem("GameMain/Build Scenes/Battle From WaveOfTheFist")]
        public static void Build()
        {
            string sourceScenePath = GetSourceScenePath();
            if (string.IsNullOrEmpty(sourceScenePath))
            {
                Debug.LogError("[UGF] WaveOfTheFist scene is missing.");
                return;
            }

            Directory.CreateDirectory("Assets/GameMain/Scenes");
            File.Copy(sourceScenePath, BattleScenePath, true);

            AssetDatabase.ImportAsset(BattleScenePath);
            AddSceneToBuildSettings(LaunchScenePath);
            AddSceneToBuildSettings(BattleScenePath);
            Debug.Log("[UGF] Synced battle scene from WaveOfTheFist: " + BattleScenePath);
        }

        private static string GetSourceScenePath()
        {
            if (File.Exists(SourceScenePath))
            {
                return SourceScenePath;
            }

            return null;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                return;
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updatedScenes, 0);
            updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updatedScenes;
        }
    }
}
