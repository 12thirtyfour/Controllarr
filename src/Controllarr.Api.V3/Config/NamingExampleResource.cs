using NzbDrone.Core.Organizer;

namespace Sonarr.Api.V3.Config
{
    public class NamingExampleResource
    {
        private string _singleGameExample;
        private string _multiGameExample;
        private string _discGameExample;
        private string _animeGameExample;
        private string _animeMultiGameExample;

        public string SingleEpisodeExample
        {
            get => _singleGameExample;
            set => _singleGameExample = value;
        }

        public string SingleGameExample
        {
            get => _singleGameExample;
            set => _singleGameExample = value;
        }

        public string MultiEpisodeExample
        {
            get => _multiGameExample;
            set => _multiGameExample = value;
        }

        public string MultiGameExample
        {
            get => _multiGameExample;
            set => _multiGameExample = value;
        }

        public string DailyEpisodeExample
        {
            get => _discGameExample;
            set => _discGameExample = value;
        }

        public string DailyGameExample
        {
            get => _discGameExample;
            set => _discGameExample = value;
        }

        public string DiscGameExample
        {
            get => _discGameExample;
            set => _discGameExample = value;
        }

        public string AnimeEpisodeExample
        {
            get => _animeGameExample;
            set => _animeGameExample = value;
        }

        public string AnimeGameExample
        {
            get => _animeGameExample;
            set => _animeGameExample = value;
        }

        public string AnimeMultiEpisodeExample
        {
            get => _animeMultiGameExample;
            set => _animeMultiGameExample = value;
        }

        public string AnimeMultiGameExample
        {
            get => _animeMultiGameExample;
            set => _animeMultiGameExample = value;
        }

        public string PlatformFolderExample { get; set; }

        public string RegionFolderExample { get; set; }

        public string UnlicensedFolderExample { get; set; }

        public string SeriesFolderExample
        {
            get => PlatformFolderExample;
            set => PlatformFolderExample = value;
        }

        public string SeasonFolderExample
        {
            get => RegionFolderExample;
            set => RegionFolderExample = value;
        }

        public string SpecialsFolderExample
        {
            get => UnlicensedFolderExample;
            set => UnlicensedFolderExample = value;
        }
    }

    public static class NamingConfigResourceMapper
    {
        public static NamingConfigResource ToResource(this NamingConfig model)
        {
            return new NamingConfigResource
            {
                Id = model.Id,

                RenameEpisodes = model.RenameEpisodes,
                RenameGames = model.RenameEpisodes,
                ReplaceIllegalCharacters = model.ReplaceIllegalCharacters,
                ColonReplacementFormat = (int)model.ColonReplacementFormat,
                CustomColonReplacementFormat = model.CustomColonReplacementFormat,
                MultiEpisodeStyle = (int)model.MultiEpisodeStyle,
                MultiGameStyle = (int)model.MultiEpisodeStyle,
                StandardGameFormat = model.StandardGameFormat,
                DiscGameFormat = model.DiscGameFormat,
                DailyGameFormat = model.DiscGameFormat,
                AnimeGameFormat = model.AnimeGameFormat,
                PlatformFolderFormat = model.PlatformFolderFormat,
                RegionFolderFormat = model.RegionFolderFormat,
                UnlicensedFolderFormat = model.UnlicensedFolderFormat
            };
        }

        public static NamingConfig ToModel(this NamingConfigResource resource)
        {
            return new NamingConfig
            {
                Id = resource.Id,

                RenameEpisodes = resource.RenameGames || resource.RenameEpisodes,
                ReplaceIllegalCharacters = resource.ReplaceIllegalCharacters,
                MultiEpisodeStyle = (MultiEpisodeStyle)(resource.MultiGameStyle != 0 ? resource.MultiGameStyle : resource.MultiEpisodeStyle),
                ColonReplacementFormat = (ColonReplacementFormat)resource.ColonReplacementFormat,
                CustomColonReplacementFormat = resource.CustomColonReplacementFormat ?? "",
                StandardGameFormat = resource.StandardGameFormat,
                DiscGameFormat = resource.DiscGameFormat,
                AnimeGameFormat = resource.AnimeGameFormat,
                PlatformFolderFormat = resource.PlatformFolderFormat,
                RegionFolderFormat = resource.RegionFolderFormat,
                UnlicensedFolderFormat = resource.UnlicensedFolderFormat
            };
        }
    }
}
