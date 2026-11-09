using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Organizer
{
    public class NamingConfig : ModelBase
    {
        public static NamingConfig Default => new NamingConfig
        {
            RenameEpisodes = false,
            ReplaceIllegalCharacters = true,
            ColonReplacementFormat = ColonReplacementFormat.Smart,
            CustomColonReplacementFormat = string.Empty,
            MultiEpisodeStyle = MultiEpisodeStyle.PrefixedRange,
            StandardGameFormat = "{Game Title}",
            DiscGameFormat = "{Game Title}/{Game Title}",
            AnimeGameFormat = "{Game Title}",
            PlatformFolderFormat = "{Manufacturer}/{Platform}",
            RegionFolderFormat = "{Region}",
            UnlicensedFolderFormat = "Unlicensed"
        };

        public bool RenameEpisodes { get; set; }
        public bool ReplaceIllegalCharacters { get; set; }
        public ColonReplacementFormat ColonReplacementFormat { get; set; }
        public string CustomColonReplacementFormat { get; set; }
        public MultiEpisodeStyle MultiEpisodeStyle { get; set; }

        public string StandardGameFormat
        {
            get => StandardEpisodeFormat;
            set => StandardEpisodeFormat = value;
        }

        public string DiscGameFormat
        {
            get => DailyEpisodeFormat;
            set => DailyEpisodeFormat = value;
        }

        public string DailyGameFormat
        {
            get => DailyEpisodeFormat;
            set => DailyEpisodeFormat = value;
        }

        public string AnimeGameFormat
        {
            get => AnimeEpisodeFormat;
            set => AnimeEpisodeFormat = value;
        }

        public string PlatformFolderFormat
        {
            get => SeriesFolderFormat;
            set => SeriesFolderFormat = value;
        }

        public string RegionFolderFormat
        {
            get => SeasonFolderFormat;
            set => SeasonFolderFormat = value;
        }

        public string UnlicensedFolderFormat
        {
            get => SpecialsFolderFormat;
            set => SpecialsFolderFormat = value;
        }

        public string StandardEpisodeFormat { get; set; }
        public string DailyEpisodeFormat { get; set; }
        public string AnimeEpisodeFormat { get; set; }

        public string SeriesFolderFormat { get; set; }
        public string SeasonFolderFormat { get; set; }
        public string SpecialsFolderFormat { get; set; }
    }
}
