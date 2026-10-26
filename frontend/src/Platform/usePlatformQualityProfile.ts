import { useQualityProfile } from 'Settings/Profiles/Quality/useQualityProfiles';
import Series from './Platform';

const usePlatformQualityProfile = (series: Series | undefined) => {
  return useQualityProfile(series?.qualityProfileId);
};

export default usePlatformQualityProfile;
