using System;
using System.Collections.Generic;
using Equ;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveIndexerSettingsValidator : AbstractValidator<InternetArchiveIndexerSettings>
    {
        public InternetArchiveIndexerSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
        }
    }

    public class InternetArchiveIndexerSettings : PropertywiseEquatable<InternetArchiveIndexerSettings>, IIndexerSettings
    {
        private static readonly InternetArchiveIndexerSettingsValidator Validator = new();

        public InternetArchiveIndexerSettings()
        {
            BaseUrl = "https://archive.org";
            PrimaryArchiveUrls = new[] { "https://archive.org/details/htgdb-gamepacks" };
            S3AccessKey = string.Empty;
            S3SecretKey = string.Empty;
            MultiLanguages = Array.Empty<int>();
            FailDownloads = new[]
            {
                (int)Indexers.FailDownloads.Executables,
                (int)Indexers.FailDownloads.PotentiallyDangerous,
                (int)Indexers.FailDownloads.UserDefinedExtensions
            };
        }

        public static string ExtractIdentifier(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId))
            {
                return null;
            }

            var trimmed = urlOrId.Trim().TrimEnd('/');
            var detailsIdx = trimmed.IndexOf("/details/", StringComparison.OrdinalIgnoreCase);
            if (detailsIdx >= 0)
            {
                var remainder = trimmed.Substring(detailsIdx + 9);
                var slashIdx = remainder.IndexOf('/');
                return slashIdx >= 0 ? remainder.Substring(0, slashIdx).Trim() : remainder.Trim();
            }

            var downloadIdx = trimmed.IndexOf("/download/", StringComparison.OrdinalIgnoreCase);
            if (downloadIdx >= 0)
            {
                var remainder = trimmed.Substring(downloadIdx + 10);
                var slashIdx = remainder.IndexOf('/');
                return slashIdx >= 0 ? remainder.Substring(0, slashIdx).Trim() : remainder.Trim();
            }

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var lastSlash = trimmed.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < trimmed.Length - 1)
                {
                    return trimmed.Substring(lastSlash + 1).Trim();
                }
            }

            return trimmed;
        }

        [FieldDefinition(0, Label = "Base URL")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "Primary Archive URLs", Type = FieldType.Tag, HelpText = "Internet Archive collection or item URLs/identifiers (e.g. https://archive.org/details/htgdb-gamepacks) that act as primary resources before searching archive.org directly")]
        public IEnumerable<string> PrimaryArchiveUrls { get; set; }

        public IEnumerable<string> Collections
        {
            get => PrimaryArchiveUrls;
            set => PrimaryArchiveUrls = value;
        }

        [FieldDefinition(2, Label = "S3 Access Key", HelpText = "Optional Internet Archive S3 Access Key")]
        public string S3AccessKey { get; set; }

        [FieldDefinition(3, Label = "S3 Secret Key", Type = FieldType.Password, Privacy = PrivacyLevel.Password, HelpText = "Optional Internet Archive S3 Secret Key")]
        public string S3SecretKey { get; set; }

        [FieldDefinition(4, Label = "Require S3 Credentials", Type = FieldType.Checkbox, HelpText = "Require S3 credentials for all releases from this indexer")]
        public bool RequireCredentials { get; set; }

        [FieldDefinition(5, Type = FieldType.Select, SelectOptions = typeof(RealLanguageFieldConverter), Label = "Multi-Language", HelpText = "IndexerSettingsMultiLanguageReleaseHelpText", Advanced = true)]
        public IEnumerable<int> MultiLanguages { get; set; }

        [FieldDefinition(6, Type = FieldType.Select, SelectOptions = typeof(FailDownloads), Label = "IndexerSettingsFailDownloads", HelpText = "IndexerSettingsFailDownloadsHelpText")]
        public IEnumerable<int> FailDownloads { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
