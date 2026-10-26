import { createElement } from 'react';
import Icon from 'Components/Icon';
import Column from 'Components/Table/Column';
import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import { icons } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import translate from 'Utilities/String/translate';

interface GameSelectOptions {
  sortKey: string;
  sortDirection: SortDirection;
  columns: Column[];
}

const { useOptions, useOption, setOptions, setOption, setSort } =
  createOptionsStore<GameSelectOptions>('rom_game_options_v3', () => {
    return {
      sortKey: 'title',
      sortDirection: 'ascending',
      columns: [
        {
          name: 'monitored',
          label: '',
          columnLabel: () => translate('Monitored'),
          isVisible: true,
          isModifiable: 'disabled',
        },
        {
          name: 'gameNumber',
          label: '#',
          isVisible: false,
          isSortable: true,
        },
        {
          name: 'title',
          label: () => translate('GameTitle'),
          isVisible: true,
          isSortable: true,
        },
        {
          name: 'romVersion',
          label: 'Version / Type',
          isVisible: true,
          isSortable: false,
        },
        {
          name: 'achievements',
          label: 'Achievements',
          isVisible: true,
          isSortable: false,
        },
        {
          name: 'path',
          label: () => translate('Path'),
          isVisible: false,
          isSortable: true,
        },
        {
          name: 'relativePath',
          label: () => translate('RelativePath'),
          isVisible: false,
          isSortable: true,
        },
        {
          name: 'airDateUtc',
          label: () => translate('AirDate'),
          isVisible: true,
          isSortable: true,
        },
        {
          name: 'runtime',
          label: () => translate('Runtime'),
          isVisible: false,
          isSortable: true,
        },
        {
          name: 'languages',
          label: () => translate('Languages'),
          isVisible: false,
        },
        {
          name: 'audioInfo',
          label: () => translate('AudioInfo'),
          isVisible: false,
        },
        {
          name: 'videoCodec',
          label: () => translate('VideoCodec'),
          isVisible: false,
        },
        {
          name: 'videoDynamicRangeType',
          label: () => translate('VideoDynamicRange'),
          isVisible: false,
        },
        {
          name: 'audioLanguages',
          label: () => translate('AudioLanguages'),
          isVisible: false,
        },
        {
          name: 'subtitleLanguages',
          label: () => translate('SubtitleLanguages'),
          isVisible: false,
        },
        {
          name: 'size',
          label: () => translate('Size'),
          isVisible: true,
          isSortable: true,
        },
        {
          name: 'releaseGroup',
          label: () => translate('ReleaseGroup'),
          isVisible: false,
        },
        {
          name: 'customFormats',
          label: () => translate('Formats'),
          isVisible: false,
        },
        {
          name: 'customFormatScore',
          columnLabel: () => translate('CustomFormatScore'),
          label: createElement(Icon, {
            name: icons.SCORE,
            title: () => translate('CustomFormatScore'),
          }),
          isVisible: false,
          isSortable: true,
        },
        {
          name: 'indexerFlags',
          columnLabel: () => translate('IndexerFlags'),
          label: createElement(Icon, {
            name: icons.FLAG,
            title: () => translate('IndexerFlags'),
          }),
          isVisible: false,
        },
        {
          name: 'status',
          label: () => translate('Status'),
          isVisible: true,
        },
        {
          name: 'actions',
          label: '',
          columnLabel: () => translate('Actions'),
          isVisible: true,
          isModifiable: 'disabled',
        },
      ],
    };
  });

export const useGameOptions = useOptions;
export const setGameOptions = setOptions;
export const useGameOption = useOption;
export const setGameOption = setOption;
export const setGameSort = setSort;
