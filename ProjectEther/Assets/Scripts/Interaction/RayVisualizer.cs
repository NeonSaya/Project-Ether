using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OsuVR
{
    /// <summary>表面实心落点与短促命中脉冲；只读取命中结果，不改变射线判定。</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RayVisualizer : MonoBehaviour
    {
        [Header("核心引用")]
        public RayController rayController;

        [Header("视觉配置")]
        public Color laserColor = new Color32(105, 209, 233, 255);
        public Color hitColor = new Color32(175, 231, 243, 255);
        public float laserWidth = 0.0024f;
        public Material rayMaterial;
        public Material reticleMaterial;

        private const float IdleLength = 4f;
        private const float VisualWidthScale = 1.875f;
        private const float ReticleRadius = 0.0045f;
        private const float PulseDuration = 0.14f;
        private LineRenderer lineRenderer;
        private LineRenderer beamOutline;
        private LineRenderer glowRenderer;
        private LineRenderer pulseRenderer;
        private Transform reticle;
        private Mesh reticleMesh;
        private MeshRenderer reticleRenderer;
        private MaterialPropertyBlock reticleProperties;
        private Material ownedMaterial;
        private Material ownedReticleMaterial;
        private Material outlineMaterial;
        private Material pulseMaterial;
        private Material glowMaterial;
        private ControllerModelVisual modelVisual;
        private bool triggerWasHeld;
        private float pressTime = float.NegativeInfinity;
        private float pulseTime = float.NegativeInfinity;
        private Vector3 pulseOrigin;
        private Vector3 pulseDestination;
        private float trackingTime = float.NegativeInfinity;
        private bool initialized;

        private void OnEnable()
        {
            RayHitFeedback.Hit += OnAcceptedHit;
            RayHitFeedback.Tracking += OnSliderTracking;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => Initialize();

        private void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            if (rayController == null)
                rayController = GetComponentInParent<RayController>();
            if (rayController != null)
                modelVisual = rayController.GetComponent<ControllerModelVisual>();
            lineRenderer = GetComponent<LineRenderer>();
            ConfigureLine(lineRenderer, 32766);

            var sourceMaterial =
                rayMaterial != null
                    ? rayMaterial
                    : Resources.Load<Material>("ControllerAppearance/Materials/PointerBeam");
            ownedMaterial = CreateMaterial(sourceMaterial);
            lineRenderer.sharedMaterial = ownedMaterial;
            SetEmissionGain(ownedMaterial, 1.8f);
            var dotMaterial =
                reticleMaterial != null
                    ? reticleMaterial
                    : Resources.Load<Material>("ControllerAppearance/Materials/PointerReticle");
            ownedReticleMaterial = CreateMaterial(dotMaterial);
            if (ownedReticleMaterial != null)
                ownedReticleMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);

            outlineMaterial = CreateMaterial(dotMaterial);
            pulseMaterial = CreateMaterial(sourceMaterial);
            SetEmissionGain(pulseMaterial, 2.3f);
            glowMaterial = CreateMaterial(sourceMaterial);
            SetEmissionGain(glowMaterial, 1.5f);
            beamOutline = CreateLine("PointerOutline", outlineMaterial, 32764);
            glowRenderer = CreateLine("PointerGlow", glowMaterial, 32765);
            pulseRenderer = CreateLine("PointerPulse", pulseMaterial, 32767);
            pulseRenderer.enabled = false;

            var cursor = new GameObject("PointerReticle", typeof(MeshFilter), typeof(MeshRenderer));
            cursor.layer = gameObject.layer;
            reticle = cursor.transform;
            reticle.SetParent(transform, false);
            reticleRenderer = cursor.GetComponent<MeshRenderer>();
            reticleRenderer.sharedMaterial = ownedReticleMaterial;
            reticleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            reticleRenderer.receiveShadows = false;
            reticleRenderer.sortingOrder = 32767;
            reticleProperties = new MaterialPropertyBlock();
            reticleMesh = CreateReticleMesh();
            cursor.GetComponent<MeshFilter>().sharedMesh = reticleMesh;
            cursor.SetActive(false);
        }

        private Material CreateMaterial(Material source)
        {
            var shader = source != null ? source.shader : Shader.Find("UI/Default");
            if (shader == null)
                return null;
            var material = source != null ? new Material(source) : new Material(shader);
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return material;
        }

        // 无光照材质直接输出HDR亮度，保留Alpha，既有Bloom可响应亮芯。
        private static void SetEmissionGain(Material material, float intensity)
        {
            if (material != null && material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(intensity, intensity, intensity, 1));
        }

        private static void ConfigureLine(LineRenderer renderer, int order)
        {
            renderer.useWorldSpace = true;
            renderer.positionCount = 2;
            renderer.numCapVertices = 4;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.alignment = LineAlignment.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = order;
        }

        private LineRenderer CreateLine(string name, Material material, int order)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<LineRenderer>();
            ConfigureLine(renderer, order);
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Mesh CreateReticleMesh()
        {
            const int segments = 48;
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 3];
            uv[0] = Vector2.one * 0.5f;
            colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                uv[i + 1] = Vector2.one * 0.5f;
                colors[i + 1] = Color.white;
                int t = i * 3;
                triangles[t] = 0;
                triangles[t + 1] = i + 1;
                triangles[t + 2] = (i + 1) % segments + 1;
            }
            var mesh = new Mesh { name = "PointerSurfaceDot" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnAcceptedHit(bool isRightHand, Transform surface)
        {
            if (
                rayController == null
                || isRightHand != rayController.isRightHand
                || !rayController.isActiveAndEnabled
                || rayController.IsGameplayPaused
                || rayController.IsHittingUI
            )
                return;
            // 同一只手同一帧的多个判定合为一次，强度不随连击累加。
            if (Time.unscaledTime - pulseTime < 0.045f || rayController.visualRay == null)
                return;
            var source = rayController.visualRay;
            Vector3 destination;
            if (surface != null)
            {
                var plane = new Plane(surface.forward, surface.position);
                var ray = new Ray(source.position, source.forward);
                if (!plane.Raycast(ray, out float distance) || distance > rayController.rayLength)
                    return;
                destination = ray.GetPoint(distance);
            }
            else if (
                rayController.IsHittingUI
                || !rayController.TryGetVisualSurface(out destination, out _)
            )
                destination =
                    source.position
                    + source.forward * Mathf.Min(IdleLength, rayController.rayLength);
            // 脉冲单独保留接受判定时的线段；下一帧目标回池或转向都不会把亮段弹到远处。
            pulseOrigin = source.position;
            pulseDestination = destination;
            pulseTime = Time.unscaledTime;
        }

        private void OnSliderTracking(bool isRightHand)
        {
            if (
                rayController != null
                && isRightHand == rayController.isRightHand
                && rayController.isActiveAndEnabled
                && !rayController.IsGameplayPaused
            )
                trackingTime = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (!initialized)
                Initialize();
            bool available =
                rayController != null
                && rayController.isActiveAndEnabled
                && rayController.visualRay != null;
            lineRenderer.enabled = available;
            beamOutline.enabled = available;
            glowRenderer.enabled = available;
            if (!available)
            {
                HideFeedback();
                return;
            }
            if (rayController.IsGameplayPaused)
                ClearFeedback();
            else if (rayController.IsHittingUI)
                ClearPulse();

            var source = rayController.visualRay;
            bool surfaceHit = rayController.TryGetVisualSurface(out var end, out var normal);
            if (!surfaceHit)
                end =
                    source.position
                    + source.forward * Mathf.Min(IdleLength, rayController.rayLength);
            bool held = rayController.IsTriggerPressed;
            if (held && !triggerWasHeld && rayController.IsHittingUI)
                pressTime = Time.unscaledTime;
            triggerWasHeld = held;
            float press = Mathf.Clamp01(1 - (Time.unscaledTime - pressTime) / 0.12f);
            float pulseAge = (Time.unscaledTime - pulseTime) / PulseDuration;
            bool pulseActive = pulseAge >= 0 && pulseAge < 1 && !rayController.IsHittingUI;
            float flash = pulseActive ? Mathf.Clamp01(1 - pulseAge * 2.8f) : 0;
            float tracking = rayController.IsHittingUI
                ? 0
                : Mathf.Clamp01(1 - (Time.unscaledTime - trackingTime) / 0.08f);
            Color color = Color.Lerp(laserColor, hitColor, surfaceHit ? 0.2f : 0);
            color = Color.Lerp(color, Color.white, 0.12f + flash * 0.3f);
            color.a = surfaceHit ? 1f : 0.9f;
            lineRenderer.startColor = lineRenderer.endColor = color;
            float width = laserWidth * VisualWidthScale * (1 + tracking * 0.08f + flash * 0.1f);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width * 0.65f;
            lineRenderer.SetPosition(0, source.position);
            lineRenderer.SetPosition(1, end);
            beamOutline.startColor = beamOutline.endColor = new Color32(9, 16, 29, 145);
            beamOutline.startWidth = width * 1.65f;
            beamOutline.endWidth = width * 1.05f;
            beamOutline.SetPosition(0, source.position);
            beamOutline.SetPosition(1, end);
            // 柔光只拓宽透明羽化层，实际亮芯与命中宽度保持原值。
            Color glowColor = laserColor;
            glowColor.a = 0.22f + flash * 0.08f;
            glowRenderer.startColor = glowRenderer.endColor = glowColor;
            glowRenderer.startWidth = width * 3.4f;
            glowRenderer.endWidth = width * 2.2f;
            glowRenderer.SetPosition(0, source.position);
            glowRenderer.SetPosition(1, end);

            pulseRenderer.enabled = pulseActive;
            if (pulseActive)
            {
                float progress = Mathf.Clamp01(pulseAge * 1.18f);
                pulseRenderer.SetPosition(
                    0,
                    Vector3.Lerp(pulseOrigin, pulseDestination, Mathf.Max(0, progress - 0.16f))
                );
                pulseRenderer.SetPosition(1, Vector3.Lerp(pulseOrigin, pulseDestination, progress));
                Color pulseColor = Color.Lerp(laserColor, Color.white, 0.65f);
                pulseColor.a = Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulseAge));
                pulseRenderer.startColor = pulseRenderer.endColor = pulseColor;
                pulseRenderer.startWidth = pulseRenderer.endWidth =
                    laserWidth * VisualWidthScale * 1.3f;
            }
            if (modelVisual != null)
                modelVisual.SetFeedbackBrightness(flash + tracking * 0.1f);

            reticle.gameObject.SetActive(surfaceHit);
            if (!surfaceHit)
                return;
            // 圆片沿表面法线放置，仅偏离0.8毫米避免深度闪烁，不跟随相机转向。
            reticle.position = end + normal * 0.0008f;
            reticle.rotation = Quaternion.LookRotation(normal);
            float distance = Vector3.Distance(source.position, end);
            float radius =
                ReticleRadius * VisualWidthScale * Mathf.Clamp(distance / 1.5f, 0.7f, 1.6f);
            float hitScale = pulseActive ? 1 - 0.12f * Mathf.Sin(Mathf.PI * pulseAge) : 1;
            reticle.localScale = Vector3.one * radius * hitScale * Mathf.Lerp(1, 0.86f, press);
            color.a = 0.95f;
            reticleProperties.SetColor("_Color", color);
            reticleRenderer.SetPropertyBlock(reticleProperties);
        }

        private void ClearPulse()
        {
            pulseTime = float.NegativeInfinity;
            pulseOrigin = pulseDestination = Vector3.zero;
            if (pulseRenderer != null)
                pulseRenderer.enabled = false;
            if (modelVisual != null)
                modelVisual.SetFeedbackBrightness(0);
        }

        private void ClearFeedback()
        {
            ClearPulse();
            trackingTime = pressTime = float.NegativeInfinity;
            triggerWasHeld = false;
        }

        private void HideFeedback()
        {
            ClearFeedback();
            if (reticle != null)
                reticle.gameObject.SetActive(false);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ClearFeedback();

        private void OnDisable()
        {
            RayHitFeedback.Hit -= OnAcceptedHit;
            RayHitFeedback.Tracking -= OnSliderTracking;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (lineRenderer != null)
                lineRenderer.enabled = false;
            if (beamOutline != null)
                beamOutline.enabled = false;
            if (glowRenderer != null)
                glowRenderer.enabled = false;
            HideFeedback();
        }

        private void OnDestroy()
        {
            if (reticleMesh != null)
                Destroy(reticleMesh);
            if (ownedMaterial != null)
                Destroy(ownedMaterial);
            if (ownedReticleMaterial != null)
                Destroy(ownedReticleMaterial);
            if (outlineMaterial != null)
                Destroy(outlineMaterial);
            if (pulseMaterial != null)
                Destroy(pulseMaterial);
            if (glowMaterial != null)
                Destroy(glowMaterial);
        }
    }
}
