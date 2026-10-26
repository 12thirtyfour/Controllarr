import ModelBase from 'App/ModelBase';
import ReleaseType from 'InteractiveImport/ReleaseType';
import Language from 'Language/Language';
import Quality from 'Quality/Quality';

export type SeriesType = 'anime' | 'daily' | 'standard';
export type SeriesMonitor =
  | 'all'
  | 'future'
  | 'missing'
  | 'existing'
  | 'recent'
  | 'pilot'
  | 'firstSeason'
  | 'lastSeason'
  | 'monitorSpecials'
  | 'unmonitorSpecials'
  | 'none';

export type PlatformStatus = 'continuing' | 'ended' | 'upcoming' | 'deleted';

export type MonitorNewItems = 'all' | 'none';

export type CoverType = 'poster' | 'banner' | 'fanart' | 'season';

export interface Image {
  coverType: CoverType;
  url: string;
  remoteUrl: string;
}

export interface Statistics {
  seasonCount: number;
  gameCount: number;
  romFileCount: number;
  percentOfGames: number;
  previousAiring?: Date;
  releaseGroups: string[];
  releaseTypes: ReleaseType[];
  romFileQualities: Quality[];
  sizeOnDisk: number;
  totalGameCount: number;
  monitoredGameCount: number;
}

export interface Season {
  monitored: boolean;
  seasonNumber: number;
  statistics: Statistics;
  title?: string;
}

export interface Ratings {
  votes: number;
  value: number;
}

export interface AlternateTitle {
  seasonNumber: number;
  sceneSeasonNumber?: number;
  title: string;
  sceneOrigin: 'unknown' | 'unknown:tvdb' | 'mixed' | 'tvdb';
  comment?: string;
}

export interface SeriesAddOptions {
  monitor: SeriesMonitor;
  searchForMissingGames: boolean;
  searchForCutoffUnmetGames: boolean;
}

export interface Translation {
  language: Language;
  title: string;
  overview: string;
}

export interface SeasonType {
  name: string;
  type: string;
  seasonNumbers: number[];
  gameCount: number;
}

interface Series extends ModelBase {
  added: string;
  alternateTitles: AlternateTitle[];
  certification: string;
  cleanTitle: string;
  ended: boolean;
  firstAired?: string;
  lastAired?: string;
  genres: string[];
  images: Image[];
  imdbId?: string;
  monitored: boolean;
  monitorNewItems: MonitorNewItems;
  collectionStrategy: string | number;
  revisionPreference: string | number;
  encryptionPreference?: string | number;
  preferredArchiveFormat?: string | number;
  preferEncryptedRoms?: boolean;
  pinnedVersion?: string;
  network: string;
  originalCountry: string;
  originalLanguage: Language;
  language: Language;
  overview: string;
  path: string;
  pendingPath?: string;
  previousAiring?: string;
  nextAiring?: string;
  qualityProfileId: number;
  ratings: Ratings;
  rootFolderPath: string;
  runtime: number;
  seasonFolder: boolean;
  seasons: Season[];
  seriesType: SeriesType;
  sortTitle: string;
  statistics?: Statistics;
  status: PlatformStatus;
  tags: number[];
  title: string;
  originalTitle?: string;
  titleSlug: string;
  tvdbId: number;
  tvMazeId: number;
  tvRageId: number;
  tmdbId: number;
  useSceneNumbering: boolean;
  seasonType: string;
  seasonTypes: SeasonType[];
  translations: Translation[];
  year: number;
  addOptions: SeriesAddOptions;
}

export default Series;
