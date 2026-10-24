import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import SeasonEpisodeNumber from 'Episode/SeasonEpisodeNumber';
import Game from 'Game/Game';
import GameSearchCell from 'Game/GameSearchCell';
import GameStatus from 'Game/GameStatus';
import GameTitleLink from 'Game/GameTitleLink';
import PlatformTitleLink from 'Platform/PlatformTitleLink';
import { useSinglePlatform } from 'Platform/usePlatform';
import { SelectStateInputProps } from 'typings/props';
import styles from './MissingRow.module.css';

interface MissingRowProps {
  id: number;
  seriesId: number;
  episodeFileId?: number;
  seasonNumber: number;
  episodeNumber: number;
  absoluteEpisodeNumber?: number;
  sceneSeasonNumber?: number;
  sceneEpisodeNumber?: number;
  sceneAbsoluteEpisodeNumber?: number;
  unverifiedSceneNumbering: boolean;
  airDateUtc?: string;
  lastSearchTime?: string;
  title: string;
  columns: Column[];
}

function MissingRow({
  id,
  seriesId,
  episodeFileId = 0,
  seasonNumber,
  episodeNumber,
  absoluteEpisodeNumber,
  sceneSeasonNumber,
  sceneEpisodeNumber,
  sceneAbsoluteEpisodeNumber,
  unverifiedSceneNumbering,
  airDateUtc,
  lastSearchTime,
  title,
  columns,
}: MissingRowProps) {
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

  if (!platform) {
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

        if (name === 'episode') {
          return (
            <TableRowCell key={name} className={styles.episode}>
              <SeasonEpisodeNumber
                seasonNumber={seasonNumber}
                episodeNumber={episodeNumber}
                absoluteEpisodeNumber={absoluteEpisodeNumber}
                seriesType={platform.seriesType}
                alternateTitles={platform.alternateTitles}
                sceneSeasonNumber={sceneSeasonNumber}
                sceneEpisodeNumber={sceneEpisodeNumber}
                sceneAbsoluteEpisodeNumber={sceneAbsoluteEpisodeNumber}
                unverifiedSceneNumbering={unverifiedSceneNumbering}
              />
            </TableRowCell>
          );
        }

        if (name === 'episodes.title') {
          return (
            <TableRowCell key={name}>
              <GameTitleLink
                episodeId={id}
                seriesId={platform.id}
                episodeEntity="wanted.missing"
                episodeTitle={title}
                showOpenSeriesButton={true}
              />
            </TableRowCell>
          );
        }

        if (name === 'episodes.airDateUtc') {
          return <RelativeDateCell key={name} date={airDateUtc} />;
        }

        if (name === 'episodes.lastSearchTime') {
          return (
            <RelativeDateCell
              key={name}
              date={lastSearchTime}
              includeSeconds={true}
            />
          );
        }

        if (name === 'status') {
          return (
            <TableRowCell key={name} className={styles.status}>
              <GameStatus
                episodeId={id}
                episodeFileId={episodeFileId}
                episodeEntity="wanted.missing"
              />
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <GameSearchCell
              key={name}
              episodeId={id}
              seriesId={platform.id}
              episodeTitle={title}
              episodeEntity="wanted.missing"
              showOpenSeriesButton={true}
            />
          );
        }

        return null;
      })}
    </TableRow>
  );
}

export default MissingRow;
