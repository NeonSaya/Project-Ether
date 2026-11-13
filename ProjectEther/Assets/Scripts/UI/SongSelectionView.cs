using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuVR
{
    /// <summary>在既有画布及三个分区内重排控件，不改场景布局或玩法事件。</summary>
    [DefaultExecutionOrder(10)]
    public sealed class SongSelectionView : MonoBehaviour
    {
        private SimpleSongSelection owner;
        private TMP_FontAsset font;
        private TMP_InputField search;
        private TextMeshProUGUI count;
        private TextMeshProUGUI metadata;
        private TextMeshProUGUI emptyMessage;
        private Button all;
        private Button favorites;
        private Button sort;
        private Button filter;
        private Button clearFilters;
        private Button selectedFavorite;
        private Button direction;
        private RectTransform sortMenu;
        private RectTransform filterMenu;
        private RectTransform empty;
        private ScrollRect difficultyScroll;
        private SongSearchKeyboard keyboard;
        private RectTransform pendingDifficulty;
        private string pendingSearch;
        private float searchDeadline;
        private int visibleCount;
        private int totalCount;
        private readonly Dictionary<TextMeshProUGUI, string> labels =
            new Dictionary<TextMeshProUGUI, string>();
        private readonly List<Button[]> filterRows = new List<Button[]>();

        public void Build(SimpleSongSelection selection)
        {
            owner = selection;
            font = owner.titleText != null ? owner.titleText.font : TMP_Settings.defaultFontAsset;
            var scroll = owner.listContent.GetComponentInParent<ScrollRect>();
            var left = (RectTransform)scroll.transform.parent;
            var info = (RectTransform)owner.infoPanel.transform;
            if (left.name != "LeftArea_SongList")
                return;

            var background = left.parent.GetComponent<Image>();
            if (background != null)
                background.color = SongSelectionStyle.Background;
            SongSelectionStyle.Fill(left, SongSelectionStyle.Panel);
            var title = left.Find("Title").GetComponent<TextMeshProUGUI>();
            SongSelectionStyle.Place(title.rectTransform, 14, 8, 350, 38);
            SongSelectionStyle.ConfigureText(title, 24);
            Bind(title, "ui_library_title");
            BuildSearch(left);
            all = LocalButton(
                left,
                "LibraryAll",
                "ui_library_all",
                () => owner.SetFavoritesOnly(false),
                12,
                104,
                56,
                36,
                14
            );
            favorites = LocalButton(
                left,
                "LibraryFavorites",
                "ui_library_favorites",
                () => owner.SetFavoritesOnly(true),
                74,
                104,
                96,
                36,
                14
            );
            sort = LocalButton(
                left,
                "LibrarySort",
                "ui_library_sort_title",
                () => ToggleMenu(sortMenu, filterMenu),
                176,
                104,
                126,
                36,
                14
            );
            filter = LocalButton(
                left,
                "LibraryFilter",
                "ui_library_filter",
                () => ToggleMenu(filterMenu, sortMenu),
                308,
                104,
                70,
                36,
                14
            );
            count = SongSelectionStyle.Label(left, "LibraryResultCount", font, 13);
            SongSelectionStyle.Place(count.rectTransform, 14, 146, 242, 28);
            count.color = SongSelectionStyle.Muted;
            clearFilters = LocalButton(
                left,
                "ClearLibraryFilters",
                "ui_library_clear",
                owner.ClearLibraryFilters,
                264,
                146,
                114,
                28,
                12
            );

            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(12, 60);
            scrollRect.offsetMax = new Vector2(-12, -180);
            SongSelectionStyle.Fill(scrollRect, SongSelectionStyle.Panel, true);
            SongSelectionStyle.AddScrollbar(scroll, "SongLibraryScrollbar");
            var listLayout = owner.listContent.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null)
            {
                listLayout.padding = new RectOffset();
                listLayout.spacing = 4;
                listLayout.childControlHeight = true;
                listLayout.childForceExpandHeight = false;
            }
            StyleScrollButton(owner.scrollUpButton, "ui_library_scroll_up");
            StyleScrollButton(owner.scrollDownButton, "ui_library_scroll_down");
            BuildEmpty(scroll.viewport);
            BuildSortMenu(left);
            BuildFilterMenu(left);
            StyleDetails(info);
            StyleMods();
            StyleActions();
            var keyboardRoot = SongSelectionStyle.Rect(
                info.parent,
                "SongSearchKeyboard",
                24,
                326,
                702,
                278
            );
            keyboard = keyboardRoot.gameObject.AddComponent<SongSearchKeyboard>();
            keyboard.Build(info.parent, search, font);
            RefreshLanguage();
        }

        private void BuildSearch(RectTransform left)
        {
            var root = SongSelectionStyle.Rect(left, "SongSearch", 12, 52, 294, 44);
            var image = SongSelectionStyle.Fill(root, SongSelectionStyle.Control, true);
            var viewport = SongSelectionStyle.Rect(root, "TextArea", 12, 0, 240, 44);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = SongSelectionStyle.Label(viewport, "Text", font, 15);
            SongSelectionStyle.Place(text.rectTransform, 0, 0, 240, 44);
            var placeholder = SongSelectionStyle.Label(viewport, "Placeholder", font, 14);
            SongSelectionStyle.Place(placeholder.rectTransform, 0, 0, 240, 44);
            placeholder.color = SongSelectionStyle.Muted;
            Bind(placeholder, "ui_library_search_placeholder");
            search = root.gameObject.AddComponent<TMP_InputField>();
            search.targetGraphic = image;
            search.textViewport = viewport;
            search.textComponent = text;
            search.placeholder = placeholder;
            search.characterLimit = 128;
            search.lineType = TMP_InputField.LineType.SingleLine;
            search.richText = false;
            search.shouldHideSoftKeyboard = true;
            search.onFocusSelectAll = false;
            search.resetOnDeActivation = false;
            search.caretColor = SongSelectionStyle.Accent;
            search.customCaretColor = true;
            search.SetTextWithoutNotify(owner.LibraryQuery.SearchText);
            search.onValueChanged.AddListener(value =>
            {
                pendingSearch = value;
                searchDeadline = Time.unscaledTime + 0.12f;
            });
            search.onSelect.AddListener(_ => ShowKeyboard());
            search.onSubmit.AddListener(_ =>
            {
                FlushSearch();
                keyboard?.Close();
            });
            SongSelectionStyle.Button(
                root,
                "ClearSearch",
                "×",
                font,
                () =>
                {
                    search.text = "";
                    FlushSearch();
                },
                258,
                4,
                32,
                36,
                20
            );
            LocalButton(
                left,
                "OpenSearchKeyboard",
                "ui_library_keys",
                ShowKeyboard,
                312,
                52,
                66,
                44,
                13
            );
        }

        private void Update()
        {
            if (pendingSearch != null && Time.unscaledTime >= searchDeadline)
                FlushSearch();
        }

        private void OnDisable()
        {
            keyboard?.Close();
        }

        public string TakePendingSearch()
        {
            string value = pendingSearch;
            pendingSearch = null;
            return value;
        }

        private void FlushSearch()
        {
            if (pendingSearch == null || owner == null)
                return;
            string value = pendingSearch;
            pendingSearch = null;
            owner.SetSearch(value);
        }

        private void ShowKeyboard()
        {
            if (keyboard == null)
                return;
            sortMenu.gameObject.SetActive(false);
            filterMenu.gameObject.SetActive(false);
            keyboard.Show();
        }

        private void ToggleMenu(RectTransform menu, RectTransform other)
        {
            bool show = !menu.gameObject.activeSelf;
            other.gameObject.SetActive(false);
            menu.gameObject.SetActive(show);
            if (show)
                menu.SetAsLastSibling();
            keyboard?.Close();
        }

        private RectTransform Menu(RectTransform parent, string name, string heading, float height)
        {
            var menu = SongSelectionStyle.Rect(parent, name, 12, 148, 366, height);
            SongSelectionStyle.Fill(menu, SongSelectionStyle.Control, true);
            var text = SongSelectionStyle.Label(menu, "Heading", font, 18);
            SongSelectionStyle.Place(text.rectTransform, 14, 8, 280, 34);
            Bind(text, heading);
            SongSelectionStyle.Button(
                menu,
                "Close",
                "×",
                font,
                () => menu.gameObject.SetActive(false),
                310,
                6,
                44,
                36,
                20
            );
            menu.gameObject.SetActive(false);
            return menu;
        }

        private void BuildSortMenu(RectTransform left)
        {
            sortMenu = Menu(left, "SongSortMenu", "ui_library_sort", 264);
            for (int i = 0; i < 4; i++)
            {
                var mode = (SongSortMode)i;
                LocalButton(
                    sortMenu,
                    "Sort_" + mode,
                    SortKey(mode),
                    () =>
                    {
                        owner.SetSort(mode, owner.LibraryQuery.Descending);
                        sortMenu.gameObject.SetActive(false);
                    },
                    14,
                    48 + i * 40,
                    338,
                    34,
                    14
                );
            }
            direction = LocalButton(
                sortMenu,
                "SortDirection",
                "ui_library_ascending",
                () => owner.SetSort(owner.LibraryQuery.SortMode, !owner.LibraryQuery.Descending),
                14,
                216,
                338,
                36,
                14
            );
        }

        private void BuildFilterMenu(RectTransform left)
        {
            filterMenu = Menu(left, "SongFilterMenu", "ui_library_filter", 330);
            BuildFilterRow(
                0,
                "ui_library_bpm",
                new[] { "", "<120", "120–180", "180+" },
                value => owner.SetBpmRange((SongBpmRange)value)
            );
            BuildFilterRow(
                1,
                "ui_library_length",
                new[] { "", "<2:00", "2–4 min", "4:00+" },
                value => owner.SetLengthRange((SongLengthRange)value)
            );
            BuildFilterRow(
                2,
                "ui_library_ar",
                new[] { "", "<6", "6–8", "8+" },
                value => owner.SetApproachRange((SongApproachRange)value)
            );
            LocalButton(
                filterMenu,
                "ResetFilters",
                "ui_library_clear",
                owner.ClearLibraryFilters,
                14,
                280,
                338,
                36,
                14
            );
        }

        private void BuildFilterRow(int row, string key, string[] values, Action<int> action)
        {
            float top = 48 + row * 76;
            var label = SongSelectionStyle.Label(filterMenu, "FilterLabel_" + row, font, 13);
            SongSelectionStyle.Place(label.rectTransform, 14, top, 338, 26);
            Bind(label, key);
            var buttons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                int value = i;
                buttons[i] = SongSelectionStyle.Button(
                    filterMenu,
                    "Filter_" + row + "_" + i,
                    values[i],
                    font,
                    () => action(value),
                    14 + i * 86,
                    top + 28,
                    80,
                    34,
                    12
                );
                if (i == 0)
                    Bind(buttons[i].GetComponentInChildren<TextMeshProUGUI>(), "ui_library_any");
            }
            filterRows.Add(buttons);
        }

        private void BuildEmpty(RectTransform viewport)
        {
            empty = SongSelectionStyle.Rect(viewport, "LibraryEmptyState", 0, 0, 342, 500);
            empty.anchorMin = Vector2.zero;
            empty.anchorMax = Vector2.one;
            empty.offsetMin = empty.offsetMax = Vector2.zero;
            SongSelectionStyle.Fill(empty, SongSelectionStyle.Panel);
            emptyMessage = SongSelectionStyle.Label(
                empty,
                "Message",
                font,
                18,
                TextAlignmentOptions.Center
            );
            SongSelectionStyle.Place(emptyMessage.rectTransform, 16, 100, 310, 90);
            emptyMessage.enableWordWrapping = true;
            LocalButton(
                empty,
                "ClearEmptyFilters",
                "ui_library_clear",
                owner.ClearLibraryFilters,
                74,
                210,
                194,
                42,
                15
            );
            empty.gameObject.SetActive(false);
        }

        private void StyleDetails(RectTransform info)
        {
            SongSelectionStyle.Fill(info, SongSelectionStyle.Panel);
            var header = info.Find("HeaderBar");
            if (header != null)
                header.gameObject.SetActive(false);
            if (owner.backgroundImage != null)
            {
                var fitter = owner.backgroundImage.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                    fitter.enabled = false;
                SongSelectionStyle.Place(owner.backgroundImage.rectTransform, 0, 0, 750, 202);
                owner.backgroundImage.raycastTarget = false;
                var backing = SongSelectionStyle.Rect(info, "TitleBacking", 0, 92, 750, 110);
                SongSelectionStyle.Fill(backing, new Color(0.07f, 0.085f, 0.14f, 0.94f));
                backing.SetSiblingIndex(owner.backgroundImage.transform.GetSiblingIndex() + 1);
            }
            SongSelectionStyle.Place(owner.titleText.rectTransform, 24, 100, 702, 54);
            SongSelectionStyle.ConfigureText(owner.titleText, 28);
            SongSelectionStyle.Place(owner.artistText.rectTransform, 24, 158, 546, 34);
            SongSelectionStyle.ConfigureText(owner.artistText, 18);
            owner.artistText.color = SongSelectionStyle.Muted;
            selectedFavorite = LocalButton(
                info,
                "SelectedSongFavorite",
                "ui_library_favorite_add",
                owner.ToggleSelectedFavorite,
                590,
                156,
                136,
                40,
                14
            );
            var stats = (RectTransform)info.Find("StatsContainer");
            SongSelectionStyle.Place(stats, 24, 216, 702, 80);
            var horizontal = stats.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
                horizontal.enabled = false;
            var statsFitter = stats.GetComponent<ContentSizeFitter>();
            if (statsFitter != null)
                statsFitter.enabled = false;
            int statIndex = 0;
            foreach (Transform cell in stats)
            {
                var vertical = cell.GetComponent<VerticalLayoutGroup>();
                if (vertical != null)
                    vertical.enabled = false;
                var cellFitter = cell.GetComponent<ContentSizeFitter>();
                if (cellFitter != null)
                    cellFitter.enabled = false;
                SongSelectionStyle.Place((RectTransform)cell, statIndex++ * 142, 0, 134, 80);
                SongSelectionStyle.Fill((RectTransform)cell, SongSelectionStyle.Control);
                var label = cell.Find("Label").GetComponent<TextMeshProUGUI>();
                var value = cell.Find("Value").GetComponent<TextMeshProUGUI>();
                SongSelectionStyle.Place(label.rectTransform, 4, 2, 120, 28);
                label.rectTransform.anchorMax = new Vector2(1, 1);
                label.rectTransform.sizeDelta = new Vector2(-8, 28);
                SongSelectionStyle.ConfigureText(label, 13, TextAlignmentOptions.Center);
                label.color = SongSelectionStyle.Muted;
                Bind(label, "ui_library_stat_" + cell.name.ToLowerInvariant());
                SongSelectionStyle.Place(value.rectTransform, 4, 32, 120, 44);
                value.rectTransform.anchorMax = new Vector2(1, 1);
                value.rectTransform.sizeDelta = new Vector2(-8, 44);
                SongSelectionStyle.ConfigureText(value, 24, TextAlignmentOptions.Center);
            }
            PlaceDivider(info.Find("Divider1"), 208);
            PlaceDivider(info.Find("Divider2"), 308);
            var selector = (RectTransform)owner.difficultyText.transform.parent;
            SongSelectionStyle.Place(selector, 24, 320, 702, 36);
            SongSelectionStyle.Fill(selector, SongSelectionStyle.Panel);
            SongSelectionStyle.Place(owner.difficultyText.rectTransform, 174, 0, 528, 36);
            SongSelectionStyle.ConfigureText(
                owner.difficultyText,
                16,
                TextAlignmentOptions.MidlineRight
            );
            owner.difficultyText.color = SongSelectionStyle.Accent;
            var caption = SongSelectionStyle.Label(selector, "DifficultyHeading", font, 18);
            SongSelectionStyle.Place(caption.rectTransform, 0, 0, 170, 36);
            Bind(caption, "ui_library_difficulty");
            difficultyScroll = MakeGridScroll(
                (RectTransform)owner.difficultyDropdownContainer,
                "Difficulty",
                24,
                366,
                702,
                210,
                new Vector2(334, 44)
            );
            owner.difficultyDropdownContainer = difficultyScroll.content;
            metadata = SongSelectionStyle.Label(info, "MapMetadata", font, 14);
            SongSelectionStyle.Place(metadata.rectTransform, 24, 584, 702, 28);
            metadata.color = SongSelectionStyle.Muted;
        }

        private void StyleMods()
        {
            var panel = (RectTransform)owner.modPanel.transform;
            SongSelectionStyle.Fill(panel, SongSelectionStyle.Panel);
            var header = panel.Find("HeaderBar");
            if (header != null)
                header.gameObject.SetActive(false);
            var heading = SongSelectionStyle.Label(panel, "ModsHeading", font, 24);
            SongSelectionStyle.Place(heading.rectTransform, 24, 14, 420, 44);
            Bind(heading, "ui_mods");
            SongSelectionStyle.Place(owner.multiplierText.rectTransform, 548, 16, 178, 48);
            SongSelectionStyle.ConfigureText(
                owner.multiplierText,
                28,
                TextAlignmentOptions.MidlineRight
            );
            SongSelectionStyle.Place(owner.activeModsText.rectTransform, 24, 64, 500, 32);
            SongSelectionStyle.ConfigureText(owner.activeModsText, 15);
            var multiplierLabel = SongSelectionStyle.Label(
                panel,
                "ScoreMultiplierLabel",
                font,
                13,
                TextAlignmentOptions.MidlineRight
            );
            SongSelectionStyle.Place(multiplierLabel.rectTransform, 548, 64, 178, 30);
            multiplierLabel.color = SongSelectionStyle.Muted;
            Bind(multiplierLabel, "ui_library_score_multiplier");
            PlaceDivider(panel.Find("Divider"), 106);
            var scroll = MakeGridScroll(
                (RectTransform)owner.modButtonContainer,
                "Mods",
                24,
                122,
                702,
                480,
                new Vector2(334, 108)
            );
            owner.modButtonContainer = scroll.content;
        }

        private ScrollRect MakeGridScroll(
            RectTransform root,
            string name,
            float x,
            float y,
            float width,
            float height,
            Vector2 cell
        )
        {
            foreach (var layout in root.GetComponents<LayoutGroup>())
                layout.enabled = false;
            var fit = root.GetComponent<ContentSizeFitter>();
            if (fit != null)
                fit.enabled = false;
            SongSelectionStyle.Place(root, x, y, width, height);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = SongSelectionStyle.Rect(root, name + "Viewport", 0, 0, width, height);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            SongSelectionStyle.Fill(viewport, SongSelectionStyle.Panel, true);
            var content = SongSelectionStyle.Rect(viewport, name + "Content", 0, 0, width - 24, 0);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = cell;
            grid.spacing = new Vector2(10, 8);
            grid.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            SongSelectionStyle.AddScrollbar(scroll, name + "Scrollbar");
            return scroll;
        }

        private void StyleActions()
        {
            SongSelectionStyle.ConfigureButton(owner.backMenuButton, SongSelectionStyle.Control);
            SongSelectionStyle.ConfigureButton(owner.openModButton, SongSelectionStyle.Control);
            SongSelectionStyle.ConfigureButton(owner.playButton, SongSelectionStyle.Pink);
            foreach (
                var button in new[] { owner.backMenuButton, owner.openModButton, owner.playButton }
            )
            {
                var rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 64);
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                SongSelectionStyle.ConfigureText(
                    text,
                    button == owner.playButton ? 24 : 18,
                    TextAlignmentOptions.Center
                );
            }
            Bind(owner.playButton.GetComponentInChildren<TextMeshProUGUI>(), "ui_library_play");
            if (owner.modStatusText != null)
            {
                SongSelectionStyle.ConfigureText(
                    owner.modStatusText,
                    14,
                    TextAlignmentOptions.MidlineRight
                );
                owner.modStatusText.color = SongSelectionStyle.Accent;
            }
        }

        private void StyleScrollButton(Button button, string key)
        {
            if (button == null)
                return;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(160, 42);
            SongSelectionStyle.ConfigureButton(button, SongSelectionStyle.Control);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            SongSelectionStyle.ConfigureText(text, 16, TextAlignmentOptions.Center);
            Bind(text, key);
        }

        private static void PlaceDivider(Transform divider, float y)
        {
            if (divider == null)
                return;
            SongSelectionStyle.Place((RectTransform)divider, 24, y, 702, 1);
            SongSelectionStyle.Fill((RectTransform)divider, SongSelectionStyle.Line);
        }

        public void ConfigureDifficulty(Button button, bool selected)
        {
            SongSelectionStyle.ConfigureButton(
                button,
                selected ? SongSelectionStyle.Selected : SongSelectionStyle.Control
            );
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            SongSelectionStyle.Place(text.rectTransform, 10, 0, 314, 44);
            SongSelectionStyle.ConfigureText(text, 16);
            text.color = selected ? SongSelectionStyle.Accent : SongSelectionStyle.Text;
            if (selected)
                pendingDifficulty = (RectTransform)button.transform;
        }

        public void ConfigureMod(ModButtonController controller, bool selected)
        {
            // Mod 面板初始停用，按钮控制器的 Awake 可能尚未运行。
            if (controller.button == null)
                controller.button = controller.GetComponent<Button>();
            controller.SetSelected(selected);
            SongSelectionStyle.ConfigureButton(
                controller.button,
                selected ? SongSelectionStyle.Selected : SongSelectionStyle.Control
            );
            if (controller.shortNameText != null)
            {
                SongSelectionStyle.Place(controller.shortNameText.rectTransform, 14, 26, 64, 48);
                SongSelectionStyle.ConfigureText(
                    controller.shortNameText,
                    25,
                    TextAlignmentOptions.Center
                );
                controller.shortNameText.color = selected
                    ? SongSelectionStyle.Accent
                    : SongSelectionStyle.Muted;
            }
            if (controller.fullNameText != null)
            {
                SongSelectionStyle.Place(controller.fullNameText.rectTransform, 94, 10, 226, 32);
                SongSelectionStyle.ConfigureText(controller.fullNameText, 16);
            }
            if (controller.descriptionText != null)
            {
                SongSelectionStyle.Place(controller.descriptionText.rectTransform, 94, 46, 226, 54);
                SongSelectionStyle.ConfigureText(controller.descriptionText, 12);
                controller.descriptionText.enableWordWrapping = true;
                controller.descriptionText.color = SongSelectionStyle.Muted;
            }
        }

        private void LateUpdate()
        {
            if (
                pendingDifficulty == null
                || difficultyScroll == null
                || !difficultyScroll.isActiveAndEnabled
            )
                return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                difficultyScroll.viewport,
                pendingDifficulty
            );
            var viewport = difficultyScroll.viewport.rect;
            float delta =
                bounds.max.y > viewport.yMax ? -(bounds.max.y - viewport.yMax)
                : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y
                : 0;
            difficultyScroll.StopMovement();
            difficultyScroll.content.anchoredPosition += new Vector2(0, delta);
            pendingDifficulty = null;
        }

        public void RefreshLibrary(int visible, int total)
        {
            visibleCount = visible;
            totalCount = total;
            RefreshQueryControls();
            empty.gameObject.SetActive(visible == 0);
            emptyMessage.text = LocalizationManager.GetText(
                total == 0 ? "ui_library_empty" : "ui_library_no_matches"
            );
        }

        public void RefreshSelected(BeatmapMetadata map)
        {
            RefreshBackground();
            bool favorite = map != null && owner.IsFavorite(map.FolderPath);
            selectedFavorite.interactable = map != null;
            selectedFavorite.GetComponentInChildren<TextMeshProUGUI>().text =
                LocalizationManager.GetText(
                    favorite ? "ui_library_favorite_remove" : "ui_library_favorite_add"
                );
            SongSelectionStyle.ConfigureButton(
                selectedFavorite,
                favorite ? SongSelectionStyle.Selected : SongSelectionStyle.Control
            );
            metadata.text =
                map == null
                    ? LocalizationManager.GetText("ui_library_pick_hint")
                    : string.Format(
                        LocalizationManager.GetText("ui_library_metadata"),
                        string.IsNullOrWhiteSpace(map.Creator) ? "-" : map.Creator,
                        map.BPM.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                    );
        }

        public void PanelChanged()
        {
            keyboard?.Close();
            sortMenu.gameObject.SetActive(false);
            filterMenu.gameObject.SetActive(false);
            owner.openModButton.GetComponentInChildren<TextMeshProUGUI>().color =
                SongSelectionStyle.Text;
        }

        private void RefreshBackground()
        {
            var image = owner.backgroundImage;
            if (image == null || image.texture == null)
                return;
            image.uvRect = SongSelectionStyle.CoverUvRect(
                new Vector2(image.texture.width, image.texture.height),
                image.rectTransform.rect.size
            );
        }

        public void RefreshLanguage()
        {
            foreach (var entry in labels)
                if (entry.Key != null)
                    entry.Key.text = LocalizationManager.GetText(entry.Value);
            keyboard?.RefreshLanguage();
            RefreshQueryControls();
            if (owner != null && owner.openModButton != null)
                owner.openModButton.GetComponentInChildren<TextMeshProUGUI>().color =
                    SongSelectionStyle.Text;
        }

        private void RefreshQueryControls()
        {
            if (count == null)
                return;
            var query = owner.LibraryQuery;
            count.text = string.Format(
                LocalizationManager.GetText("ui_library_count"),
                visibleCount,
                totalCount
            );
            clearFilters.gameObject.SetActive(query.HasFilters);
            SongSelectionStyle.ConfigureButton(
                all,
                !query.FavoritesOnly ? SongSelectionStyle.Selected : SongSelectionStyle.Control
            );
            SongSelectionStyle.ConfigureButton(
                favorites,
                query.FavoritesOnly ? SongSelectionStyle.Selected : SongSelectionStyle.Control
            );
            sort.GetComponentInChildren<TextMeshProUGUI>().text =
                LocalizationManager.GetText(SortKey(query.SortMode))
                + (query.Descending ? " ↓" : " ↑");
            filter.GetComponentInChildren<TextMeshProUGUI>().text =
                LocalizationManager.GetText("ui_library_filter")
                + (query.HasAttributeFilters ? " ●" : "");
            direction.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetText(
                query.Descending ? "ui_library_descending" : "ui_library_ascending"
            );
            int[] values =
            {
                (int)query.BpmRange,
                (int)query.LengthRange,
                (int)query.ApproachRange,
            };
            for (int row = 0; row < filterRows.Count; row++)
            for (int column = 0; column < 4; column++)
                SongSelectionStyle.ConfigureButton(
                    filterRows[row][column],
                    values[row] == column ? SongSelectionStyle.Selected : SongSelectionStyle.Panel
                );
            if (pendingSearch == null)
                search.SetTextWithoutNotify(query.SearchText);
        }

        private Button LocalButton(
            Transform parent,
            string name,
            string key,
            UnityEngine.Events.UnityAction action,
            float x,
            float y,
            float width,
            float height,
            float size = 15
        )
        {
            var button = SongSelectionStyle.Button(
                parent,
                name,
                "",
                font,
                action,
                x,
                y,
                width,
                height,
                size
            );
            Bind(button.GetComponentInChildren<TextMeshProUGUI>(), key);
            return button;
        }

        private void Bind(TextMeshProUGUI text, string key)
        {
            labels[text] = key;
            var localized = text.GetComponent<LocalizedText>();
            if (localized != null)
                localized.localizationKey = key;
            text.text = LocalizationManager.GetText(key);
        }

        private static string SortKey(SongSortMode mode) =>
            "ui_library_sort_" + mode.ToString().ToLowerInvariant();
    }
}
