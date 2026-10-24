import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import Language from 'Language/Language';
import { usePreferredMetadataLanguage } from 'Language/useLanguages';
import { SeriesMonitor, SeriesType } from 'Series/Series';

export interface AddSeriesOptions {
  rootFolderPath: string;
  language: Language | null;
  monitor: SeriesMonitor;
  qualityProfileId: number;
  seriesType: SeriesType;
  seasonFolder: boolean;
  collectionStrategy: string;
  revisionPreference: string;
  pinnedVersion: string;
  enableCompression: boolean;
  trackUnlicensed: boolean;
  trackHomebrew: boolean;
  trackRomHacks: boolean;
  searchForMissingEpisodes: boolean;
  searchForCutoffUnmetEpisodes: boolean;
  tags: number[];
}

const { useOptions, useOption, setOption } =
  createOptionsStore<AddSeriesOptions>('add_series_options', () => {
    return {
      rootFolderPath: '',
      language: null,
      monitor: 'all',
      qualityProfileId: 1,
      seriesType: 'standard',
      seasonFolder: true,
      collectionStrategy: 'oneGameOneRom',
      revisionPreference: 'latestRevision',
      pinnedVersion: '',
      enableCompression: true,
      trackUnlicensed: false,
      trackHomebrew: false,
      trackRomHacks: false,
      searchForMissingEpisodes: false,
      searchForCutoffUnmetEpisodes: false,
      tags: [],
    };
  });

export const useAddSeriesOptions = useOptions;
export const useAddSeriesOption = useOption;
export const setAddSeriesOption = setOption;

export const useAddSeriesLanguage = () => {
  const language = useAddSeriesOption('language');
  const preferredLanguage = usePreferredMetadataLanguage();

  return language ?? preferredLanguage;
};
