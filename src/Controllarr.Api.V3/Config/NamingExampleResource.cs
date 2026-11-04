using NzbDrone.Core.Organizer;

namespace Sonarr.Api.V3.Config
{
    public class NamingExampleResource
    {
        private string _singleEpisodeExample;
        private string _multiEpisodeExample;
        private string _dailyEpisodeExample;
        private string _animeEpisodeExample;
        private string _animeMultiEpisodeExample;

        public string SingleEpisodeExample
        {
            get => _singleEpisodeExample;
            set => _singleEpisodeExample = value;
        }

        public string SingleGameExample
        {
            get => _singleEpisodeExample;
            set => _singleEpisodeExample = value;
        }

        public string MultiEpisodeExample
        {
            get => _multiEpisodeExample;
            set => _multiEpisodeExample = value;
        }

        public string MultiGameExample
        {
            get => _multiEpisodeExample;
            set => _multiEpisodeExample = value;
        }

        public string DailyEpisodeExample
        {
            get => _dailyEpisodeExample;
            set => _dailyEpisodeExample = value;
        }

        public string DailyGameExample
        {
            get => _dailyEpisodeExample;
            set => _dailyEpisodeExample = value;
        }

        public string AnimeEpisodeExample
        {
            get => _animeEpisodeExample;
            set => _animeEpisodeExample = value;
        }

        public string AnimeGameExample
        {
            get => _animeEpisodeExample;
            set => _animeEpisodeExample = value;
        }

        public string AnimeMultiEpisodeExample
        {
            get => _animeMultiEpisodeExample;
            set => _animeMultiEpisodeExample = value;
        }

        public string AnimeMultiGameExample
        {
            get => _animeMultiEpisodeExample;
            set => _animeMultiEpisodeExample = value;
        }

        public string SeriesFolderExample { get; set; }

        public string SeasonFolderExample { get; set; }

        public string SpecialsFolderExample { get; set; }
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
                DailyGameFormat = model.DailyGameFormat,
                AnimeGameFormat = model.AnimeGameFormat,
                SeriesFolderFormat = model.SeriesFolderFormat,
                SeasonFolderFormat = model.SeasonFolderFormat,
                SpecialsFolderFormat = model.SpecialsFolderFormat
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
                StandardGameFormat = resource.StandardGameFormat ?? resource.StandardGameFormat,
                DailyGameFormat = resource.DailyGameFormat ?? resource.DailyGameFormat,
                AnimeGameFormat = resource.AnimeGameFormat ?? resource.AnimeGameFormat,
                SeriesFolderFormat = resource.SeriesFolderFormat,
                SeasonFolderFormat = resource.SeasonFolderFormat,
                SpecialsFolderFormat = resource.SpecialsFolderFormat
            };
        }
    }
}
