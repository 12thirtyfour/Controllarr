using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(247)]
    public class add_dat_entries_sha256_index : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (Schema.Table("DatEntries").Exists() &&
                Schema.Table("DatEntries").Column("Sha256").Exists() &&
                !Schema.Table("DatEntries").Index("IX_DatEntries_Sha256").Exists())
            {
                Create.Index("IX_DatEntries_Sha256").OnTable("DatEntries").OnColumn("Sha256");
            }
        }
    }
}
