import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import GameFormats from 'Game/GameFormats';
import GameSearchCell from 'Game/GameSearchCell';
import GameTitleLink from 'Game/GameTitleLink';
import IndexerFlags from 'Game/IndexerFlags';
import { icons, kinds } from 'Helpers/Props';
import { useSingleSeries } from 'Platform/usePlatform';
import RomFileLanguages from 'RomFile/RomFileLanguages';
import { useRomFile } from 'RomFile/RomFileProvider';
import { loadLocalLibretroConfig } from 'Settings/Libretro/LibretroSettings';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import formatRuntime from 'Utilities/Number/formatRuntime';
import translate from 'Utilities/String/translate';
import {
  getGameAchievementSummary,
  useRetroAchievementsLibraryVersion,
} from './gameAchievementsHelper';
import RetroAchievementsShowcaseModal from './RetroAchievementsShowcaseModal';
import styles from './GameRow.module.css';

interface GameRowProps {
  id: number;
  seriesId: number;
  romFileId?: number;
  monitored: boolean;
  seasonNumber: number;
  gameNumber: number;
  absoluteGameNumber?: number;
  sceneSeasonNumber?: number;
  sceneGameNumber?: number;
  sceneAbsoluteGameNumber?: number;
  airDate?: string;
  airDateUtc?: string;
  runtime?: number;
  finaleType?: string;
  title: string;
  isSaving?: boolean;
  isAlternateGroup?: boolean;
  romVersionLabel?: string;
  alternateVersionCount?: number;
  alternateType?: 'versions' | 'discs' | 'both' | 'updates';
  isGroupExpanded?: boolean;
  groupKey?: string;
  onToggleGroupExpand?: (groupKey: string) => void;
  columns: Column[];
  onMonitorGamePress: (
    gameId: number,
    value: boolean,
    { shiftKey }: { shiftKey: boolean }
  ) => void;
}

function GameRow({
  id,
  seriesId,
  romFileId,
  monitored,
  seasonNumber,
  airDate,
  airDateUtc,
  runtime,
  finaleType,
  title,
  isSaving,
  isAlternateGroup = false,
  romVersionLabel,
  alternateVersionCount = 0,
  alternateType = 'versions',
  isGroupExpanded = false,
  groupKey = '',
  onToggleGroupExpand,
  columns,
  onMonitorGamePress,
}: GameRowProps) {
  const series = useSingleSeries(seriesId);
  const platformTitle = series?.titleSlug || series?.title;
  const romFile = useRomFile(romFileId);
  const [isAchievementsModalOpen, setIsAchievementsModalOpen] = useState(false);

  const customFormats = romFile?.customFormats ?? [];
  const customFormatScore = romFile?.customFormatScore ?? 0;
  const effectiveVersion = romVersionLabel || finaleType || 'Retail (v1.0)';

  const raLibraryVersion = useRetroAchievementsLibraryVersion(platformTitle);

  const achievementSummary = useMemo(
    () =>
      getGameAchievementSummary(
        title,
        seasonNumber,
        raLibraryVersion,
        platformTitle
      ),
    [title, seasonNumber, raLibraryVersion, platformTitle]
  );

  const handleToggleGroupExpandPress = useCallback(() => {
    onToggleGroupExpand?.(groupKey);
  }, [groupKey, onToggleGroupExpand]);

  const handleMonitorGamePress = useCallback(
    (monitored: boolean, options: { shiftKey: boolean }) => {
      onMonitorGamePress(id, monitored, options);
    },
    [id, onMonitorGamePress]
  );

  const navigate = useNavigate();

  const handleOpenAchievements = useCallback(() => {
    setIsAchievementsModalOpen(true);
  }, []);

  const handleCloseAchievements = useCallback(() => {
    setIsAchievementsModalOpen(false);
  }, []);

  const handlePlayPress = useCallback(() => {
    const config = loadLocalLibretroConfig();
    const romPathValue = romFile?.path || romFile?.relativePath || '';

    if (config.preferredLaunchMode === 'protocol') {
      const protocolParams = new URLSearchParams();

      if (romPathValue) {
        protocolParams.set('rom', romPathValue);
      }

      const query = protocolParams.toString();
      window.location.href = `controllarr://play/${id}${
        query ? `?${query}` : ''
      }`;
      return;
    }

    const params = new URLSearchParams({
      gameId: String(id),
    });

    if (romFileId) {
      params.set('romFileId', String(romFileId));
    }

    if (platformTitle) {
      params.set('platform', platformTitle);
    }

    if (romPathValue) {
      params.set('rom', romPathValue);
    }

    navigate(`/webplayer?${params.toString()}`);
  }, [id, navigate, platformTitle, romFile, romFileId]);

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
                onPress={handleMonitorGamePress}
              />
            </TableRowCell>
          );
        }

        if (name === 'gameNumber') {
          return null;
        }

        if (name === 'title') {
          return (
            <TableRowCell key={name} className={styles.title}>
              <GameTitleLink
                gameId={id}
                seriesId={seriesId}
                gameTitle={title}
                gameEntity="games"
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
                  {(() => {
                    if (isGroupExpanded) {
                      if (alternateType === 'discs') {
                        return 'Hide discs';
                      }

                      if (alternateType === 'updates') {
                        return 'Hide updates';
                      }

                      if (alternateType === 'both') {
                        return 'Hide discs & versions';
                      }

                      return 'Hide versions';
                    }

                    if (alternateType === 'discs') {
                      return `+${alternateVersionCount} disc${
                        alternateVersionCount > 1 ? 's' : ''
                      }`;
                    }

                    if (alternateType === 'updates') {
                      return `+${alternateVersionCount} update${
                        alternateVersionCount > 1 ? 's' : ''
                      }`;
                    }

                    if (alternateType === 'both') {
                      return `+${alternateVersionCount} (updates & versions)`;
                    }

                    return `+${alternateVersionCount} version${
                      alternateVersionCount > 1 ? 's' : ''
                    }`;
                  })()}
                </button>
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'achievements') {
          if (
            achievementSummary.tier === 'none' ||
            achievementSummary.total <= 0
          ) {
            return <TableRowCell key={name} className={styles.achievements} />;
          }

          let badgeClass = styles.achievementOutline;
          let badgeIcon = '◇';
          let badgeText =
            achievementSummary.total > 0
              ? `${achievementSummary.unlocked}/${achievementSummary.total}`
              : '';
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
                platformTitle={platformTitle}
                badges={achievementSummary.badges}
                onModalClose={handleCloseAchievements}
              />
            </TableRowCell>
          );
        }

        if (name === 'path') {
          return <TableRowCell key={name}>{romFile?.path}</TableRowCell>;
        }

        if (name === 'relativePath') {
          return (
            <TableRowCell key={name}>{romFile?.relativePath}</TableRowCell>
          );
        }

        if (name === 'airDateUtc') {
          return (
            <RelativeDateCell key={name} date={airDateUtc} airDate={airDate} />
          );
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
              <GameFormats formats={customFormats} />
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
                tooltip={<GameFormats formats={customFormats} />}
                position="left"
              />
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name} className={styles.languages}>
              <RomFileLanguages romFileId={romFileId} />
            </TableRowCell>
          );
        }

        if (name === 'size') {
          return (
            <TableRowCell key={name} className={styles.size}>
              {!!romFile?.size && formatBytes(romFile?.size)}
            </TableRowCell>
          );
        }

        if (name === 'releaseGroup') {
          return (
            <TableRowCell key={name} className={styles.releaseGroup}>
              {romFile?.releaseGroup}
            </TableRowCell>
          );
        }

        if (name === 'indexerFlags') {
          return (
            <TableRowCell key={name} className={styles.indexerFlags}>
              {romFile?.indexerFlags ? (
                <Popover
                  anchor={<Icon name={icons.FLAG} kind="default" size={14} />}
                  title={translate('IndexerFlags')}
                  body={<IndexerFlags indexerFlags={romFile?.indexerFlags} />}
                  position="left"
                />
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'status') {
          return (
            <TableRowCell key={name} className={styles.status}>
              {romFileId ? (
                <IconButton
                  name={icons.SERIES_CONTINUING}
                  kind={kinds.SUCCESS}
                  size={16}
                  title="Play Now"
                  aria-label="Play Now"
                  onPress={handlePlayPress}
                />
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <GameSearchCell
              key={name}
              gameId={id}
              gameEntity="games"
              seriesId={seriesId}
              gameTitle={title}
              romFileId={romFileId}
              showOpenSeriesButton={false}
            />
          );
        }

        return null;
      })}
    </TableRow>
  );
}

export default GameRow;
