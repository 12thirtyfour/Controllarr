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
  collectionStrategy: number;
  revisionPreference: number;
  pinnedVersion: string;
  compressionFormat: string;
  trackHomebrew: boolean;
  trackRomHacks: boolean;
  trackPrototypes: boolean;
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
      collectionStrategy: 0,
      revisionPreference: 0,
      pinnedVersion: '',
      compressionFormat: 'zip',
      trackHomebrew: false,
      trackRomHacks: false,
      trackPrototypes: false,
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
