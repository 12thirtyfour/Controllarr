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
    }
}
