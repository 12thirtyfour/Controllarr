import { CalendarStatus } from 'typings/Calendar';
import translate from 'Utilities/String/translate';

const statusTranslationKeys: Record<CalendarStatus, string> = {
  downloaded: 'CalendarLegendGameDownloadedTooltip',
  downloading: 'CalendarLegendGameDownloadingTooltip',
  unmonitored: 'CalendarLegendGameUnmonitoredTooltip',
  onAir: 'CalendarLegendGameOnAirTooltip',
  missing: 'CalendarLegendGameMissingTooltip',
  unaired: 'CalendarLegendGameUnairedTooltip',
};

function getCalendarStatusLabel(status: CalendarStatus) {
  return translate(statusTranslationKeys[status]);
}

export default getCalendarStatusLabel;
