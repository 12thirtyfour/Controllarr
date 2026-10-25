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
            RuleFor(c => c.UrlBase).ValidRootUrl();
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

        [FieldDefinition(0, Label = "UrlBase", Type = FieldType.Textbox)]
        public string UrlBase { get; set; }

        [FieldDefinition(1, Label = "S3 Access Key", Type = FieldType.Textbox)]
        public string S3AccessKey { get; set; }

        [FieldDefinition(2, Label = "S3 Secret Key", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string S3SecretKey { get; set; }

        [FieldDefinition(3, Label = "Allow Anonymous Fallback", Type = FieldType.Checkbox)]
        public bool AllowAnonymousFallback { get; set; } = true;

        [FieldDefinition(4, Label = "Primary Archive URLs", Type = FieldType.Tag, HelpText = "Links or identifiers to Internet Archive collections/items (e.g. https://archive.org/details/htgdb-gamepacks) that act as primary resources before searching archive.org directly.")]
        public IEnumerable<string> PrimaryArchiveUrls { get; set; }

        public IEnumerable<string> PreferredArchives
        {
            get => PrimaryArchiveUrls;
            set => PrimaryArchiveUrls = value;
        }

        [FieldDefinition(5, Label = "Download Path", Type = FieldType.Path, HelpText = "Optional download directory. Defaults to AppData/downloads.")]
        public string DownloadPath { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
