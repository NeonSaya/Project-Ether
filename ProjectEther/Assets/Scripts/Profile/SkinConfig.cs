using UnityEngine;

namespace OsuVR
{
    [CreateAssetMenu(fileName = "DefaultSkin", menuName = "OsuVR/Skin Config")]
    public class SkinConfig : ScriptableObject
    {
        [Header("Normal Set")]
        public AudioClip normal_hitnormal;
        public AudioClip normal_hitwhistle;
        public AudioClip normal_hitfinish;
        public AudioClip normal_hitclap;
        public AudioClip normal_sliderslide;
        public AudioClip normal_slidertick;
        public AudioClip normal_sliderwhistle;

        [Header("Soft Set")]
        public AudioClip soft_hitnormal;
        public AudioClip soft_hitwhistle;
        public AudioClip soft_hitfinish;
        public AudioClip soft_hitclap;
        public AudioClip soft_sliderslide;
        public AudioClip soft_slidertick;
        public AudioClip soft_sliderwhistle;

        [Header("Drum Set")]
        public AudioClip drum_hitnormal;
        public AudioClip drum_hitwhistle;
        public AudioClip drum_hitfinish;
        public AudioClip drum_hitclap;
        public AudioClip drum_sliderslide;
        public AudioClip drum_slidertick;
        public AudioClip drum_sliderwhistle;

        // 辅助方法：根据枚举获取默认切片
        public AudioClip GetDefaultClip(SampleSet set, HitSoundType type, bool isSliderSlide = false, bool isSliderTick = false)
        {
            // 简单路由逻辑
            switch (set)
            {
                case SampleSet.Drum:
                    if (isSliderSlide) return drum_sliderslide;
                    if (isSliderTick) return drum_slidertick;
                    if ((type & HitSoundType.Finish) != 0) return drum_hitfinish;
                    if ((type & HitSoundType.Whistle) != 0) return drum_hitwhistle; // SliderWhistle 通常复用这个或单独处理
                    if ((type & HitSoundType.Clap) != 0) return drum_hitclap;
                    return drum_hitnormal;

                case SampleSet.Soft:
                    if (isSliderSlide) return soft_sliderslide;
                    if (isSliderTick) return soft_slidertick;
                    if ((type & HitSoundType.Finish) != 0) return soft_hitfinish;
                    if ((type & HitSoundType.Whistle) != 0) return soft_hitwhistle;
                    if ((type & HitSoundType.Clap) != 0) return soft_hitclap;
                    return soft_hitnormal;

                case SampleSet.Normal:
                default:
                    if (isSliderSlide) return normal_sliderslide;
                    if (isSliderTick) return normal_slidertick;
                    if ((type & HitSoundType.Finish) != 0) return normal_hitfinish;
                    if ((type & HitSoundType.Whistle) != 0) return normal_hitwhistle;
                    if ((type & HitSoundType.Clap) != 0) return normal_hitclap;
                    return normal_hitnormal;
            }
        }
    }
}