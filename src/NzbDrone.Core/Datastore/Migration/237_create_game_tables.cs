using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(237)]
    public class create_game_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("GamePlatforms")
                  .WithColumn("Name").AsString().Nullable()
                  .WithColumn("Slug").AsString().Nullable()
                  .WithColumn("IgdbId").AsInt32().Nullable()
                  .WithColumn("Overview").AsString().Nullable()
                  .WithColumn("Path").AsString().Nullable()
                  .WithColumn("Monitored").AsBoolean().WithDefaultValue(false)
                  .WithColumn("RootFolderPath").AsString().Nullable();

            Create.TableForModel("GameTitles")
                  .WithColumn("PlatformId").AsInt32().NotNullable()
                  .WithColumn("Title").AsString().Nullable()
                  .WithColumn("CleanTitle").AsString().Nullable()
                  .WithColumn("IgdbId").AsInt32().Nullable()
                  .WithColumn("Monitored").AsBoolean().WithDefaultValue(false)
                  .WithColumn("ProviderId").AsString().Nullable();

            Create.TableForModel("GameReleases")
                  .WithColumn("GameTitleId").AsInt32().NotNullable()
                  .WithColumn("PlatformId").AsInt32().NotNullable()
                  .WithColumn("Title").AsString().Nullable()
                  .WithColumn("ReleaseName").AsString().Nullable()
                  .WithColumn("VersionTag").AsString().Nullable()
                  .WithColumn("Revision").AsString().Nullable()
                  .WithColumn("Region").AsString().Nullable()
                  .WithColumn("Monitored").AsBoolean().WithDefaultValue(false)
                  .WithColumn("FileId").AsInt32().Nullable();

            Create.Index("IX_GameTitles_PlatformId").OnTable("GameTitles").OnColumn("PlatformId");
            Create.Index("IX_GameReleases_GameTitleId").OnTable("GameReleases").OnColumn("GameTitleId");
            Create.Index("IX_GameReleases_PlatformId").OnTable("GameReleases").OnColumn("PlatformId");
        }
    }
}
