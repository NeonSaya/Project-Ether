using UnityEngine;

namespace OsuVR
{
    public class ModEffectsApplier
    {
        private ModSelection modSelection;

        public float SpeedMultiplier { get; private set; } = 1f;
        public float CircleSizeModifier { get; private set; } = 0f;
        public float ApproachRateModifier { get; private set; } = 0f;
        public float OverallDifficultyModifier { get; private set; } = 0f;
        public float NoteScaleModifier { get; private set; } = 1f;
        public bool IsAutoPlay { get; private set; } = false;
        public bool IsSuddenDeath { get; private set; } = false;
        public bool IsPerfect { get; private set; } = false;
        public float ScoreMultiplier { get; private set; } = 1f;

        public ModEffectsApplier(ModSelection selection)
        {
            modSelection = selection ?? new ModSelection();
            ApplyEffects();
        }

        public void ApplyEffects()
        {
            SpeedMultiplier = 1f;
            CircleSizeModifier = 0f;
            ApproachRateModifier = 0f;
            OverallDifficultyModifier = 0f;
            NoteScaleModifier = 1f;
            IsAutoPlay = false;
            IsSuddenDeath = false;
            IsPerfect = false;
            ScoreMultiplier = modSelection.GetTotalScoreMultiplier();

            foreach (var mod in modSelection.GetActiveMods())
            {
                ApplyModEffect(mod);
            }
        }

        private void ApplyModEffect(ModType mod)
        {
            switch (mod)
            {
                case ModType.HardRock:
                    ApplyHardRock();
                    break;

                case ModType.Easy:
                    ApplyEasy();
                    break;

                case ModType.Auto:
                    ApplyAuto();
                    break;

                case ModType.SuddenDeath:
                    ApplySuddenDeath();
                    break;

                case ModType.Perfect:
                    ApplyPerfect();
                    break;

                case ModType.DoubleTime:
                    ApplyDoubleTime();
                    break;

                case ModType.HalfTime:
                    ApplyHalfTime();
                    break;

                case ModType.Hidden:
                case ModType.FadeIn:
                case ModType.Flashlight:
                    break;
            }
        }

        private void ApplyHardRock()
        {
            CircleSizeModifier += 0.4f;
            ApproachRateModifier += 0.4f;
            OverallDifficultyModifier += 0.4f;
            NoteScaleModifier *= 0.9f;
        }

        private void ApplyEasy()
        {
            CircleSizeModifier -= 0.4f;
            ApproachRateModifier -= 0.4f;
            OverallDifficultyModifier -= 0.4f;
            NoteScaleModifier *= 1.1f;
        }

        private void ApplyAuto()
        {
            IsAutoPlay = true;
        }

        private void ApplySuddenDeath()
        {
            IsSuddenDeath = true;
        }

        private void ApplyPerfect()
        {
            IsPerfect = true;
        }

        private void ApplyDoubleTime()
        {
            SpeedMultiplier = 1.5f;
        }

        private void ApplyHalfTime()
        {
            SpeedMultiplier = 0.75f;
        }

        public float GetModifiedCS(float baseCS)
        {
            return Mathf.Max(0f, baseCS + CircleSizeModifier);
        }

        public float GetModifiedAR(float baseAR)
        {
            return Mathf.Clamp(baseAR + ApproachRateModifier, 0f, 10f);
        }

        public float GetModifiedOD(float baseOD)
        {
            return Mathf.Clamp(baseOD + OverallDifficultyModifier, 0f, 10f);
        }

        public float GetModifiedNoteScale(float baseScale)
        {
            return baseScale * NoteScaleModifier;
        }

        public double GetModifiedTimePreempt(float baseAR)
        {
            float modifiedAR = GetModifiedAR(baseAR);
            modifiedAR = Mathf.Clamp(modifiedAR, 0f, 10f);

            if (modifiedAR < 5)
            {
                return 1200 + 120 * (5 - modifiedAR);
            }
            else
            {
                return 1200 - 150 * (modifiedAR - 5);
            }
        }

        public double GetModifiedHitWindow(float baseOD, int windowType)
        {
            float modifiedOD = GetModifiedOD(baseOD);

            double baseWindow;
            switch (windowType)
            {
                case 300:
                    baseWindow = 80 - 6 * modifiedOD;
                    break;
                case 100:
                    baseWindow = 140 - 8 * modifiedOD;
                    break;
                case 50:
                    baseWindow = 200 - 10 * modifiedOD;
                    break;
                default:
                    baseWindow = 200 - 10 * modifiedOD;
                    break;
            }

            return baseWindow / SpeedMultiplier;
        }

        public bool ShouldFailOnMiss()
        {
            return IsSuddenDeath || IsPerfect;
        }

        public bool ShouldFailOnNon300()
        {
            return IsPerfect;
        }

        public string GetModString()
        {
            return modSelection.GetModString();
        }

        public bool IsRanked()
        {
            return modSelection.IsRanked();
        }
    }
}
