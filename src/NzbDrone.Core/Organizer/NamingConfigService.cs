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
            var changed = false;

            if (string.IsNullOrWhiteSpace(config.SeasonFolderFormat) ||
                config.SeasonFolderFormat.Equals("Season {season}", StringComparison.OrdinalIgnoreCase) ||
                config.SeasonFolderFormat.Equals("Season {season:00}", StringComparison.OrdinalIgnoreCase) ||
                config.SeasonFolderFormat.Equals("Season {season:0}", StringComparison.OrdinalIgnoreCase))
            {
                config.SeasonFolderFormat = "{Region}";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.SpecialsFolderFormat) ||
                config.SpecialsFolderFormat.Equals("Specials", StringComparison.OrdinalIgnoreCase))
            {
                config.SpecialsFolderFormat = "Unlicensed";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.SeriesFolderFormat) ||
                config.SeriesFolderFormat.Equals("{Series Title}", StringComparison.OrdinalIgnoreCase))
            {
                config.SeriesFolderFormat = "{Platform}";
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(config.StandardEpisodeFormat) &&
                config.StandardEpisodeFormat.IndexOf("S{season", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                config.StandardEpisodeFormat = "{Game Title}";
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(config.DailyEpisodeFormat) &&
                config.DailyEpisodeFormat.IndexOf("{Air-Date}", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                config.DailyEpisodeFormat = "{Game Title}";
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(config.AnimeEpisodeFormat) &&
                config.AnimeEpisodeFormat.IndexOf("S{season", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                config.AnimeEpisodeFormat = "{Game Title}";
                changed = true;
            }

            return changed;
        }
    }
}
