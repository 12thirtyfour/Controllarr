using System.Collections.Generic;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DataAugmentation.Xem.Model;

namespace NzbDrone.Core.DataAugmentation.Xem
{
    public interface IXemProxy
    {
        List<int> GetXemSeriesIds();
        List<XemSceneTvdbMapping> GetSceneTvdbMappings(int id);
        List<SceneMapping> GetSceneTvdbNames();
    }

    public class XemProxy : IXemProxy
    {
        public List<int> GetXemSeriesIds()
        {
            return new List<int>();
        }

        public List<XemSceneTvdbMapping> GetSceneTvdbMappings(int id)
        {
            return new List<XemSceneTvdbMapping>();
        }

        public List<SceneMapping> GetSceneTvdbNames()
        {
            return new List<SceneMapping>();
        }
    }
}
