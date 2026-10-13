#if UNITY_EDITOR
using UnityEditor;

/// <summary>Retains the existing editor menu entry for the current command-room builder.</summary>
public static class ZombieUiPrefabBuilder
{
    [MenuItem("Tools/Zombie/Build UGUI Main Menu Prefabs")]
    public static void Build() => CommandRoomMenuBuilder.Build();
}
#endif
