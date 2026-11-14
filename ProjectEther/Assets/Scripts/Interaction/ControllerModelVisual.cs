using UnityEngine;
using UnityEngine.Rendering;

namespace OsuVR
{
    /// <summary>掌心模型按Direct瞄准旋转与手柄本体共同转动，发射轴与射线共线。</summary>
    [RequireComponent(typeof(RayController))]
    public sealed class ControllerModelVisual : MonoBehaviour
    {
        [SerializeField]
        private GameObject legacyVisual;

        private RayController rayController;
        private readonly GameObject[] variants = new GameObject[ControllerAppearance.ModelCount];
        private Transform visualRoot;
        private bool legacyWasActive;
        private readonly Renderer[][] feedbackRenderers = new Renderer[
            ControllerAppearance.ModelCount
        ][];
        private readonly Color[][] feedbackEmission = new Color[ControllerAppearance.ModelCount][];
        private MaterialPropertyBlock feedbackProperties;
        private int activeIndex = -1;
        private float feedbackBrightness;

        private void Awake()
        {
            rayController = GetComponent<RayController>();
            feedbackProperties = new MaterialPropertyBlock();
            legacyWasActive = legacyVisual != null && legacyVisual.activeSelf;
            visualRoot = new GameObject("ControllerAppearance").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.gameObject.layer = gameObject.layer;
            visualRoot.localScale = Vector3.one * 1.25f;
        }

        private void OnEnable()
        {
            ControllerAppearance.Changed += ApplySelection;
            ApplySelection(ControllerAppearance.SelectedIndex);
        }

        private void OnDisable()
        {
            ControllerAppearance.Changed -= ApplySelection;
            SetFeedbackBrightness(0);
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);
            if (legacyVisual != null && legacyWasActive)
                legacyVisual.SetActive(true);
        }

        private void ApplySelection(int index)
        {
            if (visualRoot == null)
                return;

            if (variants[index] == null)
            {
                string id = ((char)('A' + index)).ToString();
                var prefab = Resources.Load<GameObject>("ControllerAppearance/Models/" + id);
                if (prefab == null)
                    return;

                var model = Instantiate(prefab, visualRoot, false);
                model.name = "Model_" + id;
                foreach (var child in model.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = gameObject.layer;

                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    if (!rayController.isRightHand)
                        continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i].name == "AccentLeft")
                            materials[i] = Resources.Load<Material>(
                                "ControllerAppearance/Materials/AccentRight"
                            );
                        else if (materials[i].name == "CoreLeft")
                            materials[i] = Resources.Load<Material>(
                                "ControllerAppearance/Materials/CoreRight"
                            );
                    }
                    renderer.sharedMaterials = materials;
                }
                variants[index] = model;
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                var lights = new System.Collections.Generic.List<Renderer>();
                var emissions = new System.Collections.Generic.List<Color>();
                foreach (var renderer in renderers)
                {
                    var material = renderer.sharedMaterial;
                    if (
                        material != null
                        && (material.name.StartsWith("Accent") || material.name.StartsWith("Core"))
                    )
                    {
                        lights.Add(renderer);
                        emissions.Add(material.GetColor("_EmissionColor"));
                    }
                }
                feedbackRenderers[index] = lights.ToArray();
                feedbackEmission[index] = emissions.ToArray();
            }

            SetFeedbackBrightness(0);
            activeIndex = index;
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null)
                    variants[i].SetActive(i == index);
            visualRoot.gameObject.SetActive(true);
            if (legacyVisual != null)
                legacyVisual.SetActive(false);
            FollowRay();
        }

        /// <summary>只微调当前模型的光缝亮度，使用属性块而非克隆或写回共享材质。</summary>
        public void SetFeedbackBrightness(float value)
        {
            value = Mathf.Clamp01(value);
            if (activeIndex < 0 || Mathf.Abs(value - feedbackBrightness) < 0.001f)
                return;
            feedbackBrightness = value;
            var renderers = feedbackRenderers[activeIndex];
            if (renderers == null)
                return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                feedbackProperties.Clear();
                feedbackProperties.SetColor(
                    "_EmissionColor",
                    feedbackEmission[activeIndex][i] * (1 + value * 0.55f)
                );
                renderers[i].SetPropertyBlock(feedbackProperties);
            }
        }

        private void LateUpdate() => FollowRay();

        private void FollowRay()
        {
            if (rayController.visualRay != null && visualRoot != null)
                visualRoot.SetPositionAndRotation(
                    rayController.visualRay.position,
                    transform.rotation * rayController.AimRotationOffset
                );
        }
    }
}
