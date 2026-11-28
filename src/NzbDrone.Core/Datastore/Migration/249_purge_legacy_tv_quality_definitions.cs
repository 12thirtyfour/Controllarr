using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(249)]
    public class purge_legacy_tv_quality_definitions : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.Sql("DELETE FROM \"QualityDefinitions\" WHERE \"Quality\" NOT IN (0, 100, 101, 102, 103, 104, 105)");
            Execute.Sql("DELETE FROM \"QualityProfileQualityRanks\" WHERE \"QualityId\" NOT IN (0, 100, 101, 102, 103, 104, 105)");
        }
    }
}
