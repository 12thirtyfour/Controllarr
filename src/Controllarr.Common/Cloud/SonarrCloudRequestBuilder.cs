namespace NzbDrone.Common.Cloud
{
    public interface ISonarrCloudRequestBuilder : IControllarrCloudRequestBuilder
    {
    }

    public class SonarrCloudRequestBuilder : ControllarrCloudRequestBuilder, ISonarrCloudRequestBuilder
    {
    }
}
