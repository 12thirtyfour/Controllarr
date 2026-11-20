using System.Collections.Generic;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Qualities
{
    public class QualityModelComparer : IComparer<Quality>, IComparer<QualityModel>
    {
        private readonly QualityProfile _profile;

        public static readonly QualityModelComparer Default = new QualityModelComparer();

        public QualityModelComparer()
        {
        }

        public QualityModelComparer(QualityProfile profile)
        {
            _profile = profile;
        }

        public int Compare(int left, int right, bool respectGroupOrder = false)
        {
            if (_profile == null || _profile.Items == null || _profile.Items.Count == 0)
            {
                return left.CompareTo(right);
            }

            var leftIndex = _profile.GetIndex(left);
            var rightIndex = _profile.GetIndex(right);

            return leftIndex.CompareTo(rightIndex, respectGroupOrder);
        }

        public int Compare(Quality left, Quality right)
        {
            return Compare(left, right, false);
        }

        public int Compare(Quality left, Quality right, bool respectGroupOrder)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (_profile == null || _profile.Items == null || _profile.Items.Count == 0)
            {
                return left.Id.CompareTo(right.Id);
            }

            var leftIndex = _profile.GetIndex(left, respectGroupOrder);
            var rightIndex = _profile.GetIndex(right, respectGroupOrder);

            return leftIndex.CompareTo(rightIndex, respectGroupOrder);
        }

        public int Compare(QualityModel left, QualityModel right)
        {
            return Compare(left, right, false);
        }

        public int Compare(QualityModel left, QualityModel right, bool respectGroupOrder)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var result = Compare(left.Quality, right.Quality, respectGroupOrder);

            if (result == 0)
            {
                result = left.Revision.CompareTo(right.Revision);
            }

            return result;
        }
    }
}
