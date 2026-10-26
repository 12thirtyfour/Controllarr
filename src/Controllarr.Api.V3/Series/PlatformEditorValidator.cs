using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace Sonarr.Api.V3.Series
{
    public class PlatformEditorValidator : AbstractValidator<NzbDrone.Core.Tv.Series>
    {
        public PlatformEditorValidator(RootFolderExistsValidator rootFolderExistsValidator, QualityProfileExistsValidator qualityProfileExistsValidator)
        {
            RuleFor(s => s.RootFolderPath).Cascade(CascadeMode.Stop)
                .IsValidPath()
                .SetValidator(rootFolderExistsValidator)
                .When(s => s.RootFolderPath.IsNotNullOrWhiteSpace());

            RuleFor(c => c.QualityProfileId).Cascade(CascadeMode.Stop)
                .ValidId()
                .SetValidator(qualityProfileExistsValidator);
        }
    }

    public class SeriesEditorValidator : PlatformEditorValidator
    {
        public SeriesEditorValidator(RootFolderExistsValidator rootFolderExistsValidator, QualityProfileExistsValidator qualityProfileExistsValidator)
            : base(rootFolderExistsValidator, qualityProfileExistsValidator)
        {
        }
    }
}
