using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface IControllarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory SkyHookTvdb { get; }
        IHttpRequestBuilderFactory Releases { get; }
    }

    public class ControllarrCloudRequestBuilder : IControllarrCloudRequestBuilder
    {
        public ControllarrCloudRequestBuilder()
        {
            Services = new HttpRequestBuilder("https://services.sonarr.tv/v1/")
                .CreateFactory();

            SkyHookTvdb = new HttpRequestBuilder("https://skyhook.sonarr.tv/v2/tvdb/{route}/")
                .CreateFactory();

            Releases = new HttpRequestBuilder("https://api.github.com/repos/12thirtyfour/Controllarr/")
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }

        public IHttpRequestBuilderFactory SkyHookTvdb { get; }

        public IHttpRequestBuilderFactory Releases { get; }
    }
}
