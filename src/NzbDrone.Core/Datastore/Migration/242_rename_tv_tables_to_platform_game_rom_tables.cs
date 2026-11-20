using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(242)]
    public class rename_tv_tables_to_platform_game_rom_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("Series").Exists() && !Schema.Table("Platforms").Exists())
            {
                Rename.Table("Series").To("Platforms");
            }

            if (Schema.Table("SeriesTranslations").Exists() && !Schema.Table("PlatformTranslations").Exists())
            {
                Rename.Table("SeriesTranslations").To("PlatformTranslations");
            }

            if (Schema.Table("Episodes").Exists() && !Schema.Table("Games").Exists())
            {
                Rename.Table("Episodes").To("Games");
            }

            if (Schema.Table("EpisodeFiles").Exists() && !Schema.Table("RomFiles").Exists())
            {
                Rename.Table("EpisodeFiles").To("RomFiles");
            }

            if (Schema.Table("SceneMappings").Exists() && !Schema.Table("PlatformMappings").Exists())
            {
                Rename.Table("SceneMappings").To("PlatformMappings");
            }

            if (Schema.Table("NamingConfig").Exists())
            {
                Execute.Sql("UPDATE \"NamingConfig\" SET \"SeasonFolderFormat\" = '{Region}' WHERE \"SeasonFolderFormat\" LIKE 'Season %'");
                Execute.Sql("UPDATE \"NamingConfig\" SET \"SpecialsFolderFormat\" = 'Unlicensed' WHERE \"SpecialsFolderFormat\" = 'Specials'");
                Execute.Sql("UPDATE \"NamingConfig\" SET \"SeriesFolderFormat\" = '{Platform}' WHERE \"SeriesFolderFormat\" = '{Series Title}'");
                Execute.Sql("UPDATE \"NamingConfig\" SET \"StandardEpisodeFormat\" = '{Game Title}' WHERE \"StandardEpisodeFormat\" LIKE '%S{season%'");
            }
        }
    }
}
