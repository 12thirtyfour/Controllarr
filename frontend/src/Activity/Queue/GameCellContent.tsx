import React from 'react';
import Game from 'Game/Game';
import SeasonGameNumber from 'Game/RegionGameNumber';
import Series from 'Platform/Platform';
import translate from 'Utilities/String/translate';

interface GameCellContentProps {
  games: Game[];
  isFullSeason: boolean;
  seasonNumber?: number;
  series?: Series;
}

export default function GameCellContent({
  games,
  isFullSeason,
  seasonNumber,
  series,
}: GameCellContentProps) {
  if (games.length === 0) {
    return '-';
  }

  if (isFullSeason && seasonNumber != null) {
    return translate('SeasonNumberToken', { seasonNumber });
  }

  if (games.length === 1) {
    const game = games[0];

    return (
      <SeasonGameNumber
        seasonNumber={game.seasonNumber}
        gameNumber={game.gameNumber}
        absoluteGameNumber={game.absoluteGameNumber}
        seriesType={series?.seriesType}
        alternateTitles={series?.alternateTitles}
        sceneSeasonNumber={game.sceneSeasonNumber}
        sceneGameNumber={game.sceneGameNumber}
        sceneAbsoluteGameNumber={game.sceneAbsoluteGameNumber}
        unverifiedSceneNumbering={game.unverifiedSceneNumbering}
      />
    );
  }

  const firstGame = games[0];
  const lastGame = games[games.length - 1];

  return (
    <>
      <SeasonGameNumber
        seasonNumber={firstGame.seasonNumber}
        gameNumber={firstGame.gameNumber}
        absoluteGameNumber={firstGame.absoluteGameNumber}
        seriesType={series?.seriesType}
        alternateTitles={series?.alternateTitles}
        sceneSeasonNumber={firstGame.sceneSeasonNumber}
        sceneGameNumber={firstGame.sceneGameNumber}
        sceneAbsoluteGameNumber={firstGame.sceneAbsoluteGameNumber}
        unverifiedSceneNumbering={firstGame.unverifiedSceneNumbering}
      />
      {' - '}
      <SeasonGameNumber
        seasonNumber={lastGame.seasonNumber}
        gameNumber={lastGame.gameNumber}
        absoluteGameNumber={lastGame.absoluteGameNumber}
        seriesType={series?.seriesType}
        alternateTitles={series?.alternateTitles}
        sceneSeasonNumber={lastGame.sceneSeasonNumber}
        sceneGameNumber={lastGame.sceneGameNumber}
        sceneAbsoluteGameNumber={lastGame.sceneAbsoluteGameNumber}
        unverifiedSceneNumbering={lastGame.unverifiedSceneNumbering}
      />
    </>
  );
}
