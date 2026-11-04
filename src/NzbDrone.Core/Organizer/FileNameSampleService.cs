using System.Collections.Generic;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Organizer
{
    public interface IFilenameSampleService
    {
        SampleResult GetStandardSample(NamingConfig nameSpec);
        SampleResult GetMultiEpisodeSample(NamingConfig nameSpec);
        SampleResult GetDailySample(NamingConfig nameSpec);
        SampleResult GetAnimeSample(NamingConfig nameSpec);
        SampleResult GetAnimeMultiEpisodeSample(NamingConfig nameSpec);
        string GetSeriesFolderSample(NamingConfig nameSpec);
        string GetSeasonFolderSample(NamingConfig nameSpec);
        string GetSpecialsFolderSample(NamingConfig nameSpec);
    }

    public class FileNameSampleService : IFilenameSampleService
    {
        private readonly IBuildFileNames _buildFileNames;
        private static Series _standardSeries;
        private static Series _dailySeries;
        private static Series _animeSeries;
        private static Episode _episode1;
        private static Episode _episode2;
        private static Episode _episode3;
        private static List<Episode> _singleEpisode;
        private static List<Episode> _multiEpisodes;
        private static EpisodeFile _singleEpisodeFile;
        private static EpisodeFile _multiEpisodeFile;
        private static EpisodeFile _dailyEpisodeFile;
        private static EpisodeFile _animeEpisodeFile;
        private static EpisodeFile _animeMultiEpisodeFile;
        private static List<CustomFormat> _customFormats;

        public FileNameSampleService(IBuildFileNames buildFileNames)
        {
            _buildFileNames = buildFileNames;

            _standardSeries = new Series
            {
                SeriesType = SeriesTypes.Standard,
                Title = "The Series Title's!",
                Year = 2010,
                ImdbId = "tt12345",
                TvdbId = 12345,
                TvMazeId = 54321,
                TmdbId = 11223,
                Seasons = new List<Season>
                {
                    new Season
                    {
                        SeasonNumber = 1,
                        Title = "Part 1"
                    }
                }
            };

            _dailySeries = new Series
            {
                SeriesType = SeriesTypes.Daily,
                Title = "The Series Title's!",
                Year = 2010,
                ImdbId = "tt12345",
                TvdbId = 12345,
                TvMazeId = 54321,
                TmdbId = 11223,
                Seasons = new List<Season>
                {
                    new Season
                    {
                        SeasonNumber = 1,
                        Title = "Part 1"
                    }
                }
            };

            _animeSeries = new Series
            {
                SeriesType = SeriesTypes.Anime,
                Title = "The Series Title's!",
                Year = 2010,
                ImdbId = "tt12345",
                TvdbId = 12345,
                TvMazeId = 54321,
                TmdbId = 11223,
                Seasons = new List<Season>
                {
                    new Season
                    {
                        SeasonNumber = 1,
                        Title = "Part 1"
                    }
                }
            };

            _episode1 = new Episode
            {
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Episode Title (1)",
                AirDate = "2013-10-30",
                AbsoluteEpisodeNumber = 1,
            };

            _episode2 = new Episode
            {
                SeasonNumber = 1,
                EpisodeNumber = 2,
                Title = "Episode Title (2)",
                AbsoluteEpisodeNumber = 2
            };

            _episode3 = new Episode
            {
                SeasonNumber = 1,
                EpisodeNumber = 3,
                Title = "Episode Title (3)",
                AbsoluteEpisodeNumber = 3
            };

            _singleEpisode = new List<Episode> { _episode1 };
            _multiEpisodes = new List<Episode> { _episode1, _episode2, _episode3 };

            _customFormats = new List<CustomFormat>
            {
                new CustomFormat
                {
                    Name = "1G1R",
                    IncludeCustomFormatWhenRenaming = true
                },
                new CustomFormat
                {
                    Name = "VerifiedGoodDump",
                    IncludeCustomFormatWhenRenaming = true
                }
            };

            _singleEpisodeFile = new EpisodeFile
            {
                Quality = new QualityModel(Quality.VerifiedGoodDump, new Revision(1)),
                RelativePath = "Super.Mario.World.USA.sfc",
                SceneName = "Super.Mario.World.USA",
                ReleaseGroup = "No-Intro",
                MediaInfo = null
            };

            _multiEpisodeFile = new EpisodeFile
            {
                Quality = new QualityModel(Quality.VerifiedGoodDump, new Revision(1)),
                RelativePath = "Final.Fantasy.VII.USA.Disc1.chd",
                SceneName = "Final.Fantasy.VII.USA.Disc1",
                ReleaseGroup = "Redump",
                MediaInfo = null
            };

            _dailyEpisodeFile = new EpisodeFile
            {
                Quality = new QualityModel(Quality.VerifiedGoodDump, new Revision(1)),
                RelativePath = "The.Legend.of.Zelda.Ocarina.of.Time.USA.Rev.A.z64",
                SceneName = "The.Legend.of.Zelda.Ocarina.of.Time.USA.Rev.A",
                ReleaseGroup = "No-Intro",
                MediaInfo = null
            };

            _animeEpisodeFile = new EpisodeFile
            {
                Quality = new QualityModel(Quality.VerifiedGoodDump, new Revision(1)),
                RelativePath = "Chrono.Trigger.USA.sfc",
                SceneName = "Chrono.Trigger.USA",
                ReleaseGroup = "No-Intro",
                MediaInfo = null
            };

            _animeMultiEpisodeFile = new EpisodeFile
            {
                Quality = new QualityModel(Quality.VerifiedGoodDump, new Revision(1)),
                RelativePath = "Metal.Gear.Solid.USA.Disc1.chd",
                SceneName = "Metal.Gear.Solid.USA.Disc1",
                ReleaseGroup = "Redump",
                MediaInfo = null
            };
        }

        public SampleResult GetStandardSample(NamingConfig nameSpec)
        {
            var result = new SampleResult
            {
                FileName = BuildSample(_singleEpisode, _standardSeries, _singleEpisodeFile, nameSpec, _customFormats),
                Series = _standardSeries,
                Episodes = _singleEpisode,
                EpisodeFile = _singleEpisodeFile
            };

            return result;
        }

        public SampleResult GetMultiEpisodeSample(NamingConfig nameSpec)
        {
            var result = new SampleResult
            {
                FileName = BuildSample(_multiEpisodes, _standardSeries, _multiEpisodeFile, nameSpec, _customFormats),
                Series = _standardSeries,
                Episodes = _multiEpisodes,
                EpisodeFile = _multiEpisodeFile
            };

            return result;
        }

        public SampleResult GetDailySample(NamingConfig nameSpec)
        {
            var result = new SampleResult
            {
                FileName = BuildSample(_singleEpisode, _dailySeries, _dailyEpisodeFile, nameSpec, _customFormats),
                Series = _dailySeries,
                Episodes = _singleEpisode,
                EpisodeFile = _dailyEpisodeFile
            };

            return result;
        }

        public SampleResult GetAnimeSample(NamingConfig nameSpec)
        {
            var result = new SampleResult
            {
                FileName = BuildSample(_singleEpisode, _animeSeries, _animeEpisodeFile, nameSpec, _customFormats),
                Series = _animeSeries,
                Episodes = _singleEpisode,
                EpisodeFile = _animeEpisodeFile
            };

            return result;
        }

        public SampleResult GetAnimeMultiEpisodeSample(NamingConfig nameSpec)
        {
            var result = new SampleResult
            {
                FileName = BuildSample(_multiEpisodes, _animeSeries, _animeMultiEpisodeFile, nameSpec, _customFormats),
                Series = _animeSeries,
                Episodes = _multiEpisodes,
                EpisodeFile = _animeMultiEpisodeFile
            };

            return result;
        }

        public string GetSeriesFolderSample(NamingConfig nameSpec)
        {
            return _buildFileNames.GetSeriesFolder(_standardSeries, nameSpec);
        }

        public string GetSeasonFolderSample(NamingConfig nameSpec)
        {
            return _buildFileNames.GetSeasonFolder(_standardSeries, _episode1.SeasonNumber, nameSpec);
        }

        public string GetSpecialsFolderSample(NamingConfig nameSpec)
        {
            return _buildFileNames.GetSeasonFolder(_standardSeries, 0, nameSpec);
        }

        private string BuildSample(List<Episode> episodes, Series series, EpisodeFile episodeFile, NamingConfig nameSpec, List<CustomFormat> customFormats)
        {
            try
            {
                return _buildFileNames.BuildFileName(episodes, series, episodeFile, "", nameSpec, customFormats);
            }
            catch (NamingFormatException)
            {
                return string.Empty;
            }
        }
    }
}
