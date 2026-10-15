using System;

namespace NzbDrone.Core.MetadataSource.Igdb
{
    public interface IIgdbTokenManager
    {
        string GetToken();
    }
}
