import { SeriesMonitor } from 'Platform/Platform';

interface MonitorOption {
  key: SeriesMonitor;
  value: string;
}

const monitorOptions: MonitorOption[] = [
  {
    key: 'all',
    value: 'All ROMs',
  },
  {
    key: 'defaultRegion',
    value: 'Default Region',
  },
  {
    key: 'allRetail',
    value: 'All Retail',
  },
  {
    key: 'nonRetail',
    value: 'Non-Retail',
  },
  {
    key: 'none',
    value: 'None',
  },
];

export default monitorOptions;
