import React from 'react';
import usePlatformHistory from 'Activity/History/usePlatformHistory';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { icons } from 'Helpers/Props';
import formatSeason from 'Region/formatRegion';
import translate from 'Utilities/String/translate';
import PlatformHistoryRow from './PlatformHistoryRow';
import styles from './PlatformHistoryModalContent.module.css';

const columns: Column[] = [
  {
    name: 'eventType',
    label: '',
    isVisible: true,
  },
  {
    name: 'game',
    label: () => translate('Game'),
    isVisible: true,
  },
  {
    name: 'sourceTitle',
    label: () => translate('SourceTitle'),
    isVisible: true,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isVisible: true,
  },
  {
    name: 'format',
    label: () => translate('Format'),
    isVisible: true,
  },
  {
    name: 'customFormats',
    label: () => translate('CustomFormats'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormatScore',
    label: React.createElement(Icon, {
      name: icons.SCORE,
      title: () => translate('CustomFormatScore'),
    }),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'date',
    label: () => translate('Date'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isVisible: true,
  },
];

export interface PlatformHistoryModalContentProps {
  seriesId: number;
  seasonNumber?: number;
  onModalClose: () => void;
}

function PlatformHistoryModalContent({
  seriesId,
  seasonNumber,
  onModalClose,
}: PlatformHistoryModalContentProps) {
  const { isFetching, isFetched, error, data } = usePlatformHistory(
    seriesId,
    seasonNumber
  );

  const fullSeries = seasonNumber == null;
  const hasItems = !!data.length;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {seasonNumber == null
          ? translate('History')
          : translate('HistoryModalHeaderSeason', {
              season: formatSeason(seasonNumber)!,
            })}
      </ModalHeader>

      <ModalBody>
        {isFetching && !isFetched ? <LoadingIndicator /> : null}

        {!isFetching && !!error ? (
          <p className={styles.error}>{translate('HistoryLoadError')}</p>
        ) : null}

        {isFetched && !hasItems && !error ? (
          <div>{translate('NoHistory')}</div>
        ) : null}

        {isFetched && hasItems && !error ? (
          <Table columns={columns}>
            <TableBody>
              {data.map((item) => {
                return (
                  <PlatformHistoryRow
                    key={item.id}
                    fullSeries={fullSeries}
                    {...item}
                  />
                );
              })}
            </TableBody>
          </Table>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default PlatformHistoryModalContent;
