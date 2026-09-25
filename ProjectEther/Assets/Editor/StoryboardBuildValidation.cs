using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StoryboardBuildValidation
{
    static readonly string[] Shaders = { "Assets/Shader/SBInstanced.shader", "Assets/Shader/SBOverlay.shader", "Assets/Shader/SBVideoOverlay.shader", "Assets/Shader/HolographicScreen.shader" };
    public static string BuildAndroidShaders()
    {
        string folder=Path.GetFullPath("Temp/StoryboardAndroidShaders");
        Directory.CreateDirectory(folder);
        var errors=new List<string>();
        Application.LogCallback listener=(message,stack,type)=>
        {
            if(type==LogType.Error && (message.Contains("Shader error") || message.Contains("Build failed"))) errors.Add(message);
        };
        Application.logMessageReceived+=listener;
        try
        {
            var bundle=new AssetBundleBuild { assetBundleName="storyboard-shaders", assetNames=Shaders };
            var result=BuildPipeline.BuildAssetBundles(folder,new[]{bundle},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.Android);
            foreach(var path in Shaders)
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
                foreach(var message in ShaderUtil.GetShaderMessages(shader))
                    if(message.severity.ToString()=="Error") errors.Add(path+": "+message.message+" ("+message.platform+")");
            }
            string report=(result!=null && errors.Count==0 ? "PASS" : "FAIL")+": Android Vulkan/OpenGLES3 storyboard shader compilation; errors="+errors.Count+Environment.NewLine+string.Join(Environment.NewLine,errors);
            File.WriteAllText(Path.Combine(folder,"result.txt"),report);
            return report;
        }
        finally { Application.logMessageReceived-=listener; }
    }

}
