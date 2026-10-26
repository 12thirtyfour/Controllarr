using System;
using System.Collections.Generic;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.InternetArchive
{
    public class InternetArchiveSettingsValidator : AbstractValidator<InternetArchiveSettings>
    {
        public InternetArchiveSettingsValidator()
        {
            RuleFor(c => c.DownloadPath).IsValidPath().When(c => !string.IsNullOrWhiteSpace(c.DownloadPath));
        }
    }

    public class InternetArchiveSettings : DownloadClientSettingsBase<InternetArchiveSettings>
    {
        private static readonly InternetArchiveSettingsValidator Validator = new InternetArchiveSettingsValidator();

        public InternetArchiveSettings()
        {
            UrlBase = "https://archive.org";
            PrimaryArchiveUrls = Array.Empty<string>();
        }

        [FieldDefinition(0, Label = "Download Path", Type = FieldType.Path, HelpText = "Optional download directory. Defaults to AppData/downloads.")]
        public string DownloadPath { get; set; }

        [FieldDefinition(1, Label = "Allow Full Game Packages", Type = FieldType.Checkbox, HelpText = "Allow downloading full game packages and complete sets (.zip / .7z archives) from Internet Archive when games are missing or when searching collections")]
        public bool AllowFullPackages { get; set; } = true;

        // Retained for backward-compatibility with serialized database records; connection and S3 credentials are now sourced directly from the Internet Archive Indexer
        public string UrlBase { get; set; }
        public string S3AccessKey { get; set; }
        public string S3SecretKey { get; set; }
        public bool AllowAnonymousFallback { get; set; } = true;
        public IEnumerable<string> PrimaryArchiveUrls { get; set; }

        public IEnumerable<string> PreferredArchives
        {
            get => PrimaryArchiveUrls;
            set => PrimaryArchiveUrls = value;
        }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
