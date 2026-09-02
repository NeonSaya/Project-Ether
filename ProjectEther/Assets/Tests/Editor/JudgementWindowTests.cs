using NUnit.Framework;

namespace OsuVR.Tests
{
    /// <summary>
    /// OD 绑定判定窗口公式测试（JudgementConfig.GetWindowMs / ScoreFromAccuracy）
    /// 公式：350 - 12.5 × OD（OD Clamp 0~10）
    /// 锚点：OD8 = 250ms（历史固定窗口，行为必须保持一致）
    /// 护栏：OD10 = 225ms（不过难）、OD0 = 350ms（不过易）
    /// </summary>
    public class JudgementWindowTests
    {
        [Test]
        public void OD8_MatchesLegacyFixedWindow()
        {
            Assert.AreEqual(250.0, JudgementConfig.GetWindowMs(8f), 1e-9,
                "OD8 必须精确等于历史固定窗口 250ms（行为不变锚点）");
        }

        [Test]
        public void OD0_Is350()
        {
            Assert.AreEqual(350.0, JudgementConfig.GetWindowMs(0f), 1e-9);
        }

        [Test]
        public void OD10_Is225()
        {
            Assert.AreEqual(225.0, JudgementConfig.GetWindowMs(10f), 1e-9);
        }

        [Test]
        public void OutOfRangeOD_IsClamped()
        {
            Assert.AreEqual(350.0, JudgementConfig.GetWindowMs(-3f), 1e-9,
                "OD < 0 应钳制到 0（最宽 350ms）");
            Assert.AreEqual(225.0, JudgementConfig.GetWindowMs(11.5f), 1e-9,
                "OD > 10 应钳制到 10（最窄 225ms）");
        }

        [Test]
        public void Window_MonotonicNonIncreasing()
        {
            double prev = JudgementConfig.GetWindowMs(0f);
            for (float od = 0.5f; od <= 10f; od += 0.5f)
            {
                double cur = JudgementConfig.GetWindowMs(od);
                Assert.LessOrEqual(cur, prev, $"OD={od} 的窗口不应比更低 OD 更宽");
                prev = cur;
            }
        }

        [Test]
        public void OD8_TierBoundaries_MatchLegacyScoring()
        {
            // 历史行为锚定：|diff|<=50ms→300, <=100ms→100, <=247.5ms→50, 250ms→0(Miss)
            double w = JudgementConfig.GetWindowMs(8f);
            Assert.AreEqual(300, JudgementConfig.ScoreFromAccuracy(1.0 - 50.0 / w));
            Assert.AreEqual(100, JudgementConfig.ScoreFromAccuracy(1.0 - 100.0 / w));
            Assert.AreEqual(50, JudgementConfig.ScoreFromAccuracy(1.0 - 247.5 / w));
            Assert.AreEqual(0, JudgementConfig.ScoreFromAccuracy(1.0 - 250.0 / w));
        }

        [Test]
        public void HigherOD_TightensScoring()
        {
            // 同一 48ms 误差：OD8 给 300，OD10 只给 100
            double acc8 = 1.0 - 48.0 / JudgementConfig.GetWindowMs(8f);
            double acc10 = 1.0 - 48.0 / JudgementConfig.GetWindowMs(10f);
            Assert.AreEqual(300, JudgementConfig.ScoreFromAccuracy(acc8));
            Assert.AreEqual(100, JudgementConfig.ScoreFromAccuracy(acc10));
        }

        [Test]
        public void LowerOD_LoosensScoring()
        {
            // 同一 60ms 误差：OD8 给 100，OD0 给 300
            double acc8 = 1.0 - 60.0 / JudgementConfig.GetWindowMs(8f);
            double acc0 = 1.0 - 60.0 / JudgementConfig.GetWindowMs(0f);
            Assert.AreEqual(100, JudgementConfig.ScoreFromAccuracy(acc8));
            Assert.AreEqual(300, JudgementConfig.ScoreFromAccuracy(acc0));
        }
    }
}
