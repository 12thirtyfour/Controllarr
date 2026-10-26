import React, { useCallback, useState } from 'react';
import Link from 'Components/Link/Link';
import GameDetailsModal from 'Game/GameDetailsModal';
import { GameEntity } from 'Game/useGame';
import FinaleType from './FinaleType';
import styles from './GameTitleLink.module.css';

interface GameTitleLinkProps {
  gameId: number;
  seriesId: number;
  gameEntity: GameEntity;
  gameTitle: string;
  finaleType?: string;
  showOpenSeriesButton: boolean;
}

function GameTitleLink(props: GameTitleLinkProps) {
  const { gameTitle, finaleType, ...otherProps } = props;
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);
  const handleLinkPress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, [setIsDetailsModalOpen]);
  const handleModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, [setIsDetailsModalOpen]);

  return (
    <div className={styles.container}>
      <Link
        className={styles.link}
        title={gameTitle}
        onPress={handleLinkPress}
      >
        {gameTitle}
      </Link>

      {finaleType ? <FinaleType finaleType={finaleType} /> : null}

      <GameDetailsModal
        isOpen={isDetailsModalOpen}
        gameTitle={gameTitle}
        {...otherProps}
        onModalClose={handleModalClose}
      />
    </div>
  );
}

export default GameTitleLink;
