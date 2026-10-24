using System;

namespace NzbDrone.Core.Organizer
{
    public interface INamingConfigService
    {
        NamingConfig GetConfig();
        void Save(NamingConfig namingConfig);
    }

    public class NamingConfigService : INamingConfigService
    {
        private readonly INamingConfigRepository _repository;

        public NamingConfigService(INamingConfigRepository repository)
        {
            _repository = repository;
        }

        public NamingConfig GetConfig()
        {
            var config = _repository.SingleOrDefault();

            if (config == null)
            {
                lock (_repository)
                {
                    config = _repository.SingleOrDefault();

                    if (config == null)
                    {
                        var defaultConfig = NamingConfig.Default;
                        defaultConfig.SeriesFolderFormat = "{Platform}";
                        defaultConfig.SeasonFolderFormat = "{Region}";
                        defaultConfig.SpecialsFolderFormat = "Unlicensed";
                        defaultConfig.StandardEpisodeFormat = "{Game Title}";
                        defaultConfig.DailyEpisodeFormat = "{Game Title}";
                        defaultConfig.AnimeEpisodeFormat = "{Game Title}";

                        _repository.Insert(defaultConfig);
                        config = _repository.Single();
                    }
                }
            }

            if (config != null && UpgradeLegacyTvNamingConfig(config))
            {
                _repository.Upsert(config);
            }

            return config;
        }

        public void Save(NamingConfig namingConfig)
        {
            _repository.Upsert(namingConfig);
        }

        private static bool UpgradeLegacyTvNamingConfig(NamingConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.SeasonFolderFormat) &&
                !config.SeasonFolderFormat.Equals("Season {season}", StringComparison.OrdinalIgnoreCase) &&
                !config.SeasonFolderFormat.Equals("Season {season:00}", StringComparison.OrdinalIgnoreCase) &&
                !config.SeasonFolderFormat.Equals("Season {season:0}", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            config.SeasonFolderFormat = "{Region}";
            config.SpecialsFolderFormat = "Unlicensed";
            config.SeriesFolderFormat = "{Platform}";
            config.StandardEpisodeFormat = "{Game Title}";
            config.DailyEpisodeFormat = "{Game Title}";
            config.AnimeEpisodeFormat = "{Game Title}";

            return true;
        }
    }
}
