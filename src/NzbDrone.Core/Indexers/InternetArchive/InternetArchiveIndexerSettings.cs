using System;
using System.Collections.Generic;
using Equ;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveIndexerSettingsValidator : AbstractValidator<InternetArchiveIndexerSettings>
    {
        public InternetArchiveIndexerSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
        }
    }

    public class InternetArchiveIndexerSettings : PropertywiseEquatable<InternetArchiveIndexerSettings>, IIndexerSettings
    {
        private static readonly InternetArchiveIndexerSettingsValidator Validator = new();

        public InternetArchiveIndexerSettings()
        {
            BaseUrl = "https://archive.org";
            Collections = new[] { "no-intro_romsets", "redump", "softwarelibrary", "retroarch_system", "open_source_software" };
            S3AccessKey = string.Empty;
            S3SecretKey = string.Empty;
            MultiLanguages = Array.Empty<int>();
            FailDownloads = Array.Empty<int>();
        }

        [FieldDefinition(0, Label = "Base URL")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "Collections", Type = FieldType.Tag, HelpText = "Internet Archive collections to search")]
        public IEnumerable<string> Collections { get; set; }

        [FieldDefinition(2, Label = "S3 Access Key", HelpText = "Optional Internet Archive S3 Access Key")]
        public string S3AccessKey { get; set; }

        [FieldDefinition(3, Label = "S3 Secret Key", Type = FieldType.Password, Privacy = PrivacyLevel.Password, HelpText = "Optional Internet Archive S3 Secret Key")]
        public string S3SecretKey { get; set; }

        [FieldDefinition(4, Label = "Require S3 Credentials", Type = FieldType.Checkbox, HelpText = "Require S3 credentials for all releases from this indexer")]
        public bool RequireCredentials { get; set; }

        [FieldDefinition(5, Type = FieldType.Select, SelectOptions = typeof(RealLanguageFieldConverter), Label = "Multi-Language", HelpText = "IndexerSettingsMultiLanguageReleaseHelpText", Advanced = true)]
        public IEnumerable<int> MultiLanguages { get; set; }

        [FieldDefinition(6, Type = FieldType.Select, SelectOptions = typeof(FailDownloads), Label = "IndexerSettingsFailDownloads", HelpText = "IndexerSettingsFailDownloadsHelpText")]
        public IEnumerable<int> FailDownloads { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
