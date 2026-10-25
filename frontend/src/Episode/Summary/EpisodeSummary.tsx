import { useQueryClient } from '@tanstack/react-query';
import classNames from 'classnames';
import React, { useCallback, useEffect, useMemo } from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import ProgressBar from 'Components/ProgressBar';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Episode from 'Episode/Episode';
import useEpisode, { EpisodeEntity } from 'Episode/useEpisode';
import { useEpisodeFile } from 'EpisodeFile/EpisodeFileProvider';
import { useDeleteEpisodeFile } from 'EpisodeFile/useEpisodeFiles';
import { icons, kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import {
  getGameAchievementSummary,
  useRetroAchievementsLibraryVersion,
} from 'Series/Details/gameAchievementsHelper';
import Series from 'Series/Series';
import { useSingleSeries } from 'Series/useSeries';
import translate from 'Utilities/String/translate';
import EpisodeFileRow from './EpisodeFileRow';
import styles from './EpisodeSummary.module.css';

const COLUMNS: Column[] = [
  {
    name: 'path',
    label: () => translate('Path'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormats',
    label: () => translate('Formats'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormatScore',
    label: React.createElement(Icon, {
      name: icons.SCORE,
      title: () => translate('CustomFormatScore'),
    }),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isSortable: false,
    isVisible: true,
  },
];

interface EpisodeSummaryProps {
  seriesId: number;
  episodeId: number;
  episodeEntity: EpisodeEntity;
  episodeFileId?: number;
}

function EpisodeSummary({
  seriesId,
  episodeId,
  episodeEntity,
  episodeFileId,
}: EpisodeSummaryProps) {
  const queryClient = useQueryClient();
  const series = useSingleSeries(seriesId) as Series;
  const seriesTitle = series?.title;
  const { network } = series;

  const {
    title = '',
    seasonNumber = 1,
    airDate,
    airDateUtc,
    finaleType,
    overview,
  } = useEpisode(episodeId, episodeEntity) as Episode;

  const {
    path,
    mediaInfo,
    size,
    languages,
    quality,
    qualityCutoffNotMet,
    customFormats,
    customFormatScore,
  } = useEpisodeFile(episodeFileId) ?? {};

  const { deleteEpisodeFile } = useDeleteEpisodeFile(
    episodeFileId!,
    episodeEntity
  );

  const handleDeleteEpisodeFile = useCallback(() => {
    deleteEpisodeFile();
  }, [deleteEpisodeFile]);

  useEffect(() => {
    if (episodeFileId && !path) {
      queryClient.invalidateQueries({ queryKey: ['/episodeFile'] });
    }
  }, [episodeFileId, path, queryClient]);

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

  const hasOverview = !!overview;
  let achievementTierLabel = (
    <Label outline={true} size={sizes.MEDIUM}>
      ◇ Achievements Available (0 Earned)
    </Label>
  );

  if (achievementSummary.tier === 'mastered') {
    achievementTierLabel = (
      <Label kind={kinds.WARNING} size={sizes.MEDIUM}>
        👑 Mastered (100% Hardcore)
      </Label>
    );
  } else if (achievementSummary.tier === 'gold') {
    achievementTierLabel = (
      <Label kind={kinds.WARNING} size={sizes.MEDIUM}>
        🥇 Hardcore Progress ({achievementSummary.hardcoreUnlocked} /{' '}
        {achievementSummary.total})
      </Label>
    );
  } else if (achievementSummary.tier === 'silver') {
    achievementTierLabel = (
      <Label kind={kinds.INFO} size={sizes.MEDIUM}>
        🥈 Softcore Progress ({achievementSummary.unlocked} /{' '}
        {achievementSummary.total})
      </Label>
    );
  }

  return (
    <div>
      <div className={styles.meta}>
        <div className={styles.metaItem}>
          <span className={styles.metaLabel}>{translate('Airs')}</span>

          <div className={styles.metaValue}>
            {airDate ??
              (airDateUtc ? airDateUtc.slice(0, 10) : translate('Unknown'))}
          </div>
        </div>

        <div className={styles.metaItem}>
          <span className={styles.metaLabel}>Version / Type</span>

          <Label outline={true} size={sizes.MEDIUM}>
            {finaleType || 'Retail (v1.0)'}
          </Label>
        </div>

        {network ? (
          <div className={styles.metaItem}>
            <span className={styles.metaLabel}>{translate('Network')}</span>

            <Label outline={true} size={sizes.MEDIUM}>
              {network}
            </Label>
          </div>
        ) : null}
      </div>

      <div className={styles.overview}>
        {hasOverview ? overview : translate('NoEpisodeOverview')}
      </div>

      {achievementSummary.tier === 'none' ? null : (
        <div className={styles.achievementsSection}>
          <div className={styles.achievementsHeader}>
            <span className={styles.achievementsTitle}>
              RetroAchievements ({achievementSummary.unlocked} /{' '}
              {achievementSummary.total} Unlocked)
            </span>

            {achievementTierLabel}
          </div>

          <ProgressBar
            progress={achievementSummary.masteryPercentage}
            kind={achievementSummary.isMastered ? kinds.WARNING : kinds.PRIMARY}
            size={sizes.MEDIUM}
            showText={true}
            text={`${achievementSummary.unlocked} / ${achievementSummary.total} (${achievementSummary.masteryPercentage}%)`}
          />

          <div className={styles.badgeGrid}>
            {achievementSummary.badges.map((badge) => {
              const isUnlocked = badge.unlocked || badge.hardcoreUnlocked;
              let badgePrefix = '◇ ';
              let badgeKind: Kind = kinds.DEFAULT;

              if (badge.hardcoreUnlocked) {
                badgePrefix = '🥇 ';
                badgeKind = kinds.WARNING;
              } else if (isUnlocked) {
                badgePrefix = '🥈 ';
                badgeKind = kinds.SUCCESS;
              }

              return (
                <div
                  key={badge.id}
                  className={classNames(
                    styles.badgeCard,
                    isUnlocked && styles.badgeCardUnlocked,
                    badge.hardcoreUnlocked && styles.badgeCardHardcore
                  )}
                >
                  <div className={styles.badgeTop}>
                    <span className={styles.badgeName}>
                      {badgePrefix}
                      {badge.title}
                    </span>
                    <Label kind={badgeKind} size={sizes.SMALL}>
                      {badge.points} pts
                    </Label>
                  </div>
                  <div className={styles.badgeDesc}>{badge.description}</div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {path ? (
        <Table columns={COLUMNS}>
          <TableBody>
            <EpisodeFileRow
              path={path}
              size={size!}
              languages={languages!}
              quality={quality!}
              qualityCutoffNotMet={qualityCutoffNotMet!}
              customFormats={customFormats!}
              customFormatScore={customFormatScore!}
              mediaInfo={mediaInfo!}
              columns={COLUMNS}
              onDeleteEpisodeFile={handleDeleteEpisodeFile}
            />
          </TableBody>
        </Table>
      ) : null}
    </div>
  );
}

export default EpisodeSummary;
