import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeNumber from 'Episode/EpisodeNumber';
import EpisodeSearchCell from 'Episode/EpisodeSearchCell';
import EpisodeStatus from 'Episode/EpisodeStatus';
import EpisodeTitleLink from 'Episode/EpisodeTitleLink';
import IndexerFlags from 'Episode/IndexerFlags';
import EpisodeFileLanguages from 'EpisodeFile/EpisodeFileLanguages';
import { useEpisodeFile } from 'EpisodeFile/EpisodeFileProvider';
import MediaInfo from 'EpisodeFile/MediaInfo';
import { icons } from 'Helpers/Props';
import { useSingleSeries } from 'Series/useSeries';
import MediaInfoModel from 'typings/MediaInfo';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import formatRuntime from 'Utilities/Number/formatRuntime';
import translate from 'Utilities/String/translate';
import {
  getGameAchievementSummary,
  useRetroAchievementsLibraryVersion,
} from './gameAchievementsHelper';
import RetroAchievementsShowcaseModal from './RetroAchievementsShowcaseModal';
import styles from './EpisodeRow.module.css';

interface EpisodeRowProps {
  id: number;
  seriesId: number;
  episodeFileId?: number;
  monitored: boolean;
  seasonNumber: number;
  episodeNumber: number;
  absoluteEpisodeNumber?: number;
  sceneSeasonNumber?: number;
  sceneEpisodeNumber?: number;
  sceneAbsoluteEpisodeNumber?: number;
  airDate?: string;
  airDateUtc?: string;
  runtime?: number;
  finaleType?: string;
  title: string;
  isSaving?: boolean;
  unverifiedSceneNumbering?: boolean;
  isAlternateGroup?: boolean;
  romVersionLabel?: string;
  alternateVersionCount?: number;
  isGroupExpanded?: boolean;
  groupKey?: string;
  onToggleGroupExpand?: (groupKey: string) => void;
  mediaInfo?: MediaInfoModel;
  columns: Column[];
  onMonitorEpisodePress: (
    episodeId: number,
    value: boolean,
    { shiftKey }: { shiftKey: boolean }
  ) => void;
}

function EpisodeRow({
  id,
  seriesId,
  episodeFileId,
  monitored,
  seasonNumber,
  episodeNumber,
  absoluteEpisodeNumber,
  sceneSeasonNumber,
  sceneEpisodeNumber,
  sceneAbsoluteEpisodeNumber,
  airDate,
  airDateUtc,
  runtime,
  finaleType,
  title,
  unverifiedSceneNumbering,
  isSaving,
  isAlternateGroup = false,
  romVersionLabel,
  alternateVersionCount = 0,
  isGroupExpanded = false,
  groupKey = '',
  onToggleGroupExpand,
  columns,
  onMonitorEpisodePress,
}: EpisodeRowProps) {
  const series = useSingleSeries(seriesId);
  const seriesTitle = series?.title;
  const {
    useSceneNumbering,
    seriesType,
    alternateTitles = [],
  } = series!;
  const episodeFile = useEpisodeFile(episodeFileId);
  const [isAchievementsModalOpen, setIsAchievementsModalOpen] = useState(false);

  const customFormats = episodeFile?.customFormats ?? [];
  const customFormatScore = episodeFile?.customFormatScore ?? 0;
  const effectiveVersion = romVersionLabel || finaleType || 'Retail (v1.0)';

  const raLibraryVersion = useRetroAchievementsLibraryVersion();

  const achievementSummary = useMemo(
    () =>
      getGameAchievementSummary(
        title,
        seasonNumber,
        raLibraryVersion,
        seriesTitle
      ),
    [title, seasonNumber, raLibraryVersion, seriesTitle]
  );

  const handleToggleGroupExpandPress = useCallback(() => {
    onToggleGroupExpand?.(groupKey);
  }, [groupKey, onToggleGroupExpand]);

  const handleMonitorEpisodePress = useCallback(
    (monitored: boolean, options: { shiftKey: boolean }) => {
      onMonitorEpisodePress(id, monitored, options);
    },
    [id, onMonitorEpisodePress]
  );

  const handleOpenAchievements = useCallback(() => {
    setIsAchievementsModalOpen(true);
  }, []);

  const handleCloseAchievements = useCallback(() => {
    setIsAchievementsModalOpen(false);
  }, []);

  return (
    <TableRow
      className={
        isAlternateGroup ? styles.alternateGameGroup : styles.primaryGameGroup
      }
    >
      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'monitored') {
          return (
            <TableRowCell key={name} className={styles.monitored}>
              <MonitorToggleButton
                monitored={monitored}
                isDisabled={false}
                isSaving={isSaving}
                size={14}
                onPress={handleMonitorEpisodePress}
              />
            </TableRowCell>
          );
        }

        if (name === 'episodeNumber') {
          return (
            <TableRowCell
              key={name}
              className={
                seriesType === 'anime'
                  ? styles.episodeNumberAnime
                  : styles.episodeNumber
              }
            >
              <EpisodeNumber
                seasonNumber={seasonNumber}
                episodeNumber={episodeNumber}
                absoluteEpisodeNumber={absoluteEpisodeNumber}
                useSceneNumbering={useSceneNumbering}
                unverifiedSceneNumbering={unverifiedSceneNumbering}
                seriesType={seriesType}
                sceneSeasonNumber={sceneSeasonNumber}
                sceneEpisodeNumber={sceneEpisodeNumber}
                sceneAbsoluteEpisodeNumber={sceneAbsoluteEpisodeNumber}
                alternateTitles={alternateTitles}
              />
            </TableRowCell>
          );
        }

        if (name === 'title') {
          return (
            <TableRowCell key={name} className={styles.title}>
              <EpisodeTitleLink
                episodeId={id}
                seriesId={seriesId}
                episodeTitle={title}
                episodeEntity="episodes"
                showOpenSeriesButton={false}
              />
            </TableRowCell>
          );
        }

        if (name === 'romVersion') {
          return (
            <TableRowCell key={name} className={styles.romVersion}>
              <span className={styles.versionBadge}>{effectiveVersion}</span>

              {alternateVersionCount > 0 && onToggleGroupExpand ? (
                <button
                  type="button"
                  className={styles.versionToggleBtn}
                  onClick={handleToggleGroupExpandPress}
                >
                  {isGroupExpanded
                    ? 'Hide versions'
                    : `+${alternateVersionCount} version${
                        alternateVersionCount > 1 ? 's' : ''
                      }`}
                </button>
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'achievements') {
          if (achievementSummary.tier === 'none') {
            return <TableRowCell key={name} className={styles.achievements} />;
          }

          let badgeClass = styles.achievementOutline;
          let badgeIcon = '◇';
          let badgeText =
            achievementSummary.total > 0
              ? `${achievementSummary.unlocked}/${achievementSummary.total}`
              : '0';
          let badgeTooltip =
            achievementSummary.total > 0
              ? `Achievements available (0/${achievementSummary.total} earned) — Click to view`
              : 'No achievements earned — Click to view';

          if (achievementSummary.tier === 'mastered') {
            badgeClass = styles.achievementMastered;
            badgeIcon = '👑';
            badgeText = 'Mastered';
            badgeTooltip = `Mastered! (${achievementSummary.hardcoreUnlocked}/${achievementSummary.total} Hardcore)`;
          } else if (achievementSummary.tier === 'gold') {
            badgeClass = styles.achievementGold;
            badgeIcon = '🥇';
            badgeText = `${achievementSummary.hardcoreUnlocked}/${achievementSummary.total}`;
            badgeTooltip = `Hardcore achievements earned (${achievementSummary.hardcoreUnlocked}/${achievementSummary.total})`;
          } else if (achievementSummary.tier === 'silver') {
            badgeClass = styles.achievementSilver;
            badgeIcon = '🥈';
            badgeText = `${achievementSummary.unlocked}/${achievementSummary.total}`;
            badgeTooltip = `Softcore achievements earned (${achievementSummary.unlocked}/${achievementSummary.total})`;
          }

          return (
            <TableRowCell key={name} className={styles.achievements}>
              <button
                type="button"
                title={badgeTooltip}
                className={classNames(styles.achievementBadge, badgeClass)}
                onClick={handleOpenAchievements}
              >
                <span>{badgeIcon}</span>
                <span>{badgeText}</span>
              </button>

              <RetroAchievementsShowcaseModal
                isOpen={isAchievementsModalOpen}
                gameTitle={title}
                seriesTitle={seriesTitle}
                badges={achievementSummary.badges}
                onModalClose={handleCloseAchievements}
              />
            </TableRowCell>
          );
        }

        if (name === 'path') {
          return <TableRowCell key={name}>{episodeFile?.path}</TableRowCell>;
        }

        if (name === 'relativePath') {
          return (
            <TableRowCell key={name}>{episodeFile?.relativePath}</TableRowCell>
          );
        }

        if (name === 'airDateUtc') {
          return <RelativeDateCell key={name} date={airDateUtc} airDate={airDate} />;
        }

        if (name === 'runtime') {
          return (
            <TableRowCell key={name} className={styles.runtime}>
              {formatRuntime(runtime)}
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name}>
              <EpisodeFormats formats={customFormats} />
            </TableRowCell>
          );
        }

        if (name === 'customFormatScore') {
          return (
            <TableRowCell key={name} className={styles.customFormatScore}>
              <Tooltip
                anchor={formatCustomFormatScore(
                  customFormatScore,
                  customFormats.length
                )}
                tooltip={<EpisodeFormats formats={customFormats} />}
                position="left"
              />
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name} className={styles.languages}>
              <EpisodeFileLanguages episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'audioInfo') {
          return (
            <TableRowCell key={name} className={styles.audio}>
              <MediaInfo type="audio" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'audioLanguages') {
          return (
            <TableRowCell key={name} className={styles.audioLanguages}>
              <MediaInfo type="audioLanguages" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'subtitleLanguages') {
          return (
            <TableRowCell key={name} className={styles.subtitles}>
              <MediaInfo type="subtitles" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'videoCodec') {
          return (
            <TableRowCell key={name} className={styles.video}>
              <MediaInfo type="video" episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'videoDynamicRangeType') {
          return (
            <TableRowCell key={name} className={styles.videoDynamicRangeType}>
              <MediaInfo
                type="videoDynamicRangeType"
                episodeFileId={episodeFileId}
              />
            </TableRowCell>
          );
        }

        if (name === 'size') {
          return (
            <TableRowCell key={name} className={styles.size}>
              {!!episodeFile?.size && formatBytes(episodeFile?.size)}
            </TableRowCell>
          );
        }

        if (name === 'releaseGroup') {
          return (
            <TableRowCell key={name} className={styles.releaseGroup}>
              {episodeFile?.releaseGroup}
            </TableRowCell>
          );
        }

        if (name === 'indexerFlags') {
          return (
            <TableRowCell key={name} className={styles.indexerFlags}>
              {episodeFile?.indexerFlags ? (
                <Popover
                  anchor={<Icon name={icons.FLAG} kind="default" size={14} />}
                  title={translate('IndexerFlags')}
                  body={
                    <IndexerFlags indexerFlags={episodeFile?.indexerFlags} />
                  }
                  position="left"
                />
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'status') {
          return (
            <TableRowCell key={name} className={styles.status}>
              <EpisodeStatus episodeId={id} episodeFileId={episodeFileId} />
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <EpisodeSearchCell
              key={name}
              episodeId={id}
              episodeEntity="episodes"
              seriesId={seriesId}
              episodeTitle={title}
              episodeFileId={episodeFileId}
              showOpenSeriesButton={false}
            />
          );
        }

        return null;
      })}
    </TableRow>
  );
}

export default EpisodeRow;
