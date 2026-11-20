using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Bios;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Export;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.Games
{
    public class SdCardExportRequestResource
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string TargetMountPath { get; set; } = string.Empty;
        public bool Overwrite { get; set; }
        public bool OptimizeFormatsForDevice { get; set; }
        public bool IncludeBios { get; set; }
        public List<SdCardExportItem> Items { get; set; } = new List<SdCardExportItem>();
    }

    public class SdCardLibraryPlatformResource
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleSlug { get; set; } = string.Empty;
        public int FileCount { get; set; }
    }

    public class SdCardLibraryProfileResource
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool SupportsZipPerRom { get; set; }
        public bool SupportsChdForOpticalDiscs { get; set; }
        public bool SupportsRvzForGameCubeWii { get; set; }
        public List<SdCardLibraryPlatformResource> Platforms { get; set; } = new List<SdCardLibraryPlatformResource>();
    }

    public class SdCardLibraryExportRequestResource
    {
        public SdCardDeviceProfile Profile { get; set; }
        public List<int> PlatformIds { get; set; } = new List<int>();
        public string TargetMountPath { get; set; } = string.Empty;
        public bool Overwrite { get; set; }
        public bool UseCartridgeCompression { get; set; }
        public bool UseDiscCompression { get; set; }
        public bool IncludeBios { get; set; }
    }

    [V3ApiController("sdcardexport")]
    [V5ApiController("sdcardexport")]
    public class SdCardExportController : Controller
    {
        private readonly ISdCardExporterService _sdCardExporterService;
        private readonly ISeriesService? _seriesService;
        private readonly IMediaFileService? _mediaFileService;
        private readonly IEpisodeService? _episodeService;
        private readonly IBiosService? _biosService;

        public SdCardExportController(
            ISdCardExporterService sdCardExporterService,
            ISeriesService? seriesService = null,
            IMediaFileService? mediaFileService = null,
            IEpisodeService? episodeService = null,
            IBiosService? biosService = null)
        {
            _sdCardExporterService = sdCardExporterService;
            _seriesService = seriesService;
            _mediaFileService = mediaFileService;
            _episodeService = episodeService;
            _biosService = biosService;
        }

        [HttpGet("profiles")]
        [Produces("application/json")]
        public ActionResult<IReadOnlyList<SdCardProfileDescriptor>> GetProfiles()
        {
            return Ok(_sdCardExporterService.GetSupportedProfiles());
        }

        [HttpGet("library-profiles")]
        [Produces("application/json")]
        public ActionResult<IReadOnlyList<SdCardLibraryProfileResource>> GetLibraryProfiles()
        {
            if (_seriesService == null || _mediaFileService == null)
            {
                return Ok(Array.Empty<SdCardLibraryProfileResource>());
            }

            var populatedPlatforms = _seriesService.GetAllSeries()
                .Select(platform =>
                {
                    var files = (_mediaFileService.GetFilesBySeries(platform.Id) ?? new List<EpisodeFile>())
                        .Where(f => f.SeasonNumber != NzbDrone.Core.MetadataSource.Game.GameCatalogProxy.RegionBios &&
                                    !NzbDrone.Core.Games.RegionHelper.IsBios(f.RelativePath))
                        .ToList();
                    return new SdCardLibraryPlatformResource
                    {
                        Id = platform.Id,
                        Title = platform.Title,
                        TitleSlug = platform.TitleSlug,
                        FileCount = files.Count
                    };
                })
                .Where(p => p.FileCount > 0)
                .OrderBy(p => p.Title)
                .ToList();

            var result = new List<SdCardLibraryProfileResource>();
            foreach (var profile in _sdCardExporterService.GetSupportedProfiles())
            {
                var matchingPlatforms = populatedPlatforms
                    .Where(p => _sdCardExporterService.IsPlatformSupportedByProfile(profile.Profile, p.TitleSlug, p.Title))
                    .ToList();

                if (matchingPlatforms.Count == 0)
                {
                    continue;
                }

                result.Add(new SdCardLibraryProfileResource
                {
                    Profile = profile.Profile,
                    Name = profile.Name,
                    Category = profile.Category,
                    SupportsZipPerRom = profile.SupportsZipPerRom,
                    SupportsChdForOpticalDiscs = profile.SupportsChdForOpticalDiscs,
                    SupportsRvzForGameCubeWii = profile.SupportsRvzForGameCubeWii,
                    Platforms = matchingPlatforms
                });
            }

            return Ok(result);
        }

        [HttpPost("library")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<SdCardExportResult> ExportFromLibrary([FromBody] SdCardLibraryExportRequestResource? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetMountPath))
            {
                return BadRequest("TargetMountPath is required.");
            }

            if (_seriesService == null || _mediaFileService == null)
            {
                return BadRequest("Library services are unavailable.");
            }

            var items = new List<SdCardExportItem>();
            foreach (var platformId in request.PlatformIds.Distinct())
            {
                var platform = _seriesService.GetSeries(platformId);
                if (platform == null || !_sdCardExporterService.IsPlatformSupportedByProfile(request.Profile, platform.TitleSlug, platform.Title))
                {
                    continue;
                }

                var files = _mediaFileService.GetFilesBySeries(platformId);
                var gamesByFileId = _episodeService != null
                    ? _episodeService.GetEpisodeBySeries(platformId)
                        .Where(g => g.EpisodeFileId > 0)
                        .GroupBy(g => g.EpisodeFileId)
                        .ToDictionary(g => g.Key, g => g.First().Title)
                    : new Dictionary<int, string>();

                var addedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    var gameTitle = gamesByFileId.TryGetValue(file.Id, out var title)
                        ? title
                        : Path.GetFileNameWithoutExtension(file.RelativePath);

                    var isBios = file.SeasonNumber == NzbDrone.Core.MetadataSource.Game.GameCatalogProxy.RegionBios ||
                                 NzbDrone.Core.Games.RegionHelper.IsBios(file.RelativePath, gameTitle);

                    if (isBios && !request.IncludeBios)
                    {
                        continue;
                    }

                    var sourcePath = !string.IsNullOrWhiteSpace(platform.Path)
                        ? Path.Combine(platform.Path, file.RelativePath)
                        : file.RelativePath;

                    if (isBios)
                    {
                        var canonicalBiosName = Path.GetFileName(sourcePath);
                        if (_biosService != null && _biosService.TryGetBiosFile(canonicalBiosName, out var matchedBios, platform.TitleSlug) && !string.IsNullOrWhiteSpace(matchedBios.FileName))
                        {
                            canonicalBiosName = matchedBios.FileName;
                        }

                        addedSourcePaths.Add(sourcePath);
                        items.Add(new SdCardExportItem
                        {
                            SourceFilePath = sourcePath,
                            PlatformFolder = platform.TitleSlug ?? platform.Title,
                            GameTitle = gameTitle,
                            IsBios = true,
                            CanonicalBiosFileName = canonicalBiosName
                        });
                        continue;
                    }

                    addedSourcePaths.Add(sourcePath);
                    items.Add(new SdCardExportItem
                    {
                        SourceFilePath = sourcePath,
                        PlatformFolder = platform.TitleSlug ?? platform.Title,
                        RegionFolder = NzbDrone.Core.MetadataSource.Game.GameCatalogProxy.GetRegionDisplayName(file.SeasonNumber),
                        GameTitle = gameTitle
                    });
                }

                if (request.IncludeBios && _biosService != null)
                {
                    var catalogBios = _biosService.GetCatalog(platform.TitleSlug);
                    foreach (var bios in catalogBios)
                    {
                        if (!bios.IsVerified ||
                            string.IsNullOrWhiteSpace(bios.ResolvedPath) ||
                            addedSourcePaths.Contains(bios.ResolvedPath))
                        {
                            continue;
                        }

                        addedSourcePaths.Add(bios.ResolvedPath);
                        items.Add(new SdCardExportItem
                        {
                            SourceFilePath = bios.ResolvedPath,
                            PlatformFolder = platform.TitleSlug ?? platform.Title,
                            GameTitle = bios.FileName,
                            IsBios = true,
                            CanonicalBiosFileName = bios.FileName
                        });
                    }
                }
            }

            var result = _sdCardExporterService.ExportToSdCard(
                request.Profile,
                request.TargetMountPath,
                items,
                request.Overwrite,
                request.UseCartridgeCompression,
                request.UseDiscCompression);

            return Ok(result);
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<SdCardExportResult> Export([FromBody] SdCardExportRequestResource? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetMountPath))
            {
                return BadRequest("TargetMountPath is required.");
            }

            if (request.IncludeBios && request.Items != null)
            {
                foreach (var item in request.Items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.SourceFilePath))
                    {
                        continue;
                    }

                    if (item.IsBios || NzbDrone.Core.Games.RegionHelper.IsBios(item.SourceFilePath, item.GameTitle))
                    {
                        item.IsBios = true;
                        if (string.IsNullOrWhiteSpace(item.CanonicalBiosFileName))
                        {
                            var fileName = Path.GetFileName(item.SourceFilePath);
                            item.CanonicalBiosFileName = _biosService != null &&
                                                         _biosService.TryGetBiosFile(fileName, out var matchedBios, item.PlatformFolder) &&
                                                         !string.IsNullOrWhiteSpace(matchedBios.FileName)
                                ? matchedBios.FileName
                                : fileName;
                        }
                    }
                }
            }

            var result = _sdCardExporterService.ExportToSdCard(
                request.Profile,
                request.TargetMountPath,
                request.Items,
                request.Overwrite,
                request.OptimizeFormatsForDevice);

            return Ok(result);
        }
    }
}
