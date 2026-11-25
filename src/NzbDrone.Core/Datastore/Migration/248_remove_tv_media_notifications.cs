using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(248)]
    public class remove_tv_media_notifications : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.Sql("DELETE FROM \"Notifications\" WHERE \"Implementation\" IN ('SynologyIndexer', 'Notifiarr', 'PlexClient', 'PlexServer', 'Xbmc', 'MediaBrowser', 'Emby', 'Trakt', 'Subgen')");
        }
    }
}
