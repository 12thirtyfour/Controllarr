using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        bool ExtractChdTracksForVerification(string chdPath, string targetDirectory, out string cuePath);
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
                var errorOutput = FormatProcessOutput(compressResult);
                var canRetryViaCue = format == DiscCompressionFormat.ChdCd &&
                                     string.Equals(Path.GetExtension(sourcePath), ".iso", StringComparison.OrdinalIgnoreCase) &&
                                     File.Exists(sourcePath);

                if (!canRetryViaCue)
                {
                    _logger.Error("Disc compression ({0}) failed for {1} with exit code {2}. Output: {3}", executable, sourcePath, compressResult?.ExitCode, errorOutput);
                    return false;
                }

                _logger.Warn("Direct disc compression ({0}) failed for {1} with exit code {2}; retrying via temporary CUE sheet. Output: {3}", executable, sourcePath, compressResult?.ExitCode, errorOutput);

                if (!TryCompressRawIsoViaCue(sourcePath, targetPath))
                {
                    return false;
                }
            }

            if (format == DiscCompressionFormat.ChdCd || format == DiscCompressionFormat.ChdDvd)
            {
                var verifyResult = _processProvider.StartAndCapture("chdman", $"verify -i \"{outputPath}\"");
                if (verifyResult == null || verifyResult.ExitCode != 0)
                {
                    var verifyOutput = FormatProcessOutput(verifyResult);
                    _logger.Error("CHD verification failed for {0}. Output: {1}", outputPath, verifyOutput);
                    return false;
                }
            }

            return true;
        }

        private bool TryCompressRawIsoViaCue(string sourcePath, string targetPath)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr-iso-cue-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tempDir);

                var fileLength = new FileInfo(sourcePath).Length;
                var sectorMode = (fileLength > 0 && fileLength % 2352 == 0 && fileLength % 2048 != 0)
                    ? "MODE1/2352"
                    : "MODE1/2048";

                var tempCuePath = Path.Combine(tempDir, "disc.cue");
                var cueContent = string.Join(
                    Environment.NewLine,
                    $"FILE \"{sourcePath}\" BINARY",
                    $"  TRACK 01 {sectorMode}",
                    "    INDEX 01 00:00:00");
                File.WriteAllText(tempCuePath, cueContent);

                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Delete(targetPath);
                    }
                    catch
                    {
                    }
                }

                var retryResult = _processProvider.StartAndCapture("chdman", $"createcd -i \"{tempCuePath}\" -o \"{targetPath}\" -f");
                if (retryResult == null || retryResult.ExitCode != 0)
                {
                    var retryOutput = FormatProcessOutput(retryResult);
                    _logger.Error("Disc compression fallback (chdman via temporary CUE) failed for {0} with exit code {1}. Output: {2}", sourcePath, retryResult?.ExitCode, retryOutput);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Disc compression fallback (chdman via temporary CUE) failed for {0}", sourcePath);
                return false;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string FormatProcessOutput(ProcessOutput output)
        {
            if (output == null)
            {
                return "(no process output)";
            }

            var lines = (output.Error ?? new List<ProcessOutputLine>())
                .Concat(output.Standard ?? new List<ProcessOutputLine>())
                .Select(l => l?.Content?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (lines.Count == 0)
            {
                return "(none)";
            }

            var combined = string.Join(" | ", lines);
            return combined.Length > 500 ? string.Concat(combined.AsSpan(0, 500), "...") : combined;
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

        public bool ExtractChdTracksForVerification(string chdPath, string targetDirectory, out string cuePath)
        {
            cuePath = null;
            if (string.IsNullOrWhiteSpace(chdPath) || string.IsNullOrWhiteSpace(targetDirectory))
            {
                return false;
            }

            cuePath = Path.Combine(targetDirectory, "disc.cue");

            try
            {
                Directory.CreateDirectory(targetDirectory);

                var splitResult = _processProvider.StartAndCapture("chdman", $"extractcd -i \"{chdPath}\" -o \"{cuePath}\" -sb");
                if (splitResult != null && splitResult.ExitCode == 0 && File.Exists(cuePath))
                {
                    return true;
                }

                var fallbackResult = _processProvider.StartAndCapture("chdman", $"extractcd -i \"{chdPath}\" -o \"{cuePath}\"");
                if (fallbackResult != null && fallbackResult.ExitCode == 0 && File.Exists(cuePath))
                {
                    return true;
                }

                _logger.Debug("CHD track extraction for verification failed for {0} (split exit: {1}, fallback exit: {2})", chdPath, splitResult?.ExitCode, fallbackResult?.ExitCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "CHD track extraction for verification failed for {0}", chdPath);
                return false;
            }
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
