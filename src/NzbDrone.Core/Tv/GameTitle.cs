using System.Collections.Generic;

namespace NzbDrone.Core.Tv
{
    public class GameTitle
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ProviderId { get; set; }
        public List<GameRelease> Releases { get; set; }

        public GameTitle()
        {
            Releases = new List<GameRelease>();
        }
    }
}
