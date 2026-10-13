using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class RunState
{
    public enum GameMode { Adventure, Endless }
    public GameMode Mode { get; private set; }
    public bool IsEndless => Mode == GameMode.Endless;
    public EndlessModeConfig EndlessConfig => Config.endlessConfig != null ? Config.endlessConfig : Resources.Load<EndlessModeConfig>("EndlessModeConfig");
    public EndlessModeController Endless { get; private set; }
    public int EndlessRank { get; private set; }

    public static void StartEndlessRun(SurvivorWeaponKind weapon = SurvivorWeaponKind.Machete, int? seed = null)
    {
        if (ZombieSceneLoadingScreen.IsLoading) return;
        if (!SurvivorMetaProgress.IsWeaponUnlocked(weapon)) weapon = SurvivorWeaponKind.Machete;
        RunState run = Instance != null ? Instance : new GameObject("ZombieRun").AddComponent<RunState>();
        run.Begin(null, seed, GameMode.Endless);
        run.StartingWeapon = weapon;
        run.LoadBattleRoom(run.EndlessConfig.scenePath);
    }
}
