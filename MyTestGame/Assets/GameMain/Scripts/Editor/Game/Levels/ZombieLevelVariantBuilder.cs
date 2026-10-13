#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ZombieLevelVariantBuilder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/GameMain/Scenes/Level/ZombieLevel01.unity",
        "Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity",
        "Assets/GameMain/Scenes/Level/ZombieLevel03/ZombieLevel03.unity",
        "Assets/GameMain/Scenes/Level/ZombieLevel04/ZombieLevel04.unity"
    };

    [MenuItem("Tools/Zombie/Build Level Variants")]
    public static void Build()
    {
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            // Level02 has its own Tilemap layout now (Tools/Zombie/Rebuild Level02); don't move objects there.
            if (i == 1)
                continue;
            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
            ApplyVariant(i);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Built distinct layouts and spawn pacing for ZombieLevel01-04.");
    }

    private static void ApplyVariant(int index)
    {
        ZombieSpawner spawner = Object.FindFirstObjectByType<ZombieSpawner>();
        if (spawner != null)
        {
            spawner.respawnAfterDeath = false;
            spawner.useObjectPool = true;
        }

        switch (index)
        {
            case 0:
                ApplyLevel01(spawner);
                break;
            case 1:
                ApplyLevel02(spawner);
                break;
            case 2:
                ApplyLevel03(spawner);
                break;
            case 3:
                ApplyLevel04(spawner);
                break;
        }
    }

    private static void ApplyLevel01(ZombieSpawner spawner)
    {
        if (spawner == null)
            return;
        spawner.initialSpawnCount = 6;
        spawner.maxAlive = 10;
        spawner.spawnInterval = 2.4f;
        spawner.minSpawnDistance = 5.5f;
        SetSpawnPattern(spawner, new[]
        {
            new Vector2(5f, 12f), new Vector2(16f, 11f), new Vector2(28f, 12f),
            new Vector2(5f, 24f), new Vector2(34f, 25f), new Vector2(8f, 37f),
            new Vector2(18f, 38f), new Vector2(30f, 37f), new Vector2(36f, 18f)
        });
    }

    private static void ApplyLevel02(ZombieSpawner spawner)
    {
        Move("ZombieStreetLayout/Buildings/West_CornerShop", new Vector3(4.5f, 13.2f, 0f), 4f);
        Move("ZombieStreetLayout/Buildings/Market_SouthResidence", new Vector3(13.0f, 17.0f, 0f), -3f);
        Move("ZombieStreetLayout/Buildings/Market_NorthShopA", new Vector3(15.0f, 25.8f, 0f), 2f);
        Move("ZombieStreetLayout/Buildings/East_CornerShop", new Vector3(28.3f, 23.0f, 0f), -5f);
        Move("ZombieStreetLayout/AbandonedVehicles/Parking_Sedan01", new Vector3(21.0f, 10.5f, 0f), 18f);
        Move("ZombieStreetLayout/AbandonedVehicles/Parking_Wreck02", new Vector3(25.0f, 15.2f, 0f), 96f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_0", new Vector3(2.2f, 8.2f, 0f), -8f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_1", new Vector3(36.5f, 18.5f, 0f), 12f);
        if (spawner == null)
            return;
        spawner.initialSpawnCount = 8;
        spawner.maxAlive = 12;
        spawner.spawnInterval = 1.9f;
        spawner.minSpawnDistance = 6.5f;
        SetSpawnPattern(spawner, new[]
        {
            new Vector2(7f, 39f), new Vector2(18f, 38f), new Vector2(31f, 38f),
            new Vector2(6f, 29f), new Vector2(22f, 27f), new Vector2(35f, 26f),
            new Vector2(8f, 16f), new Vector2(20f, 14f), new Vector2(32f, 13f)
        });
    }

    private static void ApplyLevel03(ZombieSpawner spawner)
    {
        Move("ZombieStreetLayout/Buildings/West_ServiceShop", new Vector3(5.0f, 26.5f, 0f), -6f);
        Move("ZombieStreetLayout/Buildings/Market_SouthResidence", new Vector3(15.5f, 13.0f, 0f), 5f);
        Move("ZombieStreetLayout/Buildings/East_Garage", new Vector3(34.0f, 18.0f, 0f), -7f);
        Move("ZombieStreetLayout/Buildings/Market_UpperResidence", new Vector3(13.5f, 36.0f, 0f), 3f);
        Move("ZombieStreetLayout/AbandonedVehicles/Parking_Sedan03", new Vector3(31.0f, 21.0f, 0f), 180f);
        Move("ZombieStreetLayout/AbandonedVehicles/Parking_Wreck02", new Vector3(11.0f, 30.0f, 0f), 248f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_2", new Vector3(37.0f, 31.0f, 0f), 18f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_3", new Vector3(3.0f, 18.0f, 0f), -20f);
        if (spawner == null)
            return;
        spawner.initialSpawnCount = 10;
        spawner.maxAlive = 14;
        spawner.spawnInterval = 1.55f;
        spawner.minSpawnDistance = 7.5f;
        SetSpawnPattern(spawner, new[]
        {
            new Vector2(5f, 8f), new Vector2(16f, 8f), new Vector2(30f, 8f),
            new Vector2(6f, 20f), new Vector2(18f, 22f), new Vector2(34f, 22f),
            new Vector2(5f, 32f), new Vector2(20f, 34f), new Vector2(34f, 34f)
        });
    }

    private static void ApplyLevel04(ZombieSpawner spawner)
    {
        Move("ZombieStreetLayout/Buildings/West_UpperResidence", new Vector3(6.0f, 35.5f, 0f), 6f);
        Move("ZombieStreetLayout/Buildings/North_CornerShop", new Vector3(24.5f, 36.8f, 0f), -4f);
        Move("ZombieStreetLayout/Buildings/East_UpperGarage", new Vector3(34.5f, 31.5f, 0f), 8f);
        Move("ZombieStreetLayout/Buildings/North_RowResidence", new Vector3(13.0f, 40.5f, 0f), -3f);
        Move("ZombieStreetLayout/AbandonedVehicles/Parking_Sedan01", new Vector3(14.0f, 28.5f, 0f), 82f);
        Move("ZombieStreetLayout/AbandonedVehicles/Parking_Sedan03", new Vector3(28.0f, 30.0f, 0f), 268f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_0", new Vector3(3.0f, 12.0f, 0f), 14f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_1", new Vector3(36.0f, 12.0f, 0f), -14f);
        Move("ZombieStreetLayout/PlantingAndDebris/DeadTree_2", new Vector3(3.0f, 30.0f, 0f), 10f);
        if (spawner == null)
            return;
        spawner.initialSpawnCount = 12;
        spawner.maxAlive = 16;
        spawner.spawnInterval = 1.2f;
        spawner.minSpawnDistance = 8.0f;
        SetSpawnPattern(spawner, new[]
        {
            new Vector2(6f, 8f), new Vector2(18f, 8f), new Vector2(32f, 8f),
            new Vector2(5f, 19f), new Vector2(16f, 20f), new Vector2(28f, 19f),
            new Vector2(36f, 22f), new Vector2(6f, 32f), new Vector2(19f, 34f)
        });
    }

    private static void SetSpawnPattern(ZombieSpawner spawner, Vector2[] positions)
    {
        if (spawner.spawnPoints == null)
            return;
        int count = Mathf.Min(spawner.spawnPoints.Length, positions.Length);
        for (int i = 0; i < count; i++)
        {
            if (spawner.spawnPoints[i] != null)
                spawner.spawnPoints[i].position = new Vector3(positions[i].x, positions[i].y, 0f);
        }
    }

    private static void Move(string path, Vector3 position, float zRotation)
    {
        GameObject go = GameObject.Find(path);
        if (go == null)
            return;
        Undo.RecordObject(go.transform, "Build zombie level variant");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        EditorUtility.SetDirty(go.transform);
    }
}
#endif
