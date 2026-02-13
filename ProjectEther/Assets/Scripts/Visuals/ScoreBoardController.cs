using UnityEngine;
using TMPro;
using System.Collections;

namespace OsuVR
{
    /// <summary>
    /// 纯视觉控制器：负责分数的滚动显示、Combo 动画、Acc 动画
    /// </summary>
    public class ScoreBoardController : MonoBehaviour
    {
        [Header("UI 组件")]
        public TextMeshProUGUI textScore;
        public TextMeshProUGUI textCombo;
        public TextMeshProUGUI textAcc;
        public GameObject comboGroup; // 将 Combo 文字和标签放在一个父物体下，方便整体隐藏

        [Header("动画设置")]
        public float scoreScrollSpeed = 5.0f; // 分数滚动速度
        public float punchScale = 1.15f;       // Combo 跳动大小
        public Color comboBreakColor = Color.red;

        // 内部显示数值 (float 用于平滑滚动)
        private float displayScore = 0;
        private double displayAcc = 1.0;

        // 目标数值
        private long targetScore = 0;
        private double targetAcc = 1.0;
        private int currentCombo = 0;

        private Vector3 comboOriginalScale;
        private Color comboOriginalColor;
        private bool isComboBroken = false;

        void Start()
        {
            if (textCombo)
            {
                comboOriginalScale = Vector3.one; // 假设初始是 1
                comboOriginalColor = textCombo.color;
            }

            // 初始状态隐藏 Combo
            if (comboGroup) comboGroup.SetActive(false);
            else if (textCombo) textCombo.gameObject.SetActive(false);
        }

        void Update()
        {
            // 1. 分数滚动逻辑 (Lerp 插值)
            // 这种写法会让数字跳动由于 "追赶" 而产生滚动感
            if (Mathf.Abs(displayScore - targetScore) > 1f)
            {
                // 使用 Lerp 会有"快->慢"的感觉
                displayScore = Mathf.Lerp(displayScore, targetScore, Time.deltaTime * scoreScrollSpeed);
                // 也可以用 MoveTowards 匀速滚动:
                // displayScore = Mathf.MoveTowards(displayScore, targetScore, Time.deltaTime * 50000f); 

                // 更新文本
                if (textScore) textScore.text = ((long)displayScore).ToString("D6");
            }
            else if ((long)displayScore != targetScore)
            {
                // 最后的吸附，防止小数误差
                displayScore = targetScore;
                if (textScore) textScore.text = targetScore.ToString("D6");
            }

            // 2. Acc 滚动逻辑
            if (Mathf.Abs((float)(displayAcc - targetAcc)) > 0.0001f)
            {
                displayAcc = Mathf.Lerp((float)displayAcc, (float)targetAcc, Time.deltaTime * 3f);
                if (textAcc) textAcc.text = $"{displayAcc * 100:F2}%";
            }
        }

        /// <summary>
        /// 外部调用：更新面板数据
        /// </summary>
        public void UpdateDashboard(long score, int combo, double acc)
        {
            targetScore = score;
            targetAcc = acc;

            // Combo 处理
            if (combo > currentCombo)
            {
                // Combo 增加：跳动动画
                currentCombo = combo;
                UpdateComboText();
                PunchCombo();

                // 确保显示
                if (comboGroup) comboGroup.SetActive(true);
                else if (textCombo) textCombo.gameObject.SetActive(true);
            }
            else if (combo == 0 && currentCombo > 0)
            {
                // Combo 断连：播放断连动画
                currentCombo = 0;
                StartCoroutine(ComboBreakEffect());
            }
            else if (combo == 0)
            {
                // 一直是 0
                currentCombo = 0;
            }
        }

        private void UpdateComboText()
        {
            if (textCombo) textCombo.text = $"{currentCombo}";
        }

        private void PunchCombo()
        {
            if (!textCombo) return;
            // 简单的放大回弹
            StopCoroutine("AnimatePunch");
            StartCoroutine("AnimatePunch");
        }

        private IEnumerator AnimatePunch()
        {
            float t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(punchScale, 1.0f, t * 10f); // 快速回弹
                textCombo.transform.localScale = comboOriginalScale * scale;
                yield return null;
            }
            textCombo.transform.localScale = comboOriginalScale;
        }

        /// <summary>
        /// 断连动画：变红 -> 掉落/消失 -> 隐藏
        /// </summary>
        private IEnumerator ComboBreakEffect()
        {
            if (!textCombo) yield break;

            isComboBroken = true;
            textCombo.color = comboBreakColor; // 变红

            // 震动一下 (可选: 往下掉)
            Vector3 originalPos = textCombo.transform.localPosition;
            float t = 0;

            while (t < 0.3f) // 0.3秒动画
            {
                t += Time.deltaTime;
                // 往下掉一点点
                textCombo.transform.localPosition = originalPos - new Vector3(0, t * 0.5f, 0);
                // 变透明
                textCombo.alpha = 1f - (t / 0.3f);
                yield return null;
            }

            // 动画结束：还原并隐藏
            if (comboGroup) comboGroup.SetActive(false);
            else textCombo.gameObject.SetActive(false);

            textCombo.color = comboOriginalColor;
            textCombo.alpha = 1f;
            textCombo.transform.localPosition = originalPos;
            textCombo.transform.localScale = comboOriginalScale;
            isComboBroken = false;
        }
    }
}