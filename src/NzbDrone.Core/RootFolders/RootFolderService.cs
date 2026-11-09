using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NLog;
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
        string ResolvePlatformFromArchive(string filePath);
        bool CheckContainsRomFiles(string path);
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

        public bool CheckContainsRomFiles(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !_diskProvider.FolderExists(path))
            {
                return false;
            }

            try
            {
                var directFiles = _diskProvider.GetFiles(path, false);
                var hasArchiveFiles = directFiles.Any(f =>
                {
                    var ext = Path.GetExtension(f);
                    return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                           ext.Equals(".7z", StringComparison.OrdinalIgnoreCase);
                });

                var subDirs = _diskProvider.GetDirectories(path)
                    .Where(d => !SpecialFolders.Contains(new DirectoryInfo(d).Name.ToLowerInvariant()))
                    .ToList();

                // Only return true when there are JUST loose ROM files directly in the root folder given
                // (no zip/7z archives and no subfolders).
                if (hasArchiveFiles || subDirs.Count > 0)
                {
                    return false;
                }

                return directFiles.Any(f =>
                {
                    var ext = Path.GetExtension(f);
                    return MediaFileExtensions.RomExtensions.Contains(ext) &&
                           !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                           !ext.Equals(".7z", StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception ex)
            {
                _logger.Trace(ex, "Error checking if path contains ROM files: {0}", path);
            }

            return false;
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
                if (_diskProvider.FileExists(path))
                {
                    var ext = Path.GetExtension(path);
                    if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                    {
                        var resolvedPlatform = ResolvePlatformFromArchive(path);
                        var folderName = Path.GetFileNameWithoutExtension(path);
                        results.Add(new UnmappedFolder
                        {
                            Name = !string.IsNullOrWhiteSpace(resolvedPlatform) ? resolvedPlatform : folderName,
                            Path = path,
                            RelativePath = Path.GetFileName(path)
                        });
                    }
                }
                else
                {
                    _logger.Debug("Path supplied does not exist: {0}", path);
                }

                return results;
            }

            // If there are just loose ROM files directly inside the root folder (no zip archives and no subfolders),
            // do not list a platform folder; let the UI present the loose ROM files themselves for per-ROM platform assignment.
            if (CheckContainsRomFiles(path))
            {
                return results;
            }

            var currentDir = new DirectoryInfo(path.Normalize());
            var currentPathPlatform = PlatformCompatibilityValidator.ResolvePlatformName(currentDir.Name);

            // Detect archives (.zip, .7z) in the root folder as importable items
            var archiveFiles = _diskProvider.GetFiles(path, false)
                .Where(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetExtension(f).Equals(".7z", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var archiveFile in archiveFiles)
            {
                var resolvedPlatform = ResolvePlatformFromArchive(archiveFile);
                var folderName = Path.GetFileNameWithoutExtension(archiveFile);
                var targetPlatformPath = Path.Combine(path, folderName);

                if (!string.IsNullOrWhiteSpace(resolvedPlatform))
                {
                    if (string.Equals(currentPathPlatform, resolvedPlatform, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(currentDir.Name, folderName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPlatformPath = currentDir.FullName;
                        folderName = currentDir.Name;
                    }

                    if (!results.Any(r => r.Path.PathEquals(targetPlatformPath)))
                    {
                        results.Add(new UnmappedFolder
                        {
                            Name = resolvedPlatform,
                            Path = targetPlatformPath,
                            RelativePath = folderName
                        });
                    }
                }
                else
                {
                    if (!results.Any(r => r.Path.PathEquals(targetPlatformPath)))
                    {
                        results.Add(new UnmappedFolder
                        {
                            Name = folderName,
                            Path = targetPlatformPath,
                            RelativePath = folderName
                        });
                    }
                }
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

            var unmappedFolders = possibleSeriesFolders.ToList();

            // Check if path itself is a recognized platform folder and has no separate platform subfolders or archives
            if (!string.IsNullOrWhiteSpace(currentPathPlatform) && archiveFiles.Count == 0)
            {
                var hasDistinctPlatformSubfolders = unmappedFolders.Any(f =>
                {
                    var di = new DirectoryInfo(f.Normalize());
                    if (PlatformCompatibilityValidator.IsRegionOrBucketFolder(di.Name) || SpecialFolders.Contains(di.Name.ToLowerInvariant()))
                    {
                        return false;
                    }

                    var subPlat = PlatformCompatibilityValidator.ResolvePlatformName(di.Name);
                    return !string.IsNullOrWhiteSpace(subPlat) && !string.Equals(subPlat, currentPathPlatform, StringComparison.OrdinalIgnoreCase);
                });

                if (!hasDistinctPlatformSubfolders)
                {
                    results.Add(new UnmappedFolder
                    {
                        Name = currentPathPlatform,
                        Path = currentDir.FullName,
                        RelativePath = currentDir.Name
                    });

                    _logger.Debug("Path itself is recognized platform: {0}", currentPathPlatform);
                    return results;
                }
            }

            var nonRegionSubfolders = unmappedFolders
                .Select(f => (Folder: f, DirInfo: new DirectoryInfo(f.Normalize())))
                .Where(x => !PlatformCompatibilityValidator.IsRegionOrBucketFolder(x.DirInfo.Name) &&
                            !SpecialFolders.Contains(x.DirInfo.Name.ToLowerInvariant()))
                .ToList();

            var detectedSubPlatforms = nonRegionSubfolders
                .Select(x => (x.Folder, x.DirInfo, Platform: PlatformCompatibilityValidator.ResolvePlatformName(x.DirInfo.Name)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Platform))
                .ToList();

            if (detectedSubPlatforms.Count > 0)
            {
                foreach (var (folder, di, plat) in nonRegionSubfolders.Select(x => (
                    x.Folder,
                    x.DirInfo,
                    Plat: PlatformCompatibilityValidator.ResolvePlatformName(x.DirInfo.Name) ?? ResolvePlatformFromDirectory(x.Folder))))
                {
                    if (!results.Any(r => r.Path.PathEquals(di.FullName)))
                    {
                        results.Add(new UnmappedFolder
                        {
                            Name = !string.IsNullOrWhiteSpace(plat) ? plat : di.Name,
                            Path = di.FullName,
                            RelativePath = path.GetRelativePath(di.FullName)
                        });
                    }
                }
            }
            else
            {
                // Subdirectories are not platform names (they might be game folders or regions).
                // Check if path itself contains ROMs recursively for a single platform.
                var platformFromPath = ResolvePlatformFromDirectory(path);
                if (!string.IsNullOrWhiteSpace(platformFromPath) &&
                    !results.Any(r => r.Path.PathEquals(currentDir.FullName)))
                {
                    results.Add(new UnmappedFolder
                    {
                        Name = platformFromPath,
                        Path = currentDir.FullName,
                        RelativePath = currentDir.Name
                    });
                }
                else
                {
                    // Fall back to evaluating each unmapped subfolder individually
                    foreach (var (folder, di) in nonRegionSubfolders)
                    {
                        var plat = ResolvePlatformFromDirectory(folder);
                        if (!results.Any(r => r.Path.PathEquals(di.FullName)))
                        {
                            results.Add(new UnmappedFolder
                            {
                                Name = !string.IsNullOrWhiteSpace(plat) ? plat : di.Name,
                                Path = di.FullName,
                                RelativePath = path.GetRelativePath(di.FullName)
                            });
                        }
                    }
                }
            }

            var setToRemove = SpecialFolders;
            results.RemoveAll(x => setToRemove.Contains(new DirectoryInfo(x.Path.ToLowerInvariant()).Name) ||
                                   PlatformCompatibilityValidator.IsRegionOrBucketFolder(x.Name));

            _logger.Debug("{0} unmapped folders detected.", results.Count);
            return results.OrderBy(u => u.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        private string ResolvePlatformFromDirectory(string dirPath)
        {
            var di = new DirectoryInfo(dirPath.Normalize());
            var resolved = PlatformCompatibilityValidator.ResolvePlatformName(di.Name);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            var sampleFiles = _diskProvider.GetFiles(dirPath, true)
                .Where(f => MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(f)))
                .Take(50)
                .ToList();

            if (sampleFiles.Count == 0)
            {
                return null;
            }

            var votes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in sampleFiles)
            {
                var plat = ResolvePlatformFromArchive(file);
                if (!string.IsNullOrWhiteSpace(plat))
                {
                    votes[plat] = votes.GetValueOrDefault(plat, 0) + 1;
                }
            }

            return votes.Count > 0 ? votes.OrderByDescending(kv => kv.Value).First().Key : null;
        }

        public string ResolvePlatformFromArchive(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var ext = Path.GetExtension(filePath);

            if (!ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
            {
                var directPlatform = PlatformCompatibilityValidator.ResolvePlatformName(fileName, ext);
                if (!string.IsNullOrWhiteSpace(directPlatform))
                {
                    return directPlatform;
                }
            }

            var resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(fileName);
            if (!string.IsNullOrWhiteSpace(resolvedPlatform))
            {
                return resolvedPlatform;
            }

            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(filePath);

                    var nativeEntry = archive.Entries.FirstOrDefault(e =>
                        !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                        MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)) &&
                        !Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                        !Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                    if (nativeEntry != null)
                    {
                        var entryExt = Path.GetExtension(nativeEntry.Name);
                        resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(nativeEntry.Name, entryExt);

                        if (string.IsNullOrWhiteSpace(resolvedPlatform) && _datCacheService != null)
                        {
                            var crc = nativeEntry.Crc32.ToString("x8").ToLowerInvariant();
                            var match = _datCacheService.FindPlatformByHash(crc);
                            if (match != null && !string.IsNullOrWhiteSpace(match.PlatformSlug))
                            {
                                resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(match.PlatformSlug);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(resolvedPlatform))
                        {
                            return resolvedPlatform;
                        }
                    }

                    var nestedZipEntry = archive.Entries.FirstOrDefault(e =>
                        !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                        Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase));

                    if (nestedZipEntry != null)
                    {
                        var nestedNamePlatform = PlatformCompatibilityValidator.ResolvePlatformName(nestedZipEntry.Name);
                        if (!string.IsNullOrWhiteSpace(nestedNamePlatform))
                        {
                            return nestedNamePlatform;
                        }

                        using var nestedStream = nestedZipEntry.Open();
                        using var innerArchive = new ZipArchive(nestedStream, ZipArchiveMode.Read);
                        var innerRomEntry = innerArchive.Entries.FirstOrDefault(e =>
                            !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                            MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)) &&
                            !Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                        if (innerRomEntry != null)
                        {
                            var innerExt = Path.GetExtension(innerRomEntry.Name);
                            resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(innerRomEntry.Name, innerExt);

                            if (string.IsNullOrWhiteSpace(resolvedPlatform) && _datCacheService != null)
                            {
                                var innerCrc = innerRomEntry.Crc32.ToString("x8").ToLowerInvariant();
                                var match = _datCacheService.FindPlatformByHash(innerCrc);
                                if (match != null && !string.IsNullOrWhiteSpace(match.PlatformSlug))
                                {
                                    resolvedPlatform = PlatformCompatibilityValidator.ResolvePlatformName(match.PlatformSlug);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(resolvedPlatform))
                            {
                                return resolvedPlatform;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex, "Could not inspect archive {0} for platform detection", filePath);
                }
            }

            return null;
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
                    rootFolder.ContainsRomFiles = CheckContainsRomFiles(rootFolder.Path);
                    rootFolder.UnmappedFolders = GetUnmappedFolders(rootFolder.Path, seriesPaths);
                }
                else if (_diskProvider.FileExists(rootFolder.Path))
                {
                    rootFolder.Accessible = true;
                    rootFolder.IsEmpty = false;
                    rootFolder.FreeSpace = _diskProvider.GetAvailableSpace(Path.GetDirectoryName(rootFolder.Path));
                    rootFolder.TotalSpace = _diskProvider.GetTotalSize(Path.GetDirectoryName(rootFolder.Path));
                    rootFolder.ContainsRomFiles = false;
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
