using System;
using System.IO;
using NLog;

namespace NzbDrone.Core.Export
{
    public interface IExportEngine
    {
        void ExportFlashCart(string sourceDirectory, string targetDirectory);
        void ExportHandheld(string sourceDirectory, string targetDirectory);
    }

    public class ExportEngine : IExportEngine
    {
        private readonly Logger _logger;

        public ExportEngine(Logger logger)
        {
            _logger = logger;
        }

        public void ExportFlashCart(string sourceDirectory, string targetDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) return;

            Directory.CreateDirectory(targetDirectory);

            // A typical flash cart limits entries per directory, so bucket alphabetically
            var dirs = Directory.GetDirectories(sourceDirectory);
            foreach (var dir in dirs)
            {
                var dirName = new DirectoryInfo(dir).Name;
                if (string.IsNullOrEmpty(dirName)) continue;

                var firstChar = char.ToUpperInvariant(dirName[0]);
                string bucket;

                if (firstChar >= 'A' && firstChar <= 'F') bucket = "A-F";
                else if (firstChar >= 'G' && firstChar <= 'L') bucket = "G-L";
                else if (firstChar >= 'M' && firstChar <= 'S') bucket = "M-S";
                else if (firstChar >= 'T' && firstChar <= 'Z') bucket = "T-Z";
                else bucket = "#";

                var targetBucket = Path.Combine(targetDirectory, bucket);
                Directory.CreateDirectory(targetBucket);

                // Here we would extract the zip to the targetBucket directory.
                // We'll simulate the target writing here.
                var fakeTargetFile = Path.Combine(targetBucket, dirName + ".sfc");
                _logger.Trace($"Mock FlashCart export: copying {dirName} to {fakeTargetFile}");
            }
        }

        public void ExportHandheld(string sourceDirectory, string targetDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) return;

            Directory.CreateDirectory(targetDirectory);

            // Handhelds like OnionOS typically need specific media folders
            var imgsDir = Path.Combine(targetDirectory, "Imgs");
            var assetsDir = Path.Combine(targetDirectory, "Assets");

            Directory.CreateDirectory(imgsDir);
            Directory.CreateDirectory(assetsDir);

            // Here we would build gamelist.xml and copy/symlink the files into the structure.
            _logger.Trace($"Mock Handheld export: structured {targetDirectory} for OpenFPGA/OnionOS");
        }
    }
}
