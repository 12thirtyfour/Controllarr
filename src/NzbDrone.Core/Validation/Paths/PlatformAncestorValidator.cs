using System.Linq;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Validation.Paths
{
    public class PlatformAncestorValidator : PropertyValidator
    {
        private readonly ISeriesService _seriesService;

        public PlatformAncestorValidator(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        protected override string GetDefaultMessageTemplate() => "Path '{path}' is an ancestor of an existing platform";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null)
            {
                return true;
            }

            context.MessageFormatter.AppendArgument("path", context.PropertyValue.ToString());

            return !_seriesService.GetAllSeriesPaths().Any(s => context.PropertyValue.ToString().IsParentPath(s.Value));
        }
    }

    public class SeriesAncestorValidator : PlatformAncestorValidator
    {
        public SeriesAncestorValidator(ISeriesService seriesService)
            : base(seriesService)
        {
        }
    }
}
