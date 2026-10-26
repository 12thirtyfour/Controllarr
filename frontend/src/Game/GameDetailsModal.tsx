import React, { useCallback, useState } from 'react';
import Modal from 'Components/Modal/Modal';
import GameDetailsTab from 'Game/GameDetailsTab';
import { GameEntity } from 'Game/useGame';
import { sizes } from 'Helpers/Props';
import GameDetailsModalContent from './GameDetailsModalContent';

interface GameDetailsModalProps {
  isOpen: boolean;
  gameId: number;
  gameEntity: GameEntity;
  seriesId: number;
  gameTitle: string;
  isSaving?: boolean;
  showOpenSeriesButton?: boolean;
  selectedTab?: GameDetailsTab;
  startInteractiveSearch?: boolean;
  onModalClose(): void;
}

function GameDetailsModal(props: GameDetailsModalProps) {
  const { selectedTab, isOpen, onModalClose, ...otherProps } = props;

  const [closeOnBackgroundClick, setCloseOnBackgroundClick] = useState(
    selectedTab !== 'search'
  );

  const handleTabChange = useCallback(
    (isSearch: boolean) => {
      setCloseOnBackgroundClick(!isSearch);
    },
    [setCloseOnBackgroundClick]
  );

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      closeOnBackgroundClick={closeOnBackgroundClick}
      onModalClose={onModalClose}
    >
      <GameDetailsModalContent
        {...otherProps}
        selectedTab={selectedTab}
        onTabChange={handleTabChange}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default GameDetailsModal;
