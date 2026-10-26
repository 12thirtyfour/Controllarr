import React from 'react';
import Popover from 'Components/Tooltip/Popover';
import Game from 'Game/Game';
import GameTitleLink from 'Game/GameTitleLink';
import Series from 'Platform/Platform';
import translate from 'Utilities/String/translate';
import styles from './GameTitleCellContent.module.css';

interface GameTitleCellContentProps {
  games: Game[];
  series?: Series;
  fallbackTitle?: string;
}

export default function GameTitleCellContent({
  games,
  series,
  fallbackTitle,
}: GameTitleCellContentProps) {
  if (games.length === 0 || !series) {
    return <span title={fallbackTitle}>{fallbackTitle || '-'}</span>;
  }

  if (games.length === 1) {
    const game = games[0];

    return (
      <GameTitleLink
        gameId={game.id}
        seriesId={series.id}
        gameTitle={game.title}
        gameEntity="games"
        showOpenSeriesButton={true}
      />
    );
  }

  return (
    <Popover
      anchor={
        <span className={styles.multiple}>{translate('MultipleGames')}</span>
      }
      title={translate('GameTitles')}
      body={
        <>
          {games.map((game) => {
            return (
              <div key={game.id} className={styles.row}>
                <div className={styles.gameNumber}>
                  {game.gameNumber}
                </div>

                <GameTitleLink
                  gameId={game.id}
                  seriesId={series.id}
                  gameTitle={game.title}
                  gameEntity="games"
                  showOpenSeriesButton={true}
                />
              </div>
            );
          })}
        </>
      }
      position="right"
    />
  );
}
