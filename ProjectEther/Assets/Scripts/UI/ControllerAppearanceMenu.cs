using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>独立于主菜单画布的手柄外观选择页，选择与持久化交给 ControllerAppearance。</summary>
    public sealed class ControllerAppearanceMenu : MonoBehaviour
    {
        private const float CanvasWidth = 720f;
        private const float CanvasHeight = 460f;
        private const float CardWidth = 208f;
        private const float CardHeight = 276f;

        private readonly Button[] cards = new Button[ControllerAppearance.ModelCount];
        private readonly Image[] indicators = new Image[ControllerAppearance.ModelCount];
        private readonly Outline[] outlines = new Outline[ControllerAppearance.ModelCount];
        private readonly LocalizedText[] names = new LocalizedText[ControllerAppearance.ModelCount];
        private readonly LocalizedText[] states = new LocalizedText[
            ControllerAppearance.ModelCount
        ];

        private Canvas canvas;
        private Action onOpened;
        private Action onClosed;
        private Action playClickSound;
        private AudioSource hoverAudioSource;
        private AudioClip hoverClip;

        private void Awake()
        {
            ControllerAppearance.Changed += OnAppearanceChanged;
            LocalizationManager.OnLanguageChanged += RefreshCards;
        }

        private void OnDestroy()
        {
            ControllerAppearance.Changed -= OnAppearanceChanged;
            LocalizationManager.OnLanguageChanged -= RefreshCards;
        }

        public void Initialize(
            Action onOpened,
            Action onClosed,
            Action playClickSound,
            AudioSource hoverAudioSource = null,
            AudioClip hoverClip = null
        )
        {
            this.onOpened = onOpened;
            this.onClosed = onClosed;
            this.playClickSound = playClickSound;
            this.hoverAudioSource = hoverAudioSource;
            this.hoverClip = hoverClip;
        }

        public void Show()
        {
            if (canvas == null)
                BuildUI();

            // 只通过回调隐藏主画布，负责返回和音效的组件仍保持启用。
            onOpened?.Invoke();
            canvas.gameObject.SetActive(true);
            RefreshCards();
            RefreshRaycasters();
        }

        public void Close()
        {
            if (canvas == null || !canvas.gameObject.activeSelf)
                return;

            canvas.gameObject.SetActive(false);
            onClosed?.Invoke();
            RefreshRaycasters();
        }

        private void BuildUI()
        {
            canvas = UILayoutHelper.CreateCanvas(
                "ControllerAppearanceCanvas",
                CanvasWidth,
                CanvasHeight
            );
            canvas.sortingOrder = 110;
            canvas.worldCamera = Camera.main;
            canvas.transform.SetParent(transform, false);
            canvas.transform.localScale = Vector3.one * 0.0025f;
            canvas.transform.localPosition = new Vector3(0f, 2.5f, 1.5f);
            var root = canvas.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * 0.5f;
            root.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            // 整页接收空白区射线，避免越过背板点击后层；装饰不接收射线。
            SongSelectionStyle.Fill(root, SongSelectionStyle.Background, true);
            MenuPageStyle.Backdrop(root);
            var title = MenuPageStyle.Label(
                root,
                "Title",
                LocalizationManager.GetText("ui_controller_appearance"),
                "ui_controller_appearance",
                28,
                18,
                664,
                44,
                24
            );
            title.fontStyle = FontStyles.Bold;
            MenuPageStyle.Label(
                root,
                "Hint",
                LocalizationManager.GetText("ui_controller_appearance_hint"),
                "ui_controller_appearance_hint",
                28,
                66,
                664,
                30,
                14,
                SongSelectionStyle.Muted
            );
            SongSelectionStyle.Fill(
                MenuPageStyle.Rect(root, "HeaderLine", 28, 104, 664, 1),
                SongSelectionStyle.Line
            );

            for (int i = 0; i < cards.Length; i++)
                CreateCard(root, i);

            SongSelectionStyle.Fill(
                MenuPageStyle.Rect(root, "FooterLine", 28, 404, 664, 1),
                SongSelectionStyle.Line
            );
            var back = MenuPageStyle.Button(
                root,
                "Back",
                LocalizationManager.GetText("ui_back"),
                "ui_back",
                () =>
                {
                    playClickSound?.Invoke();
                    Close();
                },
                148,
                36
            );
            SongSelectionStyle.Place((RectTransform)back.transform, 28, 416, 148, 36);
            UILayoutHelper.AddHoverSoundEffect(back.gameObject, hoverAudioSource, hoverClip, 0.5f);
            MenuPageStyle.Label(
                root,
                "AutoSaveHint",
                LocalizationManager.GetText("ui_controller_auto_save"),
                "ui_controller_auto_save",
                196,
                418,
                496,
                30,
                13,
                SongSelectionStyle.Muted,
                TextAlignmentOptions.MidlineRight
            );
            canvas.gameObject.SetActive(false);
        }

        private void CreateCard(Transform parent, int index)
        {
            char id = (char)('A' + index);
            var rect = MenuPageStyle.Rect(
                parent,
                $"ControllerCard_{id}",
                28 + index * 228,
                114,
                CardWidth,
                CardHeight
            );
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = SongSelectionStyle.Fill(rect, Color.white, true);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                playClickSound?.Invoke();
                ControllerAppearance.SetSelectedIndex(index);
            });
            UILayoutHelper.AddHoverSoundEffect(rect.gameObject, hoverAudioSource, hoverClip, 0.5f);
            cards[index] = button;
            outlines[index] = rect.gameObject.AddComponent<Outline>();
            outlines[index].effectDistance = new Vector2(1, -1);
            indicators[index] = SongSelectionStyle.Fill(
                MenuPageStyle.Rect(rect, "SelectionIndicator", 0, 0, CardWidth, 3),
                Color.clear
            );

            var previewFrame = MenuPageStyle.Rect(rect, "PreviewFrame", 14, 12, 180, 135);
            SongSelectionStyle.Fill(previewFrame, SongSelectionStyle.Background);
            var previewRect = MenuPageStyle.Rect(previewFrame, "Preview", 0, 0, 180, 135);
            var preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            preview.texture = Resources.Load<Texture2D>($"ControllerAppearance/Previews/{id}");
            if (preview.texture != null)
            {
                // 完整显示透明缩略图，按源比例缩放并居中，不裁剪或拉伸模型。
                float scale = Mathf.Min(
                    180f / preview.texture.width,
                    135f / preview.texture.height
                );
                float width = preview.texture.width * scale;
                float height = preview.texture.height * scale;
                SongSelectionStyle.Place(
                    previewRect,
                    (180 - width) * 0.5f,
                    (135 - height) * 0.5f,
                    width,
                    height
                );
            }
            else
            {
                preview.enabled = false;
                Debug.LogWarning($"[ControllerAppearanceMenu] 缺少手柄缩略图：{id}");
            }

            var name = MenuPageStyle.Label(rect, "ModelName", "", null, 14, 151, 180, 35, 18);
            name.fontStyle = FontStyles.Bold;
            // 空 key 只复用字体切换，动态名称和状态不会被固定词条覆盖。
            names[index] = name.gameObject.AddComponent<LocalizedText>();
            string descriptionKey = $"ui_controller_description_{char.ToLowerInvariant(id)}";
            var description = MenuPageStyle.Label(
                rect,
                "Description",
                LocalizationManager.GetText(descriptionKey),
                descriptionKey,
                14,
                190,
                180,
                44,
                12,
                SongSelectionStyle.Muted
            );
            description.enableWordWrapping = true;
            var state = MenuPageStyle.Label(rect, "SelectionState", "", null, 14, 238, 180, 28, 13);
            states[index] = state.gameObject.AddComponent<LocalizedText>();
        }

        private void OnAppearanceChanged(int index)
        {
            RefreshCards();
        }

        private void RefreshCards()
        {
            if (canvas == null)
                return;

            int selected = ControllerAppearance.SelectedIndex;
            for (int i = 0; i < cards.Length; i++)
            {
                bool active = i == selected;
                SongSelectionStyle.ConfigureButton(
                    cards[i],
                    active ? SongSelectionStyle.Selected : SongSelectionStyle.Control
                );
                indicators[i].color = active ? SongSelectionStyle.Accent : Color.clear;
                outlines[i].effectColor = active
                    ? SongSelectionStyle.Accent
                    : SongSelectionStyle.Line;
                names[i]
                    .SetTextDirectly($"{(char)('A' + i)} · {ControllerAppearance.GetModelName(i)}");
                states[i]
                    .SetTextDirectly(
                        LocalizationManager.GetText(
                            active ? "ui_controller_selected" : "ui_controller_select"
                        )
                    );
                states[i].GetComponent<TextMeshProUGUI>().color = active
                    ? SongSelectionStyle.Accent
                    : SongSelectionStyle.Muted;
            }
        }

        private static void RefreshRaycasters()
        {
            Canvas.ForceUpdateCanvases();
            RayController.NotifyUICanvasChanged();
        }
    }
}
