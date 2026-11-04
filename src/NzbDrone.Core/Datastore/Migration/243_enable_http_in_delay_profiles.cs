using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(243)]
    public class enable_http_in_delay_profiles : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("DelayProfiles").Exists() && Schema.Table("DelayProfiles").Column("EnableHttp").Exists())
            {
                Update.Table("DelayProfiles").Set(new { EnableHttp = true }).AllRows();
            }
        }
    }
}
