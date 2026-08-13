using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LocalPvp.Editor
{
    public static class OnlineWebBuildMenu
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string BuildFolder = "Builds/WebGL";
        private const string ArchivePath = "Builds/ForestArena-Web.zip";
        private const string WebTemplate = "PROJECT:ForestArena";

        [MenuItem("Local PvP/Online/Build Web Version for GitHub Pages")]
        public static void BuildForGitHubPages()
        {
            if (!ConfirmOnlineServicesAreReady())
            {
                return;
            }

            if (!BuildWebVersion())
            {
                return;
            }

            Debug.Log($"Web build is ready for GitHub Pages: {Path.GetFullPath(BuildFolder)}");
            EditorUtility.RevealInFinder(BuildFolder);
        }

        [MenuItem("Local PvP/Online/Build Web Version for itch.io")]
        public static void BuildForItchIo()
        {
            if (!ConfirmOnlineServicesAreReady() || !BuildWebVersion())
            {
                return;
            }

            if (File.Exists(ArchivePath))
            {
                File.Delete(ArchivePath);
            }

            ZipFile.CreateFromDirectory(
                BuildFolder,
                ArchivePath,
                System.IO.Compression.CompressionLevel.Optimal,
                false);
            AssetDatabase.Refresh();
            Debug.Log($"Web build is ready for itch.io: {Path.GetFullPath(ArchivePath)}");
            EditorUtility.RevealInFinder(ArchivePath);
        }

        public static void BuildFromCommandLine()
        {
            if (!BuildWebVersion())
            {
                throw new System.InvalidOperationException("WebGL build failed.");
            }
        }

        private static bool BuildWebVersion()
        {
            Directory.CreateDirectory(BuildFolder);
            PlayerSettings.WebGL.template = WebTemplate;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Web build failed: {report.summary.result}");
                return false;
            }

            File.WriteAllText(Path.Combine(BuildFolder, ".nojekyll"), string.Empty);
            AssetDatabase.Refresh();
            return true;
        }

        private static bool ConfirmOnlineServicesAreReady()
        {
            if (!string.IsNullOrWhiteSpace(CloudProjectSettings.projectId))
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                "Unity Cloud project is not linked",
                "The WebGL build can be created, but online rooms will not work until this Unity project is linked in Edit > Project Settings > Services.",
                "Build anyway",
                "Cancel");
        }
    }
}
