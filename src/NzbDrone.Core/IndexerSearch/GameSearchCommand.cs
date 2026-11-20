using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class GameSearchCommand : Command
    {
        public List<int> GameIds { get; set; }

        public override bool SendUpdatesToClient => true;

        public GameSearchCommand()
        {
        }

        public GameSearchCommand(List<int> gameIds)
        {
            GameIds = gameIds;
        }
    }
}
