using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.ImportFolders
{
    public interface IImportFolderService
    {
        List<ImportFolder> All();
        List<ImportFolder> AllWithUnmappedFolders();
        ImportFolder Add(ImportFolder importFolder);
        void Remove(int id);
        ImportFolder Get(int id, bool timeout);
    }

    public class ImportFolderService : IImportFolderService
    {
        private readonly IImportFolderRepository _importFolderRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly IRootFolderService _rootFolderService;
        private readonly Logger _logger;

        public ImportFolderService(IImportFolderRepository importFolderRepository,
                                   IDiskProvider diskProvider,
                                   IRootFolderService rootFolderService,
                                   Logger logger)
        {
            _importFolderRepository = importFolderRepository;
            _diskProvider = diskProvider;
            _rootFolderService = rootFolderService;
            _logger = logger;
        }

        public List<ImportFolder> All()
        {
            return _importFolderRepository.All().ToList();
        }

        public List<ImportFolder> AllWithUnmappedFolders()
        {
            var importFolders = _importFolderRepository.All().ToList();

            importFolders.ForEach(folder =>
            {
                try
                {
                    if (folder.Path.IsPathValid(PathValidationType.CurrentOs))
                    {
                        GetDetails(folder, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to get details for import source folder {0}", folder.Path);
                    folder.UnmappedFolders = new List<UnmappedFolder>();
                }
            });

            return importFolders;
        }

        public ImportFolder Get(int id, bool timeout)
        {
            var folder = _importFolderRepository.Get(id);
            if (folder != null)
            {
                GetDetails(folder, timeout);
            }

            return folder;
        }

        public ImportFolder Add(ImportFolder importFolder)
        {
            var all = All();

            if (string.IsNullOrWhiteSpace(importFolder.Path) || !Path.IsPathRooted(importFolder.Path))
            {
                throw new ArgumentException("Invalid path");
            }

            if (!_diskProvider.FolderExists(importFolder.Path) && !_diskProvider.FileExists(importFolder.Path))
            {
                throw new DirectoryNotFoundException("Can't add import source directory that doesn't exist.");
            }

            if (all.Exists(r => r.Path.PathEquals(importFolder.Path)))
            {
                throw new InvalidOperationException("Import source directory already exists.");
            }

            _importFolderRepository.Insert(importFolder);
            GetDetails(importFolder, true);

            return importFolder;
        }

        public void Remove(int id)
        {
            _importFolderRepository.Delete(id);
        }

        private void GetDetails(ImportFolder folder, bool timeout)
        {
            Task.Run(() =>
            {
                if (_diskProvider.FolderExists(folder.Path))
                {
                    folder.Accessible = true;
                    folder.IsEmpty = _diskProvider.FolderEmpty(folder.Path);
                    folder.FreeSpace = _diskProvider.GetAvailableSpace(folder.Path);
                    folder.TotalSpace = _diskProvider.GetTotalSize(folder.Path);
                    folder.ContainsRomFiles = _rootFolderService.CheckContainsRomFiles(folder.Path);
                    folder.UnmappedFolders = _rootFolderService.GetUnmappedFolders(folder.Path);
                }
                else if (_diskProvider.FileExists(folder.Path))
                {
                    folder.Accessible = true;
                    folder.IsEmpty = false;
                    folder.FreeSpace = _diskProvider.GetAvailableSpace(Path.GetDirectoryName(folder.Path));
                    folder.TotalSpace = _diskProvider.GetTotalSize(Path.GetDirectoryName(folder.Path));
                    folder.ContainsRomFiles = false;
                    folder.UnmappedFolders = _rootFolderService.GetUnmappedFolders(folder.Path);
                }
            }).Wait(timeout ? 5000 : -1);
        }
    }
}
