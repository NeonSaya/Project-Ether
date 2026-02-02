using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace OsuVR
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("配置")]
        public SkinConfig defaultSkin; // 拖入刚才创建的 SkinData
        [Range(0, 1)] public float masterVolume = 1.0f; // 全局音效音量

        [Header("运行时")]
        // 缓存当前谱面的自定义音效：Key = "soft-hitnormal2", Value = AudioClip
        private Dictionary<string, AudioClip> beatmapSkinCache = new Dictionary<string, AudioClip>();

        // 专门用于滑条滑动的循环音源
        private AudioSource sliderLoopSource;
        private AudioSource spinnerLoopSource;
        private List<AudioSource> oneShotPool = new List<AudioSource>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // ✅ 加上这句：切换场景不销毁
            }
            else
            {
                Destroy(gameObject); // 防止重复创建
                return; // 重要：如果销毁了自己，不要继续执行后面的初始化
            }

            InitializeAudioSources();
        }

        private void InitializeAudioSources()
        {
            // 1. 初始化 OneShot 池 (用于打击音)
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject("SFX_OneShot_" + i);
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0; // 2D 声音，不需要空间感
                oneShotPool.Add(src);
            }

            // 2. 初始化滑条循环源
            var slideGo = new GameObject("SFX_SliderLoop");
            slideGo.transform.SetParent(transform);
            sliderLoopSource = slideGo.AddComponent<AudioSource>();
            sliderLoopSource.loop = true;
            sliderLoopSource.playOnAwake = false;
            sliderLoopSource.spatialBlend = 0;

            // 3. 初始化转盘循环源
            var spinGo = new GameObject("SFX_SpinnerLoop");
            spinGo.transform.SetParent(transform);
            spinnerLoopSource = spinGo.AddComponent<AudioSource>();
            spinnerLoopSource.loop = true;
            spinnerLoopSource.playOnAwake = false;
            spinnerLoopSource.spatialBlend = 0;
        }

        // =========================================================
        // 1. 加载谱面自定义音效 (在 RhythmGameManager 加载谱面时调用)
        // =========================================================
        public void LoadBeatmapSamples(string mapFolderPath)
        {
            // 清理上一首歌的缓存
            foreach (var clip in beatmapSkinCache.Values)
            {
                if (clip != null) Destroy(clip); // 释放内存
            }
            beatmapSkinCache.Clear();

            StartCoroutine(LoadSamplesRoutine(mapFolderPath));
        }

        private IEnumerator LoadSamplesRoutine(string folder)
        {
            // 扫描文件夹下所有的 .wav 文件
            string[] files = Directory.GetFiles(folder, "*.wav");
            foreach (var filePath in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower(); // 转小写作为 Key

                // 只加载符合 osu 命名规范的文件 (micro-optimization)
                if (!fileName.Contains("hit") && !fileName.Contains("slider")) continue;

                string url = "file://" + filePath;
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                        clip.name = fileName;
                        beatmapSkinCache[fileName] = clip;
                    }
                }
            }
            Debug.Log($"[Audio] 加载了 {beatmapSkinCache.Count} 个自定义音效");
        }

        // =========================================================
        // 2. 核心播放逻辑
        // =========================================================

        /// <summary>
        /// 播放打击音效 (Note / SliderHead / SliderEnd)
        /// </summary>
        public void PlayHitSound(HitObject hitObject)
        {
            // 1. 获取并修正 HitSound 类型
            // 如果是 None (0)，强制设为 Normal (1)，否则没声音
            HitSoundType soundType = hitObject.HitSound;
            if (soundType == HitSoundType.None)
            {
                soundType = HitSoundType.Normal;
            }

            Debug.Log($"[Audio] 收到播放请求: {soundType} (原始: {hitObject.HitSound})");

            // 2. 获取基础信息
            SampleSet set = hitObject.SampleSet;
            if (set == SampleSet.None) set = SampleSet.Normal; // 默认音效库

            // 注意：osu! 中 Additions (Whistle/Finish/Clap) 通常使用 AdditionSet，
            // 但为了保持和你现有逻辑一致，这里暂时都用 set (SampleSet)
            SampleSet additionSet = hitObject.AdditionSet; 
            if (additionSet == SampleSet.None) additionSet = set;

            int customIndex = hitObject.CustomIndex;
            float volume = hitObject.SampleVolume / 100f;
            if (volume <= 0.01f) volume = 1.0f; // 兜底音量

            // 3. 播放逻辑
            // 始终播放 Base Hit (Normal) - 除非你只想听纯哨子，否则通常都会叠加一个底鼓声
            PlaySpecificSample(set, HitSoundType.Normal, customIndex, volume);

            // 4. 叠加音效 (使用修正后的 soundType 进行判断)
            if ((soundType & HitSoundType.Whistle) != 0)
                PlaySpecificSample(set, HitSoundType.Whistle, customIndex, volume);

            if ((soundType & HitSoundType.Finish) != 0)
                PlaySpecificSample(set, HitSoundType.Finish, customIndex, volume);

            if ((soundType & HitSoundType.Clap) != 0)
                PlaySpecificSample(set, HitSoundType.Clap, customIndex, volume);
        }

        private void PlaySpecificSample(SampleSet set, HitSoundType type, int index, float volume)
        {
            AudioClip clip = GetClip(set, type, index);
            if (clip != null)
            {
                PlayOneShot(clip, volume * masterVolume);
            }
        }

        /// <summary>
        /// 查找音频文件的核心算法
        /// </summary>
        private AudioClip GetClip(SampleSet set, HitSoundType type, int index, bool isSlide = false, bool isTick = false)
        {
            // 1. 构建文件名
            // 格式: {set}-hit{type}{index} 或 {set}-slider{type}{index}

            string prefix = set.ToString().ToLower(); // "normal", "soft", "drum"
            string middle = "hit";
            string suffix = type.ToString().ToLower();

            if (isSlide) { middle = "slider"; suffix = "slide"; }
            else if (isTick) { middle = "slider"; suffix = "tick"; }
            else if (type == HitSoundType.Normal) suffix = "normal"; // hitnormal

            string indexStr = (index > 1) ? index.ToString() : ""; // index=0或1时不加后缀，index=2变 "2"

            string searchKey = $"{prefix}-{middle}{suffix}{indexStr}"; // e.g., "soft-hitnormal2"

            // 2. 尝试从自定义缓存找
            if (beatmapSkinCache.TryGetValue(searchKey, out AudioClip customClip))
            {
                return customClip;
            }

            // 3. 如果没找到且 index > 1，尝试回退找无后缀的 (soft-hitnormal2 -> soft-hitnormal)
            if (index > 1)
            {
                string fallbackKey = $"{prefix}-{middle}{suffix}";
                if (beatmapSkinCache.TryGetValue(fallbackKey, out AudioClip fallbackClip))
                    return fallbackClip;
            }

            // 4. 从默认皮肤找
            return defaultSkin.GetDefaultClip(set, type, isSlide, isTick);
        }

        // --- 播放工具 ---
        private void PlayOneShot(AudioClip clip, float vol)
        {
            var src = oneShotPool.Find(s => !s.isPlaying);
            if (src != null)
            {
                src.volume = vol;
                src.PlayOneShot(clip);
            }
        }

        // =========================================================
        // 3. 滑条与转盘专用控制
        // =========================================================

        public void PlaySliderTick(SampleSet set, int index, float volume)
        {
            AudioClip clip = GetClip(set, HitSoundType.Normal, index, false, true); // isTick = true
            if (clip) PlayOneShot(clip, volume * masterVolume);
        }

        public void ToggleSliderLoop(bool isPlaying, SampleSet set = SampleSet.Normal, int index = 0)
        {
            if (isPlaying)
            {
                if (!sliderLoopSource.isPlaying)
                {
                    // 查找 Loop 声音
                    AudioClip clip = GetClip(set, HitSoundType.Normal, index, true, false); // isSlide = true
                    if (clip)
                    {
                        sliderLoopSource.clip = clip;
                        sliderLoopSource.volume = 0.5f * masterVolume; // 滑行声音通常小一点
                        sliderLoopSource.Play();
                    }
                }
            }
            else
            {
                sliderLoopSource.Stop();
            }
        }

        public void UpdateSpinnerLoop(bool isSpinning, float intensity)
        {
            if (isSpinning)
            {
                // 如果没有 Spin 音效，可以用 soft-sliderslide 暂代，或者你需要提供 drum-spinnerspin
                // 这里假设你有，或者就用 slide
                if (!spinnerLoopSource.isPlaying && defaultSkin.soft_sliderslide) // 兜底
                {
                    spinnerLoopSource.clip = defaultSkin.soft_sliderslide; // 暂时用 slide 替代
                    spinnerLoopSource.Play();
                }

                // 随速度改变音量和音调
                spinnerLoopSource.volume = Mathf.Clamp01(intensity) * masterVolume;
                spinnerLoopSource.pitch = 1.0f + (intensity * 0.2f); // 越快越尖
            }
            else
            {
                spinnerLoopSource.Stop();
            }
        }
    }
}