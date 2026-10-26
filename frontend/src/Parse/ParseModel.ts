import ModelBase from 'App/ModelBase';
import Game from 'Game/Game';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import Series from 'Platform/Platform';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';

export interface PlatformTitleInfo {
  title: string;
  titleWithoutYear: string;
  year: number;
  allTitles: string[];
}

export interface ParsedGameInfo {
  releaseTitle: string;
  platformTitle: string;
  platformTitleInfo: PlatformTitleInfo;
  quality: QualityModel;
  seasonNumber: number | null;
  gameNumbers: number[];
  absoluteGameNumbers: number[];
  specialAbsoluteGameNumbers: number[];
  languages: Language[];
  fullSeason: boolean;
  isPartialSeason: boolean;
  isMultiSeason: boolean;
  isSeasonExtra: boolean;
  special: boolean;
  releaseHash: string;
  seasonPart: number;
  releaseGroup?: string;
  releaseTokens: string;
  airDate?: string;
  isDaily: boolean;
  isAbsoluteNumbering: boolean;
  isPossibleSpecialGame: boolean;
  isPossibleSceneSeasonSpecial: boolean;
}

export interface ParseModel extends ModelBase {
  title: string;
  parsedGameInfo: ParsedGameInfo;
  series?: Series;
  games: Game[];
  languages?: Language[];
  customFormats?: CustomFormat[];
  customFormatScore?: number;
}
