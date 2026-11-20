using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class NamingConfigResource : RestResource
    {
        private string _standardGameFormat;
        private string _discGameFormat;
        private string _animeGameFormat;
        private string _platformFolderFormat;
        private string _regionFolderFormat;
        private string _unlicensedFolderFormat;
        private bool _renameGames;
        private int _multiGameStyle;

        public bool RenameEpisodes
        {
            get => _renameGames;
            set => _renameGames = value;
        }

        public bool RenameGames
        {
            get => _renameGames;
            set => _renameGames = value;
        }

        public bool ReplaceIllegalCharacters { get; set; }

        public int ColonReplacementFormat { get; set; }

        public string CustomColonReplacementFormat { get; set; }

        public int MultiEpisodeStyle
        {
            get => _multiGameStyle;
            set => _multiGameStyle = value;
        }

        public int MultiGameStyle
        {
            get => _multiGameStyle;
            set => _multiGameStyle = value;
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

        public string DiscGameFormat
        {
            get => _discGameFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _discGameFormat == null)
                {
                    _discGameFormat = value;
                }
            }
        }

        public string DailyGameFormat
        {
            get => _discGameFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _discGameFormat == null)
                {
                    _discGameFormat = value;
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

        public string PlatformFolderFormat
        {
            get => _platformFolderFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _platformFolderFormat == null)
                {
                    _platformFolderFormat = value;
                }
            }
        }

        public string SeriesFolderFormat
        {
            get => _platformFolderFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _platformFolderFormat == null)
                {
                    _platformFolderFormat = value;
                }
            }
        }

        public string RegionFolderFormat
        {
            get => _regionFolderFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _regionFolderFormat == null)
                {
                    _regionFolderFormat = value;
                }
            }
        }

        public string SeasonFolderFormat
        {
            get => _regionFolderFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _regionFolderFormat == null)
                {
                    _regionFolderFormat = value;
                }
            }
        }

        public string UnlicensedFolderFormat
        {
            get => _unlicensedFolderFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _unlicensedFolderFormat == null)
                {
                    _unlicensedFolderFormat = value;
                }
            }
        }

        public string SpecialsFolderFormat
        {
            get => _unlicensedFolderFormat;
            set
            {
                if (!string.IsNullOrEmpty(value) || _unlicensedFolderFormat == null)
                {
                    _unlicensedFolderFormat = value;
                }
            }
        }
    }
}
