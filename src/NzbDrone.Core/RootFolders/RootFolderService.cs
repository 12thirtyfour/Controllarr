using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Dat;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.RootFolders
{
    public interface IRootFolderService
    {
        List<RootFolder> All();
        List<RootFolder> AllWithUnmappedFolders();
        RootFolder Add(RootFolder rootDir);
        void Remove(int id);
        RootFolder Get(int id, bool timeout);
        string GetBestRootFolderPath(string path);
    }

    public class RootFolderService : IRootFolderService
    {
        private readonly IRootFolderRepository _rootFolderRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly ISeriesRepository _seriesRepository;
        private readonly INamingConfigService _namingConfigService;
        private readonly IDatCacheService _datCacheService;
        private readonly Logger _logger;

        private readonly ICached<string> _cache;

        private static readonly HashSet<string> SpecialFolders = new HashSet<string>
                                                                 {
                                                                     "$recycle.bin",
                                                                     "system volume information",
                                                                     "recycler",
                                                                     "lost+found",
                                                                     ".appledb",
                                                                     ".appledesktop",
                                                                     ".appledouble",
                                                                     "@eadir",
                                                                     ".grab"
                                                                 };

        public RootFolderService(IRootFolderRepository rootFolderRepository,
                                 IDiskProvider diskProvider,
                                 ISeriesRepository seriesRepository,
                                 INamingConfigService namingConfigService,
                                 ICacheManager cacheManager,
                                 Logger logger,
                                 IDatCacheService datCacheService = null)
        {
            _rootFolderRepository = rootFolderRepository;
            _diskProvider = diskProvider;
            _seriesRepository = seriesRepository;
            _namingConfigService = namingConfigService;
            _datCacheService = datCacheService;
            _logger = logger;

            _cache = cacheManager.GetCache<string>(GetType());
        }

        public List<RootFolder> All()
        {
            var rootFolders = _rootFolderRepository.All().ToList();

            return rootFolders;
        }

        public List<RootFolder> AllWithUnmappedFolders()
        {
            var rootFolders = _rootFolderRepository.All().ToList();
            var seriesPaths = _seriesRepository.AllSeriesPaths();

            rootFolders.ForEach(folder =>
            {
                try
                {
                    if (folder.Path.IsPathValid(PathValidationType.CurrentOs))
                    {
                        GetDetails(folder, seriesPaths, true);
                    }
                }

                // We don't want an exception to prevent the root folders from loading in the UI, so they can still be deleted
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to get free space and unmapped folders for root folder {0}", folder.Path);
                    folder.UnmappedFolders = new List<UnmappedFolder>();
                }
            });

            return rootFolders;
        }

        public RootFolder Add(RootFolder rootFolder)
        {
            var all = All();

            if (string.IsNullOrWhiteSpace(rootFolder.Path) || !Path.IsPathRooted(rootFolder.Path))
            {
                throw new ArgumentException("Invalid path");
            }

            if (!_diskProvider.FolderExists(rootFolder.Path))
            {
                throw new DirectoryNotFoundException("Can't add root directory that doesn't exist.");
            }

            if (all.Exists(r => r.Path.PathEquals(rootFolder.Path)))
            {
                throw new InvalidOperationException("Recent directory already exists.");
            }

            if (!_diskProvider.FolderWritable(rootFolder.Path))
            {
                throw new UnauthorizedAccessException($"Root folder path '{rootFolder.Path}' is not writable by user '{Environment.UserName}'");
            }

            _rootFolderRepository.Insert(rootFolder);
            var seriesPaths = _seriesRepository.AllSeriesPaths();

            GetDetails(rootFolder, seriesPaths, true);
            _cache.Clear();

            return rootFolder;
        }

        public void Remove(int id)
        {
            _rootFolderRepository.Delete(id);
            _cache.Clear();
        }

        private List<UnmappedFolder> GetUnmappedFolders(string path, Dictionary<int, string> seriesPaths)
        {
            _logger.Debug("Generating list of unmapped folders");

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Invalid path provided", nameof(path));
            }

            var results = new List<UnmappedFolder>();

            if (!_diskProvider.FolderExists(path))
            {
                _logger.Debug("Path supplied does not exist: {0}", path);
                return results;
            }

            var subFolderDepth = _namingConfigService.GetConfig().SeriesFolderFormat.Count(f => f == Path.DirectorySeparatorChar);
            var possibleSeriesFolders = _diskProvider.GetDirectories(path).ToList();

            if (subFolderDepth > 0)
            {
                for (var i = 0; i < subFolderDepth; i++)
                {
                    possibleSeriesFolders = possibleSeriesFolders.SelectMany(_diskProvider.GetDirectories).ToList();
                }
            }

            var unmappedFolders = possibleSeriesFolders.Except(seriesPaths.Select(s => s.Value), PathEqualityComparer.Instance).ToList();

            // Detect collection ZIP files in the root folder as unmapped platforms
            var zipFiles = _diskProvider.GetFiles(path, false)
                .Where(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var zipFile in zipFiles)
            {
                var zipFileName = Path.GetFileName(zipFile);
                var resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(zipFileName);

                if (string.IsNullOrWhiteSpace(resolvedPlatform))
                {
                    try
                    {
                        using var archive = ZipFile.OpenRead(zipFile);
                        var innerEntry = archive.Entries.FirstOrDefault(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") && MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)));
                        if (innerEntry != null)
                        {
                            resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(innerEntry.Name, Path.GetExtension(innerEntry.Name));
                            if (string.IsNullOrWhiteSpace(resolvedPlatform) && _datCacheService != null)
                            {
                                var crc = innerEntry.Crc32.ToString("x8").ToLowerInvariant();
                                var match = _datCacheService.FindPlatformByHash(crc);
                                if (match != null && !string.IsNullOrWhiteSpace(match.PlatformSlug))
                                {
                                    resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(match.PlatformSlug);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrWhiteSpace(resolvedPlatform))
                {
                    var folderName = Path.GetFileNameWithoutExtension(zipFile);
                    var targetPlatformPath = Path.Combine(path, folderName);

                    var currentDir = new DirectoryInfo(path.Normalize());
                    var pathPlatform = PlatformCompatibilityValidator.ResolvePlatformName(currentDir.Name);
                    if (string.Equals(pathPlatform, resolvedPlatform, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(currentDir.Name, folderName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPlatformPath = currentDir.FullName;
                        folderName = currentDir.Name;
                    }

                    if (!seriesPaths.Values.Contains(targetPlatformPath, PathEqualityComparer.Instance) &&
                        !results.Any(r => r.Path.PathEquals(targetPlatformPath)))
                    {
                        results.Add(new UnmappedFolder
                        {
                            Name = resolvedPlatform,
                            Path = targetPlatformPath,
                            RelativePath = folderName
                        });
                    }
                }
            }

            foreach (var unmappedFolder in unmappedFolders)
            {
                var di = new DirectoryInfo(unmappedFolder.Normalize());
                if (PlatformCompatibilityValidator.IsRegionOrBucketFolder(di.Name))
                {
                    continue;
                }

                var resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(di.Name);

                if (string.IsNullOrWhiteSpace(resolvedPlatformName))
                {
                    var sampleFile = _diskProvider.GetFiles(unmappedFolder, true)
                        .FirstOrDefault(f => MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(f)));

                    if (sampleFile != null)
                    {
                        resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(Path.GetFileName(sampleFile), Path.GetExtension(sampleFile));
                        if (string.IsNullOrWhiteSpace(resolvedPlatformName) && Path.GetExtension(sampleFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using var archive = ZipFile.OpenRead(sampleFile);
                                var innerEntry = archive.Entries.FirstOrDefault(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") && MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)));
                                if (innerEntry != null)
                                {
                                    resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(innerEntry.Name, Path.GetExtension(innerEntry.Name));
                                    if (string.IsNullOrWhiteSpace(resolvedPlatformName) && _datCacheService != null)
                                    {
                                        var crc = innerEntry.Crc32.ToString("x8").ToLowerInvariant();
                                        var match = _datCacheService.FindPlatformByHash(crc);
                                        if (match != null && !string.IsNullOrWhiteSpace(match.PlatformSlug))
                                        {
                                            resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(match.PlatformSlug);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                results.Add(new UnmappedFolder
                {
                    Name = !string.IsNullOrWhiteSpace(resolvedPlatformName) ? resolvedPlatformName : di.Name,
                    Path = di.FullName,
                    RelativePath = path.GetRelativePath(di.FullName)
                });
            }

            var setToRemove = SpecialFolders;
            results.RemoveAll(x => setToRemove.Contains(new DirectoryInfo(x.Path.ToLowerInvariant()).Name) ||
                                   PlatformCompatibilityValidator.IsRegionOrBucketFolder(x.Name));

            if (results.Count == 0 && !seriesPaths.Values.Contains(path, PathEqualityComparer.Instance))
            {
                // Check if path itself or archives in path contain ROMs
                var romFiles = _diskProvider.GetFiles(path, true)
                    .Where(f => MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(f)))
                    .Take(50)
                    .ToList();

                if (romFiles.Count > 0)
                {
                    var di = new DirectoryInfo(path.Normalize());

                    // Try to detect platform from path name or from the ROM files
                    var resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(di.Name);
                    if (string.IsNullOrWhiteSpace(resolvedPlatformName))
                    {
                        foreach (var romFile in romFiles)
                        {
                            resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(Path.GetFileName(romFile), Path.GetExtension(romFile));
                            if (string.IsNullOrWhiteSpace(resolvedPlatformName) && Path.GetExtension(romFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    using var archive = ZipFile.OpenRead(romFile);
                                    var innerEntry = archive.Entries.FirstOrDefault(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") && MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)));
                                    if (innerEntry != null)
                                    {
                                        resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(innerEntry.Name, Path.GetExtension(innerEntry.Name));
                                        if (string.IsNullOrWhiteSpace(resolvedPlatformName) && _datCacheService != null)
                                        {
                                            var crc = innerEntry.Crc32.ToString("x8").ToLowerInvariant();
                                            var match = _datCacheService.FindPlatformByHash(crc);
                                            if (match != null && !string.IsNullOrWhiteSpace(match.PlatformSlug))
                                            {
                                                resolvedPlatformName = PlatformCompatibilityValidator.ResolvePlatformName(match.PlatformSlug);
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(resolvedPlatformName))
                            {
                                break;
                            }
                        }
                    }

                    results.Add(new UnmappedFolder
                    {
                        Name = !string.IsNullOrWhiteSpace(resolvedPlatformName) ? resolvedPlatformName : di.Name,
                        Path = di.FullName,
                        RelativePath = di.Name
                    });
                }
            }

            _logger.Debug("{0} unmapped folders detected.", results.Count);
            return results.OrderBy(u => u.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        public RootFolder Get(int id, bool timeout)
        {
            var rootFolder = _rootFolderRepository.Get(id);
            var seriesPaths = _seriesRepository.AllSeriesPaths();

            GetDetails(rootFolder, seriesPaths, timeout);

            return rootFolder;
        }

        public string GetBestRootFolderPath(string path)
        {
            return _cache.Get(path, () => GetBestRootFolderPathInternal(path), TimeSpan.FromDays(1));
        }

        private void GetDetails(RootFolder rootFolder, Dictionary<int, string> seriesPaths, bool timeout)
        {
            Task.Run(() =>
            {
                if (_diskProvider.FolderExists(rootFolder.Path))
                {
                    rootFolder.Accessible = true;
                    rootFolder.IsEmpty = _diskProvider.FolderEmpty(rootFolder.Path);
                    rootFolder.FreeSpace = _diskProvider.GetAvailableSpace(rootFolder.Path);
                    rootFolder.TotalSpace = _diskProvider.GetTotalSize(rootFolder.Path);
                    rootFolder.UnmappedFolders = GetUnmappedFolders(rootFolder.Path, seriesPaths);
                }
            }).Wait(timeout ? 5000 : -1);
        }

        private string GetBestRootFolderPathInternal(string path)
        {
            var possibleRootFolder = All().Where(r => r.Path.IsParentPath(path)).MaxBy(r => r.Path.Length);

            if (possibleRootFolder == null)
            {
                var osPath = new OsPath(path);

                return osPath.Directory.ToString().GetCleanPath();
            }

            return possibleRootFolder.Path.GetCleanPath();
        }
    }
}
