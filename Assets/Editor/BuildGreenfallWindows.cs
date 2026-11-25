using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Burst;

public static class BuildGreenfallWindows
{
    private const string OutputPath = "Build/Greenfall/Greenfall.exe";

    [MenuItem("Tools/Game/Build Greenfall for Windows")]
    private static void Build()
    {
        BuildReport report = BuildPlayerPipeline();
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Greenfall Windows build failed: " + report.summary.result + ".");
            return;
        }

        Debug.Log("Greenfall Windows build created at " + OutputPath + ".");
    }

    public static void BuildFromBatchMode()
    {
        BuildReport report = BuildPlayerPipeline();
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException("Greenfall Windows build failed: " + report.summary.result + ".");
        }

        Debug.Log("Greenfall Windows build created at " + OutputPath + ".");
    }

    private static BuildReport BuildPlayerPipeline()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes are configured in Build Settings.");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildTarget target = BuildTarget.StandaloneWindows64;
        GraphicsDeviceType[] originalGraphicsAPIs = PlayerSettings.GetGraphicsAPIs(target);
        bool originalBurstCompilation = BurstCompiler.Options.EnableBurstCompilation;
        try
        {
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
            BurstCompiler.Options.EnableBurstCompilation = false;
            return BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            BurstCompiler.Options.EnableBurstCompilation = originalBurstCompilation;
            PlayerSettings.SetGraphicsAPIs(target, originalGraphicsAPIs);
        }
    }

    public sealed class GreenfallWindShaderStripper : IPreprocessShaders
    {
        private const string BranchShaderName = "Universal Render Pipeline/Nature/SpeedTreeURP_Branch";
        private const string BarkShaderName = "Universal Render Pipeline/Nature/SpeedTreeURP_Bark";
        private static readonly string[] UnusedBranchKeywords =
        {
            "_WINDQUALITY_FASTEST",
            "_WINDQUALITY_FAST",
            "_WINDQUALITY_BEST",
            "_WINDQUALITY_PALM",
            "BACKFACE_NORMAL_MODE_FLIP",
            "BACKFACE_NORMAL_MODE_NONE"
        };
        private static readonly string[] UnusedBarkKeywords =
        {
            "BACKFACE_NORMAL_MODE_FLIP",
            "EFFECT_BILLBOARD"
        };

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null)
            {
                return;
            }

            string[] unusedKeywords;
            if (string.Equals(shader.name, BranchShaderName, StringComparison.Ordinal))
            {
                unusedKeywords = UnusedBranchKeywords;
            }
            else if (string.Equals(shader.name, BarkShaderName, StringComparison.Ordinal))
            {
                unusedKeywords = UnusedBarkKeywords;
            }
            else
            {
                return;
            }

            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                ShaderKeywordSet keywords = data[i].shaderKeywordSet;
                bool usesUnusedVariant = false;
                foreach (string keywordName in unusedKeywords)
                {
                    if (keywords.IsEnabled(new ShaderKeyword(shader, keywordName)))
                    {
                        usesUnusedVariant = true;
                        break;
                    }
                }

                if (usesUnusedVariant)
                {
                    data.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log("Greenfall stripped " + removed + " unused SpeedTree variants from " +
                          snippet.passName + ".");
            }
        }
    }
}
