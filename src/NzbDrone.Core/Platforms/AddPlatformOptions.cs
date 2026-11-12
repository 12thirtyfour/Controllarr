namespace NzbDrone.Core.Tv
{
    public class AddPlatformOptions : MonitoringOptions
    {
        public bool SearchForMissingEpisodes { get; set; }
        public bool SearchForCutoffUnmetEpisodes { get; set; }
        public string ImportMode { get; set; }
    }
}
