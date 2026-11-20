using System.Linq;
using FluentValidation.Validators;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Validation.Paths
{
    public class PlatformPathValidator : PropertyValidator
    {
        private readonly ISeriesService _seriesService;

        public PlatformPathValidator(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        protected override string GetDefaultMessageTemplate() => "Path '{path}' is already configured for another platform";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null)
            {
                return true;
            }

            context.MessageFormatter.AppendArgument("path", context.PropertyValue.ToString());

            dynamic instance = context.ParentContext.InstanceToValidate;
            var instanceId = (int)instance.Id;

            // Skip the path for this platform and any invalid paths
            return !_seriesService.GetAllSeriesPaths().Any(s => s.Key != instanceId &&
                                                                s.Value.IsPathValid(PathValidationType.CurrentOs) &&
                                                                s.Value.PathEquals(context.PropertyValue.ToString()));
        }
    }

    public class SeriesPathValidator : PlatformPathValidator
    {
        public SeriesPathValidator(ISeriesService seriesService)
            : base(seriesService)
        {
        }
    }
}
