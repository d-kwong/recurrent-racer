using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Racing.Editor
{
    public static class PortfolioBuild
    {
        public static void Build()
        {
            string[] args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--portfolio-build-output");
            string output=index>=0 ? args[index+1] : "Builds/macOS/RacingCurriculum.app";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.runInBackground=true;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new [] {TrainingSceneBuilder.ScenePath},locationPathName=output,
                target=BuildTarget.StandaloneOSX,options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Portfolio build failed: "+report.summary.result);
            Debug.Log("PORTFOLIO_BUILD_SUCCESS: "+output);
        }
    }
}
