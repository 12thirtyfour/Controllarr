using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace NzbDrone.Core.MediaFiles.Disc
{
    public interface IChdmanService
    {
        bool ConvertDiscToChd(string cueFilePath, out string outputChdPath);
        void GenerateM3uPlaylist(string[] chdPaths, string outputM3uPath);
    }

    public class ChdmanService : IChdmanService
    {
        private readonly Logger _logger;
        private static readonly Regex ProgressRegex = new Regex(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled);

        public ChdmanService(Logger logger)
        {
            _logger = logger;
        }

        public bool ConvertDiscToChd(string cueFilePath, out string outputChdPath)
        {
            outputChdPath = Path.ChangeExtension(cueFilePath, ".chd");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chdman",
                    Arguments = $"createcd -i \"{cueFilePath}\" -o \"{outputChdPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += ParseChdmanProgress;

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.Error($"Chdman failed with exit code {process.ExitCode}");
                return false;
            }

            // Verification phase
            var verifyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chdman",
                    Arguments = $"verify -i \"{outputChdPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            verifyProcess.Start();
            verifyProcess.WaitForExit();

            if (verifyProcess.ExitCode != 0)
            {
                _logger.Error("CHD Verification failed after conversion.");
                return false;
            }

            return true;
        }

        private void ParseChdmanProgress(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                var match = ProgressRegex.Match(e.Data);
                if (match.Success)
                {
                    _logger.Trace($"CHDMAN Progress: {match.Groups[1].Value}%");
                }
            }
        }

        public void GenerateM3uPlaylist(string[] chdPaths, string outputM3uPath)
        {
            var sb = new StringBuilder();
            foreach (var path in chdPaths)
            {
                sb.AppendLine(Path.GetFileName(path));
            }

            File.WriteAllText(outputM3uPath, sb.ToString());
        }
    }
}
