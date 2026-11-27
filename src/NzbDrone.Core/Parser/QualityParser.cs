using System;
using System.IO;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    public class QualityParser
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(QualityParser));

        private static readonly Regex ProperRegex = new(@"\b(?<proper>proper)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RepackRegex = new(@"\b(?<repack>repack\d?|rerip\d?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VersionRegex = new(
            @"\d[-._ ]?v(?<version>\d)[-._ ]|\[v(?<version>\d)\]|(?:\b|\s)v(?<version>\d)(?:\b|\s|$)|repack(?<version>\d)|rerip(?<version>\d)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RealRegex = new(@"\b(?<real>REAL)\b",
            RegexOptions.Compiled);

        private static readonly Regex RedumpRegex = new(
            @"\bredump\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex NoIntroRegex = new(
            @"\bno[-_.\s]?intro\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VerifiedGoodDumpRegex = new(
            @"\[\s*!\s*p?\s*\]|\b(?:verified|gooddump)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BetaProtoRegex = new(
            @"[\(\[]\s*(?:Beta|Proto(?:types?)?|Demo|Sample|Shareware|Kiosk)(?:\s*\d+)?\s*[\)\]]|\b(?:Beta|Proto(?:types?)?|Demo|Sample|Shareware|Kiosk)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TranslationPatchRegex = new(
            @"\[\s*(?:T[+\-][^\]]+|h\d*)\s*\]|\b(?:Translation|Translated|Rom\s*Hack|Hack)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UnverifiedRawRegex = new(
            @"\[\s*b\d*\s*\]|\b(?:bad\s*dump|unverified|overdump)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static QualityModel ParseQuality(string name)
        {
            Logger.Debug("Trying to parse quality for '{0}'", name);

            if (name.IsNullOrWhiteSpace())
            {
                return new QualityModel { Quality = Quality.Unknown };
            }

            name = name.Trim();

            var result = ParseQualityName(name);

            if (result.Quality == Quality.Unknown && !name.ContainsInvalidPathChars())
            {
                try
                {
                    result.Quality = MediaFileExtensions.GetQualityForExtension(Path.GetExtension(name));
                    result.SourceDetectionSource = QualityDetectionSource.Extension;
                    result.ResolutionDetectionSource = QualityDetectionSource.Extension;
                }
                catch (ArgumentException)
                {
                    // Swallow exception for cases where string contains illegal path characters.
                }
            }

            return result;
        }

        public static QualityModel ParseQualityName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new QualityModel { Quality = Quality.Unknown };
            }

            var normalizedName = name.Replace('_', ' ').Trim();
            var result = ParseQualityModifiers(name, normalizedName);

            if (RedumpRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.VerifiedRedump;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                return result;
            }

            if (NoIntroRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.NoIntroVerified;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                return result;
            }

            if (VerifiedGoodDumpRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.VerifiedGoodDump;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                return result;
            }

            if (TranslationPatchRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.TranslationPatch;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                return result;
            }

            if (BetaProtoRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.BetaProto;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                return result;
            }

            if (UnverifiedRawRegex.IsMatch(normalizedName))
            {
                result.Quality = Quality.UnverifiedRaw;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                return result;
            }

            var parsedRom = RomParser.ParseRomName(name);
            if (parsedRom?.Quality != null && parsedRom.Quality.Quality != Quality.Unknown)
            {
                result.Quality = parsedRom.Quality.Quality;
                result.SourceDetectionSource = QualityDetectionSource.Name;
                result.ResolutionDetectionSource = QualityDetectionSource.Unknown;
            }

            return result;
        }

        private static QualityModel ParseQualityModifiers(string name, string normalizedName)
        {
            var result = new QualityModel { Quality = Quality.Unknown };

            var versionRegexResult = VersionRegex.Match(normalizedName);

            if (versionRegexResult.Success)
            {
                result.Revision.Version = Convert.ToInt32(versionRegexResult.Groups["version"].Value);
                result.RevisionDetectionSource = QualityDetectionSource.Name;
            }

            if (ProperRegex.IsMatch(normalizedName))
            {
                result.Revision.Version = versionRegexResult.Success ? Convert.ToInt32(versionRegexResult.Groups["version"].Value) + 1 : 2;
                result.RevisionDetectionSource = QualityDetectionSource.Name;
            }

            if (RepackRegex.IsMatch(normalizedName))
            {
                result.Revision.Version = versionRegexResult.Success ? Convert.ToInt32(versionRegexResult.Groups["version"].Value) + 1 : 2;
                result.Revision.IsRepack = true;
                result.RevisionDetectionSource = QualityDetectionSource.Name;
            }

            var realRegexResult = RealRegex.Matches(name);

            if (realRegexResult.Count > 0)
            {
                result.Revision.Real = realRegexResult.Count;
                result.RevisionDetectionSource = QualityDetectionSource.Name;
            }

            return result;
        }
    }
}
