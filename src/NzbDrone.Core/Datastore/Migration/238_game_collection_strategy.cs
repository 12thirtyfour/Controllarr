using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(238)]
    public class game_collection_strategy : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("GamePlatforms").AddColumn("CollectionStrategy").AsInt32().WithDefaultValue(0);
            Alter.Table("GamePlatforms").AddColumn("RevisionPreference").AsInt32().WithDefaultValue(0);

            Alter.Table("GameTitles").AddColumn("PinnedVersion").AsString().Nullable();

            Alter.Table("Series").AddColumn("CollectionStrategy").AsInt32().WithDefaultValue(0);
            Alter.Table("Series").AddColumn("RevisionPreference").AsInt32().WithDefaultValue(0);
            Alter.Table("Series").AddColumn("PinnedVersion").AsString().Nullable();
        }
    }
}
