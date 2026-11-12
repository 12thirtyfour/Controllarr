using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.ImportFolders
{
    public interface IImportFolderRepository : IBasicRepository<ImportFolder>
    {
    }

    public class ImportFolderRepository : BasicRepository<ImportFolder>, IImportFolderRepository
    {
        public ImportFolderRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
