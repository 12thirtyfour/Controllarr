using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications
{
    public class PlatformAddMessage
    {
        public string Message { get; set; }
        public Series Series { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
