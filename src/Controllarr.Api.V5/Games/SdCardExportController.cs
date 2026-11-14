using Microsoft.AspNetCore.Mvc;
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
    }

    [V3ApiController("sdcardexport")]
    [V5ApiController("sdcardexport")]
    public class SdCardExportController : Controller
    {
        private readonly ISdCardExporterService _sdCardExporterService;
        private readonly ISeriesService? _seriesService;
        private readonly IMediaFileService? _mediaFileService;
        private readonly IEpisodeService? _episodeService;

        public SdCardExportController(
            ISdCardExporterService sdCardExporterService,
            ISeriesService? seriesService = null,
            IMediaFileService? mediaFileService = null,
            IEpisodeService? episodeService = null)
        {
            _sdCardExporterService = sdCardExporterService;
            _seriesService = seriesService;
            _mediaFileService = mediaFileService;
            _episodeService = episodeService;
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
                    var files = _mediaFileService.GetFilesBySeries(platform.Id);
                    return new SdCardLibraryPlatformResource
                    {
                        Id = platform.Id,
                        Title = platform.Title,
                        TitleSlug = platform.TitleSlug,
                        FileCount = files?.Count ?? 0
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

                foreach (var file in files)
                {
                    var sourcePath = !string.IsNullOrWhiteSpace(platform.Path)
                        ? Path.Combine(platform.Path, file.RelativePath)
                        : file.RelativePath;

                    var gameTitle = gamesByFileId.TryGetValue(file.Id, out var title)
                        ? title
                        : Path.GetFileNameWithoutExtension(file.RelativePath);

                    items.Add(new SdCardExportItem
                    {
                        SourceFilePath = sourcePath,
                        PlatformFolder = platform.TitleSlug ?? platform.Title,
                        GameTitle = gameTitle
                    });
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
