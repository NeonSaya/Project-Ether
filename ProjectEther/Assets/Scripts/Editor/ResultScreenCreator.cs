#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using OsuVR;

namespace OsuVR.Editor
{
    public static class ResultScreenCreator
    {
        private const float CANVAS_SCALE = 0.0025f;
        private const float CANVAS_WIDTH = 900f;
        private const float CANVAS_HEIGHT = 650f;

        [MenuItem("Project Ether/简单配置/结算界面场景", false, 4)]
        public static void SetupResultScreen()
        {
            GameObject root = CreateResultScreenRoot();
            
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create ResultScreen");
            
            Debug.Log("[ResultScreenCreator] 结算界面场景配置完成！");
        }

        [MenuItem("Project Ether/工具/创建结算界面预制体", false, 105)]
        public static void CreateResultScreenPrefab()
        {
            GameObject root = CreateResultScreenRoot();
            
            string prefabPath = "Assets/Prefabs/UI/ResultScreen.prefab";
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[ResultScreenCreator] 结算界面预制体已创建: {prefabPath}");
        }

        public static GameObject CreateResultScreenRoot()
        {
            GameObject root = new GameObject("[ResultScreen]");
            ResultScreenController controller = root.AddComponent<ResultScreenController>();
            
            Canvas canvas = CreateWorldCanvas("ResultCanvas", root.transform);
            
            GameObject resultPanel = CreateResultPanel(canvas.transform);
            controller.resultPanel = resultPanel;
            
            CanvasGroup canvasGroup = resultPanel.GetComponent<CanvasGroup>();
            controller.canvasGroup = canvasGroup;
            
            CreateBackground(resultPanel.transform);
            
            CreateHeaderSection(resultPanel.transform, controller);
            
            CreateScoreSection(resultPanel.transform, controller);
            
            CreateJudgmentSection(resultPanel.transform, controller);
            
            CreateButtonSection(resultPanel.transform, controller);
            
            CreateFullComboEffect(root.transform, controller);
            
            AudioSource audioSource = root.AddComponent<AudioSource>();
            controller.audioSource = audioSource;
            
            SetupAnimationCurve(controller);
            
            return root;
        }

        private static Canvas CreateWorldCanvas(string name, Transform parent)
        {
            GameObject canvasObj = new GameObject(name);
            canvasObj.transform.SetParent(parent);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT);
            rect.localScale = Vector3.one * CANVAS_SCALE;
            rect.localPosition = new Vector3(0, 1.5f, 2f);
            rect.localRotation = Quaternion.identity;

            return canvas;
        }

        private static GameObject CreateResultPanel(Transform parent)
        {
            GameObject panel = new GameObject("ResultPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.05f, 0.92f);

            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            return panel;
        }

        private static void CreateBackground(Transform parent)
        {
            GameObject gradientBg = new GameObject("GradientBackground");
            gradientBg.transform.SetParent(parent, false);

            RectTransform rect = gradientBg.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image bgImage = gradientBg.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.02f, 0.1f, 0.5f);
        }

        private static void CreateHeaderSection(Transform parent, ResultScreenController controller)
        {
            GameObject header = new GameObject("HeaderSection");
            header.transform.SetParent(parent, false);

            RectTransform rect = header.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 100);
            rect.anchoredPosition = new Vector2(0, -20);

            Image headerBg = header.AddComponent<Image>();
            headerBg.color = new Color(0.08f, 0.05f, 0.15f, 0.6f);

            GameObject titleObj = CreateLocalizedLabel("Title", header.transform, "", 
                new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(700, 40), 28, TextAlignmentOptions.Center, null);
            controller.textTitle = titleObj.GetComponent<TextMeshProUGUI>();
            controller.textTitle.fontStyle = FontStyles.Bold;
            controller.textTitle.color = new Color(1f, 0.9f, 0.95f);

            GameObject artistObj = CreateLocalizedLabel("Artist", header.transform, "", 
                new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f), new Vector2(500, 28), 18, TextAlignmentOptions.Center, null);
            controller.textArtist = artistObj.GetComponent<TextMeshProUGUI>();
            controller.textArtist.color = new Color(0.8f, 0.75f, 0.85f);

            GameObject diffObj = CreateLocalizedLabel("Difficulty", header.transform, "", 
                new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(400, 24), 16, TextAlignmentOptions.Center, null);
            controller.textDifficulty = diffObj.GetComponent<TextMeshProUGUI>();
            controller.textDifficulty.color = new Color(0.6f, 0.7f, 1f);

            GameObject mapperObj = CreateLocalizedLabel("Mapper", header.transform, "", 
                new Vector2(1f, 0.15f), new Vector2(1f, 0.15f), new Vector2(300, 24), 14, TextAlignmentOptions.Right, null);
            controller.textMapper = mapperObj.GetComponent<TextMeshProUGUI>();
            controller.textMapper.color = new Color(0.6f, 0.6f, 0.65f);
            RectTransform mapperRect = mapperObj.GetComponent<RectTransform>();
            mapperRect.anchoredPosition = new Vector2(-30, 0);
        }

        private static void CreateScoreSection(Transform parent, ResultScreenController controller)
        {
            GameObject scoreSection = new GameObject("ScoreSection");
            scoreSection.transform.SetParent(parent, false);

            RectTransform rect = scoreSection.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.65f);
            rect.anchorMax = new Vector2(0.5f, 0.9f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 180);
            rect.anchoredPosition = Vector2.zero;

            GameObject rankObj = CreateLocalizedLabel("Rank", scoreSection.transform, "", 
                new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), new Vector2(120, 100), 72, TextAlignmentOptions.Center, null);
            controller.textRank = rankObj.GetComponent<TextMeshProUGUI>();
            controller.textRank.fontStyle = FontStyles.Bold;
            controller.textRank.color = Color.yellow;

            GameObject scoreLabel = CreateLocalizedLabel("ScoreLabel", scoreSection.transform, "Score", 
                new Vector2(0.3f, 0.55f), new Vector2(0.3f, 0.55f), new Vector2(150, 30), 18, TextAlignmentOptions.Right, "ui_score");
            scoreLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.7f, 0.75f);

            GameObject scoreValue = CreateLocalizedLabel("ScoreValue", scoreSection.transform, "0000000", 
                new Vector2(0.35f, 0.55f), new Vector2(0.35f, 0.55f), new Vector2(200, 40), 32, TextAlignmentOptions.Left, null);
            controller.textScore = scoreValue.GetComponent<TextMeshProUGUI>();
            controller.textScore.fontStyle = FontStyles.Bold;
            controller.textScore.color = Color.white;

            GameObject accLabel = CreateLocalizedLabel("AccuracyLabel", scoreSection.transform, "Accuracy", 
                new Vector2(0.3f, 0.35f), new Vector2(0.3f, 0.35f), new Vector2(150, 30), 18, TextAlignmentOptions.Right, "ui_accuracy");
            accLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.7f, 0.75f);

            GameObject accValue = CreateLocalizedLabel("AccuracyValue", scoreSection.transform, "100.00%", 
                new Vector2(0.35f, 0.35f), new Vector2(0.35f, 0.35f), new Vector2(150, 30), 24, TextAlignmentOptions.Left, null);
            controller.textAccuracy = accValue.GetComponent<TextMeshProUGUI>();
            controller.textAccuracy.color = new Color(0.4f, 1f, 0.6f);

            GameObject comboLabel = CreateLocalizedLabel("ComboLabel", scoreSection.transform, "Max Combo", 
                new Vector2(0.3f, 0.15f), new Vector2(0.3f, 0.15f), new Vector2(150, 30), 18, TextAlignmentOptions.Right, "ui_max_combo");
            comboLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.7f, 0.75f);

            GameObject comboValue = CreateLocalizedLabel("ComboValue", scoreSection.transform, "0x", 
                new Vector2(0.35f, 0.15f), new Vector2(0.35f, 0.15f), new Vector2(150, 30), 24, TextAlignmentOptions.Left, null);
            controller.textMaxCombo = comboValue.GetComponent<TextMeshProUGUI>();
            controller.textMaxCombo.color = new Color(1f, 0.8f, 0.3f);

            GameObject modsObj = CreateLocalizedLabel("ModsDisplay", scoreSection.transform, "", 
                new Vector2(0.7f, 0.35f), new Vector2(0.7f, 0.35f), new Vector2(200, 30), 16, TextAlignmentOptions.Left, null);
            controller.textMods = modsObj.GetComponent<TextMeshProUGUI>();
            controller.textMods.color = new Color(0.6f, 0.8f, 1f);
            modsObj.SetActive(false);
        }

        private static void CreateJudgmentSection(Transform parent, ResultScreenController controller)
        {
            GameObject judgmentSection = new GameObject("JudgmentSection");
            judgmentSection.transform.SetParent(parent, false);

            RectTransform rect = judgmentSection.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.15f);
            rect.anchorMax = new Vector2(0.85f, 0.45f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            Image sectionBg = judgmentSection.AddComponent<Image>();
            sectionBg.color = new Color(0.05f, 0.05f, 0.08f, 0.5f);

            GameObject hit300Label = CreateLocalizedLabel("Hit300Label", judgmentSection.transform, "300", 
                new Vector2(0.2f, 0.75f), new Vector2(0.2f, 0.75f), new Vector2(80, 30), 20, TextAlignmentOptions.Right, "ui_hit300");
            hit300Label.GetComponent<TextMeshProUGUI>().color = new Color(0.3f, 0.9f, 0.5f);

            GameObject hit300Value = CreateLocalizedLabel("Hit300Value", judgmentSection.transform, "0", 
                new Vector2(0.25f, 0.75f), new Vector2(0.25f, 0.75f), new Vector2(100, 30), 22, TextAlignmentOptions.Left, null);
            controller.textHit300 = hit300Value.GetComponent<TextMeshProUGUI>();
            controller.textHit300.color = new Color(0.3f, 0.9f, 0.5f);

            GameObject hit100Label = CreateLocalizedLabel("Hit100Label", judgmentSection.transform, "100", 
                new Vector2(0.2f, 0.5f), new Vector2(0.2f, 0.5f), new Vector2(80, 30), 20, TextAlignmentOptions.Right, "ui_hit100");
            hit100Label.GetComponent<TextMeshProUGUI>().color = new Color(0.6f, 0.8f, 1f);

            GameObject hit100Value = CreateLocalizedLabel("Hit100Value", judgmentSection.transform, "0", 
                new Vector2(0.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(100, 30), 22, TextAlignmentOptions.Left, null);
            controller.textHit100 = hit100Value.GetComponent<TextMeshProUGUI>();
            controller.textHit100.color = new Color(0.6f, 0.8f, 1f);

            GameObject hit50Label = CreateLocalizedLabel("Hit50Label", judgmentSection.transform, "50", 
                new Vector2(0.2f, 0.25f), new Vector2(0.2f, 0.25f), new Vector2(80, 30), 20, TextAlignmentOptions.Right, "ui_hit50");
            hit50Label.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.6f, 0.4f);

            GameObject hit50Value = CreateLocalizedLabel("Hit50Value", judgmentSection.transform, "0", 
                new Vector2(0.25f, 0.25f), new Vector2(0.25f, 0.25f), new Vector2(100, 30), 22, TextAlignmentOptions.Left, null);
            controller.textHit50 = hit50Value.GetComponent<TextMeshProUGUI>();
            controller.textHit50.color = new Color(0.7f, 0.6f, 0.4f);

            GameObject missLabel = CreateLocalizedLabel("MissLabel", judgmentSection.transform, "Miss", 
                new Vector2(0.55f, 0.75f), new Vector2(0.55f, 0.75f), new Vector2(80, 30), 20, TextAlignmentOptions.Right, "ui_miss");
            missLabel.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.3f, 0.3f);

            GameObject missValue = CreateLocalizedLabel("MissValue", judgmentSection.transform, "0", 
                new Vector2(0.6f, 0.75f), new Vector2(0.6f, 0.75f), new Vector2(100, 30), 22, TextAlignmentOptions.Left, null);
            controller.textMiss = missValue.GetComponent<TextMeshProUGUI>();
            controller.textMiss.color = new Color(1f, 0.3f, 0.3f);

            GameObject sliderLabel = CreateLocalizedLabel("SliderLabel", judgmentSection.transform, "Sliders", 
                new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(80, 30), 18, TextAlignmentOptions.Right, null);
            sliderLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.6f, 0.6f, 0.65f);

            GameObject sliderValue = CreateLocalizedLabel("SliderValue", judgmentSection.transform, "", 
                new Vector2(0.6f, 0.5f), new Vector2(0.6f, 0.5f), new Vector2(150, 30), 18, TextAlignmentOptions.Left, null);
            controller.textSliderInfo = sliderValue.GetComponent<TextMeshProUGUI>();
            controller.textSliderInfo.color = new Color(0.8f, 0.8f, 0.85f);

            GameObject spinnerLabel = CreateLocalizedLabel("SpinnerLabel", judgmentSection.transform, "Spinner", 
                new Vector2(0.55f, 0.25f), new Vector2(0.55f, 0.25f), new Vector2(80, 30), 18, TextAlignmentOptions.Right, null);
            spinnerLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.6f, 0.6f, 0.65f);

            GameObject spinnerValue = CreateLocalizedLabel("SpinnerValue", judgmentSection.transform, "", 
                new Vector2(0.6f, 0.25f), new Vector2(0.6f, 0.25f), new Vector2(150, 30), 18, TextAlignmentOptions.Left, null);
            controller.textSpinnerBonus = spinnerValue.GetComponent<TextMeshProUGUI>();
            controller.textSpinnerBonus.color = new Color(1f, 0.85f, 0.4f);
        }

        private static void CreateButtonSection(Transform parent, ResultScreenController controller)
        {
            GameObject buttonSection = new GameObject("ButtonSection");
            buttonSection.transform.SetParent(parent, false);

            RectTransform rect = buttonSection.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0.12f);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = buttonSection.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject retryBtn = CreateActionButton("Btn_Retry", buttonSection.transform, 
                new Vector2(180, 55), new Color(0.2f, 0.6f, 0.9f, 0.9f), "ui_retry");
            controller.buttonRetry = retryBtn.GetComponent<Button>();
            AddButtonCollider(retryBtn);

            GameObject replayBtn = CreateActionButton("Btn_WatchReplay", buttonSection.transform, 
                new Vector2(200, 55), new Color(0.5f, 0.4f, 0.7f, 0.9f), "ui_watch_replay");
            controller.buttonWatchReplay = replayBtn.GetComponent<Button>();
            AddButtonCollider(replayBtn);

            GameObject backBtn = CreateActionButton("Btn_Back", buttonSection.transform, 
                new Vector2(180, 55), new Color(0.5f, 0.5f, 0.5f, 0.9f), "ui_back");
            controller.buttonBackToMenu = backBtn.GetComponent<Button>();
            AddButtonCollider(backBtn);
        }

        private static void CreateFullComboEffect(Transform parent, ResultScreenController controller)
        {
            GameObject fcEffect = new GameObject("FullComboEffect");
            fcEffect.transform.SetParent(parent, false);
            fcEffect.SetActive(false);
            controller.fullComboEffect = fcEffect;

            ParticleSystem particles = fcEffect.AddComponent<ParticleSystem>();
            controller.fullComboParticles = particles;

            var main = particles.main;
            main.startLifetime = 2f;
            main.startSpeed = 2f;
            main.startSize = 0.1f;
            main.startColor = new Color(1f, 0.9f, 0.3f);
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 50;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
        }

        private static GameObject CreateLocalizedLabel(string name, Transform parent, string text, 
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size, float fontSize, 
            TextAlignmentOptions alignment, string localizationKey)
        {
            GameObject labelObj = new GameObject(name);
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            if (!string.IsNullOrEmpty(localizationKey))
            {
                LocalizedText localizedText = labelObj.AddComponent<LocalizedText>();
                localizedText.localizationKey = localizationKey;
            }

            return labelObj;
        }

        private static GameObject CreateActionButton(string name, Transform parent, Vector2 size, Color color, string localizationKey)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            Image image = btnObj.AddComponent<Image>();
            image.color = color;

            Button button = btnObj.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f, color.a);
            colors.pressedColor = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f, color.a);
            button.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = LocalizationManager.GetText(localizationKey);
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            if (!string.IsNullOrEmpty(localizationKey))
            {
                LocalizedText localizedText = textObj.AddComponent<LocalizedText>();
                localizedText.localizationKey = localizationKey;
            }

            return btnObj;
        }

        private static void AddButtonCollider(GameObject button)
        {
            BoxCollider collider = button.AddComponent<BoxCollider>();
            RectTransform rect = button.GetComponent<RectTransform>();
            
            collider.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 5f);
            collider.center = Vector3.zero;
            
            collider.isTrigger = true;
        }

        private static void SetupAnimationCurve(ResultScreenController controller)
        {
            controller.scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            controller.scoreScrollSpeed = 8000f;
            controller.rankAppearDelay = 1.5f;
            controller.fadeDuration = 0.5f;
            controller.panelScaleDuration = 0.3f;
            controller.rankPunchScale = 1.3f;
            controller.rankPunchDuration = 0.3f;
            controller.rankGlowColor = Color.yellow;
        }
    }
}
#endif
