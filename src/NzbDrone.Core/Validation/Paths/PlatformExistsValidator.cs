using System;
using FluentValidation.Validators;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Validation.Paths
{
    public class PlatformExistsValidator : PropertyValidator
    {
        private readonly ISeriesService _seriesService;

        public PlatformExistsValidator(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        protected override string GetDefaultMessageTemplate() => "This platform has already been added";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            if (context.PropertyValue == null)
            {
                return true;
            }

            var tvdbId = Convert.ToInt32(context.PropertyValue.ToString());

            return !_seriesService.AllSeriesTvdbIds().ContainsValue(tvdbId);
        }
    }

    public class SeriesExistsValidator : PlatformExistsValidator
    {
        public SeriesExistsValidator(ISeriesService seriesService)
            : base(seriesService)
        {
        }
    }
}
