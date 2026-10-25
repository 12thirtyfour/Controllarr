using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Dat.Commands
{
    public class UpdateDatCatalogsCommand : Command
    {
        public List<int> PlatformTvdbIds { get; set; } = new List<int>();

        public override bool SendUpdatesToClient => true;
    }
}
