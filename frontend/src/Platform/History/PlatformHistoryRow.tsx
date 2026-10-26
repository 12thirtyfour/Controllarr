import React, { useCallback, useMemo, useState } from 'react';
import HistoryDetails from 'Activity/History/Details/HistoryDetails';
import HistoryEventTypeCell from 'Activity/History/HistoryEventTypeCell';
import { useMarkAsFailed } from 'Activity/History/useHistory';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import GameFormats from 'Game/GameFormats';
import GameLanguages from 'Game/GameLanguages';
import GameNumber from 'Game/GameNumber';
import GameQuality from 'Game/GameQuality';
import SeasonGameNumber from 'Game/RegionGameNumber';
import useGame from 'Game/useGame';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import { useSingleSeries } from 'Platform/usePlatform';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import { HistoryData, HistoryEventType } from 'typings/History';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import styles from './PlatformHistoryRow.module.css';

interface PlatformHistoryRowProps {
  id: number;
  seriesId: number;
  gameId: number;
  eventType: HistoryEventType;
  sourceTitle: string;
  languages?: Language[];
  quality: QualityModel;
  qualityCutoffNotMet: boolean;
  customFormats?: CustomFormat[];
  date: string;
  data: HistoryData;
  downloadId?: string;
  fullSeries: boolean;
  customFormatScore: number;
}

function PlatformHistoryRow({
  id,
  seriesId,
  gameId,
  eventType,
  sourceTitle,
  languages = [],
  quality,
  qualityCutoffNotMet,
  customFormats = [],
  date,
  data,
  downloadId,
  fullSeries,
  customFormatScore,
}: PlatformHistoryRowProps) {
  const series = useSingleSeries(seriesId);
  const game = useGame(gameId, 'games');
  const { markAsFailed } = useMarkAsFailed(id, 'series');
  const [isMarkAsFailedModalOpen, setIsMarkAsFailedModalOpen] = useState(false);

  const GameComponent = fullSeries ? SeasonGameNumber : GameNumber;

  const title = useMemo(() => {
    switch (eventType) {
      case 'grabbed':
        return 'Grabbed';
      case 'seriesFolderImported':
        return 'Series Folder Imported';
      case 'downloadFolderImported':
        return 'Download Folder Imported';
      case 'downloadFailed':
        return 'Download Failed';
      case 'romFileDeleted':
        return 'Game File Deleted';
      case 'romFileRenamed':
        return 'Game File Renamed';
      default:
        return 'Unknown';
    }
  }, [eventType]);

  const handleMarkAsFailedPress = useCallback(() => {
    setIsMarkAsFailedModalOpen(true);
  }, []);

  const handleConfirmMarkAsFailed = useCallback(() => {
    markAsFailed();
    setIsMarkAsFailedModalOpen(false);
  }, [markAsFailed]);

  const handleMarkAsFailedModalClose = useCallback(() => {
    setIsMarkAsFailedModalOpen(false);
  }, []);

  if (!series || !game) {
    return null;
  }

  return (
    <TableRow>
      <HistoryEventTypeCell eventType={eventType} data={data} />

      <TableRowCell>
        <GameComponent
          seasonNumber={game.seasonNumber}
          gameNumber={game.gameNumber}
          absoluteGameNumber={game.absoluteGameNumber}
          seriesType={series.seriesType}
          alternateTitles={series.alternateTitles}
          sceneSeasonNumber={game.sceneSeasonNumber}
          sceneGameNumber={game.sceneGameNumber}
          sceneAbsoluteGameNumber={game.sceneAbsoluteGameNumber}
        />
      </TableRowCell>

      <TableRowCell className={styles.sourceTitle}>{sourceTitle}</TableRowCell>

      <TableRowCell>
        <GameLanguages languages={languages} />
      </TableRowCell>

      <TableRowCell>
        <GameQuality
          quality={quality}
          isCutoffNotMet={qualityCutoffNotMet}
        />
      </TableRowCell>

      <TableRowCell>
        <GameFormats formats={customFormats} />
      </TableRowCell>

      <TableRowCell>
        {formatCustomFormatScore(customFormatScore, customFormats.length)}
      </TableRowCell>

      <RelativeDateCell date={date} includeSeconds={true} includeTime={true} />

      <TableRowCell className={styles.actions}>
        <Popover
          anchor={<Icon name={icons.INFO} />}
          title={title}
          body={
            <HistoryDetails
              eventType={eventType}
              sourceTitle={sourceTitle}
              data={data}
              downloadId={downloadId}
            />
          }
          position={tooltipPositions.LEFT}
        />

        {eventType === 'grabbed' ? (
          <IconButton
            title={translate('MarkAsFailed')}
            aria-label={translate('MarkAsFailed')}
            name={icons.REMOVE}
            size={14}
            onPress={handleMarkAsFailedPress}
          />
        ) : null}
      </TableRowCell>

      <ConfirmModal
        isOpen={isMarkAsFailedModalOpen}
        kind={kinds.DANGER}
        title={translate('MarkAsFailed')}
        message={translate('MarkAsFailedConfirmation', { sourceTitle })}
        confirmLabel={translate('MarkAsFailed')}
        onConfirm={handleConfirmMarkAsFailed}
        onCancel={handleMarkAsFailedModalClose}
      />
    </TableRow>
  );
}

export default PlatformHistoryRow;
