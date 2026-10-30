namespace NzbDrone.Core.Games
{
    public static class RegionHelper
    {
        public static string GetRegionName(int seasonNumber)
        {
            return seasonNumber switch
            {
                0 => "Unlicensed",
                1 => "USA",
                2 => "Europe",
                3 => "Japan",
                4 => "World",
                5 => "Homebrew",
                6 => "ROM Hacks & Translations",
                _ => $"Region {seasonNumber}"
            };
        }

        public static int GetSeasonNumber(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                return 1;
            }

            var r = region.Trim().ToLowerInvariant();
            return r switch
            {
                "unlicensed" => 0,
                "usa" or "us" or "north america" or "na" => 1,
                "europe" or "eu" or "pal" => 2,
                "japan" or "jp" or "ja" => 3,
                "world" or "global" => 4,
                "homebrew" => 5,
                "rom hacks" or "rom hack" or "translation" or "translations" => 6,
                _ => 1
            };
        }
    }
}
