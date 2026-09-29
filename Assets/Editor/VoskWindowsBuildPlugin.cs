using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Guarantees that the native Vosk library and its dependencies ship with Windows builds.</summary>
public sealed class VoskWindowsBuildPlugin : IPostprocessBuildWithReport
{
    private const string SourceFolder = "Assets/Plugins/vosk-unity-asr-master/Assets/ThirdParty/Vosk/Plugins/Windows";
    private static readonly string[] LibraryNames =
    {
        "libvosk.dll", "libstdc++-6.dll", "libgcc_s_seh-1.dll", "libwinpthread-1.dll"
    };

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64) return;

        string executable = report.summary.outputPath;
        string destination = Path.Combine(Path.GetDirectoryName(executable),
            Path.GetFileNameWithoutExtension(executable) + "_Data", "Plugins", "x86_64");
        Directory.CreateDirectory(destination);
        foreach (string library in LibraryNames)
        {
            string source = Path.Combine(Path.GetDirectoryName(Application.dataPath), SourceFolder, library);
            if (!File.Exists(source))
                throw new BuildFailedException("Отсутствует нативная библиотека Vosk: " + source);
            File.Copy(source, Path.Combine(destination, library), true);
        }
    }
}
