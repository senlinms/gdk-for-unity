using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Improbable.Gdk.BuildSystem.Configuration;
using Improbable.Gdk.Core;
using Improbable.Gdk.Tools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Improbable.Gdk.BuildSystem
{
    [InitializeOnLoad]
    public static class WorkerBuilder
    {
        private static readonly string PlayerBuildDirectory =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), EditorPaths.AssetDatabaseDirectory,
                "worker"));

        private static readonly string AssetDatabaseDirectory =
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), EditorPaths.AssetDatabaseDirectory));

        private const string BuildWorkerTypes = "buildWorkerTypes";

        static WorkerBuilder()
        {
            BuildWorkerMenu.MenuBuildLocal = workerTypes => MenuBuild(BuildEnvironment.Local, workerTypes);
            BuildWorkerMenu.MenuBuildCloud = workerTypes => MenuBuild(BuildEnvironment.Cloud, workerTypes);
            BuildWorkerMenu.MenuCleanAll = () =>
            {
                Clean();
                Debug.Log("Clean completed.");
            };
        }

        /// <summary>
        ///     Build method that is invoked by commandline
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static void Build()
        {
            try
            {
                var commandLine = Environment.GetCommandLineArgs();
                var buildTargetArg = CommandLineUtility.GetCommandLineValue(commandLine, "buildTarget", "local");

                BuildEnvironment buildEnvironment;
                switch (buildTargetArg.ToLower())
                {
                    case "cloud":
                        buildEnvironment = BuildEnvironment.Cloud;
                        break;
                    case "local":
                        buildEnvironment = BuildEnvironment.Local;
                        break;
                    default:
                        throw new BuildFailedException("Unknown build target value: " + buildTargetArg);
                }

                var workerTypesArg =
                    CommandLineUtility.GetCommandLineValue(commandLine, BuildWorkerTypes,
                        "UnityClient,UnityGameLogic");
                var wantedWorkerTypes = workerTypesArg.Split(',');

                ScriptingImplementation scriptingBackend;
                var wantedScriptingBackend =
                    CommandLineUtility.GetCommandLineValue(commandLine, "scriptingBackend", "mono");
                switch (wantedScriptingBackend)
                {
                    case "mono":
                        scriptingBackend = ScriptingImplementation.Mono2x;
                        break;
                    case "il2cpp":
                        scriptingBackend = ScriptingImplementation.IL2CPP;
                        break;
                    default:
                        throw new BuildFailedException("Unknown scripting backend value: " + wantedScriptingBackend);
                }

                LocalLaunch.BuildConfig();

                var workerResults = new Dictionary<string, bool>();
                foreach (var wantedWorkerType in wantedWorkerTypes)
                {
                    var result = BuildWorkerForEnvironment(wantedWorkerType, buildEnvironment, scriptingBackend);
                    workerResults[wantedWorkerType] = result;
                }

                var missingWorkerTypes = string.Join(" ", workerResults.Keys.Where(k => workerResults[k]));
                var completedWorkerTypes = string.Join(" ", workerResults.Keys.Where(k => !workerResults[k]));
                Debug.LogWarning(
                    $"Completed build for {buildEnvironment} target.\n"
                    + $"Completed builds for: {completedWorkerTypes}\n"
                    + $"Skipped builds for: {missingWorkerTypes}. See above for more information.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (e is BuildFailedException)
                {
                    throw;
                }

                throw new BuildFailedException(e);
            }
        }

        private static void MenuBuild(BuildEnvironment environment, params string[] workerTypes)
        {
            // Delaying build by a frame to ensure the editor has re-rendered the UI to avoid odd glitches.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    LocalLaunch.BuildConfig();

                    foreach (var workerType in workerTypes)
                    {
                        BuildWorkerForEnvironment(workerType, environment);
                    }

                    Debug.LogFormat("Completed build for {0} target", environment);
                }
                catch (Exception)
                {
                    DisplayBuildFailureDialog();

                    throw;
                }
            };
        }

        private static void DisplayBuildFailureDialog()
        {
            EditorUtility.DisplayDialog("Build Failed",
                "Build failed. Please see the Unity Console Window for information.",
                "OK");
        }        

        internal static bool BuildWorkerForEnvironment(string workerType, BuildEnvironment targetEnvironment, ScriptingImplementation? scriptingBackend = null)
        {
            var spatialOSBuildConfiguration = BuildConfig.GetInstance();
            var environmentConfig = spatialOSBuildConfiguration.GetEnvironmentConfigForWorker(workerType, targetEnvironment);
            if (environmentConfig == null || environmentConfig.BuildTargets.Count == 0)
            {
                Debug.LogWarning($"Skipping build for {workerType}.");
                return false;
            }

            if (!Directory.Exists(PlayerBuildDirectory))
            {
                Directory.CreateDirectory(PlayerBuildDirectory);
            }

            foreach (var config in environmentConfig.BuildTargets)
            {
                var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(config.Target);
                var activeScriptingBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup);
                try
                {
                    if (scriptingBackend != null)
                    {
                        Debug.Log($"Setting scripting backend to {scriptingBackend.Value}");
                        PlayerSettings.SetScriptingBackend(buildTargetGroup, scriptingBackend.Value);
                    }

                    BuildWorkerForTarget(workerType, config.Target, config.Options, targetEnvironment);
                }
                catch (Exception e)
                {
                    throw new BuildFailedException(e);
                }
                finally
                {
                    PlayerSettings.SetScriptingBackend(buildTargetGroup, activeScriptingBackend);
                }
            }

            return true;
        }

        public static void Clean()
        {
            if (Directory.Exists(AssetDatabaseDirectory))
            {
                Directory.Delete(AssetDatabaseDirectory, true);
            }

            if (Directory.Exists(EditorPaths.BuildScratchDirectory))
            {
                Directory.Delete(EditorPaths.BuildScratchDirectory, true);
            }
        }


        private static void BuildWorkerForTarget(string workerType, BuildTarget buildTarget,
            BuildOptions buildOptions, BuildEnvironment targetEnvironment)
        {
            if (!WorkerBuildData.BuildTargetsThatCanBeBuilt[buildTarget])
            {
                Debug.LogWarning($"Skipping {buildTarget} because support is not installed in the Unity Editor.");
                return;
            }

            Debug.Log(
                $"Building \"{buildTarget}\" for worker platform: \"{workerType}\", environment: \"{targetEnvironment}\"");

            var spatialOSBuildConfiguration = BuildConfig.GetInstance();
            var workerBuildData = new WorkerBuildData(workerType, buildTarget);
            var scenes = spatialOSBuildConfiguration.GetScenePathsForWorker(workerType);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                options = buildOptions,
                target = buildTarget,
                scenes = scenes,
                locationPathName = workerBuildData.BuildScratchDirectory
            };

            var result = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (result.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Build failed for {workerType}");
            }

            if (buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iOS)
            {
                // Mobile clients can only be run locally, no need to package them
                return;
            }

            var zipPath = Path.Combine(PlayerBuildDirectory, workerBuildData.PackageName);
            var basePath = Path.Combine(EditorPaths.BuildScratchDirectory, workerBuildData.PackageName);
            Zip(zipPath, basePath, targetEnvironment == BuildEnvironment.Cloud);
        }

        private static void Zip(string zipAbsolutePath, string basePath, bool useCompression)
        {
            using (new ShowProgressBarScope($"Package {basePath}"))
            {
                RedirectedProcess.Command(Common.SpatialBinary)
                    .WithArgs("file", "zip", $"--output=\"{Path.GetFullPath(zipAbsolutePath)}\"",
                        $"--basePath=\"{Path.GetFullPath(basePath)}\"", "\"**\"",
                        $"--compression={useCompression}")
                    .Run();
            }
        }
    }
}
