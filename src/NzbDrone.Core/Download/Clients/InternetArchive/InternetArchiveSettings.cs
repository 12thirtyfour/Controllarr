using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.InternetArchive
{
    public class InternetArchiveSettingsValidator : AbstractValidator<InternetArchiveSettings>
    {
        public InternetArchiveSettingsValidator()
        {
            RuleFor(c => c.UrlBase).ValidUrlBase();
        }
    }

    public class InternetArchiveSettings : DownloadClientSettingsBase<InternetArchiveSettings>
    {
        private static readonly InternetArchiveSettingsValidator Validator = new InternetArchiveSettingsValidator();

        public InternetArchiveSettings()
        {
            UrlBase = "https://archive.org";
        }

        [FieldDefinition(0, Label = "UrlBase", Type = FieldType.Textbox)]
        public string UrlBase { get; set; }

        [FieldDefinition(1, Label = "S3 Access Key", Type = FieldType.Textbox)]
        public string S3AccessKey { get; set; }

        [FieldDefinition(2, Label = "S3 Secret Key", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string S3SecretKey { get; set; }

        [FieldDefinition(3, Label = "Allow Anonymous Fallback", Type = FieldType.Checkbox)]
        public bool AllowAnonymousFallback { get; set; } = true;

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
