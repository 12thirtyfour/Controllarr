using System;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    [Flags]
    public enum SearchMode
    {
        Default = 0,
        SearchID = 1,
        SearchTitle = 2
    }
}
