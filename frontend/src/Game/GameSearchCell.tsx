import React, { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { GameEntity } from 'Game/useGame';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons } from 'Helpers/Props';
import { loadLocalLibretroConfig } from 'Settings/Libretro/LibretroSettings';
import translate from 'Utilities/String/translate';
import GameDetailsModal from './GameDetailsModal';
import styles from './GameSearchCell.module.css';

interface GameSearchCellProps {
  gameId: number;
  gameEntity: GameEntity;
  seriesId: number;
  gameTitle: string;
  romFileId?: number;
  showOpenSeriesButton: boolean;
}

function GameSearchCell({
  gameId,
  gameEntity,
  seriesId,
  gameTitle,
  romFileId,
  showOpenSeriesButton,
}: GameSearchCellProps) {
  const navigate = useNavigate();
  const isSearching = useCommandExecuting(CommandNames.GameSearch, {
    gameIds: [gameId],
  });

  const executeCommand = useExecuteCommand();

  const [isDetailsModalOpen, setDetailsModalOpen, setDetailsModalClosed] =
    useModalOpenState(false);

  const handlePlayPress = useCallback(() => {
    const config = loadLocalLibretroConfig();

    if (config.preferredLaunchMode === 'protocol') {
      window.location.href = `controllarr://play/${gameId}`;
      return;
    }

    navigate(`/webplayer?gameId=${gameId}`);
  }, [gameId, navigate]);

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.GameSearch,
      gameIds: [gameId],
    });
  }, [gameId, executeCommand]);

  return (
    <TableRowCell className={styles.gameSearchCell}>
      {romFileId ? (
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

      <GameDetailsModal
        isOpen={isDetailsModalOpen}
        gameId={gameId}
        gameEntity={gameEntity}
        seriesId={seriesId}
        gameTitle={gameTitle}
        selectedTab="search"
        startInteractiveSearch={true}
        showOpenSeriesButton={showOpenSeriesButton}
        onModalClose={setDetailsModalClosed}
      />
    </TableRowCell>
  );
}

export default GameSearchCell;
