using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Equ;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Newznab
{
    public class NewznabSettingsValidator : AbstractValidator<NewznabSettings>
    {
        private static readonly string[] ApiKeyAllowList =
        {
            "nzbs.org",
            "nzb.life",
            "dognzb.cr",
            "nzbplanet.net",
            "nzbid.org",
            "nzbndx.com",
            "nzbindex.in"
        };

        private static bool ShouldHaveApiKey(NewznabSettings settings)
        {
            return settings.BaseUrl != null && ApiKeyAllowList.Any(c => settings.BaseUrl.ToLowerInvariant().Contains(c));
        }

        private static readonly Regex AdditionalParametersRegex = new(@"(&.+?\=.+?)+", RegexOptions.Compiled);

        public NewznabSettingsValidator()
        {
            RuleFor(c => c.Categories).NotEmpty().WithMessage("'Categories' must be provided");

            RuleFor(c => c.BaseUrl).ValidRootUrl();
            RuleFor(c => c.ApiPath).ValidUrlBase("/api");
            RuleFor(c => c.ApiKey).NotEmpty().When(ShouldHaveApiKey);
            RuleFor(c => c.AdditionalParameters).Matches(AdditionalParametersRegex)
                                                .When(c => !c.AdditionalParameters.IsNullOrWhiteSpace());
        }
    }

    public class NewznabSettings : PropertywiseEquatable<NewznabSettings>, IIndexerSettings
    {
        private static readonly NewznabSettingsValidator Validator = new();

        public NewznabSettings()
        {
            ApiPath = "/api";
            Categories = new[] { 1000, 1010, 1020, 1030, 1040, 1050, 1060, 1070, 1080, 1090, 1110, 1120, 1130, 1140, 1180, 8000, 8010 };
            MultiLanguages = Array.Empty<int>();
            FailDownloads = Array.Empty<int>();
        }

        [FieldDefinition(0, Label = "URL")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "IndexerSettingsApiPath", HelpText = "IndexerSettingsApiPathHelpText", Advanced = true)]
        [FieldToken(TokenField.HelpText, "IndexerSettingsApiPath", "url", "/api")]
        public string ApiPath { get; set; }

        [FieldDefinition(2, Label = "ApiKey", Privacy = PrivacyLevel.ApiKey)]
        public string ApiKey { get; set; }

        [FieldDefinition(3, Label = "IndexerSettingsCategories", Type = FieldType.Select, SelectOptionsProviderAction = "newznabCategories", HelpText = "IndexerSettingsCategoriesHelpText")]
        public IEnumerable<int> Categories { get; set; }

        [FieldDefinition(4, Label = "IndexerSettingsAdditionalParameters", HelpText = "IndexerSettingsAdditionalNewznabParametersHelpText", Advanced = true)]
        public string AdditionalParameters { get; set; }

        [FieldDefinition(5, Type = FieldType.Select, SelectOptions = typeof(RealLanguageFieldConverter), Label = "IndexerSettingsMultiLanguageRelease", HelpText = "IndexerSettingsMultiLanguageReleaseHelpText", Advanced = true)]
        public IEnumerable<int> MultiLanguages { get; set; }

        [FieldDefinition(6, Type = FieldType.Select, SelectOptions = typeof(FailDownloads), Label = "IndexerSettingsFailDownloads", HelpText = "IndexerSettingsFailDownloadsHelpText")]
        public IEnumerable<int> FailDownloads { get; set; }

        // Field 8 is used by TorznabSettings MinimumSeeders
        // If you need to add another field here, update TorznabSettings as well and this comment

        public virtual NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
