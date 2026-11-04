using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class NamingConfigResource : RestResource
    {
        private string _standardGameFormat;
        private string _dailyGameFormat;
        private string _animeGameFormat;
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

        public string StandardGameFormat
        {
            get => _standardGameFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _standardGameFormat == null)
                {
                    _standardGameFormat = value;
                }
            }
        }

        public string DailyGameFormat
        {
            get => _dailyGameFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _dailyGameFormat == null)
                {
                    _dailyGameFormat = value;
                }
            }
        }

        public string AnimeGameFormat
        {
            get => _animeGameFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _animeGameFormat == null)
                {
                    _animeGameFormat = value;
                }
            }
        }

        public string SeriesFolderFormat { get; set; }

        public string SeasonFolderFormat { get; set; }

        public string SpecialsFolderFormat { get; set; }
    }
}
