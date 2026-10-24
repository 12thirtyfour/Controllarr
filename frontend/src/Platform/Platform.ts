import Series, {
  AlternateTitle,
  CoverType,
  Image,
  MonitorNewItems,
  Ratings,
  Season as Region,
  SeasonType as RegionType,
  SeriesAddOptions as PlatformAddOptions,
  SeriesMonitor as PlatformMonitor,
  SeriesStatus as PlatformStatus,
  SeriesType as PlatformType,
  Statistics,
  Translation,
} from 'Series/Series';

export type {
  AlternateTitle,
  CoverType,
  Image,
  MonitorNewItems,
  PlatformAddOptions,
  PlatformMonitor,
  PlatformStatus,
  PlatformType,
  Ratings,
  Region,
  RegionType,
  Statistics,
  Translation,
};

type Platform = Series;

export default Platform;
