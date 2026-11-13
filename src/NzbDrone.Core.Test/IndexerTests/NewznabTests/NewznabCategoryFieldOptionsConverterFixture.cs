using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    [TestFixture]
    public class NewznabCategoryFieldOptionsConverterFixture : CoreTest
    {
        [Test]
        public void should_return_only_console_categories_when_categories_is_null()
        {
            var options = NewznabCategoryFieldOptionsConverter.GetFieldSelectOptions(null);

            options.Should().NotBeEmpty();
            options.Should().OnlyContain(opt => opt.Value >= 1000 && opt.Value < 2000);
            options.Should().NotContain(opt => opt.Value >= 4000 && opt.Value < 5000);
            options.Should().NotContain(opt => opt.Value >= 8000 && opt.Value < 9000);
        }

        [Test]
        public void should_filter_out_pc_and_other_categories_from_indexer_capabilities()
        {
            var categories = new List<NewznabCategory>
            {
                new NewznabCategory
                {
                    Id = 1000,
                    Name = "Console",
                    Subcategories = new List<NewznabCategory>
                    {
                        new NewznabCategory { Id = 1010, Name = "NDS" },
                        new NewznabCategory { Id = 1020, Name = "PSP" }
                    }
                },
                new NewznabCategory
                {
                    Id = 2000,
                    Name = "Movies"
                },
                new NewznabCategory
                {
                    Id = 3000,
                    Name = "Audio"
                },
                new NewznabCategory
                {
                    Id = 4000,
                    Name = "PC",
                    Subcategories = new List<NewznabCategory>
                    {
                        new NewznabCategory { Id = 4010, Name = "0day" },
                        new NewznabCategory { Id = 4020, Name = "ISO" }
                    }
                },
                new NewznabCategory
                {
                    Id = 5000,
                    Name = "TV"
                },
                new NewznabCategory
                {
                    Id = 7000,
                    Name = "Books"
                },
                new NewznabCategory
                {
                    Id = 8000,
                    Name = "Other",
                    Subcategories = new List<NewznabCategory>
                    {
                        new NewznabCategory { Id = 8010, Name = "Misc" }
                    }
                }
            };

            var options = NewznabCategoryFieldOptionsConverter.GetFieldSelectOptions(categories);

            options.Should().HaveCount(3);
            options.Select(opt => opt.Value).Should().BeEquivalentTo(new[] { 1000, 1010, 1020 });
            options.Should().NotContain(opt => opt.Value >= 4000 && opt.Value < 5000);
            options.Should().NotContain(opt => opt.Value >= 8000 && opt.Value < 9000);
        }

        [Test]
        public void should_return_default_console_categories_when_categories_is_empty_or_has_no_console_categories()
        {
            var emptyOptions = NewznabCategoryFieldOptionsConverter.GetFieldSelectOptions(new List<NewznabCategory>());
            emptyOptions.Should().NotBeEmpty();
            emptyOptions.Should().OnlyContain(opt => opt.Value >= 1000 && opt.Value < 2000);

            var nonConsoleCategories = new List<NewznabCategory>
            {
                new NewznabCategory { Id = 4000, Name = "PC" },
                new NewznabCategory { Id = 5000, Name = "TV" }
            };

            var fallbackOptions = NewznabCategoryFieldOptionsConverter.GetFieldSelectOptions(nonConsoleCategories);
            fallbackOptions.Should().NotBeEmpty();
            fallbackOptions.Should().OnlyContain(opt => opt.Value >= 1000 && opt.Value < 2000);
        }
    }
}
