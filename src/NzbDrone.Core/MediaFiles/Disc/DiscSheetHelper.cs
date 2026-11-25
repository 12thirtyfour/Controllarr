using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.MediaFiles.Disc
{
    public static class DiscSheetHelper
    {
        private static readonly HashSet<string> DiscSheetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cue", ".gdi", ".ccd"
        };

        private static readonly HashSet<string> CompanionTrackExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bin", ".img", ".iso", ".raw", ".wav", ".sub", ".mdf", ".mds"
        };

        private static readonly HashSet<string> FallbackSingleTrackExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bin", ".img", ".iso"
        };

        public static bool IsDiscSheetExtension(string extension)
        {
            return !string.IsNullOrWhiteSpace(extension) && DiscSheetExtensions.Contains(extension);
        }

        public static bool IsCompanionTrackExtension(string extension)
        {
            return !string.IsNullOrWhiteSpace(extension) && CompanionTrackExtensions.Contains(extension);
        }

        public static List<string> ParseTrackReferences(string sheetPath)
        {
            var references = new List<string>();
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return references;
            }

            var ext = Path.GetExtension(sheetPath)?.ToLowerInvariant() ?? string.Empty;
            if (ext == ".ccd")
            {
                var baseName = Path.GetFileNameWithoutExtension(sheetPath);
                if (!string.IsNullOrWhiteSpace(baseName))
                {
                    references.Add(baseName + ".img");
                    references.Add(baseName + ".sub");
                }

                return references;
            }

            if (ext != ".cue" && ext != ".gdi")
            {
                return references;
            }

            try
            {
                foreach (var line in File.ReadLines(sheetPath))
                {
                    var trackRef = ext == ".cue"
                        ? TryParseCueFileReference(line)
                        : TryParseGdiFileReference(line);

                    if (!string.IsNullOrWhiteSpace(trackRef))
                    {
                        references.Add(trackRef);
                    }
                }
            }
            catch
            {
            }

            return references;
        }

        public static List<string> ResolveTrackFiles(string sheetPath)
        {
            var resolved = new List<string>();
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return resolved;
            }

            var sheetDir = Path.GetDirectoryName(sheetPath);
            if (string.IsNullOrWhiteSpace(sheetDir) || !Directory.Exists(sheetDir))
            {
                return resolved;
            }

            var ext = Path.GetExtension(sheetPath)?.ToLowerInvariant() ?? string.Empty;
            var references = ParseTrackReferences(sheetPath);
            if (references.Count == 0)
            {
                return resolved;
            }

            string[] dirFiles = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var reference in references)
            {
                var trackPath = ResolveSingleTrackReference(sheetDir, reference, references.Count, ext == ".cue", ref dirFiles);
                if (!string.IsNullOrWhiteSpace(trackPath) && seen.Add(trackPath))
                {
                    resolved.Add(trackPath);
                }
            }

            return resolved;
        }

        public static List<string> ResolveTrackFilesPrioritizingData(string sheetPath)
        {
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return new List<string>();
            }

            var ext = Path.GetExtension(sheetPath)?.ToLowerInvariant() ?? string.Empty;
            if (ext != ".cue")
            {
                return ResolveTrackFiles(sheetPath);
            }

            var sheetDir = Path.GetDirectoryName(sheetPath);
            if (string.IsNullOrWhiteSpace(sheetDir) || !Directory.Exists(sheetDir))
            {
                return new List<string>();
            }

            var cueBlocks = new List<(string Reference, bool IsDataTrack)>();
            try
            {
                foreach (var line in File.ReadLines(sheetPath))
                {
                    var fileRef = TryParseCueFileReference(line);
                    if (!string.IsNullOrWhiteSpace(fileRef))
                    {
                        cueBlocks.Add((fileRef, false));
                        continue;
                    }

                    if (cueBlocks.Count > 0 && IsCueDataTrackLine(line))
                    {
                        var lastIndex = cueBlocks.Count - 1;
                        cueBlocks[lastIndex] = (cueBlocks[lastIndex].Reference, true);
                    }
                }
            }
            catch
            {
            }

            if (cueBlocks.Count == 0)
            {
                return ResolveTrackFiles(sheetPath);
            }

            string[] dirFiles = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dataTracks = new List<string>();
            var otherTracks = new List<string>();

            foreach (var (reference, isDataTrack) in cueBlocks)
            {
                var trackPath = ResolveSingleTrackReference(sheetDir, reference, cueBlocks.Count, true, ref dirFiles);
                if (!string.IsNullOrWhiteSpace(trackPath) && seen.Add(trackPath))
                {
                    if (isDataTrack)
                    {
                        dataTracks.Add(trackPath);
                    }
                    else
                    {
                        otherTracks.Add(trackPath);
                    }
                }
            }

            return dataTracks.Concat(otherTracks).ToList();
        }

        private static bool IsCueDataTrackLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && string.Equals(parts[0], "TRACK", StringComparison.OrdinalIgnoreCase))
            {
                var mode = parts[2];
                return mode.StartsWith("MODE1", StringComparison.OrdinalIgnoreCase) ||
                       mode.StartsWith("MODE2", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static long GetEffectiveFileSize(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return 0;
            }

            try
            {
                var total = new FileInfo(filePath).Length;
                if (IsDiscSheetExtension(Path.GetExtension(filePath)))
                {
                    foreach (var track in ResolveTrackFiles(filePath))
                    {
                        if (File.Exists(track))
                        {
                            total += new FileInfo(track).Length;
                        }
                    }
                }

                return total;
            }
            catch
            {
                return 0;
            }
        }

        public static string BuildRenamedTrackFileName(string destBaseName, string sourceTrackPath, int trackIndex, int totalTracks, string sheetExtension = ".cue")
        {
            var trackExt = Path.GetExtension(sourceTrackPath) ?? string.Empty;
            if ((sheetExtension != null && sheetExtension.Equals(".ccd", StringComparison.OrdinalIgnoreCase)) ||
                totalTracks <= 1)
            {
                return destBaseName + trackExt;
            }

            var pad = totalTracks >= 10 ? "D2" : "D1";
            return $"{destBaseName} (Track {trackIndex.ToString(pad)}){trackExt}";
        }

        public static bool NeedsTrackReferenceRewrite(string sheetPath)
        {
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return false;
            }

            var ext = Path.GetExtension(sheetPath)?.ToLowerInvariant() ?? string.Empty;
            if (ext != ".cue" && ext != ".gdi")
            {
                return false;
            }

            var sheetDir = Path.GetDirectoryName(sheetPath);
            if (string.IsNullOrWhiteSpace(sheetDir) || !Directory.Exists(sheetDir))
            {
                return false;
            }

            var references = ParseTrackReferences(sheetPath);
            if (references.Count == 0)
            {
                return false;
            }

            string[] dirFiles = null;
            foreach (var reference in references)
            {
                if (reference.Contains('/') || reference.Contains('\\'))
                {
                    return true;
                }

                var resolved = ResolveSingleTrackReference(sheetDir, reference, references.Count, ext == ".cue", ref dirFiles);
                if (!string.IsNullOrWhiteSpace(resolved) &&
                    !string.Equals(Path.GetFileName(resolved), reference, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static string RewriteSheetContent(string sheetPath, IReadOnlyDictionary<string, string> resolvedSourcePathToNewFileName)
        {
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return string.Empty;
            }

            var rawContent = File.ReadAllText(sheetPath);
            if (resolvedSourcePathToNewFileName == null || resolvedSourcePathToNewFileName.Count == 0)
            {
                return rawContent;
            }

            var ext = Path.GetExtension(sheetPath)?.ToLowerInvariant() ?? string.Empty;
            if (ext != ".cue" && ext != ".gdi")
            {
                return rawContent;
            }

            var sheetDir = Path.GetDirectoryName(sheetPath);
            if (string.IsNullOrWhiteSpace(sheetDir))
            {
                return rawContent;
            }

            var newline = rawContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var hadTrailingNewline = rawContent.EndsWith("\n", StringComparison.Ordinal);
            var lines = rawContent.Replace("\r\n", "\n").Split('\n');
            if (hadTrailingNewline && lines.Length > 0 && lines[lines.Length - 1].Length == 0)
            {
                lines = lines.Take(lines.Length - 1).ToArray();
            }

            var references = ParseTrackReferences(sheetPath);
            string[] dirFiles = null;

            var normalizedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in resolvedSourcePathToNewFileName)
            {
                normalizedMap[Path.GetFullPath(kvp.Key)] = kvp.Value;
            }

            var outputLines = new List<string>(lines.Length);
            foreach (var line in lines)
            {
                var updatedLine = ext == ".cue"
                    ? RewriteCueLine(line, sheetDir, references.Count, normalizedMap, ref dirFiles)
                    : RewriteGdiLine(line, sheetDir, references.Count, normalizedMap, ref dirFiles);
                outputLines.Add(updatedLine);
            }

            var result = string.Join(newline, outputLines);
            if (hadTrailingNewline)
            {
                result += newline;
            }

            return result;
        }

        public static bool RepairSheetInPlaceIfNeeded(string sheetPath)
        {
            if (!NeedsTrackReferenceRewrite(sheetPath))
            {
                return false;
            }

            var resolvedTracks = ResolveTrackFiles(sheetPath);
            if (resolvedTracks.Count == 0)
            {
                return false;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var track in resolvedTracks)
            {
                map[track] = Path.GetFileName(track);
            }

            var rewritten = RewriteSheetContent(sheetPath, map);
            if (!string.IsNullOrEmpty(rewritten))
            {
                File.WriteAllText(sheetPath, rewritten, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }

            return false;
        }

        private static string TryParseCueFileReference(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var trimmed = line.Trim();
            if (!trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Length <= 4 ||
                !char.IsWhiteSpace(trimmed[4]))
            {
                return null;
            }

            var rest = trimmed.Substring(4).TrimStart();
            if (rest.StartsWith("\"", StringComparison.Ordinal))
            {
                var endQuote = rest.IndexOf('"', 1);
                if (endQuote > 1)
                {
                    return rest.Substring(1, endQuote - 1);
                }
            }
            else
            {
                var nextSpace = rest.IndexOfAny(new[] { ' ', '\t' });
                return nextSpace > 0 ? rest.Substring(0, nextSpace) : rest;
            }

            return null;
        }

        private static string TryParseGdiFileReference(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var trimmed = line.Trim();
            var firstQuote = trimmed.IndexOf('"');
            if (firstQuote >= 0)
            {
                var secondQuote = trimmed.IndexOf('"', firstQuote + 1);
                if (secondQuote > firstQuote + 1)
                {
                    return trimmed.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                }
            }

            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5 && int.TryParse(parts[0], out _))
            {
                return parts[4];
            }

            return null;
        }

        private static string ResolveSingleTrackReference(
            string sheetDir,
            string reference,
            int totalReferenceCount,
            bool isCue,
            ref string[] dirFiles)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var normalizedRef = reference.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var directPath = Path.Combine(sheetDir, normalizedRef);
            if (File.Exists(directPath))
            {
                return Path.GetFullPath(directPath);
            }

            var bareName = Path.GetFileName(reference.Replace('\\', '/'));
            if (!string.IsNullOrWhiteSpace(bareName))
            {
                var barePath = Path.Combine(sheetDir, bareName);
                if (File.Exists(barePath))
                {
                    return Path.GetFullPath(barePath);
                }

                dirFiles ??= Directory.GetFiles(sheetDir);
                var caseInsensitiveMatch = dirFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), bareName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(caseInsensitiveMatch))
                {
                    return Path.GetFullPath(caseInsensitiveMatch);
                }
            }

            if (isCue && totalReferenceCount == 1)
            {
                dirFiles ??= Directory.GetFiles(sheetDir);
                var candidates = dirFiles.Where(f =>
                    FallbackSingleTrackExtensions.Contains(Path.GetExtension(f) ?? string.Empty)).ToList();
                if (candidates.Count == 1)
                {
                    return Path.GetFullPath(candidates[0]);
                }
            }

            return null;
        }

        private static string RewriteCueLine(
            string line,
            string sheetDir,
            int totalReferenceCount,
            Dictionary<string, string> normalizedMap,
            ref string[] dirFiles)
        {
            var reference = TryParseCueFileReference(line);
            if (string.IsNullOrWhiteSpace(reference))
            {
                return line;
            }

            var resolved = ResolveSingleTrackReference(sheetDir, reference, totalReferenceCount, true, ref dirFiles);
            if (string.IsNullOrWhiteSpace(resolved) || !normalizedMap.TryGetValue(Path.GetFullPath(resolved), out var newFileName))
            {
                return line;
            }

            var fileIdx = line.IndexOf("FILE", StringComparison.OrdinalIgnoreCase);
            if (fileIdx < 0)
            {
                return line;
            }

            var prefix = line.Substring(0, fileIdx + 4);
            var afterFile = line.Substring(fileIdx + 4);
            var trimmedAfter = afterFile.TrimStart();
            var whitespaceLen = afterFile.Length - trimmedAfter.Length;
            var spacer = whitespaceLen > 0 ? afterFile.Substring(0, whitespaceLen) : " ";

            string remainder;
            if (trimmedAfter.StartsWith("\"", StringComparison.Ordinal))
            {
                var endQuote = trimmedAfter.IndexOf('"', 1);
                if (endQuote <= 1)
                {
                    return line;
                }

                remainder = trimmedAfter.Substring(endQuote + 1);
            }
            else
            {
                var nextSpace = trimmedAfter.IndexOfAny(new[] { ' ', '\t' });
                remainder = nextSpace > 0 ? trimmedAfter.Substring(nextSpace) : string.Empty;
            }

            return $"{prefix}{spacer}\"{newFileName}\"{remainder}";
        }

        private static string RewriteGdiLine(
            string line,
            string sheetDir,
            int totalReferenceCount,
            Dictionary<string, string> normalizedMap,
            ref string[] dirFiles)
        {
            var reference = TryParseGdiFileReference(line);
            if (string.IsNullOrWhiteSpace(reference))
            {
                return line;
            }

            var resolved = ResolveSingleTrackReference(sheetDir, reference, totalReferenceCount, false, ref dirFiles);
            if (string.IsNullOrWhiteSpace(resolved) || !normalizedMap.TryGetValue(Path.GetFullPath(resolved), out var newFileName))
            {
                return line;
            }

            var firstQuote = line.IndexOf('"');
            if (firstQuote >= 0)
            {
                var secondQuote = line.IndexOf('"', firstQuote + 1);
                if (secondQuote > firstQuote + 1)
                {
                    return string.Concat(line.AsSpan(0, firstQuote + 1), newFileName, line.AsSpan(secondQuote));
                }
            }

            var formattedName = newFileName.Contains(' ', StringComparison.Ordinal) ? $"\"{newFileName}\"" : newFileName;
            var idx = line.IndexOf(reference, StringComparison.Ordinal);
            if (idx >= 0)
            {
                return string.Concat(line.AsSpan(0, idx), formattedName, line.AsSpan(idx + reference.Length));
            }

            return line;
        }
    }
}
