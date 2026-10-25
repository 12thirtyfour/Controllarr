import classNames from 'classnames';
import React, { useEffect } from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import ProgressBar from 'Components/ProgressBar';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { icons, kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import translate from 'Utilities/String/translate';
import styles from './RetroAchievementsShowcaseModal.module.css';

export interface RetroAchievementBadge {
  id: number;
  title: string;
  description: string;
  points: number;
  badgeName: string;
  unlocked: boolean;
  hardcoreUnlocked: boolean;
}

export interface RetroAchievementsProgressRequestResource {
  username: string;
  gameTitle: string;
  consoleName?: string;
  romMd5Hash: string;
  badges: RetroAchievementBadge[];
}

export interface RetroAchievementsGameProgress {
  gameId?: number;
  username: string;
  gameTitle: string;
  consoleName?: string;
  romMd5Hash: string;
  totalAchievements: number;
  unlockedAchievements: number;
  hardcoreUnlockedAchievements: number;
  masteryPercentage: number;
  isMastered: boolean;
  badges: RetroAchievementBadge[];
}

export interface RetroAchievementsShowcaseModalProps {
  isOpen: boolean;
  gameTitle: string;
  seriesTitle?: string;
  romMd5Hash?: string;
  badges?: RetroAchievementBadge[];
  onModalClose: () => void;
}

const EMPTY_BADGES: RetroAchievementBadge[] = [];

function RetroAchievementsShowcaseModal({
  isOpen,
  gameTitle,
  seriesTitle,
  romMd5Hash = 'd41d8cd98f00b204e9800998ecf8427e',
  badges = EMPTY_BADGES,
  onModalClose,
}: RetroAchievementsShowcaseModalProps) {
  const {
    mutate: fetchProgress,
    data: progress,
    isPending,
    error,
  } = useApiMutation<
    RetroAchievementsGameProgress,
    RetroAchievementsProgressRequestResource
  >({
    path: '/retroachievements/progress',
    method: 'POST',
  });

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const storedUsername =
      window.localStorage.getItem('controllarr_ra_username') || 'PlayerOne';

    fetchProgress({
      username: storedUsername,
      gameTitle,
      consoleName: seriesTitle,
      romMd5Hash,
      badges,
    });
  }, [isOpen, gameTitle, seriesTitle, romMd5Hash, badges, fetchProgress]);

  return (
    <Modal isOpen={isOpen} size={sizes.LARGE} onModalClose={onModalClose}>
      {isOpen ? (
        <ModalContent onModalClose={onModalClose}>
          <ModalHeader>
            {translate('RetroAchievementsShowcaseTitle', { title: gameTitle })}
          </ModalHeader>

          <ModalBody>
            {isPending ? <LoadingIndicator /> : null}

            {!isPending && error ? (
              <Alert kind={kinds.DANGER}>
                {translate('RetroAchievementsLoadError')}
              </Alert>
            ) : null}

            {!isPending && progress ? (
              <div className={styles.container}>
                <div className={styles.summaryCard}>
                  <div className={styles.summaryHeader}>
                    <div className={styles.titleBlock}>
                      <div className={styles.gameTitle}>
                        {progress.gameTitle}
                      </div>
                      <div className={styles.metaRow}>
                        <span>
                          {translate('Username')}: {progress.username}
                        </span>
                        <span className={styles.hashChip}>
                          MD5: {progress.romMd5Hash}
                        </span>
                      </div>
                    </div>

                    <div className={styles.masteryStats}>
                      {progress.isMastered ? (
                        <Label kind={kinds.WARNING} size={sizes.LARGE}>
                          <Icon name={icons.SCORE} size={14} />{' '}
                          {translate('RetroAchievementsMastered')}
                        </Label>
                      ) : (
                        <Label kind={kinds.INFO} size={sizes.LARGE}>
                          {progress.masteryPercentage}%{' '}
                          {translate('RetroAchievementsMastery')}
                        </Label>
                      )}
                    </div>
                  </div>

                  <div className={styles.progressSection}>
                    <ProgressBar
                      progress={Number(progress.masteryPercentage)}
                      kind={progress.isMastered ? kinds.WARNING : kinds.PRIMARY}
                      size={sizes.LARGE}
                      showText={true}
                      text={`${progress.unlockedAchievements} / ${progress.totalAchievements} (${progress.masteryPercentage}%)`}
                    />

                    <div className={styles.countsRow}>
                      <span>
                        {translate('RetroAchievementsUnlockedCount', {
                          unlocked: progress.unlockedAchievements,
                          total: progress.totalAchievements,
                        })}
                      </span>
                      <span>
                        {translate('RetroAchievementsHardcoreCount', {
                          hardcore: progress.hardcoreUnlockedAchievements,
                          total: progress.totalAchievements,
                        })}
                      </span>
                    </div>
                  </div>
                </div>

                <div className={styles.badgeGrid}>
                  {progress.badges.map((badge) => {
                    const isUnlocked = badge.unlocked || badge.hardcoreUnlocked;
                    let pointsKind: Kind = kinds.DEFAULT;
                    let statusText = translate('RetroAchievementsLocked');

                    if (badge.hardcoreUnlocked) {
                      pointsKind = kinds.WARNING;
                      statusText = translate('RetroAchievementsHardcore');
                    } else if (isUnlocked) {
                      pointsKind = kinds.SUCCESS;
                      statusText = translate('RetroAchievementsUnlocked');
                    }

                    return (
                      <div
                        key={badge.id}
                        className={classNames(
                          styles.badgeCard,
                          isUnlocked
                            ? styles.badgeUnlocked
                            : styles.badgeLocked,
                          badge.hardcoreUnlocked && styles.badgeHardcore
                        )}
                      >
                        <div className={styles.badgeHeader}>
                          <span className={styles.badgeTitle}>
                            {badge.title}
                          </span>
                          <Label kind={pointsKind} size={sizes.SMALL}>
                            {badge.points} pts
                          </Label>
                        </div>

                        <div className={styles.badgeDescription}>
                          {badge.description}
                        </div>

                        <div className={styles.badgeFooter}>
                          <Label kind={pointsKind} size={sizes.SMALL}>
                            {statusText}
                          </Label>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            ) : null}
          </ModalBody>

          <ModalFooter>
            <Button onPress={onModalClose}>{translate('Close')}</Button>
          </ModalFooter>
        </ModalContent>
      ) : null}
    </Modal>
  );
}

export default RetroAchievementsShowcaseModal;
