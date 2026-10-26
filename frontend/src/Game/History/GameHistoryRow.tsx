import React, { useCallback, useState } from 'react';
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
import GameQuality from 'Game/GameQuality';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import { HistoryData, HistoryEventType } from 'typings/History';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import styles from './GameHistoryRow.module.css';

function getTitle(eventType: HistoryEventType) {
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
}

interface GameHistoryRowProps {
  id: number;
  eventType: HistoryEventType;
  sourceTitle: string;
  languages: Language[];
  quality: QualityModel;
  qualityCutoffNotMet: boolean;
  customFormats: CustomFormat[];
  customFormatScore: number;
  date: string;
  data: HistoryData;
  downloadId?: string;
}

function GameHistoryRow({
  id,
  eventType,
  sourceTitle,
  languages,
  quality,
  qualityCutoffNotMet,
  customFormats,
  customFormatScore,
  date,
  data,
  downloadId,
}: GameHistoryRowProps) {
  const [isMarkAsFailedModalOpen, setIsMarkAsFailedModalOpen] = useState(false);
  const { markAsFailed } = useMarkAsFailed(id, 'game');

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

  return (
    <TableRow>
      <HistoryEventTypeCell eventType={eventType} data={data} />

      <TableRowCell>{sourceTitle}</TableRowCell>

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
          title={getTitle(eventType)}
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

        {eventType === 'grabbed' && (
          <IconButton
            title={translate('MarkAsFailed')}
            aria-label={translate('MarkAsFailed')}
            name={icons.REMOVE}
            size={14}
            onPress={handleMarkAsFailedPress}
          />
        )}
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

export default GameHistoryRow;
