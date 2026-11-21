using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

// 编辑器加载时 / 退出运行模式时：竞技场没生成过、或者生成逻辑更新了（版本号变了），就自动重新生成；
// 当前是空白场景就打开竞技场
[InitializeOnLoad]
public static class OpenLevelOnLoad
{
    const string VersionFile = "Library/ArenaBuilderVersion.txt";

    static OpenLevelOnLoad()
    {
        EditorApplication.delayCall += Check;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Check;
        };
    }

    static void Check()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string built = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : "";
        if (!File.Exists(ArenaBuilder.ScenePath) || built != ArenaBuilder.Version.ToString())
        {
            ArenaBuilder.Build();
            File.WriteAllText(VersionFile, ArenaBuilder.Version.ToString());
            return;
        }

        var active = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(active.path) && !active.isDirty)
            EditorSceneManager.OpenScene(ArenaBuilder.ScenePath);
    }
}
