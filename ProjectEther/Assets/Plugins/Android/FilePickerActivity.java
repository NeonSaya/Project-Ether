package com.nyaon.projectether;

import android.content.ClipData;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.util.Log;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

import java.io.Closeable;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.List;

/**
 * 自定义 Unity 主 Activity：桥接 Android 文件选择器的结果回传。
 *
 * 真机（Pico/Quest）实测教训：从 VR 应用切到 2D 系统选择器再返回时，
 * Activity 可能因配置变化（uiMode/screenSize 等）被销毁重建，Unity 随之
 * 重新初始化（表现为黑屏加载圈）。此时 onActivityResult 送达新实例，
 * 场景里的 GameObject 还不存在，UnitySendMessage 会丢失。
 *
 * 因此本类把「复制文件」这一步直接下沉到 Java 后台线程完成，完全不依赖
 * Unity 的恢复时序：
 *   - 选中文件 -> 后台线程逐个通过 ContentResolver 复制到
 *     <externalFilesDir>/Songs/（即 Unity 的 persistentDataPath/Songs）
 *   - 结果写入 SharedPreferences（慢通道，供 C# 恢复后主动拉取）
 *   - 同时 UnitySendMessage 通知 C# 立即解压（快通道）
 * C# 侧 BeatmapImporter.ImportNewOszFiles() 负责解压，两条通道幂等。
 */
public class FilePickerActivity extends UnityPlayerActivity
{
    private static final String TAG = "FilePickerActivity";

    /** 必须与 BeatmapImporter.cs 中 startActivityForResult 的 requestCode 一致 */
    private static final int REQUEST_PICK_OSU = 1001;

    /** 必须与 BeatmapImporter.cs 中 GetOrCreateHelper() 创建的 GameObject 名一致 */
    private static final String UNITY_HELPER_OBJECT = "[BeatmapImporterHelper]";
    private static final String UNITY_HELPER_METHOD = "OnFilesPicked";

    private static final String PREFS_NAME = "beatmap_import";
    private static final String PREF_KEY_RESULT = "pending_result";

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data)
    {
        // 先交给 Unity 处理自身发起的请求（权限等），保证不影响引擎原有链路
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != REQUEST_PICK_OSU)
            return;

        List<Uri> uris = collectUris(resultCode, data);
        if (uris.isEmpty())
        {
            // 用户取消选择
            UnityPlayer.UnitySendMessage(UNITY_HELPER_OBJECT, UNITY_HELPER_METHOD, "");
            return;
        }

        Log.i(TAG, "picked " + uris.size() + " file(s), copying in background");

        final Context app = getApplicationContext();
        final List<Uri> picked = uris;
        new Thread(new Runnable()
        {
            @Override
            public void run()
            {
                // 消息格式（与 BeatmapImporter.HandleImportMessage 约定）:
                //   "ok:name1.osz|name2.osz[|err:原因]"  至少一个成功
                //   "err:原因"                          全部失败
                String message = copyToSongs(app, picked);
                savePendingResult(app, message);
                // UnitySendMessage 线程安全；Unity 处于 pause 时消息会排队，
                // 恢复后投递。若因 Activity 重建丢失，则由 C# 端
                // PullPendingImports() 从 SharedPreferences 拉取同一结果。
                UnityPlayer.UnitySendMessage(UNITY_HELPER_OBJECT, UNITY_HELPER_METHOD, message);
            }
        }, "osz-import").start();
    }

    private List<Uri> collectUris(int resultCode, Intent data)
    {
        List<Uri> uris = new ArrayList<Uri>();
        if (resultCode != RESULT_OK || data == null)
            return uris;

        // 多选: 系统选择器把结果放在 ClipData 里
        ClipData clip = data.getClipData();
        if (clip != null)
        {
            for (int i = 0; i < clip.getItemCount(); i++)
            {
                Uri uri = clip.getItemAt(i).getUri();
                if (uri != null)
                    uris.add(uri);
            }
        }

        // 单选: 结果在 data 里
        if (uris.isEmpty() && data.getData() != null)
            uris.add(data.getData());

        return uris;
    }

    /** 后台线程: 把选中的文件复制到 Songs 目录，返回结果消息 */
    private static String copyToSongs(Context app, List<Uri> uris)
    {
        File externalFilesDir = app.getExternalFilesDir(null);
        if (externalFilesDir == null)
            return "err:外部存储不可用";

        File songsDir = new File(externalFilesDir, "Songs");
        if (!songsDir.exists() && !songsDir.mkdirs())
            return "err:无法创建 Songs 目录";

        StringBuilder okNames = new StringBuilder();
        String firstError = null;

        for (Uri uri : uris)
        {
            File dest = null;
            InputStream in = null;
            OutputStream out = null;
            try
            {
                String name = queryDisplayName(app, uri);
                if (name == null || name.isEmpty())
                    name = "imported.osz";
                if (!name.endsWith(".osz"))
                    name = name + ".osz";
                name = sanitizeFileName(name);

                dest = new File(songsDir, name);
                in = app.getContentResolver().openInputStream(uri);
                out = new FileOutputStream(dest, false);

                byte[] buf = new byte[8192];
                int n;
                long total = 0;
                while ((n = in.read(buf)) > 0)
                {
                    out.write(buf, 0, n);
                    total += n;
                }
                out.flush();
                Log.i(TAG, "copied " + name + " (" + total + " bytes)");

                if (okNames.length() > 0)
                    okNames.append('|');
                okNames.append(name);
            }
            catch (Throwable t)
            {
                Log.e(TAG, "copy failed: " + uri, t);
                // 清掉半截文件，避免被当作完整 .osz 解压
                if (dest != null && dest.exists())
                    dest.delete();
                if (firstError == null)
                    firstError = t.getMessage();
            }
            finally
            {
                closeQuietly(in);
                closeQuietly(out);
            }
        }

        if (okNames.length() > 0)
        {
            String msg = "ok:" + okNames;
            if (firstError != null)
                msg = msg + "|err:" + firstError;
            return msg;
        }
        return "err:" + (firstError != null ? firstError : "文件复制失败");
    }

    /** C# 通过 JNI 调用: 取走并清空未送达的导入结果（UnitySendMessage 丢失时兜底） */
    public static String consumePendingResult(Context context)
    {
        try
        {
            SharedPreferences sp = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            String result = sp.getString(PREF_KEY_RESULT, "");
            if (result != null && !result.isEmpty())
                sp.edit().remove(PREF_KEY_RESULT).commit();
            return result == null ? "" : result;
        }
        catch (Throwable t)
        {
            Log.e(TAG, "consumePendingResult failed", t);
            return "";
        }
    }

    private static void savePendingResult(Context app, String message)
    {
        try
        {
            app.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
                .edit().putString(PREF_KEY_RESULT, message).commit();
        }
        catch (Throwable t)
        {
            Log.e(TAG, "savePendingResult failed", t);
        }
    }

    private static String queryDisplayName(Context app, Uri uri)
    {
        Cursor cursor = null;
        try
        {
            cursor = app.getContentResolver().query(uri, null, null, null, null);
            if (cursor != null && cursor.moveToFirst())
            {
                int idx = cursor.getColumnIndex("_display_name");
                if (idx >= 0)
                    return cursor.getString(idx);
            }
        }
        catch (Throwable t)
        {
            Log.w(TAG, "query display name failed: " + t);
        }
        finally
        {
            if (cursor != null)
                cursor.close();
        }
        return null;
    }

    private static String sanitizeFileName(String name)
    {
        return name.replaceAll("[\\\\/:*?\"<>|]", "_");
    }

    private static void closeQuietly(Closeable c)
    {
        try
        {
            if (c != null)
                c.close();
        }
        catch (Throwable ignored)
        {
        }
    }
}
