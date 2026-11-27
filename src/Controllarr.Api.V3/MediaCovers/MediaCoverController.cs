using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using Sonarr.Http;

namespace Sonarr.Api.V3.MediaCovers
{
    [V3ApiController]
    public class MediaCoverController : Controller
    {
        private static readonly Regex RegexResizedImage = new Regex(@"-\d+\.jpg$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IContentTypeProvider _mimeTypeProvider;

        public MediaCoverController(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _mimeTypeProvider = new FileExtensionContentTypeProvider();
        }

        [HttpGet(@"{seriesId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public IActionResult GetMediaCover(int seriesId, string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) ||
                filename.Contains("..") ||
                filename.Contains('/') ||
                filename.Contains('\\') ||
                Path.GetFileName(filename) != filename)
            {
                return NotFound();
            }

            var mediaCoverDir = Path.GetFullPath(Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", seriesId.ToString()));
            var filePath = Path.GetFullPath(Path.Combine(mediaCoverDir, filename));
            var expectedPrefix = Path.TrimEndingDirectorySeparator(mediaCoverDir) + Path.DirectorySeparatorChar;
            if (!filePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
            {
                // Return the full sized image if someone requests a non-existing resized one.
                // TODO: This code can be removed later once everyone had the update for a while.
                var basefilePath = RegexResizedImage.Replace(filePath, ".jpg");
                if (basefilePath == filePath || !_diskProvider.FileExists(basefilePath))
                {
                    return NotFound();
                }

                filePath = basefilePath;
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }

        private string GetContentType(string filePath)
        {
            if (!_mimeTypeProvider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }
    }
}
