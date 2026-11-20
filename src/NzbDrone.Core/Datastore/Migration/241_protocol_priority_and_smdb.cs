using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(241)]
    public class protocol_priority_and_smdb : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("DelayProfiles").Column("EnableHttp").Exists())
            {
                Alter.Table("DelayProfiles").AddColumn("EnableHttp").AsBoolean().WithDefaultValue(false);
            }

            if (!Schema.Table("DelayProfiles").Column("HttpDelay").Exists())
            {
                Alter.Table("DelayProfiles").AddColumn("HttpDelay").AsInt32().WithDefaultValue(0);
            }

            if (!Schema.Table("DelayProfiles").Column("ProtocolPriority").Exists())
            {
                Alter.Table("DelayProfiles").AddColumn("ProtocolPriority").AsString().Nullable();
            }

            if (Schema.Table("DatEntries").Exists())
            {
                if (!Schema.Table("DatEntries").Column("Sha256").Exists())
                {
                    Alter.Table("DatEntries").AddColumn("Sha256").AsString().Nullable();
                }

                if (!Schema.Table("DatEntries").Column("SmdbPath").Exists())
                {
                    Alter.Table("DatEntries").AddColumn("SmdbPath").AsString().Nullable();
                }

                if (!Schema.Table("DatEntries").Index("IX_DatEntries_Sha256").Exists())
                {
                    Create.Index("IX_DatEntries_Sha256").OnTable("DatEntries").OnColumn("Sha256");
                }
            }
        }
    }
}
