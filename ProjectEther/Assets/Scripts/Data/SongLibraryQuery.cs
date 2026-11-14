using System;
using System.Collections.Generic;
using System.Text;

namespace OsuVR
{
    public enum SongSortMode
    {
        Title,
        Artist,
        Bpm,
        Length,
    }

    public enum SongBpmRange
    {
        Any,
        Under120,
        From120To180,
        AtLeast180,
    }

    public enum SongLengthRange
    {
        Any,
        Under2Minutes,
        From2To4Minutes,
        AtLeast4Minutes,
    }

    public enum SongApproachRange
    {
        Any,
        Under6,
        From6To8,
        AtLeast8,
    }

    /// <summary>选歌查询条件。仅投影元数据，不修改索引或正式谱面。</summary>
    public sealed class SongLibraryQuery
    {
        public string SearchText = "";
        public SongSortMode SortMode;
        public bool Descending;
        public bool FavoritesOnly;
        public SongBpmRange BpmRange;
        public SongLengthRange LengthRange;
        public SongApproachRange ApproachRange;

        public bool HasAttributeFilters =>
            BpmRange != SongBpmRange.Any
            || LengthRange != SongLengthRange.Any
            || ApproachRange != SongApproachRange.Any;
        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(SearchText) || FavoritesOnly || HasAttributeFilters;

        public void ClearFilters()
        {
            SearchText = "";
            FavoritesOnly = false;
            BpmRange = SongBpmRange.Any;
            LengthRange = SongLengthRange.Any;
            ApproachRange = SongApproachRange.Any;
        }

        public List<BeatmapSet> Apply(
            IReadOnlyList<BeatmapSet> catalog,
            Func<string, bool> isFavorite,
            bool originalLanguage = false
        )
        {
            var result = new List<BeatmapSet>();
            string[] words = Normalize(SearchText)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var source in catalog)
            {
                if (FavoritesOnly && (isFavorite == null || !isFavorite(source.FolderPath)))
                    continue;
                var visible = new BeatmapSet
                {
                    Title = source.Title,
                    Artist = source.Artist,
                    FolderPath = source.FolderPath,
                    BackgroundPath = source.BackgroundPath,
                };
                foreach (var difficulty in source.Difficulties)
                    if (Matches(difficulty, words))
                        visible.Difficulties.Add(difficulty);
                if (visible.Difficulties.Count > 0)
                    result.Add(visible);
            }
            result.Sort((left, right) => Compare(left, right, originalLanguage));
            return result;
        }

        private bool Matches(BeatmapMetadata map, string[] words)
        {
            bool bpm =
                BpmRange == SongBpmRange.Any
                || (BpmRange == SongBpmRange.Under120 && map.BPM < 120f)
                || (BpmRange == SongBpmRange.From120To180 && map.BPM >= 120f && map.BPM < 180f)
                || (BpmRange == SongBpmRange.AtLeast180 && map.BPM >= 180f);
            bool length =
                LengthRange == SongLengthRange.Any
                || (LengthRange == SongLengthRange.Under2Minutes && map.Length < 120000f)
                || (
                    LengthRange == SongLengthRange.From2To4Minutes
                    && map.Length >= 120000f
                    && map.Length < 240000f
                )
                || (LengthRange == SongLengthRange.AtLeast4Minutes && map.Length >= 240000f);
            bool approach =
                ApproachRange == SongApproachRange.Any
                || (ApproachRange == SongApproachRange.Under6 && map.ApproachRate < 6f)
                || (
                    ApproachRange == SongApproachRange.From6To8
                    && map.ApproachRate >= 6f
                    && map.ApproachRate < 8f
                )
                || (ApproachRange == SongApproachRange.AtLeast8 && map.ApproachRate >= 8f);
            if (!bpm || !length || !approach)
                return false;
            if (words.Length == 0)
                return true;
            string text = Normalize(
                string.Join(
                    "\n",
                    map.Title,
                    map.TitleUnicode,
                    map.Artist,
                    map.ArtistUnicode,
                    map.Creator,
                    map.Version
                )
            );
            foreach (string word in words)
                if (!text.Contains(word))
                    return false;
            return true;
        }

        private int Compare(BeatmapSet left, BeatmapSet right, bool originalLanguage)
        {
            var a = left.GetDefaultDifficulty();
            var b = right.GetDefaultDifficulty();
            int primary = SortMode switch
            {
                SongSortMode.Artist => StringComparer.OrdinalIgnoreCase.Compare(
                    a.GetDisplayArtist(originalLanguage),
                    b.GetDisplayArtist(originalLanguage)
                ),
                SongSortMode.Bpm => a.BPM.CompareTo(b.BPM),
                SongSortMode.Length => a.Length.CompareTo(b.Length),
                _ => StringComparer.OrdinalIgnoreCase.Compare(
                    a.GetDisplayTitle(originalLanguage),
                    b.GetDisplayTitle(originalLanguage)
                ),
            };
            if (primary != 0)
                return Descending ? -primary : primary;
            return StringComparer.Ordinal.Compare(left.FolderPath, right.FolderPath);
        }

        private static string Normalize(string text) =>
            (text ?? "").Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    }
}
