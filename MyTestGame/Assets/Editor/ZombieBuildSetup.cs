using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ZombieBuildSetup
{
    public const string PickerPath = "Assets/GameMain/UI/Prefabs/StartingWeaponPicker.prefab";
    [MenuItem("Tools/Zombie/Setup Build Choices and Drops")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var config = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
        var root = new GameObject("HealthPickup");
        var sprite = root.AddComponent<SpriteRenderer>();
        sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameMain/UI/Art/ZombieHUD/Icon_Health.png");
        sprite.sortingOrder = 5200;
        root.transform.localScale = Vector3.one * .32f;
        root.AddComponent<HealthPickup>();
        config.healthPickupPrefab = PrefabUtility.SaveAsPrefabAsset(root,"Assets/GameMain/Entity/Drops/HealthPickup.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        config.healthDropChance = .05f;
        var picker = new GameObject("StartingWeaponPicker",typeof(RectTransform));
        picker.AddComponent<StartingWeaponPicker>().Build(config.uiFont,
            AssetDatabase.LoadAssetAtPath<Sprite>(SurvivorWeaponSetup.WeaponFolder+"Machete.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(SurvivorWeaponSetup.WeaponFolder+"IronBar.png"));
        var prefab = PrefabUtility.SaveAsPrefabAsset(picker,PickerPath);
        config.startingWeaponPickerPrefab = prefab;
        UnityEngine.Object.DestroyImmediate(picker);
        foreach (string path in new[] {"Assets/GameMain/UI/Prefabs/ZombieMainMenu.prefab","Assets/GameMain/UI/Prefabs/ZombieChapterSelect.prefab"})
        {
            var content = PrefabUtility.LoadPrefabContents(path);
            foreach (var flow in content.GetComponentsInChildren<ZombieUiFlow>(true)) flow.weaponPickerPrefab = prefab;
            PrefabUtility.SaveAsPrefabAsset(content,path); PrefabUtility.UnloadPrefabContents(content);
        }
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Zombie/Repair Door Interactions")]
    public static void RepairDoorInteractions()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var cfg = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
        foreach (string path in cfg.roomScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var nav = roots.SelectMany(g=>g.GetComponentsInChildren<ZombieGridPathfinder>(true)).First();
                nav.Rebuild(); bool changed = false;
                foreach (var door in roots.SelectMany(g=>g.GetComponentsInChildren<SafeDoor>(true)))
                {
                    Vector2 original = door.transform.position;
                    if (nav.IsWalkablePosition(original)) continue;
                    Vector2 best = original; float bestDistance = float.MaxValue;
                    // Place door triggers on nearby floor; keep building art and collision intact.
                    for(int x=-12;x<=12;x++) for(int y=-12;y<=12;y++)
                    {
                        Vector2 candidate = original + new Vector2(x,y)*.25f;
                        float distance = (candidate-original).sqrMagnitude;
                        if(distance>=bestDistance || !nav.IsWalkablePosition(candidate)) continue;
                        best=candidate;bestDistance=distance;
                    }
                    if (bestDistance==float.MaxValue) throw new Exception("No safe interaction near "+door.name);
                    Undo.RecordObject(door.transform,"Move door to accessible floor");
                    door.transform.position = new Vector3(best.x,best.y,door.transform.position.z);
                    changed = true;
                    Debug.Log(path+" "+door.name+": "+original+" -> "+best);
                }
                if(changed) {EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            }
            finally {EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
