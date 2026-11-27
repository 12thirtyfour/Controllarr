using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Qualities
{
    public class Quality : IEmbeddedDocument, IEquatable<Quality>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public QualitySource Source { get; set; }
        public int Resolution { get; set; }

        public Quality()
        {
        }

        private Quality(int id, string name, QualitySource source, int resolution)
        {
            Id = id;
            Name = name;
            Source = source;
            Resolution = resolution;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(Quality other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as Quality);
        }

        public static bool operator ==(Quality left, Quality right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Quality left, Quality right)
        {
            return !Equals(left, right);
        }

        public static Quality Unknown => new Quality(0, "Unknown", QualitySource.Unknown, 0);
        public static Quality VerifiedGoodDump => new Quality(100, "VerifiedGoodDump", QualitySource.Verified, 0);
        public static Quality VerifiedRedump => new Quality(101, "VerifiedRedump", QualitySource.Redump, 0);
        public static Quality NoIntroVerified => new Quality(102, "NoIntroVerified", QualitySource.NoIntro, 0);
        public static Quality UnverifiedRaw => new Quality(103, "UnverifiedRaw", QualitySource.Unverified, 0);
        public static Quality BetaProto => new Quality(104, "BetaProto", QualitySource.Prototype, 0);
        public static Quality TranslationPatch => new Quality(105, "TranslationPatch", QualitySource.Patch, 0);

        static Quality()
        {
            All = new List<Quality>
            {
                Unknown,
                BetaProto,
                TranslationPatch,
                UnverifiedRaw,
                VerifiedGoodDump,
                NoIntroVerified,
                VerifiedRedump
            };

            AllLookup = All.ToDictionary(q => q.Id, q => q);

            DefaultQualityDefinitions = new HashSet<QualityDefinition>
            {
                new QualityDefinition(Quality.Unknown) { Weight = 1, MinSize = 0, MaxSize = null, PreferredSize = 100 },
                new QualityDefinition(Quality.BetaProto) { Weight = 100, MinSize = 0, MaxSize = null, PreferredSize = 100 },
                new QualityDefinition(Quality.TranslationPatch) { Weight = 101, MinSize = 0, MaxSize = null, PreferredSize = 100 },
                new QualityDefinition(Quality.UnverifiedRaw) { Weight = 102, MinSize = 0, MaxSize = null, PreferredSize = 100 },
                new QualityDefinition(Quality.VerifiedGoodDump) { Weight = 103, MinSize = 0, MaxSize = null, PreferredSize = 100 },
                new QualityDefinition(Quality.NoIntroVerified) { Weight = 104, MinSize = 0, MaxSize = null, PreferredSize = 100 },
                new QualityDefinition(Quality.VerifiedRedump) { Weight = 105, MinSize = 0, MaxSize = null, PreferredSize = 100 }
            };
        }

        public static readonly List<Quality> All;

        public static readonly Dictionary<int, Quality> AllLookup;

        public static readonly HashSet<QualityDefinition> DefaultQualityDefinitions;

        public static Quality FindById(int id)
        {
            if (id == 0)
            {
                return Unknown;
            }

            return AllLookup.TryGetValue(id, out var quality) ? quality : Unknown;
        }

        public static explicit operator Quality(int id)
        {
            return FindById(id);
        }

        public static explicit operator int(Quality quality)
        {
            return quality.Id;
        }
    }
}
