import React, { useCallback, useRef, useState } from 'react';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import Game from 'Game/Game';
import GameDetailsTab from 'Game/GameDetailsTab';
import gameEntities from 'Game/gameEntities';
import useGame, {
  GameEntity,
  getQueryKey,
  useToggleGamesMonitored,
} from 'Game/useGame';
import { useClearReleasesOnUnmount } from 'InteractiveSearch/useReleases';
import formatSeason from 'Region/formatRegion';
import Series from 'Platform/Platform';
import { useSingleSeries } from 'Platform/usePlatform';
import translate from 'Utilities/String/translate';
import GameHistory from './History/GameHistory';
import GameSearch from './Search/GameSearch';
import GameSummary from './Summary/GameSummary';
import styles from './GameDetailsModalContent.module.css';

const TABS: GameDetailsTab[] = ['details', 'history', 'search'];

export interface GameDetailsModalContentProps {
  gameId: number;
  gameEntity: GameEntity;
  seriesId: number;
  gameTitle: string;
  showOpenSeriesButton?: boolean;
  selectedTab?: GameDetailsTab;
  startInteractiveSearch?: boolean;
  onTabChange(isSearch: boolean): void;
  onModalClose(): void;
}

function GameDetailsModalContent({
  gameId,
  gameEntity = gameEntities.EPISODES,
  seriesId,
  gameTitle,
  showOpenSeriesButton = false,
  startInteractiveSearch = false,
  selectedTab = 'details',
  onTabChange,
  onModalClose,
}: GameDetailsModalContentProps) {
  const [currentlySelectedTab, setCurrentlySelectedTab] = useState(selectedTab);
  const modalBodyRef = useRef<HTMLDivElement>(null);

  const { title: platformTitle, titleSlug } = useSingleSeries(seriesId) as Series;

  const { romFileId, seasonNumber, monitored } = useGame(
    gameId,
    gameEntity
  ) as Game;

  const { toggleGamesMonitored, isToggling } = useToggleGamesMonitored(
    getQueryKey(gameEntity)!
  );

  useClearReleasesOnUnmount({ gameId });

  const handleTabSelect = useCallback(
    (selectedIndex: number) => {
      const tab = TABS[selectedIndex];
      onTabChange(tab === 'search');
      setCurrentlySelectedTab(tab);
    },
    [onTabChange]
  );

  const handleMonitorGamePress = useCallback(
    (monitored: boolean) => {
      toggleGamesMonitored({
        gameIds: [gameId],
        monitored,
      });
    },
    [gameId, toggleGamesMonitored]
  );

  const seriesLink = `/platforms/${titleSlug}`;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader className={styles.header}>
        <div className={styles.scopeLine}>
          <span className={styles.scope}>
            <span className={styles.scopeSeries}>{platformTitle}</span>

            <span className={styles.scopeGame}>
              {formatSeason(seasonNumber, false, titleSlug)}
            </span>
          </span>
        </div>

        <div className={styles.titleRow}>
          <MonitorToggleButton
            monitored={monitored}
            size={18}
            isDisabled={false}
            isSaving={isToggling}
            onPress={handleMonitorGamePress}
          />

          <span className={styles.gameTitle}>{gameTitle}</span>
        </div>
      </ModalHeader>

      <ModalBody ref={modalBodyRef}>
        <Tabs
          className={styles.tabs}
          selectedIndex={TABS.indexOf(currentlySelectedTab)}
          onSelect={handleTabSelect}
        >
          <TabList className={styles.tabList}>
            <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
              {translate('Details')}
            </Tab>

            <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
              {translate('History')}
            </Tab>

            <Tab className={styles.tab} selectedClassName={styles.selectedTab}>
              {translate('Search')}
            </Tab>
          </TabList>

          <TabPanel>
            <div className={styles.tabContent}>
              <GameSummary
                gameId={gameId}
                gameEntity={gameEntity}
                romFileId={romFileId}
                seriesId={seriesId}
              />
            </div>
          </TabPanel>

          <TabPanel>
            <div className={styles.tabContent}>
              <GameHistory gameId={gameId} />
            </div>
          </TabPanel>

          <TabPanel>
            {/* Don't wrap in tabContent so we not have a top margin */}
            <GameSearch
              gameId={gameId}
              startInteractiveSearch={startInteractiveSearch}
              scrollerRef={modalBodyRef}
              onModalClose={onModalClose}
            />
          </TabPanel>
        </Tabs>
      </ModalBody>

      <ModalFooter>
        {showOpenSeriesButton && (
          <Button
            className={styles.openSeriesButton}
            to={seriesLink}
            onPress={onModalClose}
          >
            {translate('OpenSeries')}
          </Button>
        )}

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default GameDetailsModalContent;
