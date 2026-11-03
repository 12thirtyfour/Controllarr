using System.Collections.Generic;

namespace NzbDrone.Core.DataAugmentation.Scene
{
    public interface ISceneMappingProxy
    {
        List<SceneMapping> Fetch();
    }

    public class SceneMappingProxy : ISceneMappingProxy
    {
        public List<SceneMapping> Fetch()
        {
            return new List<SceneMapping>();
        }
    }
}
