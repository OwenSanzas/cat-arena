using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 菜单：Tools > 打包游戏（发给朋友）
// 打包成 Windows 程序，并压缩成一个 zip，放在项目的 Builds 文件夹
public static class GameExporter
{
    const string OutDir = "Builds/CatArena";
    const string ZipPath = "Builds/CatArena.zip";

    [MenuItem("Tools/打包游戏（发给朋友）")]
    public static void Export()
    {
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        if (File.Exists(ZipPath)) File.Delete(ZipPath);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ArenaBuilder.ScenePath },
            locationPathName = OutDir + "/CatArena.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("打包失败", "打包没成功，看一下 Console 窗口里的红色报错。", "好");
            return;
        }

        // 调试用的文件夹不用发给朋友
        foreach (var d in Directory.GetDirectories(OutDir, "*DoNotShip*")) Directory.Delete(d, true);

        // 用系统自带的 PowerShell 压缩成 zip
        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
            $"-NoProfile -Command \"Compress-Archive -Path '{Path.GetFullPath(OutDir)}\\*' -DestinationPath '{Path.GetFullPath(ZipPath)}' -Force\"")
        { UseShellExecute = false, CreateNoWindow = true };
        using (var p = System.Diagnostics.Process.Start(psi)) p.WaitForExit();
        if (!File.Exists(ZipPath))
        {
            EditorUtility.RevealInFinder(OutDir + "/CatArena.exe");
            EditorUtility.DisplayDialog("打包完成（没压缩）", "游戏已打包到 Builds/CatArena 文件夹，但自动压缩失败。\n右键这个文件夹 → 压缩为 ZIP 文件，再发给朋友。", "好");
            return;
        }
        long mb = new FileInfo(ZipPath).Length / (1024 * 1024);
        Debug.Log($"[打包] 完成：{Path.GetFullPath(ZipPath)}（{mb} MB）");
        EditorUtility.RevealInFinder(ZipPath);
        EditorUtility.DisplayDialog("打包完成",
            $"已生成 CatArena.zip（约 {mb} MB），文件夹已经打开。\n\n把这个 zip 发给朋友：解压后双击 CatArena.exe 就能玩。", "好");
    }
}
