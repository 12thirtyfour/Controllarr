using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Patching
{
    public enum RomPatchFormat
    {
        Unknown = 0,
        Ips = 1,
        Bps = 2,
        Xdelta = 3
    }

    public class RomPatchRequestModel
    {
        public int? SeriesId { get; set; }
        public int? BaseEpisodeFileId { get; set; }
        public string BaseRomPath { get; set; }
        public string PatchFilePath { get; set; }
        public string PatchFileName { get; set; }
        public string PatchBase64Content { get; set; }
        public string OutputTitle { get; set; }
    }

    public class RomPatchResultModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Format { get; set; }
        public string BaseRomPath { get; set; }
        public string OutputRomPath { get; set; }
        public string OutputCrc32 { get; set; }
        public string OutputSha1 { get; set; }
        public string OutputSha256 { get; set; }
        public bool VerifiedInCatalog { get; set; }
        public string MatchedCatalogName { get; set; }
        public int? EpisodeId { get; set; }
        public int? EpisodeFileId { get; set; }
    }

    public interface IRomPatchingService
    {
        bool IsPatchFile(string filePathOrName);
        RomPatchFormat DetectFormat(byte[] patchBytes, string fileName = null);
        byte[] ApplyPatch(byte[] sourceBytes, byte[] patchBytes, RomPatchFormat format = RomPatchFormat.Unknown);
        uint? TryReadBpsSourceCrc32(byte[] patchBytes);
        uint? TryReadBpsTargetCrc32(byte[] patchBytes);
        RomPatchResultModel ApplyPatchToLibrary(RomPatchRequestModel request);
        RomPatchResultModel TryApplyPatchFileForSeries(string patchOrArchivePath, Series series);
    }
}
