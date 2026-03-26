using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using System.Collections.Generic;
using System.Linq;

public static class FontAtlasFixer
{
    private static readonly string[] CJK_COMMON_CHARS = new string[]
    {
        "游戏设置语言音频画面控制器主音量音乐音效偏移画质抗锯齿粒子密度启用手柄震动强度显示歌曲原名左手轴右手旋转偏移重置保存继续暂停主菜单低中高超高关闭开启滑条完美转盘奖励未知艺术家谱师曲目列表玩法修改确认开始返回圈距缩圈判定血量选择一首普通困难模式简单放轻松慢慢来自动演示观看完美的双倍速加速至半倍减速隐藏音符逐渐消失手电筒有限的可见区域重试回放分数准确率最大连击评级结算时长难度制作人员分数倍率"
    };

    private static readonly string HIRAGANA = "ぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞただちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょよらりるれろゎわゐゑをんゔゕゖゝゞゟ";
    private static readonly string KATAKANA = "゠ァアィイゥウェエォオカガキギクグケゲコゴサザシジスズセゼソゾタダチヂッツヅテデトドナニヌネノハバパヒビピフブプヘベペホボポマミムメモャヤュユョヨラリルレロヮワヰヱヲンヴヵヶヷヸヹヺ・ーヽヾヿ";
    private static readonly string JP_PUNCTUATION = "。「」、・ぁぃぅぇぉっゃゅょゎゕゖゝゞゟ゠ァィゥェォッャュョヮ・カヽヾ・联社";
    private static readonly string JP_KANJI = "語始終記難易速遅消見限視界部全被倍率延再初完成功失認知配置利使用設定定確認除戻選択続完了表示増減画像音色楽譜面映画面操作方向左右高低超大中小基本項目の内容項目完了";

    private static readonly string[] JP_COMMON_CHARS = new string[]
    {
        HIRAGANA + KATAKANA + JP_PUNCTUATION + JP_KANJI + "ビートマップモッド確定ゲーム開始戻るサークルサイズアプローチ率全体難易度ドレイン設定言語なし選択不明なタイトルノーマルプレイ終了クレジットスコア倍率ゲームオーディオグラフィックコントローラーオープンマスターボリューム音楽ボリューム効果音オフセット品質アンチエイリアスパーティクル密度触覚有効化強度曲名原語左コントローラーオフセット右回転リセット保存再開一時停止メインメニュー低中高ウルトラオフオンユーザー名スライダーパーフェクトスピナーボーナス不明なアーティストマッパーリザルト長さ難易度ハードロックイージーオートダブルタイムハーフタイムヒドゥンフラッシュライトリトライリプレイスコア精度最大コンボランクすべてが難しくなるリラックスして楽しもう完璧なオートプレイを見るに加速減速ノーツが徐々に消える視界が制限される見る"
    };

    [MenuItem("Tools/Fix TMP Font Atlas Textures")]
    public static void FixFonts()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            
            if (fontAsset != null)
            {
                bool needsFix = false;
                try
                {
                    if (fontAsset.atlasTexture == null || fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
                    {
                        needsFix = true;
                    }
                }
                catch
                {
                    needsFix = true;
                }
                
                if (needsFix)
                {
                    Debug.Log($"Fixing font asset: {path}");
                    
                    fontAsset.ClearFontAssetData(true);
                    
                    int width = fontAsset.creationSettings.atlasWidth > 0 ? fontAsset.creationSettings.atlasWidth : 1024;
                    int height = fontAsset.creationSettings.atlasHeight > 0 ? fontAsset.creationSettings.atlasHeight : 1024;
                    
                    Texture2D tex = new Texture2D(width, height, TextureFormat.Alpha8, false);
                    tex.name = fontAsset.name + " Atlas";
                    
                    fontAsset.atlasTextures = new Texture2D[] { tex };
                    
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                    
                    if (fontAsset.material != null)
                    {
                        fontAsset.material.SetTexture(ShaderUtilities.ID_MainTex, tex);
                        EditorUtility.SetDirty(fontAsset.material);
                    }
                    
                    EditorUtility.SetDirty(fontAsset);
                }
            }
        }
        AssetDatabase.SaveAssets();
        
        if (TMP_Settings.instance != null && TMP_Settings.fallbackFontAssets != null)
        {
            for (int i = TMP_Settings.fallbackFontAssets.Count - 1; i >= 0; i--)
            {
                var font = TMP_Settings.fallbackFontAssets[i];
                if (font == null || font.atlasTexture == null || font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
                {
                    Debug.LogWarning($"[FontAtlasFixer] Removing corrupted runtime fallback font from TMP Settings: {(font != null ? font.name : "Null")}");
                    TMP_Settings.fallbackFontAssets.RemoveAt(i);
                }
            }
        }
        
        Debug.Log("Font Atlas fix complete!");
    }

    [MenuItem("Tools/Rebuild CJK Font Assets")]
    public static void RebuildCJKFontAssets()
    {
        Debug.Log("[FontAtlasFixer] Starting CJK font asset rebuild...");
        
        string[] cjkFontPaths = new string[]
        {
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSansSC-Regular SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSans-Regular SDF.asset"
        };

        foreach (string fontPath in cjkFontPaths)
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[FontAtlasFixer] Font asset not found: {fontPath}");
                continue;
            }

            Debug.Log($"[FontAtlasFixer] Rebuilding: {fontAsset.name}");
            
            Font sourceFont = fontAsset.sourceFontFile;
            if (sourceFont == null)
            {
                Debug.LogError($"[FontAtlasFixer] Source font file is null for: {fontAsset.name}");
                continue;
            }

            string charSet = "";
            if (fontAsset.name.Contains("SC"))
            {
                charSet = string.Join("", CJK_COMMON_CHARS);
            }
            else
            {
                charSet = string.Join("", CJK_COMMON_CHARS) + string.Join("", JP_COMMON_CHARS);
            }
            
            charSet += " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()[]{}|;':\",./<>?`~-_=+";

            HashSet<char> uniqueChars = new HashSet<char>(charSet);
            charSet = new string(uniqueChars.ToArray());

            Debug.Log($"[FontAtlasFixer] Character set size: {charSet.Length} unique characters");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            
            try
            {
                List<uint> unicodeChars = new List<uint>();
                foreach (char c in charSet)
                {
                    unicodeChars.Add((uint)c);
                }
                fontAsset.TryAddCharacters(unicodeChars.ToArray(), out uint[] missingChars);
                Debug.Log($"[FontAtlasFixer] Added characters. Missing: {missingChars.Length}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FontAtlasFixer] TryAddCharacters failed: {e.Message}. Font will use dynamic mode.");
            }

            fontAsset.isMultiAtlasTexturesEnabled = true;
            
            EditorUtility.SetDirty(fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("[FontAtlasFixer] CJK font asset rebuild complete!");
    }

    [MenuItem("Tools/Verify Font Asset Integrity")]
    public static void VerifyFontAssets()
    {
        Debug.Log("[FontAtlasFixer] Starting font asset verification...");
        
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int errorCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            
            if (fontAsset == null)
            {
                Debug.LogError($"[FontAtlasFixer] Failed to load font asset: {path}");
                errorCount++;
                continue;
            }

            List<string> issues = new List<string>();

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
            {
                issues.Add("No atlas textures");
            }
            else if (fontAsset.atlasTextures[0] == null)
            {
                issues.Add("First atlas texture is null");
            }

            if (fontAsset.material == null)
            {
                issues.Add("No material assigned");
            }
            else if (fontAsset.material.mainTexture == null)
            {
                issues.Add("Material has no main texture");
            }

            if (fontAsset.sourceFontFile == null && fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                issues.Add("No source font file (required for dynamic fonts)");
            }

            if (fontAsset.characterTable == null || fontAsset.characterTable.Count == 0)
            {
                issues.Add("Empty character table (may need rebuild for static fonts)");
            }

            if (issues.Count > 0)
            {
                Debug.LogWarning($"[FontAtlasFixer] Issues found in {fontAsset.name}:\n  - {string.Join("\n  - ", issues)}");
                errorCount++;
            }
            else
            {
                Debug.Log($"[FontAtlasFixer] OK: {fontAsset.name} ({fontAsset.characterTable?.Count ?? 0} characters)");
            }
        }

        if (TMP_Settings.instance != null)
        {
            Debug.Log($"[FontAtlasFixer] TMP Settings default font: {TMP_Settings.defaultFontAsset?.name ?? "None"}");
            Debug.Log($"[FontAtlasFixer] TMP Settings fallback count: {TMP_Settings.fallbackFontAssets?.Count ?? 0}");
            
            if (TMP_Settings.fallbackFontAssets != null)
            {
                foreach (var fallback in TMP_Settings.fallbackFontAssets)
                {
                    if (fallback == null)
                    {
                        Debug.LogWarning("[FontAtlasFixer] Null fallback in TMP Settings");
                        errorCount++;
                    }
                    else
                    {
                        Debug.Log($"[FontAtlasFixer] Fallback: {fallback.name}");
                    }
                }
            }
        }

        Debug.Log($"[FontAtlasFixer] Verification complete. {errorCount} issues found.");
    }

    [MenuItem("Tools/Fix Font Fallback Chain")]
    public static void FixFontFallbackChain()
    {
        Debug.Log("[FontAtlasFixer] Fixing font fallback chains...");
        
        TMP_FontAsset scFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSansSC-Regular SDF.asset");
        TMP_FontAsset jpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSans-Regular SDF.asset");
        TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");

        if (scFont != null)
        {
            scFont.fallbackFontAssetTable.Clear();
            if (jpFont != null && !scFont.fallbackFontAssetTable.Contains(jpFont))
            {
                scFont.fallbackFontAssetTable.Add(jpFont);
            }
            if (fallbackFont != null && !scFont.fallbackFontAssetTable.Contains(fallbackFont))
            {
                scFont.fallbackFontAssetTable.Add(fallbackFont);
            }
            EditorUtility.SetDirty(scFont);
            Debug.Log($"[FontAtlasFixer] Fixed fallback chain for {scFont.name}");
        }

        if (jpFont != null)
        {
            jpFont.fallbackFontAssetTable.Clear();
            if (fallbackFont != null && !jpFont.fallbackFontAssetTable.Contains(fallbackFont))
            {
                jpFont.fallbackFontAssetTable.Add(fallbackFont);
            }
            EditorUtility.SetDirty(jpFont);
            Debug.Log($"[FontAtlasFixer] Fixed fallback chain for {jpFont.name}");
        }

        if (TMP_Settings.instance != null)
        {
            TMP_Settings.fallbackFontAssets.Clear();
            if (scFont != null) TMP_Settings.fallbackFontAssets.Add(scFont);
            if (jpFont != null) TMP_Settings.fallbackFontAssets.Add(jpFont);
            if (fallbackFont != null) TMP_Settings.fallbackFontAssets.Add(fallbackFont);
            EditorUtility.SetDirty(TMP_Settings.instance);
            Debug.Log("[FontAtlasFixer] Fixed TMP Settings fallback chain");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FontAtlasFixer] Fallback chain fix complete!");
    }

    [MenuItem("Tools/Force Rebuild Japanese Font Atlas")]
    public static void ForceRebuildJapaneseFontAtlas()
    {
        Debug.Log("[FontAtlasFixer] Force rebuilding Japanese font atlas...");
        
        string fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSans-Regular SDF.asset";
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        
        if (fontAsset == null)
        {
            Debug.LogError($"[FontAtlasFixer] Font asset not found: {fontPath}");
            return;
        }

        Font sourceFont = fontAsset.sourceFontFile;
        if (sourceFont == null)
        {
            Debug.LogError($"[FontAtlasFixer] Source font file is null!");
            return;
        }

        Debug.Log($"[FontAtlasFixer] Source font: {sourceFont.name}");
        Debug.Log($"[FontAtlasFixer] Current atlas textures: {fontAsset.atlasTextures?.Length ?? 0}");
        Debug.Log($"[FontAtlasFixer] Current character table: {fontAsset.characterTable?.Count ?? 0}");

        string charSet = string.Join("", CJK_COMMON_CHARS) + string.Join("", JP_COMMON_CHARS);
        charSet += " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()[]{}|;':\",./<>?`~-_=+";
        
        HashSet<char> uniqueChars = new HashSet<char>(charSet);
        charSet = new string(uniqueChars.ToArray());

        Debug.Log($"[FontAtlasFixer] Character set size: {charSet.Length} unique characters");

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        List<uint> unicodeChars = new List<uint>();
        foreach (char c in charSet)
        {
            unicodeChars.Add((uint)c);
        }

        Debug.Log("[FontAtlasFixer] Adding characters to font (without clearing)...");
        
        try
        {
            fontAsset.TryAddCharacters(unicodeChars.ToArray(), out uint[] missingChars);
            Debug.Log($"[FontAtlasFixer] Added characters. Missing: {missingChars?.Length ?? 0}");

            if (missingChars != null && missingChars.Length > 0)
            {
                string missingStr = "";
                foreach (uint c in missingChars)
                {
                    if (c >= 0x3040 && c <= 0x30FF)
                    {
                        missingStr += (char)c;
                    }
                }
                if (!string.IsNullOrEmpty(missingStr))
                {
                    Debug.LogWarning($"[FontAtlasFixer] Missing Japanese characters: {missingStr}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FontAtlasFixer] TryAddCharacters failed: {e.Message}\n{e.StackTrace}");
            return;
        }

        Debug.Log($"[FontAtlasFixer] New atlas textures: {fontAsset.atlasTextures?.Length ?? 0}");
        Debug.Log($"[FontAtlasFixer] New character table: {fontAsset.characterTable?.Count ?? 0}");

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[FontAtlasFixer] Japanese font atlas rebuild complete!");
    }

    [MenuItem("Tools/Rebuild Japanese Font with JP Source")]
    public static void RebuildJapaneseFontWithJPSource()
    {
        Debug.Log("[FontAtlasFixer] Rebuilding Japanese font with correct JP source...");
        
        string fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSans-Regular SDF.asset";
        string jpSourcePath = "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSans JP-Regular.otf";
        
        Font jpSourceFont = AssetDatabase.LoadAssetAtPath<Font>(jpSourcePath);
        if (jpSourceFont == null)
        {
            Debug.LogError($"[FontAtlasFixer] Japanese source font not found: {jpSourcePath}");
            return;
        }
        
        Debug.Log($"[FontAtlasFixer] Found JP source font: {jpSourceFont.name}");
        
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (fontAsset == null)
        {
            Debug.LogError($"[FontAtlasFixer] Font asset not found: {fontPath}");
            return;
        }

        Debug.Log($"[FontAtlasFixer] Clearing existing font data...");
        
        fontAsset.ClearFontAssetData(true);
        
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        string hiragana = "ぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞただちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょよらりるれろゎわゐゑをんゔゕゖゝゞゟ";
        string katakana = "゠ァアィイゥウェエォオカガキギクグケゲコゴサザシジスズセゼソゾタダチヂッツヅテデトドナニヌネノハバパヒビピフブプヘベペホボポマミムメモャヤュユョヨラリルレロヮワヰヱヲンヴヵヶヷヸヹヺ・ーヽヾヿ";
        string punctuation = "。「」、・";
        string kanji = "語始終記難易速遅消見限視界部全被倍率延再初完成功失認知配置利使用設定定確認除戻選択続完了表示増減画像音色楽譜面映画面操作方向左右高低超大中小基本項目の内容完了開始戻る設定言語なし選択不明なタイトルノーマルプレイ終了クレジットスコア倍率ゲームオーディオグラフィックコントローラーオープンマスターボリューム音楽ボリューム効果音オフセット品質アンチエイリアスパーティクル密度触覚有効化強度曲名原語左コントローラーオフセット右回転リセット保存再開一時停止メインメニュー低中高ウルトラオフオンユーザー名スライダーパーフェクトスピナーボーナス不明なアーティストマッパーリザルト長さ難易度ハードロックイージーオートダブルタイムハーフタイムヒドゥンフラッシュライトリトライリプレイスコア精度最大コンボランクすべてが難しくなるリラックスして楽しもう完璧なオートプレイを見るに加速減速ノーツが徐々に消える視界が制限される見る";
        
        string charSet = hiragana + katakana + punctuation + kanji + string.Join("", CJK_COMMON_CHARS);
        charSet += " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()[]{}|;':\",./<>?`~-_=+";
        
        HashSet<char> uniqueChars = new HashSet<char>(charSet);
        charSet = new string(uniqueChars.ToArray());

        Debug.Log($"[FontAtlasFixer] Character set size: {charSet.Length} unique characters");

        List<uint> unicodeChars = new List<uint>();
        foreach (char c in charSet)
        {
            unicodeChars.Add((uint)c);
        }

        Debug.Log("[FontAtlasFixer] Adding characters to font...");
        
        try
        {
            fontAsset.TryAddCharacters(unicodeChars.ToArray(), out uint[] missingChars);
            Debug.Log($"[FontAtlasFixer] Added characters. Missing: {missingChars?.Length ?? 0}");

            if (missingChars != null && missingChars.Length > 0)
            {
                string missingStr = "";
                foreach (uint c in missingChars)
                {
                    if (c >= 0x3040 && c <= 0x30FF)
                    {
                        missingStr += (char)c;
                    }
                }
                if (!string.IsNullOrEmpty(missingStr))
                {
                    Debug.LogWarning($"[FontAtlasFixer] Missing Japanese characters: {missingStr}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FontAtlasFixer] TryAddCharacters failed: {e.Message}\n{e.StackTrace}");
            return;
        }

        Debug.Log($"[FontAtlasFixer] New atlas textures: {fontAsset.atlasTextures?.Length ?? 0}");
        Debug.Log($"[FontAtlasFixer] New character table: {fontAsset.characterTable?.Count ?? 0}");

        fontAsset.fallbackFontAssetTable.Clear();
        TMP_FontAsset liberationFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
        if (liberationFont != null)
        {
            fontAsset.fallbackFontAssetTable.Add(liberationFont);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[FontAtlasFixer] Japanese font rebuild complete!");
    }

    [MenuItem("Tools/Check Atlas Texture Data")]
    public static void CheckAtlasTextureData()
    {
        Debug.Log("[FontAtlasFixer] Checking atlas texture data...");
        
        string[] fontPaths = new string[]
        {
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSans-Regular SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSansSC-Regular SDF.asset"
        };

        foreach (string fontPath in fontPaths)
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[FontAtlasFixer] Font not found: {fontPath}");
                continue;
            }

            Debug.Log($"[FontAtlasFixer] Checking: {fontAsset.name}");
            Debug.Log($"[FontAtlasFixer]   Atlas textures: {fontAsset.atlasTextures?.Length ?? 0}");
            Debug.Log($"[FontAtlasFixer]   Character table: {fontAsset.characterTable?.Count ?? 0}");

            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    Texture2D tex = fontAsset.atlasTextures[i];
                    if (tex == null)
                    {
                        Debug.LogWarning($"[FontAtlasFixer]   Atlas {i}: NULL");
                        continue;
                    }

                    Debug.Log($"[FontAtlasFixer]   Atlas {i}: {tex.name}, {tex.width}x{tex.height}, format: {tex.format}");
                    
                    try
                    {
                        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(tex, rt);
                        RenderTexture prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        
                        Texture2D tempTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                        tempTex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                        tempTex.Apply();
                        
                        RenderTexture.active = prev;
                        RenderTexture.ReleaseTemporary(rt);

                        Color32[] pixels = tempTex.GetPixels32();
                        int nonZeroCount = 0;
                        int totalPixels = pixels.Length;
                        
                        for (int p = 0; p < Mathf.Min(1000, pixels.Length); p++)
                        {
                            if (pixels[p].r != 0 || pixels[p].g != 0 || pixels[p].b != 0 || pixels[p].a != 0)
                            {
                                nonZeroCount++;
                            }
                        }

                        Debug.Log($"[FontAtlasFixer]     Sample pixels: {nonZeroCount}/1000 non-zero");
                        
                        if (nonZeroCount == 0)
                        {
                            Debug.LogError($"[FontAtlasFixer]     ATLAS TEXTURE IS EMPTY! This font will not render correctly!");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[FontAtlasFixer]     Could not read texture: {e.Message}");
                    }
                }
            }
        }
    }

    [MenuItem("Tools/Create Japanese Fallback Font from System")]
    public static void CreateJapaneseFallbackFont()
    {
        Debug.Log("[FontAtlasFixer] Creating Japanese fallback font from system fonts...");
        
        string[] japaneseFontNames = new string[]
        {
            "Yu Gothic",
            "Meiryo",
            "MS Gothic",
            "Hiragino Sans",
            "Noto Sans CJK JP",
            "Source Han Sans JP"
        };

        Font systemFont = null;
        string foundFontName = null;

        foreach (string fontName in japaneseFontNames)
        {
            string[] guids = AssetDatabase.FindAssets(fontName + " t:Font");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                systemFont = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (systemFont != null)
                {
                    foundFontName = fontName;
                    Debug.Log($"[FontAtlasFixer] Found font asset: {path}");
                    break;
                }
            }
        }

        if (systemFont == null)
        {
            Debug.LogWarning("[FontAtlasFixer] No Japanese system font found in project. Trying to use OS fonts...");
            
            string[] osFontPaths = new string[]
            {
                @"C:\Windows\Fonts\yugothic.ttc",
                @"C:\Windows\Fonts\meiryo.ttc",
                @"C:\Windows\Fonts\msgothic.ttc"
            };

            foreach (string fontPath in osFontPaths)
            {
                if (System.IO.File.Exists(fontPath))
                {
                    Debug.Log($"[FontAtlasFixer] Found OS font: {fontPath}");
                    
                    string destPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/SystemJapanese.ttf";
                    if (!System.IO.File.Exists(destPath))
                    {
                        System.IO.File.Copy(fontPath, destPath, true);
                        AssetDatabase.ImportAsset(destPath);
                    }
                    
                    systemFont = AssetDatabase.LoadAssetAtPath<Font>(destPath);
                    if (systemFont != null)
                    {
                        foundFontName = System.IO.Path.GetFileNameWithoutExtension(fontPath);
                        break;
                    }
                }
            }
        }

        if (systemFont == null)
        {
            Debug.LogError("[FontAtlasFixer] Could not find any Japanese font. Please download Source Han Sans JP or another Japanese font.");
            return;
        }

        Debug.Log($"[FontAtlasFixer] Using font: {foundFontName}");

        string outputPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/JapaneseFallback SDF.asset";
        
        TMP_FontAsset existingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        if (existingFont != null)
        {
            Debug.Log("[FontAtlasFixer] Deleting existing Japanese fallback font...");
            AssetDatabase.DeleteAsset(outputPath);
        }

        Debug.Log("[FontAtlasFixer] Creating new TMP font asset...");
        
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(systemFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        
        if (fontAsset == null)
        {
            Debug.LogError("[FontAtlasFixer] Failed to create font asset!");
            return;
        }

        fontAsset.name = "JapaneseFallback SDF";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        AssetDatabase.CreateAsset(fontAsset, outputPath);

        string hiragana = "ぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞただちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょよらりるれろゎわゐゑをんゔゕゖゝゞゟ";
        string katakana = "゠ァアィイゥウェエォオカガキギクグケゲコゴサザシジスズセゼソゾタダチヂッツヅテデトドナニヌネノハバパヒビピフブプヘベペホボポマミムメモャヤュユョヨラリルレロヮワヰヱヲンヴヵヶヷヸヹヺ・ーヽヾヿ";
        string punctuation = "。「」、・";
        
        string charSet = hiragana + katakana + punctuation + " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        List<uint> unicodeChars = new List<uint>();
        foreach (char c in charSet)
        {
            unicodeChars.Add((uint)c);
        }

        Debug.Log("[FontAtlasFixer] Adding Japanese characters...");
        fontAsset.TryAddCharacters(unicodeChars.ToArray(), out uint[] missingChars);
        Debug.Log($"[FontAtlasFixer] Added characters. Missing: {missingChars?.Length ?? 0}");

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TMP_FontAsset scFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSansSC-Regular SDF.asset");
        
        if (scFont != null)
        {
            scFont.fallbackFontAssetTable.Clear();
            scFont.fallbackFontAssetTable.Add(fontAsset);
            
            TMP_FontAsset liberationFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
            if (liberationFont != null)
            {
                scFont.fallbackFontAssetTable.Add(liberationFont);
            }
            
            EditorUtility.SetDirty(scFont);
            Debug.Log("[FontAtlasFixer] Updated Chinese font fallback chain");
        }

        Debug.Log($"[FontAtlasFixer] Japanese fallback font created at: {outputPath}");
        Debug.Log("[FontAtlasFixer] Done!");
    }
}
