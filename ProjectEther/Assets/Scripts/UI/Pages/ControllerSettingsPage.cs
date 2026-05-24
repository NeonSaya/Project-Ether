using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OsuVR
{
    /// <summary>
    /// 控制器偏移设置页面：Left/Right Z Offset, Left/Right Y Offset, Rotation
    /// </summary>
    public class ControllerSettingsPage : SettingsPageBase
    {
        public override string TabLocalizationKey => "ui_tab_controller";

        private Slider leftZSlider;
        private Slider rightZSlider;
        private Slider leftYSlider;
        private Slider rightYSlider;
        private Slider rotationSlider;

        private const string OffsetFormat = "{0:F2} m";
        private const string RotationFormat = "{0:F0}°";

        // Cached references for batch apply
        private GameSettings cachedTempSettings;

        public override void BuildContent(RectTransform parent, GameSettings tempSettings, float contentWidth)
        {
            cachedTempSettings = tempSettings;

            leftZSlider = CreateSlider(parent, "Left Controller Z Offset", "ui_left_controller_z_offset",
                -0.5f, 0.5f, tempSettings.leftControllerZOffset, OffsetFormat,
                v =>
                {
                    tempSettings.leftControllerZOffset = v;
                    ApplyControllerOffsets();
                });

            rightZSlider = CreateSlider(parent, "Right Controller Z Offset", "ui_right_controller_z_offset",
                -0.5f, 0.5f, tempSettings.rightControllerZOffset, OffsetFormat,
                v =>
                {
                    tempSettings.rightControllerZOffset = v;
                    ApplyControllerOffsets();
                });

            leftYSlider = CreateSlider(parent, "Left Controller Y Offset", "ui_left_controller_y_offset",
                -0.3f, 0.3f, tempSettings.leftControllerYOffset, OffsetFormat,
                v =>
                {
                    tempSettings.leftControllerYOffset = v;
                    ApplyControllerOffsets();
                });

            rightYSlider = CreateSlider(parent, "Right Controller Y Offset", "ui_right_controller_y_offset",
                -0.3f, 0.3f, tempSettings.rightControllerYOffset, OffsetFormat,
                v =>
                {
                    tempSettings.rightControllerYOffset = v;
                    ApplyControllerOffsets();
                });

            rotationSlider = CreateSlider(parent, "Controller Rotation", "ui_controller_rotation_offset",
                -45f, 45f, tempSettings.controllerRotationOffset, RotationFormat,
                v =>
                {
                    tempSettings.controllerRotationOffset = v;
                    ApplyControllerOffsets();
                });
        }

        public override void RefreshUI(GameSettings tempSettings)
        {
            cachedTempSettings = tempSettings;
            SetSliderValueWithoutNotify(leftZSlider, tempSettings.leftControllerZOffset, OffsetFormat);
            SetSliderValueWithoutNotify(rightZSlider, tempSettings.rightControllerZOffset, OffsetFormat);
            SetSliderValueWithoutNotify(leftYSlider, tempSettings.leftControllerYOffset, OffsetFormat);
            SetSliderValueWithoutNotify(rightYSlider, tempSettings.rightControllerYOffset, OffsetFormat);
            SetSliderValueWithoutNotify(rotationSlider, tempSettings.controllerRotationOffset, RotationFormat);
        }

        protected override string GetFormatForSlider(Slider slider)
        {
            if (slider == rotationSlider) return RotationFormat;
            return OffsetFormat;
        }

        /// <summary>
        /// 批量应用控制器偏移（5 个值一起提交）
        /// </summary>
        private void ApplyControllerOffsets()
        {
            if (cachedTempSettings == null) return;
            SettingsManager.Instance.SetControllerOffsets(
                cachedTempSettings.leftControllerZOffset,
                cachedTempSettings.rightControllerZOffset,
                cachedTempSettings.leftControllerYOffset,
                cachedTempSettings.rightControllerYOffset,
                cachedTempSettings.controllerRotationOffset);
        }
    }
}
