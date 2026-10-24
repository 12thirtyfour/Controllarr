using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NLog;
using NzbDrone.Common.Processes;

namespace NzbDrone.Core.MediaFiles.Disc
{
    public enum DiscCompressionFormat
    {
        ChdCd = 0,
        ChdDvd = 1,
        Rvz = 2
    }

    public interface IDiscCompressionService
    {
        (string Executable, string Arguments, DiscCompressionFormat Format, string OutputPath) BuildCompressionCommand(string sourcePath, string platformSlug, string explicitOutputPath = null);
        bool CompressDisc(string sourcePath, string platformSlug, out string outputPath, string explicitOutputPath = null);
        (string Executable, string Arguments, string PrimaryOutputPath) BuildDecompressionCommand(string sourcePath, string targetDirectory, string targetFileNameWithoutExtension = null);
        bool DecompressDisc(string sourcePath, string targetDirectory, out string primaryOutputPath, string targetFileNameWithoutExtension = null);
        void GenerateM3uPlaylist(IEnumerable<string> discPaths, string outputM3uPath);
    }

    public class DiscCompressionService : IDiscCompressionService
    {
        private static readonly HashSet<string> RvzPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gamecube",
            "nintendo gamecube",
            "gc",
            "wii",
            "nintendo wii"
        };

        private static readonly HashSet<string> DvdPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ps2",
            "playstation 2",
            "sony playstation 2",
            "psp",
            "xbox"
        };

        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        public DiscCompressionService(IProcessProvider processProvider, Logger logger)
        {
            _processProvider = processProvider;
            _logger = logger;
        }

        public (string Executable, string Arguments, DiscCompressionFormat Format, string OutputPath) BuildCompressionCommand(string sourcePath, string platformSlug, string explicitOutputPath = null)
        {
            var normalizedPlatform = platformSlug?.Trim() ?? string.Empty;

            if (RvzPlatforms.Contains(normalizedPlatform))
            {
                var rvzOutput = !string.IsNullOrWhiteSpace(explicitOutputPath)
                    ? Path.ChangeExtension(explicitOutputPath, ".rvz")
                    : Path.ChangeExtension(sourcePath, ".rvz");
                var args = $"convert -i \"{sourcePath}\" -o \"{rvzOutput}\" -f rvz -b 131072 -c zstd -l 5";
                return ("dolphin-tool", args, DiscCompressionFormat.Rvz, rvzOutput);
            }

            var chdOutput = !string.IsNullOrWhiteSpace(explicitOutputPath)
                ? Path.ChangeExtension(explicitOutputPath, ".chd")
                : Path.ChangeExtension(sourcePath, ".chd");
            if (DvdPlatforms.Contains(normalizedPlatform))
            {
                var dvdArgs = $"createdvd -i \"{sourcePath}\" -o \"{chdOutput}\"";
                return ("chdman", dvdArgs, DiscCompressionFormat.ChdDvd, chdOutput);
            }

            var cdArgs = $"createcd -i \"{sourcePath}\" -o \"{chdOutput}\"";
            return ("chdman", cdArgs, DiscCompressionFormat.ChdCd, chdOutput);
        }

        public bool CompressDisc(string sourcePath, string platformSlug, out string outputPath, string explicitOutputPath = null)
        {
            var (executable, arguments, format, targetPath) = BuildCompressionCommand(sourcePath, platformSlug, explicitOutputPath);
            outputPath = targetPath;

            var compressResult = _processProvider.StartAndCapture(executable, arguments);
            if (compressResult == null || compressResult.ExitCode != 0)
            {
                _logger.Error("Disc compression ({0}) failed for {1} with exit code {2}", executable, sourcePath, compressResult?.ExitCode);
                return false;
            }

            if (format == DiscCompressionFormat.ChdCd || format == DiscCompressionFormat.ChdDvd)
            {
                var verifyResult = _processProvider.StartAndCapture("chdman", $"verify -i \"{outputPath}\"");
                if (verifyResult == null || verifyResult.ExitCode != 0)
                {
                    _logger.Error("CHD verification failed for {0}", outputPath);
                    return false;
                }
            }

            return true;
        }

        public (string Executable, string Arguments, string PrimaryOutputPath) BuildDecompressionCommand(
            string sourcePath,
            string targetDirectory,
            string targetFileNameWithoutExtension = null)
        {
            var baseName = !string.IsNullOrWhiteSpace(targetFileNameWithoutExtension)
                ? targetFileNameWithoutExtension.Trim()
                : Path.GetFileNameWithoutExtension(sourcePath);

            var ext = Path.GetExtension(sourcePath)?.ToLowerInvariant() ?? string.Empty;

            if (ext == ".rvz")
            {
                var isoOutput = Path.Combine(targetDirectory ?? string.Empty, baseName + ".iso");
                var rvzArgs = $"convert -i \"{sourcePath}\" -o \"{isoOutput}\" -f iso";
                return ("dolphin-tool", rvzArgs, isoOutput);
            }

            var cueOutput = Path.Combine(targetDirectory ?? string.Empty, baseName + ".cue");
            var binOutput = Path.Combine(targetDirectory ?? string.Empty, baseName + ".bin");
            var chdExtractArgs = $"extractcd -i \"{sourcePath}\" -o \"{cueOutput}\" -ob \"{binOutput}\"";
            return ("chdman", chdExtractArgs, cueOutput);
        }

        public bool DecompressDisc(
            string sourcePath,
            string targetDirectory,
            out string primaryOutputPath,
            string targetFileNameWithoutExtension = null)
        {
            var (executable, arguments, outputPath) = BuildDecompressionCommand(sourcePath, targetDirectory, targetFileNameWithoutExtension);
            primaryOutputPath = outputPath;

            var extractResult = _processProvider.StartAndCapture(executable, arguments);
            if (extractResult == null || extractResult.ExitCode != 0)
            {
                _logger.Error("Disc extraction ({0}) failed for {1} with exit code {2}", executable, sourcePath, extractResult?.ExitCode);
                return false;
            }

            return true;
        }

        public void GenerateM3uPlaylist(IEnumerable<string> discPaths, string outputM3uPath)
        {
            if (discPaths == null || string.IsNullOrWhiteSpace(outputM3uPath))
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var path in discPaths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    sb.AppendLine(Path.GetFileName(path));
                }
            }

            File.WriteAllText(outputM3uPath, sb.ToString());
        }
    }
}
