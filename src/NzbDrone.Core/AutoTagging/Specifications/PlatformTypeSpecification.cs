using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.AutoTagging.Specifications
{
    public class PlatformTypeSpecificationValidator : AbstractValidator<PlatformTypeSpecification>
    {
        public PlatformTypeSpecificationValidator()
        {
            RuleFor(c => (SeriesTypes)c.Value).IsInEnum();
        }
    }

    public class PlatformTypeSpecification : AutoTaggingSpecificationBase
    {
        private static readonly PlatformTypeSpecificationValidator Validator = new PlatformTypeSpecificationValidator();

        public override int Order => 2;
        public override string ImplementationName => "Platform Type";

        [FieldDefinition(1, Label = "AutoTaggingSpecificationSeriesType", Type = FieldType.Select, SelectOptions = typeof(SeriesTypes))]
        public int Value { get; set; }

        protected override bool IsSatisfiedByWithoutNegate(Series series)
        {
            return (int)series.SeriesType == Value;
        }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
