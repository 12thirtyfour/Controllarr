import ModelBase from 'App/ModelBase';
import { InteractiveImportCommandOptions } from 'InteractiveImport/InteractiveImport';

export type CommandStatus =
  | 'queued'
  | 'started'
  | 'completed'
  | 'failed'
  | 'aborted'
  | 'cancelled'
  | 'orphaned';

export type CommandResult = 'unknown' | 'successful' | 'unsuccessful';
export type CommandPriority = 'low' | 'normal' | 'high';

// Base command body with common properties
export interface BaseCommandBody {
  sendUpdatesToClient: boolean;
  updateScheduledTask: boolean;
  completionMessage: string;
  requiresDiskAccess: boolean;
  isExclusive: boolean;
  isLongRunning: boolean;
  name: string;
  lastExecutionTime: string;
  lastStartTime: string;
  trigger: string;
  suppressMessages: boolean;
}

// Specific command body interfaces
export interface SeriesCommandBody extends BaseCommandBody {
  seriesId: number;
}

export interface MultipleSeriesCommandBody extends BaseCommandBody {
  seriesIds: number[];
}

export interface SeasonCommandBody extends BaseCommandBody {
  seriesId: number;
  seasonNumber: number;
}

export interface GameCommandBody extends BaseCommandBody {
  gameIds: number[];
}

export interface SeriesGameCommandBody extends BaseCommandBody {
  seriesId: number;
  gameIds: number[];
}

export interface RenameFilesCommandBody extends BaseCommandBody {
  seriesId: number;
  files: number[];
}

export interface MovePlatformCommandBody extends BaseCommandBody {
  seriesId: number;
  destinationPath: string;
}

export interface ManualImportCommandBody extends BaseCommandBody {
  files: Array<{
    path: string;
    seriesId: number;
    gameIds: number[];
    quality: Record<string, unknown>;
    language: Record<string, unknown>;
    releaseGroup?: string;
  }>;
}

export type CommandBody =
  | SeriesCommandBody
  | MultipleSeriesCommandBody
  | SeasonCommandBody
  | GameCommandBody
  | SeriesGameCommandBody
  | RenameFilesCommandBody
  | MovePlatformCommandBody
  | ManualImportCommandBody
  | BaseCommandBody;

// Simplified interface for creating new commands
export interface NewCommandBody {
  name: string;
  priority?: CommandPriority;
  seriesId?: number;
  seriesIds?: number[];
  seasonNumber?: number;
  gameIds?: number[];
  files?: number[] | InteractiveImportCommandOptions[];
  destinationPath?: string;
  [key: string]: string | number | boolean | number[] | object | undefined;
}

export interface CommandBodyMap {
  RefreshSeries: SeriesCommandBody | MultipleSeriesCommandBody;
  SeriesSearch: SeriesCommandBody;
  PlatformCollectionSearch: SeriesCommandBody;
  SeasonSearch: SeasonCommandBody;
  GameSearch: GameCommandBody | SeriesGameCommandBody;
  MissingGameSearch: BaseCommandBody;
  CutoffUnmetGameSearch: BaseCommandBody;
  RenameFiles: RenameFilesCommandBody;
  RenameSeries: MultipleSeriesCommandBody;
  MovePlatform: MovePlatformCommandBody;
  ManualImport: ManualImportCommandBody;
  DownloadedGamesScan: SeriesCommandBody | BaseCommandBody;
  RssSync: BaseCommandBody;
  ApplicationUpdate: BaseCommandBody;
  Backup: BaseCommandBody;
  ClearBlocklist: BaseCommandBody;
  ClearLog: BaseCommandBody;
  DeleteLogFiles: BaseCommandBody;
  DeleteUpdateLogFiles: BaseCommandBody;
  RefreshMonitoredDownloads: BaseCommandBody;
  ResetApiKey: BaseCommandBody;
  ResetQualityDefinitions: BaseCommandBody;
}

export type CommandBodyForName<T extends keyof CommandBodyMap> =
  CommandBodyMap[T];

interface Command extends ModelBase {
  name: string;
  commandName: string;
  message: string;
  body: CommandBody;
  priority: CommandPriority;
  status: CommandStatus;
  result: CommandResult;
  queued: string;
  started: string;
  ended: string;
  duration: string;
  trigger: string;
  stateChangeTime: string;
  sendUpdatesToClient: boolean;
  updateScheduledTask: boolean;
  lastExecutionTime: string;
}

export default Command;
