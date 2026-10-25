import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import Language from 'Language/Language';
import { usePreferredMetadataLanguage } from 'Language/useLanguages';
import { SeriesMonitor, SeriesType } from 'Series/Series';

export interface PlatformRegionItem {
  id: number;
  label: string;
  description: string;
}

export const DEFAULT_REGION_ORDER: number[] = [-1, 1, 3, 2, 4, 0, 6, 5];

export const PLATFORM_REGION_ITEMS: PlatformRegionItem[] = [
  {
    id: -1,
    label: 'BIOS',
    description: 'System firmwares, boot ROMs & verification matrix',
  },
  {
    id: 1,
    label: 'USA',
    description: 'North America / NTSC-U retail releases',
  },
  {
    id: 3,
    label: 'Japan',
    description: 'Japan / NTSC-J retail releases (e.g. Famicom, Mega Drive)',
  },
  {
    id: 2,
    label: 'Europe',
    description: 'Europe / PAL retail releases',
  },
  {
    id: 4,
    label: 'World',
    description: 'Worldwide & multi-region retail releases',
  },
  {
    id: 0,
    label: 'Unlicensed',
    description: 'Unlicensed commercial & historical releases',
  },
  {
    id: 6,
    label: 'ROM Hacks & Translations',
    description: 'Community ROM hacks & fan translations',
  },
  {
    id: 5,
    label: 'Homebrew & Aftermarket',
    description: 'Modern homebrew & indie aftermarket cartridges/discs',
  },
];

export function normalizeRegionOrder(order?: number[]): number[] {
  const result: number[] = [];
  const validIds = new Set(DEFAULT_REGION_ORDER);

  if (Array.isArray(order)) {
    for (const id of order) {
      const num = Number(id);

      if (validIds.has(num) && !result.includes(num)) {
        result.push(num);
      }
    }
  }

  for (const id of DEFAULT_REGION_ORDER) {
    if (!result.includes(id)) {
      result.push(id);
    }
  }

  return result;
}

export interface AddSeriesOptions {
  rootFolderPath: string;
  language: Language | null;
  monitor: SeriesMonitor;
  qualityProfileId: number;
  seriesType: SeriesType;
  seasonFolder: boolean;
  collectionStrategy: string;
  revisionPreference: string;
  encryptionPreference: string;
  pinnedVersion: string;
  enableCompression: boolean;
  preferEncryptedRoms: boolean;
  trackUnlicensed: boolean;
  trackHomebrew: boolean;
  trackRomHacks: boolean;
  regionOrder?: number[];
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
      encryptionPreference: 'notEncrypted',
      pinnedVersion: '',
      enableCompression: true,
      preferEncryptedRoms: false,
      trackUnlicensed: false,
      trackHomebrew: false,
      trackRomHacks: false,
      regionOrder: DEFAULT_REGION_ORDER,
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
