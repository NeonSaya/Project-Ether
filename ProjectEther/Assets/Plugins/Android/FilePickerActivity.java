package com.nyaon.projectether;

import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

/**
 * 自定义 Unity 主 Activity：桥接 Android 文件选择器的结果回传。
 *
 * 背景：Unity 默认的 UnityPlayerActivity 不会把 onActivityResult 分发给 C# 脚本，
 * 导致 BeatmapImporter.OpenAndroidFilePicker 发出的 startActivityForResult(1001)
 * 选中文件后无人接收。本类把结果（含多选 ClipData）通过 UnitySendMessage
 * 回传给场景中的 "[BeatmapImporterHelper]" GameObject（OnFilesPicked 方法），
 * 多个 URI 之间用 '\n' 分隔；取消/失败时回传空字符串。
 */
public class FilePickerActivity extends UnityPlayerActivity
{
    /** 必须与 BeatmapImporter.cs 中 startActivityForResult 的 requestCode 一致 */
    private static final int REQUEST_PICK_OSU = 1001;

    /** 必须与 BeatmapImporter.cs 中 GetOrCreateHelper() 创建的 GameObject 名一致 */
    private static final String UNITY_HELPER_OBJECT = "[BeatmapImporterHelper]";
    private static final String UNITY_HELPER_METHOD = "OnFilesPicked";

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data)
    {
        // 先交给 Unity 处理自身发起的请求（权限等），保证不影响引擎原有链路
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != REQUEST_PICK_OSU)
            return;

        String uriList = collectUris(resultCode, data);
        // onActivityResult 运行在 UI 线程；UnitySendMessage 线程安全，
        // 会在下一帧于 Unity 主线程上调用目标方法
        UnityPlayer.UnitySendMessage(UNITY_HELPER_OBJECT, UNITY_HELPER_METHOD, uriList);
    }

    private String collectUris(int resultCode, Intent data)
    {
        if (resultCode != RESULT_OK || data == null)
            return "";

        StringBuilder sb = new StringBuilder();

        // 多选：系统选择器把结果放在 ClipData 里
        ClipData clip = data.getClipData();
        if (clip != null)
        {
            for (int i = 0; i < clip.getItemCount(); i++)
            {
                Uri uri = clip.getItemAt(i).getUri();
                if (uri != null)
                {
                    if (sb.length() > 0)
                        sb.append('\n');
                    sb.append(uri.toString());
                }
            }
        }

        // 单选：结果在 data 里
        if (sb.length() == 0 && data.getData() != null)
            sb.append(data.getData().toString());

        return sb.toString();
    }
}
