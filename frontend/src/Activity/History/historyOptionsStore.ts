import React from 'react';
import Icon from 'Components/Icon';
import {
  createOptionsStore,
  PageableOptions,
} from 'Helpers/Hooks/useOptionsStore';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

export type HistoryOptions = PageableOptions;

const { useOptions, useOption, setOptions, setOption, setSort } =
  createOptionsStore<HistoryOptions>('history_options_v2', () => {
    return {
      pageSize: 20,
      selectedFilterKey: 'all',
      sortKey: 'time',
      sortDirection: 'descending',
      columns: [
        {
          name: 'eventType',
          label: '',
          columnLabel: () => translate('EventType'),
          isVisible: true,
          isModifiable: 'onlyPosition',
        },
        {
          name: 'series.sortTitle',
          label: () => translate('SeriesSingular'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'game',
          label: () => translate('Game'),
          isVisible: true,
        },
        {
          name: 'games.title',
          label: () => translate('GameTitle'),
          isVisible: true,
        },
        {
          name: 'languages',
          label: () => translate('Languages'),
          isVisible: false,
        },
        {
          name: 'format',
          label: () => translate('Format'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'customFormats',
          label: () => translate('Formats'),
          isSortable: false,
          isVisible: true,
        },
        {
          name: 'date',
          label: () => translate('Date'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'downloadClient',
          label: () => translate('DownloadClient'),
          isVisible: false,
        },
        {
          name: 'indexer',
          label: () => translate('Indexer'),
          isVisible: false,
        },
        {
          name: 'releaseGroup',
          label: () => translate('ReleaseGroup'),
          isVisible: false,
        },
        {
          name: 'sourceTitle',
          label: () => translate('SourceTitle'),
          isVisible: false,
        },
        {
          name: 'customFormatScore',
          columnLabel: () => translate('CustomFormatScore'),
          label: React.createElement(Icon, {
            name: icons.SCORE,
            title: () => translate('CustomFormatScore'),
          }),
          isVisible: false,
        },
        {
          name: 'details',
          label: '',
          columnLabel: () => translate('Details'),
          isVisible: true,
          isModifiable: 'onlyPosition',
        },
      ],
    };
  });

export const useHistoryOptions = useOptions;
export const setHistoryOptions = setOptions;
export const useHistoryOption = useOption;
export const setHistoryOption = setOption;
export const setHistorySort = setSort;
