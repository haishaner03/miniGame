using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.Editor
{
    [InitializeOnLoad]
    public static class PlayerPrefabBuilder
    {
        private const string BattleScenePath = "Assets/GameMain/Scenes/Battle.unity";
        private const string PlayerPrefabPath = "Assets/GameMain/Prefabs/Characters/Player.prefab";

        static PlayerPrefabBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("GameMain/Build Prefabs/Player From Battle Scene")]
        public static void Build()
        {
            string currentScenePath = SceneManager.GetActiveScene().path;
            bool shouldRestoreScene = !string.IsNullOrEmpty(currentScenePath) && currentScenePath != BattleScenePath;

            Scene battleScene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[UGF] Cannot build Player prefab. Player is missing in scene: " + BattleScenePath);
                RestoreScene(currentScenePath, shouldRestoreScene);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath, out bool success);
            if (!success)
            {
                Debug.LogError("[UGF] Save Player prefab failed: " + PlayerPrefabPath);
                RestoreScene(currentScenePath, shouldRestoreScene);
                return;
            }

            AssetDatabase.ImportAsset(PlayerPrefabPath);
            Debug.Log("[UGF] Built Player prefab from Battle scene: " + PlayerPrefabPath);
            RestoreScene(currentScenePath, shouldRestoreScene && battleScene.IsValid());
        }

        private static void BuildIfMissing()
        {
            EditorApplication.delayCall -= BuildIfMissing;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(PlayerPrefabPath) || !File.Exists(BattleScenePath))
            {
                return;
            }

            Build();
        }

        private static void RestoreScene(string currentScenePath, bool shouldRestoreScene)
        {
            if (!shouldRestoreScene)
            {
                return;
            }

            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }
    }
}
