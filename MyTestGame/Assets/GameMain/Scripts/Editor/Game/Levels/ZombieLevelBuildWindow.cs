#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>商业街关卡构造器：输入编号，预览目标路径，再创建或重建场景。</summary>
public sealed class ZombieLevelBuildWindow : EditorWindow
{
    [SerializeField] private string levelNumber = "02";
    [SerializeField] private int version;
    [SerializeField] private int randomSeed = 20261012;
    [SerializeField] private bool useNewSeed = true;
    [SerializeField] private string resultMessage;
    [SerializeField] private bool resultIsError;
    private Vector2 scroll;

    internal static void Open(int number)
    {
        var window = GetWindow<ZombieLevelBuildWindow>(true, "构造商业街关卡", true);
        window.levelNumber = number.ToString("00");
        window.resultMessage = string.Empty;
        window.minSize = new Vector2(590f, 450f);
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("构造关卡几？", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("保存原图为默认版本，或生成可复现、路线连通的随机街区。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(8f);

        EditorGUI.BeginChangeCheck();
        GUI.SetNextControlName("LevelNumber");
        levelNumber = EditorGUILayout.TextField("关卡编号（1–99）", levelNumber);
        if (EditorGUI.EndChangeCheck())
            resultMessage = string.Empty;
        bool valid = ZombieLevel02Builder.TryParseLevelNumber(levelNumber, out int number);
        bool busy = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
        bool exists = false;

        if (valid)
        {
            string path = ZombieLevel02Builder.GetScenePath(number);
            exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
            EditorGUILayout.LabelField("目标场景", $"ZombieLevel{number:00}");
            EditorGUILayout.SelectableLabel(path, EditorStyles.wordWrappedLabel, GUILayout.Height(36f));
            EditorGUILayout.HelpBox(exists
                ? $"将覆盖此关卡的地图布局，重建前自动备份到 Backups/ZombieLevel{number:00}/。"
                : "将复用商业街的游戏组件新建此编号的场景。", MessageType.Info);
        }
        else
            EditorGUILayout.HelpBox("请输入 1–99 的整数，例如 2 或 02。", MessageType.Warning);

        bool hasDefault = valid && AssetDatabase.LoadAssetAtPath<SceneAsset>(ZombieLevel02Builder.GetDefaultScenePath(number)) != null;
        bool currentTarget = valid && SceneManager.GetActiveScene().path == ZombieLevel02Builder.GetScenePath(number);
        version = EditorGUILayout.Popup("地图版本", version, new[] { "默认版本（保存的原图）", "商业街模板" });
        EditorGUILayout.LabelField("默认版本状态", hasDefault ? "已保存，可恢复" : "尚未保存");
        using (new EditorGUI.DisabledScope(!currentTarget || busy))
            if (GUILayout.Button(hasDefault ? "更新默认版本为当前地图" : "将当前地图保存为默认版本", GUILayout.Height(28f)))
                SaveDefault(number);
        if (!currentTarget)
            EditorGUILayout.LabelField("保存默认版本时，请先打开上方编号对应的场景。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(!valid || busy || (version == 0 && !hasDefault)))
            if (GUILayout.Button(version == 0 ? "恢复默认版本" : (exists ? "按商业街模板重建" : "按商业街模板新建"), GUILayout.Height(30f)))
                Build(number, false);

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("随机地图", EditorStyles.boldLabel);
        randomSeed = EditorGUILayout.IntField("随机种子", randomSeed);
        useNewSeed = EditorGUILayout.Toggle("每次使用新种子", useNewSeed);
        EditorGUILayout.HelpBox("随机改变街道、建筑、停车区、树木、杂物和刷怪点。保留出入口，检查真实碰撞及所有刷怪点的连通性，通过后才保存。关闭新种子可复现同一布局。", MessageType.Info);
        using (new EditorGUI.DisabledScope(!valid || busy))
            if (GUILayout.Button("生成随机地图", GUILayout.Height(34f)))
                Build(number, true);
        EditorGUILayout.HelpBox("草地和杂草没有碰撞；房屋、树干和废车保留碰撞。地图四周由独立边界框限制。99 号关卡用于无尽模式。", MessageType.Info);
        using (new EditorGUI.DisabledScope(!currentTarget || busy))
            if (GUILayout.Button("修正当前随机地图的草地碰撞", GUILayout.Height(28f)))
            {
                try
                {
                    int removed = ZombieLevel02Builder.RemoveCurrentRandomGrassCollisions();
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    resultIsError = false;
                    resultMessage = "已移除 " + removed + " 个地面阻挡格，保留实体障碍；地图边界已改为独立碰撞框。";
                }
                catch (System.Exception error) { resultIsError = true; resultMessage = error.Message; }
            }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox("修改地图前自动保留默认版本并备份目标场景。新关卡要参与肉鸽跑局，还需加入 ZombieRunConfig 的 Room Scene Paths。", MessageType.Info);
        if (busy)
            EditorGUILayout.HelpBox("请先停止游戏并等待编译结束。", MessageType.Warning);

        if (!string.IsNullOrEmpty(resultMessage))
            EditorGUILayout.HelpBox(resultMessage, resultIsError ? MessageType.Error : MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private void Build(int number, bool random)
    {
        try
        {
            if (random && useNewSeed) randomSeed = System.BitConverter.ToInt32(System.Guid.NewGuid().ToByteArray(), 0);
            var mode = random ? ZombieLevel02Builder.MapVersion.Random :
                version == 0 ? ZombieLevel02Builder.MapVersion.Default : ZombieLevel02Builder.MapVersion.CommercialStreet;
            string backup = ZombieLevel02Builder.BuildLevel(number, mode, randomSeed);
            resultIsError = false;
            resultMessage = backup == null ? "已取消构造，场景未重建。" :
                $"ZombieLevel{number:00} 已保存并打开。" + (random ? "\n随机种子：" + randomSeed : string.Empty) +
                (string.IsNullOrEmpty(backup) ? string.Empty : "\n备份：" + backup);
        }
        catch (System.Exception exception)
        {
            resultIsError = true;
            resultMessage = "构造失败：" + exception.Message;
            Debug.LogException(exception);
        }
        Repaint();
    }

    private void SaveDefault(int number)
    {
        try
        {
            string path = ZombieLevel02Builder.SaveCurrentAsDefault(number);
            resultIsError = false;
            resultMessage = "默认版本已保存：\n" + path;
        }
        catch (System.Exception exception)
        {
            resultIsError = true;
            resultMessage = "保存失败：" + exception.Message;
            Debug.LogException(exception);
        }
        Repaint();
    }
}
#endif
