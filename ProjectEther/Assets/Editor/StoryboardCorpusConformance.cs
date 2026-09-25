using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OsuVR.Storyboard;
using OsuVR.Storyboard.Engine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StoryboardCorpusConformance
{
    public static string Run(string songsDirectory)
    {
        if(StoryboardRenderer.Instance!=null)throw new InvalidOperationException("Run with an idle Edit Mode editor");
        var scene=EditorSceneManager.NewPreviewScene();
        var go=new GameObject("Storyboard corpus regression");
        SceneManager.MoveGameObjectToScene(go,scene);
        var renderer=go.AddComponent<StoryboardRenderer>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if(StoryboardRenderer.Instance==null)typeof(StoryboardRenderer).GetMethod("Awake",flags).Invoke(renderer,null);
        var output=new List<string>();var warnings=new List<string>();var errors=new List<string>();
        Application.LogCallback log=(message,stack,type)=>
        {
            if(type==LogType.Error || type==LogType.Exception)errors.Add(message);
            if(type==LogType.Warning && (message.Contains("[SBParser]")||message.Contains("[SBRenderer]")))warnings.Add(message);
        };
        Application.logMessageReceived+=log;
        try
        {
            foreach(string path in Directory.GetFiles(songsDirectory,"*.osb",SearchOption.AllDirectories))
            {
                warnings.Clear();errors.Clear();
                var source=StoryboardParser.ParseFile(path);
                renderer.LoadStoryboard(source,Path.GetDirectoryName(path),true);
                var timeline=(SBFlatTimelineData)typeof(StoryboardRenderer).GetField("_flatTimeline",flags).GetValue(renderer);
                double first=double.MaxValue,last=0;
                for(int i=0;i<timeline.SpriteCount;i++)
                {
                    var sprite=timeline.Sprites[i];
                    if(sprite.EndTime<=sprite.StartTime)continue;
                    first=Math.Min(first,sprite.StartTime);last=Math.Max(last,sprite.EndTime);
                }
                if(first==double.MaxValue)first=0;
                foreach(double fraction in new[]{0.01,0.25,0.5,0.75,0.99})
                    renderer.RenderAtTime(first+(last-first)*fraction);
                var textures=(System.Collections.ICollection)typeof(StoryboardRenderer).GetField("storyboardTextures",flags).GetValue(renderer);
                output.Add(Path.GetFileName(path)+": sprites="+timeline.SpriteCount+", textures="+textures.Count+", frames=5, warnings="+warnings.Count+", errors="+errors.Count);
                output.AddRange(warnings);output.AddRange(errors);
                if(errors.Count>0)throw new Exception(string.Join(Environment.NewLine,errors));
                renderer.UnloadAll();
            }
            string result=string.Join(Environment.NewLine,output);
            Directory.CreateDirectory("Temp/StoryboardValidation");
            File.WriteAllText("Temp/StoryboardValidation/corpus-results.txt",result);
            return result;
        }
        finally
        {
            Application.logMessageReceived-=log;
            UnityEngine.Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
