using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace OsuVR
{
    /// <summary>
    /// 音频管理器：处理打击音效、滑条音效、转盘音效
    /// 对齐 osu!lazer 的音效系统
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // =========================================================
        // 配置
        // =========================================================
        [Header("配置")]
        public SkinConfig defaultSkin;
        [Range(0, 1)] public float masterVolume = 1.0f;
        [Range(0, 1)] public float musicVolume = 0.8f;
        [Range(0, 1)] public float sfxVolume = 1.0f;

        // =========================================================
        // 运行时状态
        // =========================================================
        [Header("运行时")]
        private Dictionary<string, AudioClip> beatmapSkinCache = new Dictionary<string, AudioClip>();
        private AudioSource sliderLoopSource;
        private AudioSource spinnerLoopSource;
        private List<AudioSource> oneShotPool = new List<AudioSource>();

        // =========================================================
        // 生命周期
        // =========================================================

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeAudioSources();
        }

        private void InitializeAudioSources()
        {
            // 1. 初始化 OneShot 池 (用于打击音效)
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject("SFX_OneShot_" + i);
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0;
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
        // 1. 加载谱面自定义音效
        // =========================================================

        /// <summary>
        /// 加载谱面文件夹中的自定义音效
        /// </summary>
        public void LoadBeatmapSamples(string mapFolderPath)
        {
            foreach (var clip in beatmapSkinCache.Values)
            {
                if (clip != null) Destroy(clip);
            }
            beatmapSkinCache.Clear();

            StartCoroutine(LoadSamplesRoutine(mapFolderPath));
        }

        private IEnumerator LoadSamplesRoutine(string folder)
        {
            string[] files = Directory.GetFiles(folder, "*.wav");
            foreach (var filePath in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();

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
        // 2. 核心播放逻辑 - 普通 HitObject
        // =========================================================

        /// <summary>
        /// 播放打击音效 (HitCircle / SliderHead)
        /// </summary>
        public void PlayHitSound(HitObject hitObject)
        {
            HitSoundType soundType = hitObject.HitSound;
            if (soundType == HitSoundType.None)
            {
                soundType = HitSoundType.Normal;
            }

            float finalVolume = hitObject.SampleVolume / 100f;
            if (finalVolume <= 0f) finalVolume = 1.0f;

            SampleSet set = hitObject.SampleSet;
            if (set == SampleSet.None) set = SampleSet.Normal;

            SampleSet additionSet = hitObject.AdditionSet;
            if (additionSet == SampleSet.None) additionSet = set;

            int customIndex = hitObject.CustomIndex;

            // 始终播放 Normal (底鼓)
            PlaySpecificSample(set, HitSoundType.Normal, customIndex, finalVolume);

            // 叠加附加音效 (使用 AdditionSet)
            if ((soundType & HitSoundType.Whistle) != 0)
                PlaySpecificSample(additionSet, HitSoundType.Whistle, customIndex, finalVolume);
            if ((soundType & HitSoundType.Finish) != 0)
                PlaySpecificSample(additionSet, HitSoundType.Finish, customIndex, finalVolume);
            if ((soundType & HitSoundType.Clap) != 0)
                PlaySpecificSample(additionSet, HitSoundType.Clap, customIndex, finalVolume);
        }

        // =========================================================
        // 3. 滑条节点音效 (osu!lazer 标准)
        // =========================================================

        /// <summary>
        /// 播放滑条节点音效
        /// osu! 中每个滑条节点都有独立的音效配置
        /// </summary>
        /// <param name="slider">滑条对象</param>
        /// <param name="nodeIndex">节点索引 (0=Head, 1+=Repeat/Tail)</param>
        public void PlaySliderNodeSound(SliderObject slider, int nodeIndex)
        {
            if (slider == null) return;

            // 获取节点音效列表
            List<HitSampleInfo> nodeSamples = null;
            if (slider.NodeSamples != null && nodeIndex < slider.NodeSamples.Count)
            {
                nodeSamples = slider.NodeSamples[nodeIndex];
            }

            // 默认音量和音效库
            float volume = slider.SampleVolume / 100f;
            if (volume <= 0f) volume = 1.0f;

            SampleSet sampleSet = slider.SampleSet;
            if (sampleSet == SampleSet.None) sampleSet = SampleSet.Normal;

            SampleSet additionSet = slider.AdditionSet;
            if (additionSet == SampleSet.None) additionSet = sampleSet;

            int customIndex = slider.CustomIndex;

            // 如果有节点音效配置，使用节点的配置
            if (nodeSamples != null && nodeSamples.Count > 0)
            {
                PlayNodeSamples(nodeSamples, volume);
            }
            else
            {
                // 回退：使用滑条默认音效
                HitSoundType soundType = slider.HitSound;
                if (soundType == HitSoundType.None) soundType = HitSoundType.Normal;

                PlaySpecificSample(sampleSet, HitSoundType.Normal, customIndex, volume);

                if ((soundType & HitSoundType.Whistle) != 0)
                    PlaySpecificSample(additionSet, HitSoundType.Whistle, customIndex, volume);
                if ((soundType & HitSoundType.Finish) != 0)
                    PlaySpecificSample(additionSet, HitSoundType.Finish, customIndex, volume);
                if ((soundType & HitSoundType.Clap) != 0)
                    PlaySpecificSample(additionSet, HitSoundType.Clap, customIndex, volume);
            }
        }

        /// <summary>
        /// 播放节点音效列表 (从 NodeSamples 解析)
        /// </summary>
        private void PlayNodeSamples(List<HitSampleInfo> samples, float volume)
        {
            foreach (var sample in samples)
            {
                if (sample is BankHitSampleInfo bankSample)
                {
                    SampleSet set = ConvertBankToSampleSet(bankSample.Bank);
                    HitSoundType type = ConvertNameToHitSoundType(bankSample.Name);

                    float vol = bankSample.Volume > 0 ? bankSample.Volume / 100f : volume;

                    PlaySpecificSample(set, type, bankSample.CustomSampleBank, vol);
                }
            }
        }

        /// <summary>
        /// 将 SampleBank 转换为 SampleSet
        /// </summary>
        private SampleSet ConvertBankToSampleSet(SampleBank bank)
        {
            return bank switch
            {
                SampleBank.Soft => SampleSet.Soft,
                SampleBank.Drum => SampleSet.Drum,
                _ => SampleSet.Normal
            };
        }

        /// <summary>
        /// 将音效名称转换为 HitSoundType
        /// </summary>
        private HitSoundType ConvertNameToHitSoundType(string name)
        {
            if (string.IsNullOrEmpty(name)) return HitSoundType.Normal;

            if (name.Contains("whistle")) return HitSoundType.Whistle;
            if (name.Contains("finish")) return HitSoundType.Finish;
            if (name.Contains("clap")) return HitSoundType.Clap;

            return HitSoundType.Normal;
        }

        // =========================================================
        // 4. 滑条滑动音效
        // =========================================================

        /// <summary>
        /// 播放滑条 Tick 音效
        /// </summary>
        public void PlaySliderTick(SampleSet set, int index, float volume)
        {
            float finalVol = volume * masterVolume;
            AudioClip clip = GetClip(set, HitSoundType.Normal, index, false, true);
            if (clip) PlayOneShot(clip, finalVol);
        }

        /// <summary>
        /// 切换滑条滑动循环音效
        /// </summary>
        public void ToggleSliderLoop(bool isPlaying, SampleSet set = SampleSet.Normal, int index = 0)
        {
            if (isPlaying)
            {
                if (!sliderLoopSource.isPlaying)
                {
                    AudioClip clip = GetClip(set, HitSoundType.Normal, index, true, false);
                    if (clip)
                    {
                        sliderLoopSource.clip = clip;
                        sliderLoopSource.volume = 0.5f * masterVolume;
                        sliderLoopSource.Play();
                    }
                }
            }
            else
            {
                sliderLoopSource.Stop();
            }
        }

        // =========================================================
        // 5. 转盘音效
        // =========================================================

        /// <summary>
        /// 更新转盘循环音效
        /// </summary>
        public void UpdateSpinnerLoop(bool isSpinning, float intensity)
        {
            if (isSpinning)
            {
                if (!spinnerLoopSource.isPlaying && defaultSkin.soft_sliderslide)
                {
                    spinnerLoopSource.clip = defaultSkin.soft_sliderslide;
                    spinnerLoopSource.Play();
                }

                spinnerLoopSource.volume = Mathf.Clamp01(intensity) * masterVolume;
                spinnerLoopSource.pitch = 1.0f + (intensity * 0.2f);
            }
            else
            {
                spinnerLoopSource.Stop();
            }
        }

        // =========================================================
        // 6. 底层播放工具
        // =========================================================

        private void PlaySpecificSample(SampleSet set, HitSoundType type, int index, float volume)
        {
            AudioClip clip = GetClip(set, type, index);
            if (clip != null)
            {
                PlayOneShot(clip, volume * masterVolume);
            }
        }

        /// <summary>
        /// 查找音频文件
        /// </summary>
        private AudioClip GetClip(SampleSet set, HitSoundType type, int index, bool isSlide = false, bool isTick = false)
        {
            string prefix = set.ToString().ToLower();
            string middle = "hit";
            string suffix = type.ToString().ToLower();

            if (isSlide) { middle = "slider"; suffix = "slide"; }
            else if (isTick) { middle = "slider"; suffix = "tick"; }
            else if (type == HitSoundType.Normal) suffix = "normal";

            string indexStr = (index > 1) ? index.ToString() : "";
            string searchKey = $"{prefix}-{middle}{suffix}{indexStr}";

            // 从自定义缓存找
            if (beatmapSkinCache.TryGetValue(searchKey, out AudioClip customClip))
            {
                return customClip;
            }

            // 回退查找
            if (index > 1)
            {
                string fallbackKey = $"{prefix}-{middle}{suffix}";
                if (beatmapSkinCache.TryGetValue(fallbackKey, out AudioClip fallbackClip))
                    return fallbackClip;
            }

            // 从默认皮肤找
            return defaultSkin.GetDefaultClip(set, type, isSlide, isTick);
        }

        private void PlayOneShot(AudioClip clip, float vol)
        {
            // [修复] 找一个空闲的 AudioSource，如果找不到就创建新的
            var src = oneShotPool.Find(s => !s.isPlaying);
            if (src == null)
            {
                // 池用完了，动态创建新的 AudioSource
                var go = new GameObject("SFX_OneShot_Dynamic_" + oneShotPool.Count);
                go.transform.SetParent(transform);
                src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0;
                oneShotPool.Add(src);
                Debug.LogWarning($"[Audio] 音效池已满，动态创建新的 AudioSource，当前池大小: {oneShotPool.Count}");
            }
            src.volume = vol;
            src.PlayOneShot(clip);
        }

        // =========================================================
        // Settings Integration
        // =========================================================

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }
    }
}
