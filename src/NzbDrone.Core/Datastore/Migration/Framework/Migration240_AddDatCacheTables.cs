using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(240)]
    public class Migration240_AddDatCacheTables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("DatFiles").Exists())
            {
                Create.TableForModel("DatFiles")
                      .WithColumn("Name").AsString().NotNullable()
                      .WithColumn("Description").AsString().Nullable()
                      .WithColumn("Version").AsString().Nullable()
                      .WithColumn("Author").AsString().Nullable()
                      .WithColumn("Category").AsString().Nullable()
                      .WithColumn("CreatedAt").AsDateTime().NotNullable();
            }

            if (!Schema.Table("DatEntries").Exists())
            {
                Create.TableForModel("DatEntries")
                      .WithColumn("DatFileId").AsInt32().NotNullable().ForeignKey("FK_DatEntries_DatFiles", "DatFiles", "Id")
                      .WithColumn("GameName").AsString().Nullable()
                      .WithColumn("RomName").AsString().Nullable()
                      .WithColumn("Size").AsInt64().NotNullable()
                      .WithColumn("Crc32").AsString().Nullable()
                      .WithColumn("Md5").AsString().Nullable()
                      .WithColumn("Sha1").AsString().Nullable()
                      .WithColumn("Region").AsString().Nullable()
                      .WithColumn("Status").AsString().Nullable();

                Create.Index("IX_DatEntries_Crc32").OnTable("DatEntries").OnColumn("Crc32");
                Create.Index("IX_DatEntries_Md5").OnTable("DatEntries").OnColumn("Md5");
                Create.Index("IX_DatEntries_Sha1").OnTable("DatEntries").OnColumn("Sha1");
                Create.Index("IX_DatEntries_DatFileId").OnTable("DatEntries").OnColumn("DatFileId");
            }
        }
    }
}
