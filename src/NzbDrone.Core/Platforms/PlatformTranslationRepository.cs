using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Tv
{
    public interface IPlatformTranslationRepository : IBasicRepository<PlatformTranslation>
    {
        List<PlatformTranslation> GetTranslations(int seriesId);
        void DeleteTranslations(List<int> seriesIds);
    }

    public class PlatformTranslationRepository : BasicRepository<PlatformTranslation>, IPlatformTranslationRepository
    {
        public PlatformTranslationRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<PlatformTranslation> GetTranslations(int seriesId)
        {
            return Query(c => c.SeriesId == seriesId);
        }

        public void DeleteTranslations(List<int> seriesIds)
        {
            Delete(t => seriesIds.Contains(t.SeriesId));
        }
    }
}
