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

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
