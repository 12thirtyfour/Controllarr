using System;
using System.Collections.Generic;
using System.IO;
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
        (string Executable, string Arguments, DiscCompressionFormat Format, string OutputPath) BuildCompressionCommand(string sourcePath, string platformSlug);
        bool CompressDisc(string sourcePath, string platformSlug, out string outputPath);
    }

    public class DiscCompressionService : IDiscCompressionService
    {
        private static readonly HashSet<string> RvzPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gamecube",
            "gc",
            "wii"
        };

        private static readonly HashSet<string> DvdPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ps2",
            "psp"
        };

        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        public DiscCompressionService(IProcessProvider processProvider, Logger logger)
        {
            _processProvider = processProvider;
            _logger = logger;
        }

        public (string Executable, string Arguments, DiscCompressionFormat Format, string OutputPath) BuildCompressionCommand(string sourcePath, string platformSlug)
        {
            var normalizedPlatform = platformSlug?.Trim() ?? string.Empty;

            if (RvzPlatforms.Contains(normalizedPlatform))
            {
                var rvzOutput = Path.ChangeExtension(sourcePath, ".rvz");
                var args = $"convert -i \"{sourcePath}\" -o \"{rvzOutput}\" -f rvz -b 131072 -c zstd -l 5";
                return ("dolphin-tool", args, DiscCompressionFormat.Rvz, rvzOutput);
            }

            var chdOutput = Path.ChangeExtension(sourcePath, ".chd");
            if (DvdPlatforms.Contains(normalizedPlatform))
            {
                var dvdArgs = $"createdvd -i \"{sourcePath}\" -o \"{chdOutput}\"";
                return ("chdman", dvdArgs, DiscCompressionFormat.ChdDvd, chdOutput);
            }

            var cdArgs = $"createcd -i \"{sourcePath}\" -o \"{chdOutput}\"";
            return ("chdman", cdArgs, DiscCompressionFormat.ChdCd, chdOutput);
        }

        public bool CompressDisc(string sourcePath, string platformSlug, out string outputPath)
        {
            var (executable, arguments, format, targetPath) = BuildCompressionCommand(sourcePath, platformSlug);
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
    }
}
