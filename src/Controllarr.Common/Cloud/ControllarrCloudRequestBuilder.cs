using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface IControllarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory Releases { get; }
    }

    public class ControllarrCloudRequestBuilder : IControllarrCloudRequestBuilder
    {
        public ControllarrCloudRequestBuilder()
        {
            Services = new HttpRequestBuilder("https://services.sonarr.tv/v1/")
                .CreateFactory();

            Releases = new HttpRequestBuilder("https://api.github.com/repos/12thirtyfour/Controllarr/")
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }

        public IHttpRequestBuilderFactory Releases { get; }
    }
}
