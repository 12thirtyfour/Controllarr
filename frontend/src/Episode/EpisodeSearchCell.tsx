import React, { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { EpisodeEntity } from 'Episode/useEpisode';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons } from 'Helpers/Props';
import { loadLocalLibretroConfig } from 'Settings/Libretro/LibretroSettings';
import translate from 'Utilities/String/translate';
import EpisodeDetailsModal from './EpisodeDetailsModal';
import styles from './EpisodeSearchCell.module.css';

interface EpisodeSearchCellProps {
  episodeId: number;
  episodeEntity: EpisodeEntity;
  seriesId: number;
  episodeTitle: string;
  episodeFileId?: number;
  showOpenSeriesButton: boolean;
}

function EpisodeSearchCell({
  episodeId,
  episodeEntity,
  seriesId,
  episodeTitle,
  episodeFileId,
  showOpenSeriesButton,
}: EpisodeSearchCellProps) {
  const navigate = useNavigate();
  const isSearching = useCommandExecuting(CommandNames.EpisodeSearch, {
    episodeIds: [episodeId],
  });

  const executeCommand = useExecuteCommand();

  const [isDetailsModalOpen, setDetailsModalOpen, setDetailsModalClosed] =
    useModalOpenState(false);

  const handlePlayPress = useCallback(() => {
    const config = loadLocalLibretroConfig();

    if (config.preferredLaunchMode === 'protocol') {
      window.location.href = `controllarr://play/${episodeId}`;
      return;
    }

    navigate(`/webplayer?gameId=${episodeId}`);
  }, [episodeId, navigate]);

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.EpisodeSearch,
      episodeIds: [episodeId],
    });
  }, [episodeId, executeCommand]);

  return (
    <TableRowCell className={styles.episodeSearchCell}>
      {episodeFileId ? (
        <IconButton
          name={icons.SERIES_CONTINUING}
          size={14}
          title="Play Game (Libretro)"
          aria-label="Play Game (Libretro)"
          onPress={handlePlayPress}
        />
      ) : null}

      <SpinnerIconButton
        name={icons.SEARCH}
        size={14}
        isSpinning={isSearching}
        title={translate('AutomaticSearch')}
        onPress={handleSearchPress}
      />

      <IconButton
        name={icons.INTERACTIVE}
        size={14}
        title={translate('InteractiveSearch')}
        aria-label={translate('InteractiveSearch')}
        onPress={setDetailsModalOpen}
      />

      <EpisodeDetailsModal
        isOpen={isDetailsModalOpen}
        episodeId={episodeId}
        episodeEntity={episodeEntity}
        seriesId={seriesId}
        episodeTitle={episodeTitle}
        selectedTab="search"
        startInteractiveSearch={true}
        showOpenSeriesButton={showOpenSeriesButton}
        onModalClose={setDetailsModalClosed}
      />
    </TableRowCell>
  );
}

export default EpisodeSearchCell;
