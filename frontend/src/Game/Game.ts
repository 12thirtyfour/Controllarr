import ModelBase from 'App/ModelBase';
import Series from 'Platform/Platform';

interface Game extends ModelBase {
  seriesId: number;
  tvdbId: number;
  romFileId: number;
  seasonNumber: number;
  gameNumber: number;
  airDate: string;
  airDateUtc?: string;
  lastSearchTime?: string;
  runtime: number;
  absoluteGameNumber?: number;
  sceneSeasonNumber?: number;
  sceneGameNumber?: number;
  sceneAbsoluteGameNumber?: number;
  overview: string;
  title: string;
  romFile?: object;
  hasFile: boolean;
  monitored: boolean;
  grabbed?: boolean;
  unverifiedSceneNumbering: boolean;
  series?: Series;
  finaleType?: string;
}

export default Game;
