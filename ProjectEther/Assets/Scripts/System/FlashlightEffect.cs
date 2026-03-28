using UnityEngine;
using System.Collections.Generic;

namespace OsuVR
{
    /// <summary>
    /// Flashlight (FL) Mod 视觉效果控制器
    /// 创建一个位于摄像机前方的遮罩，根据左右手射线的朝向，在游戏平面上打出"手电筒"区域
    /// </summary>
    public class FlashlightEffect : MonoBehaviour
    {
        private Material flashlightMat;
        private GameObject maskQuad;
        private RayController leftRay;
        private RayController rightRay;

        // 默认平面Z坐标，对应 CoordinateMapper 中的 TargetDistance
        private const float DefaultPlaneZ = 2.0f;

        public void Initialize(RhythmGameManager manager)
        {
            // 如果没开启 FL，则不需要初始化
            if (manager == null || manager.GetModEffects() == null || !manager.GetModEffects().IsFlashlight)
            {
                this.enabled = false;
                return;
            }

            // 查找 Shader
            Shader shader = Shader.Find("OsuVR/FlashlightMask");
            if (shader == null)
            {
                Debug.LogError("Flashlight Shader 'OsuVR/FlashlightMask' not found!");
                this.enabled = false;
                return;
            }

            flashlightMat = new Material(shader);
            
            // 调整遮罩颜色，避免纯黑死黑
            // osu! 原版在较低 combo 时会有环境微光
            // 这里用 0.98 的 alpha 使得背景不会完全黑死，隐约能感觉到一点空间
            flashlightMat.SetColor("_Color", new Color(0.0f, 0.0f, 0.0f, 0.99f));

            // 设置手电筒的半径和边缘羽化
            // 稍微调大一点点让体验在 VR 里不至于太挣扎
            flashlightMat.SetFloat("_Radius", 0.5f); 
            flashlightMat.SetFloat("_Feather", 0.2f);
            flashlightMat.SetFloat("_PlaneZ", DefaultPlaneZ);

            // 获取摄像机
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("Main Camera not found for Flashlight effect!");
                this.enabled = false;
                return;
            }

            // [新增优化] 让 ScoreManager 的 UI 在 FL 模式下自发光，使其能在黯淡的背景下易于看清
            var scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager != null && scoreManager.boardController != null)
            {
                var texts = scoreManager.boardController.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    t.color = new Color(t.color.r * 2.5f, t.color.g * 2.5f, t.color.b * 2.5f, t.color.a);
                }
            }

            // 创建遮罩 Quad
            maskQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            maskQuad.name = "FlashlightMask";
            
            // 移除不需要的碰撞体
            Destroy(maskQuad.GetComponent<Collider>());
            
            // 将其附着在摄像机上
            maskQuad.transform.SetParent(mainCam.transform);
            
            // 放置在摄像机正前方非常近的位置 (0.1米)，并缩放以覆盖整个视野
            maskQuad.transform.localPosition = new Vector3(0, 0, 0.1f);
            maskQuad.transform.localRotation = Quaternion.identity;
            maskQuad.transform.localScale = new Vector3(10f, 10f, 1f); // 足够大以覆盖周边视野
            
            // 应用材质
            MeshRenderer renderer = maskQuad.GetComponent<MeshRenderer>();
            renderer.material = flashlightMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // 寻找左右手射线控制器
            FindRayControllers();
        }

        private void FindRayControllers()
        {
            var rays = FindObjectsOfType<RayController>();
            foreach (var r in rays)
            {
                if (r.isRightHand) rightRay = r;
                else leftRay = r;
            }
        }

        void Update()
        {
            if (flashlightMat == null) return;

            // 如果射线中途丢失，尝试重新寻找
            if (leftRay == null || rightRay == null)
            {
                FindRayControllers();
            }

            // 传递左手射线信息
            if (leftRay != null && leftRay.visualRay != null)
            {
                flashlightMat.SetVector("_LeftRayOrigin", leftRay.visualRay.position);
                flashlightMat.SetVector("_LeftRayDir", leftRay.visualRay.forward);
            }
            else
            {
                // 如果没有找到左手，将其方向设为后方，这样就不会在屏幕上画出光圈
                flashlightMat.SetVector("_LeftRayDir", Vector3.back); 
            }

            // 传递右手射线信息
            if (rightRay != null && rightRay.visualRay != null)
            {
                flashlightMat.SetVector("_RightRayOrigin", rightRay.visualRay.position);
                flashlightMat.SetVector("_RightRayDir", rightRay.visualRay.forward);
            }
            else
            {
                // 如果没有找到右手，将其方向设为后方
                flashlightMat.SetVector("_RightRayDir", Vector3.back);
            }
        }

        void OnDestroy()
        {
            // 清理动态创建的对象
            if (maskQuad != null)
            {
                Destroy(maskQuad);
            }
            if (flashlightMat != null)
            {
                Destroy(flashlightMat);
            }
        }
    }
}