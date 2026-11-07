import { SeriesMonitor } from 'Platform/Platform';
import translate from 'Utilities/String/translate';

interface MonitorOption {
  key: SeriesMonitor;
  value: string;
}

const monitorOptions: MonitorOption[] = [
  {
    key: 'all',
    get value() {
      return translate('MonitorAllGames');
    },
  },
  {
    key: 'future',
    get value() {
      return translate('MonitorFutureGames');
    },
  },
  {
    key: 'missing',
    get value() {
      return translate('MonitorMissingGames');
    },
  },
  {
    key: 'existing',
    get value() {
      return translate('MonitorExistingGames');
    },
  },
  {
    key: 'recent',
    get value() {
      return translate('MonitorRecentGames');
    },
  },
  {
    key: 'pilot',
    get value() {
      return translate('MonitorFirstGame');
    },
  },
  {
    key: 'firstSeason',
    get value() {
      return translate('MonitorFirstRegion');
    },
  },
  {
    key: 'lastSeason',
    get value() {
      return translate('MonitorLastRegion');
    },
  },
  {
    key: 'monitorSpecials',
    get value() {
      return translate('MonitorSpecialGames');
    },
  },
  {
    key: 'unmonitorSpecials',
    get value() {
      return translate('UnmonitorSpecialGames');
    },
  },
  {
    key: 'none',
    get value() {
      return translate('MonitorNoGames');
    },
  },
];

export default monitorOptions;
