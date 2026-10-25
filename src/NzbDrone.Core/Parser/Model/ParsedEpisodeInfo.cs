using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser.Model
{
    public class ParsedEpisodeInfo
    {
        public string ReleaseTitle { get; set; }
        public string SeriesTitle { get; set; }
        public SeriesTitleInfo SeriesTitleInfo { get; set; }
        public QualityModel Quality { get; set; }
        public List<int> SeasonNumbers { get; set; } = [];

        // TODO: Remove this once `SeasonNumbers` replaces `SeasonNumber`
        [JsonPropertyOrder(-1)]
        public int? SeasonNumber
        {
            get => SeasonNumbers.Count > 0 ? SeasonNumbers[0] : null;
            set => SeasonNumbers = value.HasValue ? [value.Value] : [];
        }

        public int[] EpisodeNumbers { get; set; }
        public int[] AbsoluteEpisodeNumbers { get; set; }
        public decimal[] SpecialAbsoluteEpisodeNumbers { get; set; }
        public string AirDate { get; set; }
        public List<Language> Languages { get; set; }
        public bool FullSeason { get; set; }
        public bool IsPartialSeason { get; set; }
        public bool IsMultiSeason => SeasonNumbers.Count > 1;
        public bool IsSeasonExtra { get; set; }
        public bool IsSeasonTitle { get; set; }
        public bool IsSplitEpisode { get; set; }
        public bool IsMiniSeries { get; set; }
        public bool Special { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseHash { get; set; }
        public int SeasonPart { get; set; }
        public string ReleaseTokens { get; set; }
        public int? DailyPart { get; set; }

        public ParsedEpisodeInfo()
        {
            EpisodeNumbers = Array.Empty<int>();
            AbsoluteEpisodeNumbers = Array.Empty<int>();
            SpecialAbsoluteEpisodeNumbers = Array.Empty<decimal>();
            Languages = new List<Language>();
        }

        public bool IsDaily
        {
            get
            {
                return !string.IsNullOrWhiteSpace(AirDate);
            }

            private set
            {
            }
        }

        public bool IsAbsoluteNumbering
        {
            get
            {
                return AbsoluteEpisodeNumbers.Any();
            }

            private set
            {
            }
        }

        public bool IsPossibleSpecialEpisode
        {
            get
            {
                return ((AirDate.IsNullOrWhiteSpace() &&
                       SeriesTitle.IsNullOrWhiteSpace() &&
                       (EpisodeNumbers.Length == 0 || SeasonNumber == 0)) || (!SeriesTitle.IsNullOrWhiteSpace() && Special)) ||
                       (EpisodeNumbers.Length == 1 && EpisodeNumbers[0] == 0);
            }

            private set
            {
            }
        }

        public bool IsPossibleSceneSeasonSpecial
        {
            get
            {
                return SeasonNumber.HasValue && SeasonNumber != 0 && EpisodeNumbers.Length == 1 && EpisodeNumbers[0] == 0;
            }

            private set
            {
            }
        }

        public ReleaseType ReleaseType
        {
            get
            {
                if (EpisodeNumbers.Length > 1 || AbsoluteEpisodeNumbers.Length > 1)
                {
                    return Model.ReleaseType.MultiEpisode;
                }

                if (EpisodeNumbers.Length == 1 || AbsoluteEpisodeNumbers.Length == 1)
                {
                    return Model.ReleaseType.SingleEpisode;
                }

                if (FullSeason)
                {
                    return Model.ReleaseType.SeasonPack;
                }

                return Model.ReleaseType.Unknown;
            }
        }

        public override string ToString()
        {
            var gameString = "[Unknown Game]";

            if (IsDaily && EpisodeNumbers.Empty())
            {
                gameString = string.Format("{0}", AirDate);
            }
            else if (FullSeason)
            {
                if (IsMultiSeason)
                {
                    gameString = string.Format("Regions {0}-{1}", NzbDrone.Core.Games.RegionHelper.GetRegionName(SeasonNumbers.First()), NzbDrone.Core.Games.RegionHelper.GetRegionName(SeasonNumbers.Last()));
                }
                else
                {
                    gameString = SeasonNumber.HasValue ? NzbDrone.Core.Games.RegionHelper.GetRegionName(SeasonNumber.Value) : "[Unknown Region]";
                }
            }
            else if (EpisodeNumbers != null && EpisodeNumbers.Any())
            {
                var region = SeasonNumber.HasValue ? NzbDrone.Core.Games.RegionHelper.GetRegionName(SeasonNumber.Value) : "Game";
                gameString = string.Format("{0} #{1}", region, string.Join("-", EpisodeNumbers));
            }
            else if (AbsoluteEpisodeNumbers != null && AbsoluteEpisodeNumbers.Any())
            {
                gameString = string.Format("Game #{0}", string.Join("-", AbsoluteEpisodeNumbers));
            }
            else if (Special)
            {
                var region = SeasonNumber.HasValue ? NzbDrone.Core.Games.RegionHelper.GetRegionName(SeasonNumber.Value) : "Unlicensed";
                gameString = string.Format("{0} Special", region);
            }

            return string.Format("{0} - {1} {2}", SeriesTitle, gameString, Quality);
        }
    }
}
