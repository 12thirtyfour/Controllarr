using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(239)]
    public class add_game_platforms_and_rom_entries : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("GamePlatforms").Exists())
            {
                Create.TableForModel("GamePlatforms")
                      .WithColumn("Name").AsString().NotNullable()
                      .WithColumn("Slug").AsString().Nullable()
                      .WithColumn("Code").AsString().Nullable()
                      .WithColumn("ReleaseYear").AsInt32().Nullable();
            }
            else
            {
                if (!Schema.Table("GamePlatforms").Column("Code").Exists())
                {
                    Alter.Table("GamePlatforms").AddColumn("Code").AsString().Nullable();
                }

                if (!Schema.Table("GamePlatforms").Column("ReleaseYear").Exists())
                {
                    Alter.Table("GamePlatforms").AddColumn("ReleaseYear").AsInt32().Nullable();
                }
            }

            if (!Schema.Table("GamePlatforms").Index("IX_GamePlatforms_Slug").Exists())
            {
                Create.Index("IX_GamePlatforms_Slug").OnTable("GamePlatforms").OnColumn("Slug").Unique();
            }

            if (!Schema.Table("RomEntries").Exists())
            {
                Create.TableForModel("RomEntries")
                      .WithColumn("PlatformId").AsInt32().NotNullable().ForeignKey("FK_RomEntries_GamePlatforms", "GamePlatforms", "Id")
                      .WithColumn("Title").AsString().NotNullable()
                      .WithColumn("CleanTitle").AsString().Nullable()
                      .WithColumn("Region").AsString().Nullable()
                      .WithColumn("Language").AsString().Nullable()
                      .WithColumn("Revision").AsString().Nullable()
                      .WithColumn("Hash").AsString().Nullable();

                Create.Index("IX_RomEntries_CleanTitle").OnTable("RomEntries").OnColumn("CleanTitle");
                Create.Index("IX_RomEntries_Hash").OnTable("RomEntries").OnColumn("Hash");
                Create.Index("IX_RomEntries_PlatformId").OnTable("RomEntries").OnColumn("PlatformId");
            }
        }
    }
}
