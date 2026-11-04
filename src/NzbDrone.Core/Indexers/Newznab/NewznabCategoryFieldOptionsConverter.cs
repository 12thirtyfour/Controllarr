using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Indexers.Newznab
{
    public static class NewznabCategoryFieldOptionsConverter
    {
        public static List<FieldSelectOption<int>> GetFieldSelectOptions(List<NewznabCategory> categories)
        {
            var result = new List<FieldSelectOption<int>>();

            if (categories == null)
            {
                // Fetching categories failed, use default Newznab Console categories
                categories = new List<NewznabCategory>
                {
                    new NewznabCategory
                    {
                        Id = 1000,
                        Name = "Console",
                        Subcategories = new List<NewznabCategory>
                        {
                            new NewznabCategory { Id = 1010, Name = "NDS" },
                            new NewznabCategory { Id = 1020, Name = "PSP" },
                            new NewznabCategory { Id = 1030, Name = "Wii" },
                            new NewznabCategory { Id = 1040, Name = "Xbox" },
                            new NewznabCategory { Id = 1050, Name = "Xbox 360" },
                            new NewznabCategory { Id = 1060, Name = "WiiWare/VC" },
                            new NewznabCategory { Id = 1070, Name = "Xbox 360 DLC" },
                            new NewznabCategory { Id = 1080, Name = "PS3" },
                            new NewznabCategory { Id = 1090, Name = "Other" },
                            new NewznabCategory { Id = 1110, Name = "3DS" },
                            new NewznabCategory { Id = 1120, Name = "PS Vita" },
                            new NewznabCategory { Id = 1130, Name = "WiiU" },
                            new NewznabCategory { Id = 1140, Name = "Xbox One" },
                            new NewznabCategory { Id = 1180, Name = "PS4" }
                        }
                    }
                };
            }

            foreach (var category in categories.Where(cat => cat.Id >= 1000 && cat.Id < 2000).OrderBy(cat => cat.Id))
            {
                result.Add(new FieldSelectOption<int>
                {
                    Value = category.Id,
                    Name = category.Name,
                    Hint = $"({category.Id})"
                });

                if (category.Subcategories != null)
                {
                    foreach (var subcat in category.Subcategories.Where(sub => sub.Id >= 1000 && sub.Id < 2000).OrderBy(cat => cat.Id))
                    {
                        result.Add(new FieldSelectOption<int>
                        {
                            Value = subcat.Id,
                            Name = subcat.Name,
                            Hint = $"({subcat.Id})",
                            ParentValue = category.Id
                        });
                    }
                }
            }

            return result;
        }
    }
}
