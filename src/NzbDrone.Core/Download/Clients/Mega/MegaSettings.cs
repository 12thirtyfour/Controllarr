using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.Mega
{
    public class MegaSettingsValidator : AbstractValidator<MegaSettings>
    {
        public MegaSettingsValidator()
        {
            RuleFor(c => c.DownloadPath).IsValidPath().When(c => !string.IsNullOrWhiteSpace(c.DownloadPath));
            RuleFor(c => c.Password).NotEmpty().When(c => !string.IsNullOrWhiteSpace(c.Email));
            RuleFor(c => c.Email).NotEmpty().When(c => !string.IsNullOrWhiteSpace(c.Password));
        }
    }

    public class MegaSettings : DownloadClientSettingsBase<MegaSettings>
    {
        private static readonly MegaSettingsValidator Validator = new MegaSettingsValidator();

        [FieldDefinition(0, Label = "Email", Type = FieldType.Textbox, HelpText = "Optional Mega.nz account email. Leave blank for anonymous downloads.")]
        public string Email { get; set; }

        [FieldDefinition(1, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password, HelpText = "Optional Mega.nz account password. Downloads are always saved directly to disk and never added to your Mega account.")]
        public string Password { get; set; }

        [FieldDefinition(2, Label = "Download Path", Type = FieldType.Path, HelpText = "Optional download directory. Defaults to AppData/downloads.")]
        public string DownloadPath { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
