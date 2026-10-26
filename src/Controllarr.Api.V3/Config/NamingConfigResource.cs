using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class NamingConfigResource : RestResource
    {
        private string _standardEpisodeFormat;
        private string _dailyEpisodeFormat;
        private string _animeEpisodeFormat;
        private bool _renameEpisodes;
        private int _multiEpisodeStyle;

        public bool RenameEpisodes
        {
            get => _renameEpisodes;
            set => _renameEpisodes = value;
        }

        public bool RenameGames
        {
            get => _renameEpisodes;
            set => _renameEpisodes = value;
        }

        public bool ReplaceIllegalCharacters { get; set; }

        public int ColonReplacementFormat { get; set; }

        public string CustomColonReplacementFormat { get; set; }

        public int MultiEpisodeStyle
        {
            get => _multiEpisodeStyle;
            set => _multiEpisodeStyle = value;
        }

        public int MultiGameStyle
        {
            get => _multiEpisodeStyle;
            set => _multiEpisodeStyle = value;
        }

        public string StandardEpisodeFormat
        {
            get => _standardEpisodeFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _standardEpisodeFormat == null)
                {
                    _standardEpisodeFormat = value;
                }
            }
        }

        public string StandardGameFormat
        {
            get => _standardEpisodeFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _standardEpisodeFormat == null)
                {
                    _standardEpisodeFormat = value;
                }
            }
        }

        public string DailyEpisodeFormat
        {
            get => _dailyEpisodeFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _dailyEpisodeFormat == null)
                {
                    _dailyEpisodeFormat = value;
                }
            }
        }

        public string DailyGameFormat
        {
            get => _dailyEpisodeFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _dailyEpisodeFormat == null)
                {
                    _dailyEpisodeFormat = value;
                }
            }
        }

        public string AnimeEpisodeFormat
        {
            get => _animeEpisodeFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _animeEpisodeFormat == null)
                {
                    _animeEpisodeFormat = value;
                }
            }
        }

        public string AnimeGameFormat
        {
            get => _animeEpisodeFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _animeEpisodeFormat == null)
                {
                    _animeEpisodeFormat = value;
                }
            }
        }

        public string SeriesFolderFormat { get; set; }

        public string SeasonFolderFormat { get; set; }

        public string SpecialsFolderFormat { get; set; }
    }
}
