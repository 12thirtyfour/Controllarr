using System;
using System.Collections.Generic;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.InternetArchive
{
    public class InternetArchiveSettingsValidator : AbstractValidator<InternetArchiveSettings>
    {
        public InternetArchiveSettingsValidator()
        {
            RuleFor(c => c.UrlBase).ValidRootUrl();
        }
    }

    public class InternetArchiveSettings : DownloadClientSettingsBase<InternetArchiveSettings>
    {
        private static readonly InternetArchiveSettingsValidator Validator = new InternetArchiveSettingsValidator();

        public InternetArchiveSettings()
        {
            UrlBase = "https://archive.org";
            PreferredArchives = Array.Empty<string>();
        }

        [FieldDefinition(0, Label = "UrlBase", Type = FieldType.Textbox)]
        public string UrlBase { get; set; }

        [FieldDefinition(1, Label = "S3 Access Key", Type = FieldType.Textbox)]
        public string S3AccessKey { get; set; }

        [FieldDefinition(2, Label = "S3 Secret Key", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string S3SecretKey { get; set; }

        [FieldDefinition(3, Label = "Allow Anonymous Fallback", Type = FieldType.Checkbox)]
        public bool AllowAnonymousFallback { get; set; } = true;

        [FieldDefinition(4, Label = "Preferred Archives", Type = FieldType.Tag)]
        public IEnumerable<string> PreferredArchives { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
