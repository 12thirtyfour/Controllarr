using System.Linq;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(250)]
    public class remove_excluded_rerelease_editions : NzbDroneMigrationBase
    {
        private static readonly string[] ExcludedEditionTerms =
        {
            "retro-bit generations",
            "retrobit generations",
            "pixel heart",
            "pixelheart",
            "pixel-heart",
            "evercade",
            "capcom town",
            "capcomtown",
            "capcom classics mini mix",
            "namco museum archives",
            "namcot collection",
            "castlevania anniversary collection",
            "castlevania advance collection",
            "castlevania dominus collection",
            "contra anniversary collection",
            "the disney afternoon collection",
            "disney afternoon collection",
            "snk 40th anniversary collection",
            "super bomberman collection",
            "mega man legacy collection",
            "mega man x legacy collection",
            "mega man battle network legacy collection",
            "marvel maximum collection",
            "cowabunga collection",
            "retro collection",
            "arcade archives",
            "collection of mana",
            "collection of saga",
            "sega genesis classics",
            "sega mega drive classics",
            "sega ages",
            "sonic origins",
            "sonic mega collection",
            "sonic gems collection",
            "atari 50",
            "atari flashback classics",
            "qubyte classics",
            "limited run",
            "strictly limited",
            "iam8bit",
            "collectorvision",
            "konami collector''s series",
            "blizzard arcade collection",
            "double dragon & kunio-kun retro brawler bundle",
            "my arcade",
            "super pocket"
        };

        protected override void MainDbUpgrade()
        {
            if (Schema.Table("DatEntries").Exists())
            {
                var datCondition = BuildParenTagClause("GameName", "RomName", "SmdbPath");
                Execute.Sql($"DELETE FROM \"DatEntries\" WHERE {datCondition}");
            }

            if (Schema.Table("Games").Exists())
            {
                var titleCondition = BuildParenTagClause("Title");
                var finaleCondition = BuildSubstringClause("FinaleType");
                Execute.Sql($"DELETE FROM \"Games\" WHERE COALESCE(\"EpisodeFileId\", 0) = 0 AND ({titleCondition} OR {finaleCondition})");
            }
        }

        private static string BuildParenTagClause(params string[] columns)
        {
            return string.Join(
                " OR ",
                columns.SelectMany(col => ExcludedEditionTerms.SelectMany(term => new[]
                {
                    $"LOWER(COALESCE(\"{col}\", '')) LIKE '%({term}%'",
                    $"LOWER(COALESCE(\"{col}\", '')) LIKE '%, {term}%'"
                })));
        }

        private static string BuildSubstringClause(params string[] columns)
        {
            return string.Join(
                " OR ",
                columns.SelectMany(col => ExcludedEditionTerms.Select(term =>
                    $"LOWER(COALESCE(\"{col}\", '')) LIKE '%{term}%'")));
        }
    }
}
