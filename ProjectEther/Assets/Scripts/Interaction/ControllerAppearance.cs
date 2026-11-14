using System;
using UnityEngine;

namespace OsuVR
{
    /// <summary>双手共用的外观选择；独立保存，不改变控制器偏移与玩法设置。</summary>
    public static class ControllerAppearance
    {
        public const int ModelCount = 3;
        private const string PreferenceKey = "Settings_ControllerAppearance";
        private static bool loaded;
        private static int selectedIndex;

        public static event Action<int> Changed;

        public static int SelectedIndex
        {
            get
            {
                if (!loaded)
                {
                    int saved = PlayerPrefs.GetInt(PreferenceKey, 0);
                    selectedIndex = saved >= 0 && saved < ModelCount ? saved : 0;
                    loaded = true;
                }
                return selectedIndex;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            loaded = false;
            Changed = null;
        }

        public static void SetSelectedIndex(int index)
        {
            if (index < 0 || index >= ModelCount || index == SelectedIndex)
                return;

            selectedIndex = index;
            PlayerPrefs.SetInt(PreferenceKey, selectedIndex);
            PlayerPrefs.Save();
            Changed?.Invoke(selectedIndex);
        }

        public static Color GetHandColor(bool rightHand) =>
            rightHand ? new Color32(205, 92, 147, 255) : new Color32(105, 209, 233, 255);

        public static string GetModelName(int index) =>
            LocalizationManager.GetText(
                index switch
                {
                    1 => "ui_controller_model_b",
                    2 => "ui_controller_model_c",
                    _ => "ui_controller_model_a",
                }
            );
    }
}
