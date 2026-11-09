using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V3.Config
{
    [V3ApiController("config/naming")]
    public class NamingConfigController : RestController<NamingConfigResource>
    {
        private readonly INamingConfigService _namingConfigService;
        private readonly IFilenameSampleService _filenameSampleService;
        private readonly IFilenameValidationService _filenameValidationService;
        private readonly IBuildFileNames _filenameBuilder;

        public NamingConfigController(INamingConfigService namingConfigService,
                                  IFilenameSampleService filenameSampleService,
                                  IFilenameValidationService filenameValidationService,
                                  IBuildFileNames filenameBuilder)
        {
            _namingConfigService = namingConfigService;
            _filenameSampleService = filenameSampleService;
            _filenameValidationService = filenameValidationService;
            _filenameBuilder = filenameBuilder;

            SharedValidator.RuleFor(c => c.MultiEpisodeStyle).InclusiveBetween(0, 5);
            SharedValidator.RuleFor(c => c.StandardGameFormat).ValidGameFormat();
            SharedValidator.RuleFor(c => c.DiscGameFormat).ValidDailyGameFormat();
            SharedValidator.RuleFor(c => c.AnimeGameFormat).ValidAnimeGameFormat();
            SharedValidator.RuleFor(c => c.PlatformFolderFormat).ValidSeriesFolderFormat();
            SharedValidator.RuleFor(c => c.RegionFolderFormat).ValidSeasonFolderFormat();
            SharedValidator.RuleFor(c => c.UnlicensedFolderFormat).ValidSpecialsFolderFormat();
            SharedValidator.RuleFor(c => c.CustomColonReplacementFormat).ValidCustomColonReplacement().When(c => c.ColonReplacementFormat == (int)ColonReplacementFormat.Custom);
        }

        protected override NamingConfigResource GetResourceById(int id)
        {
            return GetNamingConfig();
        }

        [HttpGet]
        public NamingConfigResource GetNamingConfig()
        {
            var nameSpec = _namingConfigService.GetConfig();
            var resource = nameSpec.ToResource();

            return resource;
        }

        [RestPutById]
        public ActionResult<NamingConfigResource> UpdateNamingConfig([FromBody] NamingConfigResource resource)
        {
            var nameSpec = resource.ToModel();
            ValidateFormatResult(nameSpec);

            _namingConfigService.Save(nameSpec);

            return Accepted(resource.Id);
        }

        [HttpGet("examples")]
        public object GetExamples([FromQuery]NamingConfigResource settings)
        {
            if (settings.Id == 0)
            {
                settings = GetNamingConfig();
            }

            var nameSpec = settings.ToModel();
            var sampleResource = new NamingExampleResource();

            var singleGameSampleResult = _filenameSampleService.GetStandardSample(nameSpec);
            var multiGameSampleResult = _filenameSampleService.GetMultiEpisodeSample(nameSpec);
            var discGameSampleResult = _filenameSampleService.GetDailySample(nameSpec);
            var animeGameSampleResult = _filenameSampleService.GetAnimeSample(nameSpec);
            var animeMultiGameSampleResult = _filenameSampleService.GetAnimeMultiEpisodeSample(nameSpec);

            var hasOriginalStandardToken = !string.IsNullOrWhiteSpace(nameSpec.StandardGameFormat) &&
                                           FileNameValidation.OriginalTokenRegex.IsMatch(nameSpec.StandardGameFormat);
            var hasOriginalDiscToken = !string.IsNullOrWhiteSpace(nameSpec.DiscGameFormat) &&
                                       FileNameValidation.OriginalTokenRegex.IsMatch(nameSpec.DiscGameFormat);
            var hasOriginalAnimeToken = !string.IsNullOrWhiteSpace(nameSpec.AnimeGameFormat) &&
                                        FileNameValidation.OriginalTokenRegex.IsMatch(nameSpec.AnimeGameFormat);

            sampleResource.SingleGameExample = !hasOriginalStandardToken && _filenameValidationService.ValidateStandardFilename(singleGameSampleResult) != null
                    ? null
                    : singleGameSampleResult.FileName;

            sampleResource.MultiGameExample = !hasOriginalStandardToken && _filenameValidationService.ValidateStandardFilename(multiGameSampleResult) != null
                    ? null
                    : multiGameSampleResult.FileName;

            sampleResource.DiscGameExample = !hasOriginalDiscToken && _filenameValidationService.ValidateDailyFilename(discGameSampleResult) != null
                    ? null
                    : discGameSampleResult.FileName;

            sampleResource.AnimeGameExample = !hasOriginalAnimeToken && _filenameValidationService.ValidateAnimeFilename(animeGameSampleResult) != null
                    ? null
                    : animeGameSampleResult.FileName;

            sampleResource.AnimeMultiGameExample = !hasOriginalAnimeToken && _filenameValidationService.ValidateAnimeFilename(animeMultiGameSampleResult) != null
                    ? null
                    : animeMultiGameSampleResult.FileName;

            sampleResource.PlatformFolderExample = nameSpec.PlatformFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetSeriesFolderSample(nameSpec);

            sampleResource.RegionFolderExample = nameSpec.RegionFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetSeasonFolderSample(nameSpec);

            sampleResource.UnlicensedFolderExample = nameSpec.UnlicensedFolderFormat.IsNullOrWhiteSpace()
                ? null
                : _filenameSampleService.GetSpecialsFolderSample(nameSpec);

            return sampleResource;
        }

        private void ValidateFormatResult(NamingConfig nameSpec)
        {
            var singleGameSampleResult = _filenameSampleService.GetStandardSample(nameSpec);
            var multiGameSampleResult = _filenameSampleService.GetMultiEpisodeSample(nameSpec);
            var discGameSampleResult = _filenameSampleService.GetDailySample(nameSpec);
            var animeGameSampleResult = _filenameSampleService.GetAnimeSample(nameSpec);
            var animeMultiGameSampleResult = _filenameSampleService.GetAnimeMultiEpisodeSample(nameSpec);

            var hasOriginalStandardToken = !string.IsNullOrWhiteSpace(nameSpec.StandardGameFormat) &&
                                           FileNameValidation.OriginalTokenRegex.IsMatch(nameSpec.StandardGameFormat);
            var hasOriginalDiscToken = !string.IsNullOrWhiteSpace(nameSpec.DiscGameFormat) &&
                                       FileNameValidation.OriginalTokenRegex.IsMatch(nameSpec.DiscGameFormat);
            var hasOriginalAnimeToken = !string.IsNullOrWhiteSpace(nameSpec.AnimeGameFormat) &&
                                        FileNameValidation.OriginalTokenRegex.IsMatch(nameSpec.AnimeGameFormat);

            var singleGameValidationResult = hasOriginalStandardToken ? null : _filenameValidationService.ValidateStandardFilename(singleGameSampleResult);
            var multiGameValidationResult = hasOriginalStandardToken ? null : _filenameValidationService.ValidateStandardFilename(multiGameSampleResult);
            var discGameValidationResult = hasOriginalDiscToken ? null : _filenameValidationService.ValidateDailyFilename(discGameSampleResult);
            var animeGameValidationResult = hasOriginalAnimeToken ? null : _filenameValidationService.ValidateAnimeFilename(animeGameSampleResult);
            var animeMultiGameValidationResult = hasOriginalAnimeToken ? null : _filenameValidationService.ValidateAnimeFilename(animeMultiGameSampleResult);

            var validationFailures = new List<ValidationFailure>();

            validationFailures.AddIfNotNull(singleGameValidationResult);
            validationFailures.AddIfNotNull(multiGameValidationResult);
            validationFailures.AddIfNotNull(discGameValidationResult);
            validationFailures.AddIfNotNull(animeGameValidationResult);
            validationFailures.AddIfNotNull(animeMultiGameValidationResult);

            if (validationFailures.Any())
            {
                throw new ValidationException(validationFailures.DistinctBy(v => v.PropertyName).ToArray());
            }
        }
    }
}
