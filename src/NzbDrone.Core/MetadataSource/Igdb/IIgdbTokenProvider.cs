namespace NzbDrone.Core.MetadataSource.Igdb
{
    public interface IIgdbTokenProvider
    {
        string GetAccessToken();
    }
}
