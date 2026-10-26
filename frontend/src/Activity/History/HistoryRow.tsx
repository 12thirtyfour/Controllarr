import React, { useCallback, useState } from 'react';
import IconButton from 'Components/Link/IconButton';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Tooltip from 'Components/Tooltip/Tooltip';
import gameEntities from 'Game/gameEntities';
import SeasonGameNumber from 'Game/RegionGameNumber';
import GameFormats from 'Game/GameFormats';
import GameLanguages from 'Game/GameLanguages';
import GameQuality from 'Game/GameQuality';
import GameTitleLink from 'Game/GameTitleLink';
import useGame from 'Game/useGame';
import { icons, tooltipPositions } from 'Helpers/Props';
import Language from 'Language/Language';
import PlatformTitleLink from 'Platform/PlatformTitleLink';
import { useSinglePlatform } from 'Platform/usePlatform';
import { QualityModel } from 'Quality/Quality';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import { HistoryData, HistoryEventType } from 'typings/History';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import HistoryDetailsModal from './Details/HistoryDetailsModal';
import HistoryEventTypeCell from './HistoryEventTypeCell';
import styles from './HistoryRow.module.css';

interface HistoryRowProps {
  id: number;
  gameId: number;
  seriesId: number;
  languages: Language[];
  quality: QualityModel;
  customFormats?: CustomFormat[];
  customFormatScore: number;
  qualityCutoffNotMet: boolean;
  eventType: HistoryEventType;
  sourceTitle: string;
  date: string;
  data: HistoryData;
  downloadId?: string;
  isMarkingAsFailed?: boolean;
  markAsFailedError?: object;
  columns: Column[];
}

function HistoryRow(props: HistoryRowProps) {
  const {
    id,
    gameId,
    seriesId,
    languages,
    quality,
    customFormats = [],
    customFormatScore,
    qualityCutoffNotMet,
    eventType,
    sourceTitle,
    date,
    data,
    downloadId,
    columns,
  } = props;

  const platform = useSinglePlatform(seriesId);
  const game = useGame(gameId, 'games');

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const handleDetailsPress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, [setIsDetailsModalOpen]);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, [setIsDetailsModalOpen]);

  if (!platform || !game) {
    return null;
  }

  return (
    <TableRow>
      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'eventType') {
          return (
            <HistoryEventTypeCell
              key={name}
              eventType={eventType}
              data={data}
            />
          );
        }

        if (name === 'series.sortTitle') {
          return (
            <TableRowCell key={name}>
              <PlatformTitleLink
                titleSlug={platform.titleSlug}
                title={platform.title}
              />
            </TableRowCell>
          );
        }

        if (name === 'game') {
          return (
            <TableRowCell key={name}>
              <SeasonGameNumber
                seasonNumber={game.seasonNumber}
                gameNumber={game.gameNumber}
                absoluteGameNumber={game.absoluteGameNumber}
                seriesType={platform.seriesType}
                alternateTitles={platform.alternateTitles}
                sceneSeasonNumber={game.sceneSeasonNumber}
                sceneGameNumber={game.sceneGameNumber}
                sceneAbsoluteGameNumber={game.sceneAbsoluteGameNumber}
              />
            </TableRowCell>
          );
        }

        if (name === 'games.title') {
          return (
            <TableRowCell key={name}>
              <GameTitleLink
                gameId={gameId}
                gameEntity={gameEntities.EPISODES}
                seriesId={platform.id}
                gameTitle={game.title}
                showOpenSeriesButton={true}
              />
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name}>
              <GameLanguages languages={languages} />
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name}>
              <GameQuality
                quality={quality}
                isCutoffNotMet={qualityCutoffNotMet}
              />
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name}>
              <GameFormats formats={customFormats} />
            </TableRowCell>
          );
        }

        if (name === 'date') {
          return <RelativeDateCell key={name} date={date} />;
        }

        if (name === 'downloadClient') {
          const downloadClientName =
            'downloadClientName' in data ? data.downloadClientName : null;
          const downloadClient =
            'downloadClient' in data ? data.downloadClient : null;

          return (
            <TableRowCell key={name} className={styles.downloadClient}>
              {downloadClientName ?? downloadClient ?? ''}
            </TableRowCell>
          );
        }

        if (name === 'indexer') {
          return (
            <TableRowCell key={name} className={styles.indexer}>
              {'indexer' in data ? data.indexer : ''}
            </TableRowCell>
          );
        }

        if (name === 'customFormatScore') {
          return (
            <TableRowCell key={name} className={styles.customFormatScore}>
              <Tooltip
                anchor={formatCustomFormatScore(
                  customFormatScore,
                  customFormats.length
                )}
                tooltip={<GameFormats formats={customFormats} />}
                position={tooltipPositions.BOTTOM}
              />
            </TableRowCell>
          );
        }

        if (name === 'releaseGroup') {
          return (
            <TableRowCell key={name} className={styles.releaseGroup}>
              {'releaseGroup' in data ? data.releaseGroup : ''}
            </TableRowCell>
          );
        }

        if (name === 'sourceTitle') {
          return <TableRowCell key={name}>{sourceTitle}</TableRowCell>;
        }

        if (name === 'details') {
          return (
            <TableRowCell key={name} className={styles.details}>
              <IconButton
                name={icons.INFO}
                aria-label={translate('Details')}
                onPress={handleDetailsPress}
              />
            </TableRowCell>
          );
        }

        return null;
      })}

      <HistoryDetailsModal
        id={id}
        isOpen={isDetailsModalOpen}
        eventType={eventType}
        sourceTitle={sourceTitle}
        data={data}
        downloadId={downloadId}
        onModalClose={handleDetailsModalClose}
      />
    </TableRow>
  );
}

export default HistoryRow;
