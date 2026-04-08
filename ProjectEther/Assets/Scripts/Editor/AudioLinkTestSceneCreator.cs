using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using OsuVR;

namespace OsuVR.Editor
{
    /// <summary>
    /// AudioLink测试场景创建工具
    /// </summary>
    public class AudioLinkTestSceneCreator
    {
        [MenuItem("Tools/Project Ether/创建AudioLink测试场景")]
        public static void CreateTestScene()
        {
            // 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 创建AudioVisualizationSystem
            var audioVisSystem = new GameObject("AudioVisualizationSystem");
            var audioVisManager = audioVisSystem.AddComponent<AudioVisualizationManager>();

            // 创建测试音频源
            var audioSourceObj = new GameObject("TestAudioSource");
            var audioSource = audioSourceObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = true;  // 自动播放
            audioSource.loop = true;

            // 关键：连接AudioSource到AudioVisualizationManager并锁定
            audioVisManager.targetAudioSource = audioSource;
            audioVisManager.lockTargetSource = true;  // 防止自动查找覆盖

            // 创建测试Cube
            var testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = "AudioReactiveCube";
            testCube.transform.position = new Vector3(0, 1, 3);
            testCube.transform.localScale = Vector3.one * 0.5f;

            var vfxDriver = testCube.AddComponent<AudioVFXDriver>();
            vfxDriver.frequencyBand = AudioVFXDriver.FrequencyBand.Bass;
            vfxDriver.driveTarget = AudioVFXDriver.DriveTarget.TransformScale;
            vfxDriver.baseScale = Vector3.one * 0.5f;
            vfxDriver.maxScaleMultiplier = 2f;

            // 创建地面
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);

            // 创建主相机
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObj = new GameObject("Main Camera");
                mainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
                cameraObj.tag = "MainCamera";
            }
            mainCamera.transform.position = new Vector3(0, 2, -5);
            mainCamera.transform.LookAt(testCube.transform);

            // 添加定向光
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 保存场景
            string scenePath = "Assets/Scenes/AudioLinkTest.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[AudioLinkTestScene] ✅ 测试场景已创建: {scenePath}");
            Debug.Log($"[AudioLinkTestScene] 下一步：");
            Debug.Log($"  1. 分配一个音频文件到 TestAudioSource 的 AudioClip 槽位");
            Debug.Log($"  2. （可选）在 AudioVisualizationSystem 上添加 AudioVisualizationDebugger 组件");
            Debug.Log($"  3. （可选）在 TestAudioSource 上添加 SimpleAudioTestController 组件");
            Debug.Log($"  4. 运行场景，观察 Cube 是否随音频缩放");
            Debug.Log($"");
            Debug.Log($"[AudioLinkTestScene] 注意：AudioVisualizationManager 已自动连接到 TestAudioSource");

            // 选中场景
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        }

        [MenuItem("Tools/Project Ether/创建AudioLink测试材质")]
        public static void CreateTestMaterial()
        {
            // 创建测试Shader
            string shaderPath = "Assets/Shaders/AudioLinkTest.shader";
            string shaderCode = @"Shader ""Custom/AudioLinkTest""
{
    Properties
    {
        _BaseColor (""Base Color"", Color) = (0.2, 0.3, 0.5, 1)
        _EmissionIntensity (""Emission Intensity"", Range(0, 5)) = 2
    }

    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        LOD 100

        HLSLINCLUDE
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _EmissionIntensity;
        CBUFFER_END

        // 全局音频变量（来自AudioVisualizationManager）
        float _Global_Audio_Bass;
        float _Global_Audio_Mid;
        float _Global_Audio_Treble;

        // AudioLink纹理（可选）
        TEXTURE2D(_AudioTexture);
        SAMPLER(sampler_AudioTexture);

        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 基础颜色
                float3 baseColor = _BaseColor.rgb;

                // 方式1：使用全局音频变量
                float3 audioColor = float3(
                    _Global_Audio_Bass,  // R: 低频（红色）
                    _Global_Audio_Mid,   // G: 中频（绿色）
                    _Global_Audio_Treble // B: 高频（蓝色）
                );

                // 方式2：采样AudioLink纹理（高级用法）
                // float4 audioLinkData = SAMPLE_TEXTURE2D(_AudioTexture, sampler_AudioTexture, float2(0.5, 0.5));

                // 发光效果
                float3 emission = audioColor * _EmissionIntensity;

                // 最终颜色
                float3 finalColor = baseColor + emission;

                return float4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}";

            System.IO.Directory.CreateDirectory("Assets/Shaders");
            System.IO.File.WriteAllText(shaderPath, shaderCode);
            AssetDatabase.Refresh();

            // 创建材质
            string materialPath = "Assets/Materials/AudioLinkTest.mat";
            System.IO.Directory.CreateDirectory("Assets/Materials");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.2f, 0.3f, 0.5f));
            material.SetFloat("_EmissionIntensity", 2f);

            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AudioLinkTestScene] ✅ 测试Shader和材质已创建");
            Debug.Log($"  Shader: {shaderPath}");
            Debug.Log($"  Material: {materialPath}");
            Debug.Log($"[AudioLinkTestScene] 将材质应用到Cube上，运行游戏观察颜色变化");

            Selection.activeObject = material;
        }
    }
}
