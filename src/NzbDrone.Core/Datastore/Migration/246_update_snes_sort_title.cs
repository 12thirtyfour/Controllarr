using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(246)]
    public class update_snes_sort_title : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("Platforms").Exists())
            {
                Execute.Sql("UPDATE \"Platforms\" SET \"SortTitle\" = 'nintendo super entertainment system' WHERE \"TvdbId\" = 1003 OR \"TitleSlug\" = 'snes' OR LOWER(\"Title\") = 'super nintendo entertainment system'");
            }
        }
    }
}
