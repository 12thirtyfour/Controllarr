import { kinds } from 'Helpers/Props';
import { PlatformStatus } from 'Platform/Platform';

function getProgressBarKind(
  _status: PlatformStatus,
  monitored: boolean,
  progress: number,
  isDownloading: boolean
) {
  if (isDownloading) {
    return kinds.PURPLE;
  }

  if (progress === 100) {
    return kinds.SUCCESS;
  }

  if (monitored) {
    return kinds.DANGER;
  }

  return kinds.WARNING;
}

export default getProgressBarKind;
