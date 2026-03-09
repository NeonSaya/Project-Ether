#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using OsuVR;

namespace OsuVR.Editor
{
    [CustomEditor(typeof(VRPauseMenu))]
    public class VRPauseMenuEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            VRPauseMenu pauseMenu = (VRPauseMenu)target;

            // 注意：Prefab模式下不需要创建默认UI结构
            // 请使用 VRPauseMenuPrefabCreator 创建Prefab

            if (GUILayout.Button("自动查找GameManager"))
            {
                var gameManager = FindObjectOfType<RhythmGameManager>();
                if (gameManager != null)
                {
                    pauseMenu.gameManager = gameManager;
                    EditorUtility.SetDirty(pauseMenu);
                    Debug.Log("[VRPauseMenu] 已自动分配RhythmGameManager");
                }
                else
                {
                    Debug.LogWarning("[VRPauseMenu] 未找到RhythmGameManager");
                }
            }
        }
    }
}
#endif