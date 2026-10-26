import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import SeasonGameNumber from 'Game/RegionGameNumber';
import Game from 'Game/Game';
import GameSearchCell from 'Game/GameSearchCell';
import GameStatus from 'Game/GameStatus';
import GameTitleLink from 'Game/GameTitleLink';
import PlatformTitleLink from 'Platform/PlatformTitleLink';
import { useSinglePlatform } from 'Platform/usePlatform';
import RomFileLanguages from 'RomFile/RomFileLanguages';
import RomFileQuality from 'RomFile/RomFileQuality';
import { SelectStateInputProps } from 'typings/props';
import styles from './CutoffUnmetRow.module.css';

interface CutoffUnmetRowProps {
  id: number;
  seriesId: number;
  romFileId?: number;
  seasonNumber: number;
  gameNumber: number;
  absoluteGameNumber?: number;
  sceneSeasonNumber?: number;
  sceneGameNumber?: number;
  sceneAbsoluteGameNumber?: number;
  unverifiedSceneNumbering: boolean;
  airDate?: string;
  airDateUtc?: string;
  lastSearchTime?: string;
  title: string;
  columns: Column[];
}

function CutoffUnmetRow({
  id,
  seriesId,
  romFileId,
  seasonNumber,
  gameNumber,
  absoluteGameNumber,
  sceneSeasonNumber,
  sceneGameNumber,
  sceneAbsoluteGameNumber,
  unverifiedSceneNumbering,
  airDate,
  airDateUtc,
  lastSearchTime,
  title,
  columns,
}: CutoffUnmetRowProps) {
  const platform = useSinglePlatform(seriesId);
  const { toggleSelected, useIsSelected } = useSelect<Game>();
  const isSelected = useIsSelected(id);

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [toggleSelected]
  );

  if (!platform || !romFileId) {
    return null;
  }

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={handleSelectedChange}
      />

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
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
            <TableRowCell key={name} className={styles.game}>
              <SeasonGameNumber
                seasonNumber={seasonNumber}
                gameNumber={gameNumber}
                absoluteGameNumber={absoluteGameNumber}
                seriesType={platform.seriesType}
                alternateTitles={platform.alternateTitles}
                sceneSeasonNumber={sceneSeasonNumber}
                sceneGameNumber={sceneGameNumber}
                sceneAbsoluteGameNumber={sceneAbsoluteGameNumber}
                unverifiedSceneNumbering={unverifiedSceneNumbering}
              />
            </TableRowCell>
          );
        }

        if (name === 'games.title') {
          return (
            <TableRowCell key={name}>
              <GameTitleLink
                gameId={id}
                seriesId={platform.id}
                gameEntity="wanted.cutoffUnmet"
                gameTitle={title}
                showOpenSeriesButton={true}
              />
            </TableRowCell>
          );
        }

        if (name === 'games.airDateUtc') {
          return <RelativeDateCell key={name} date={airDateUtc} airDate={airDate} />;
        }

        if (name === 'games.lastSearchTime') {
          return (
            <RelativeDateCell
              key={name}
              date={lastSearchTime}
              includeSeconds={true}
            />
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name} className={styles.languages}>
              <RomFileLanguages romFileId={romFileId} />
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name}>
              <RomFileQuality romFileId={romFileId} />
            </TableRowCell>
          );
        }

        if (name === 'status') {
          return (
            <TableRowCell key={name} className={styles.status}>
              <GameStatus
                gameId={id}
                romFileId={romFileId}
                gameEntity="wanted.cutoffUnmet"
              />
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <GameSearchCell
              key={name}
              gameId={id}
              seriesId={platform.id}
              gameTitle={title}
              gameEntity="wanted.cutoffUnmet"
              showOpenSeriesButton={true}
            />
          );
        }

        return null;
      })}
    </TableRow>
  );
}

export default CutoffUnmetRow;
