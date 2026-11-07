using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Tv
{
    public class Game : ModelBase, IComparable
    {
        public Game()
        {
            Images = new List<MediaCover.MediaCover>();
        }

        public const string AIR_DATE_FORMAT = "yyyy-MM-dd";

        public int SeriesId { get; set; }
        public int PlatformId { get => SeriesId; set => SeriesId = value; }
        public int TvdbId { get; set; }
        public int EpisodeFileId { get; set; }
        public int RomFileId { get => EpisodeFileId; set => EpisodeFileId = value; }
        public int SeasonNumber { get; set; }
        public int RegionNumber { get => SeasonNumber; set => SeasonNumber = value; }
        public int EpisodeNumber { get; set; }
        public int GameNumber { get => EpisodeNumber; set => EpisodeNumber = value; }
        public string Title { get; set; }
        public string AirDate { get; set; }
        public DateTime? AirDateUtc { get; set; }
        public string Overview { get; set; }
        public bool Monitored { get; set; }
        public int? AbsoluteEpisodeNumber { get; set; }
        public int? AbsoluteGameNumber { get => AbsoluteEpisodeNumber; set => AbsoluteEpisodeNumber = value; }
        public int? SceneAbsoluteEpisodeNumber { get; set; }
        public int? SceneSeasonNumber { get; set; }
        public int? SceneEpisodeNumber { get; set; }
        public bool UnverifiedSceneNumbering { get; set; }
        public Ratings Ratings { get; set; }
        public List<MediaCover.MediaCover> Images { get; set; }
        public DateTime? LastSearchTime { get; set; }
        public int Runtime { get; set; }
        public string FinaleType { get; set; }

        public string SeriesTitle { get; private set; }

        public LazyLoaded<EpisodeFile> EpisodeFile { get; set; }

        public Series Series { get; set; }

        public bool HasFile => EpisodeFileId > 0;
        public bool AbsoluteEpisodeNumberAdded { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}]{1}", Id, Title.NullSafe());
        }

        public int CompareTo(object obj)
        {
            var other = (Game)obj;

            if (SeasonNumber > other.SeasonNumber)
            {
                return 1;
            }

            if (SeasonNumber < other.SeasonNumber)
            {
                return -1;
            }

            if (EpisodeNumber > other.EpisodeNumber)
            {
                return 1;
            }

            if (EpisodeNumber < other.EpisodeNumber)
            {
                return -1;
            }

            return 0;
        }
    }

    public class Episode : Game
    {
    }
}
