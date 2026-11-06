import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useAppDimension } from 'App/appStore';
import CommandNames from 'Commands/CommandNames';
import { useCommands, useExecuteCommand } from 'Commands/useCommands';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import MenuItem from 'Components/Menu/MenuItem';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import SpinnerIcon from 'Components/SpinnerIcon';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Popover from 'Components/Tooltip/Popover';
import Game from 'Game/Game';
import {
  setGameOptions,
  setGameSort,
  useGameOptions,
} from 'Game/gameOptionsStore';
import { getQueryKey, useToggleGamesMonitored } from 'Game/useGame';
import { useRegionGames } from 'Game/useGames';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { align, icons, sortDirections, tooltipPositions } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import OrganizePreviewModal from 'Organize/OrganizePreviewModal';
import formatSeason from 'Region/formatRegion';
import PlatformHistoryModal from 'Platform/History/PlatformHistoryModal';
import RegionInteractiveSearchModal from 'Platform/Search/RegionInteractiveSearchModal';
import { Statistics } from 'Platform/Platform';
import { useSingleSeries, useToggleSeasonMonitored } from 'Platform/usePlatform';
import { TableOptionsChangePayload } from 'typings/Table';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import isBefore from 'Utilities/Date/isBefore';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import getToggledRange from 'Utilities/Table/getToggledRange';
import GameRow from './GameRow';
import RegionInfo from './RegionInfo';
import RegionProgressLabel from './RegionProgressLabel';
import styles from './PlatformDetailsRegion.module.css';

function isPreReleaseLabel(label: string) {
  return /\b(proto|prototype|beta|demo|sample)\b/i.test(label);
}

function isEncryptedLabel(label: string) {
  return /\bencrypted\b/i.test(label);
}

function parseDiscNumber(versionLabel: string, title?: string): number {
  const match = (versionLabel || '').match(/\bdisc\s*(\d+)\b/i) || (title || '').match(/\bdisc\s*(\d+)\b/i);
  return match ? parseInt(match[1], 10) : 1;
}

function hasDiscTag(versionLabel: string, title?: string): boolean {
  return /\bdisc\s*\d+\b/i.test(versionLabel || '') || /\bdisc\s*\d+\b/i.test(title || '');
}

function parseCleanVersion(versionLabel: string): string {
  const clean = (versionLabel || '')
    .replace(/\bdisc\s*\d+\b/i, '')
    .replace(/^[\s,-]+|[\s,-]+$/g, '')
    .trim();
  return clean || 'Retail (v1.0)';
}

function normalizeGroupKey(title: string): string {
  return (title || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[\s\-_:]+/g, ' ')
    .trim()
    .toLowerCase();
}

function parseBaseGameAndVersion(item: Game) {
  const rawTitle = item.title ?? '';

  if (
    item.finaleType &&
    item.finaleType !== 'Retail (v1.0)' &&
    rawTitle.endsWith(`(${item.finaleType})`)
  ) {
    return {
      baseTitle: rawTitle
        .slice(0, rawTitle.length - item.finaleType.length - 2)
        .trim(),
      versionLabel: item.finaleType,
    };
  }

  const match = rawTitle.match(
    /^(.*?)\s*[\(\[](Rev\.?\s*[A-Za-z0-9.]+|v\d+[.\dA-Za-z]*|Version\s*[\d.]+|Alt\s*\d*|PRG\d+|Encrypted[^)\]]*|Beta[^)\]]*|Proto[^)\]]*|Prototype[^)\]]*|Demo[^)\]]*|Sample[^)\]]*|Virtual Console[^)\]]*|Re-Release|Unlicensed|Unl|Pirate|Homebrew|Aftermarket|ROM Hack)[\)\]]$/i
  );

  if (match) {
    return {
      baseTitle: match[1].trim(),
      versionLabel: item.finaleType || match[2].trim(),
    };
  }

  return {
    baseTitle: rawTitle.trim(),
    versionLabel: item.finaleType || 'Retail (v1.0)',
  };
}

function getSeasonStatistics(games: Game[]) {
  let gameCount = 0;
  let romFileCount = 0;
  let totalGameCount = 0;
  let monitoredGameCount = 0;
  let hasMonitoredGames = false;
  const sizeOnDisk = 0;

  games.forEach((game) => {
    if (game.romFileId) {
      gameCount++;
      romFileCount++;
    } else if (game.monitored && isBefore(game.airDateUtc)) {
      gameCount++;
    }

    if (game.monitored) {
      monitoredGameCount++;
      hasMonitoredGames = true;
    }

    totalGameCount++;
  });

  return {
    gameCount,
    romFileCount,
    totalGameCount,
    monitoredGameCount,
    hasMonitoredGames,
    sizeOnDisk,
  };
}

function useIsSearching(seriesId: number, seasonNumber: number) {
  const { data: commands } = useCommands();
  return isCommandExecuting(
    findCommand(commands, {
      name: CommandNames.RegionSearch,
      seriesId,
      seasonNumber,
    })
  );
}

interface PlatformDetailsSeasonProps {
  seriesId: number;
  monitored: boolean;
  seasonNumber: number;
  statistics?: Statistics;
  title?: string;
  isExpanded?: boolean;
  onExpandPress: (seasonNumber: number, isExpanded: boolean) => void;
}

function PlatformDetailsSeason({
  seriesId,
  monitored,
  seasonNumber,
  statistics = {} as Statistics,
  title,
  isExpanded,
  onExpandPress,
}: PlatformDetailsSeasonProps) {
  const executeCommand = useExecuteCommand();
  const {
    monitored: seriesMonitored,
    path,
    titleSlug,
    collectionStrategy = 0,
    revisionPreference = 0,
  } = useSingleSeries(seriesId)!;
  const { data: items } = useRegionGames(seriesId, seasonNumber);

  const { columns, sortKey, sortDirection } = useGameOptions();

  const isSmallScreen = useAppDimension('isSmallScreen');
  const isSearching = useIsSearching(seriesId, seasonNumber);

  const { isTogglingSeasonMonitored, toggleSeasonMonitored } =
    useToggleSeasonMonitored(seriesId);

  const { isToggling, togglingGameIds, toggleGamesMonitored } =
    useToggleGamesMonitored(getQueryKey('games')!);

  const { sizeOnDisk = 0 } = statistics;

  const {
    gameCount,
    romFileCount,
    totalGameCount,
    monitoredGameCount,
    hasMonitoredGames,
  } = getSeasonStatistics(items);

  const previousRomFileCount = usePrevious(romFileCount);

  const [isOrganizeModalOpen, setIsOrganizeModalOpen] = useState(false);
  const [isManageGamesOpen, setIsManageGamesOpen] = useState(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [isInteractiveSearchModalOpen, setIsInteractiveSearchModalOpen] =
    useState(false);
  const [expandedGameGroups, setExpandedGameGroups] = useState<
    Record<string, boolean>
  >({});

  const lastToggledGame = useRef<number | null>(null);
  const hasSetInitalExpand = useRef(false);

  const seasonNumberTitle =
    formatSeason(seasonNumber, false, titleSlug) ??
    translate('SeasonNumberToken', { seasonNumber });

  const isCompletionist =
    String(collectionStrategy) === 'preservationist' ||
    Number(collectionStrategy) === 1;
  const preferOriginalRelease =
    String(revisionPreference) === 'initialRelease' ||
    Number(revisionPreference) === 1;

  const toggleGroupExpand = useCallback((groupKey: string) => {
    setExpandedGameGroups((prev) => ({
      ...prev,
      [groupKey]: !prev[groupKey],
    }));
  }, []);

  const displayRows = useMemo(() => {
    const groupsMap = new Map<
      string,
      { item: Game; versionLabel: string; baseTitle: string }[]
    >();

    items.forEach((item) => {
      const { baseTitle, versionLabel } = parseBaseGameAndVersion(item);
      const key = normalizeGroupKey(baseTitle);
      const existing = groupsMap.get(key);

      if (existing) {
        existing.push({ item, versionLabel, baseTitle });
      } else {
        groupsMap.set(key, [{ item, versionLabel, baseTitle }]);
      }
    });

    const rows: {
      item: Game;
      versionLabel: string;
      isAlternateGroup: boolean;
      alternateVersionCount: number;
      alternateType: 'versions' | 'discs' | 'both';
      isGroupExpanded: boolean;
      groupKey: string;
    }[] = [];

    const ARTICLE_REGEX = /^(?:the|a|an)\s+/i;
    const sortedGroups = Array.from(groupsMap.entries()).sort(
      ([keyA, entriesA], [keyB, entriesB]) => {
        const titleA = entriesA[0]?.baseTitle || keyA;
        const titleB = entriesB[0]?.baseTitle || keyB;
        const normA = normalizeGroupKey(titleA.replace(ARTICLE_REGEX, ''));
        const normB = normalizeGroupKey(titleB.replace(ARTICLE_REGEX, ''));
        return normA.localeCompare(normB);
      }
    );

    let groupIndex = 0;
    sortedGroups.forEach(([groupKey, rawEntries]) => {
      const isAlternateGroup = groupIndex % 2 === 1;
      groupIndex++;

      const uniqueEntries: typeof rawEntries = [];
      const seenEntryKeys = new Set<string>();
      rawEntries.forEach((entry) => {
        const cleanVer = parseCleanVersion(entry.versionLabel);
        const disc = parseDiscNumber(entry.versionLabel, entry.item.title);
        const entryKey = `${cleanVer}:${disc}:${isEncryptedLabel(entry.versionLabel)}`;
        if (seenEntryKeys.add(entryKey)) {
          uniqueEntries.push(entry);
        } else if (entry.item.monitored || entry.item.romFileId) {
          const existingIdx = uniqueEntries.findIndex((e) => {
            const eVer = parseCleanVersion(e.versionLabel);
            const eDisc = parseDiscNumber(e.versionLabel, e.item.title);
            return `${eVer}:${eDisc}:${isEncryptedLabel(e.versionLabel)}` === entryKey;
          });
          if (existingIdx >= 0) {
            uniqueEntries[existingIdx] = entry;
          }
        }
      });

      // Sort unencrypted retail releases first, then encrypted retail releases, then Prototype/Beta/Demo/Sample pre-releases
      const entries = [...uniqueEntries].sort((a, b) => {
        const aBucket = isPreReleaseLabel(a.versionLabel)
          ? 2
          : isEncryptedLabel(a.versionLabel)
          ? 1
          : 0;
        const bBucket = isPreReleaseLabel(b.versionLabel)
          ? 2
          : isEncryptedLabel(b.versionLabel)
          ? 1
          : 0;
        if (aBucket !== bBucket) {
          return aBucket - bBucket;
        }

        const aVer = parseCleanVersion(a.versionLabel);
        const bVer = parseCleanVersion(b.versionLabel);
        if (aVer !== bVer) {
          return preferOriginalRelease
            ? aVer.localeCompare(bVer)
            : bVer.localeCompare(aVer);
        }

        const aDisc = parseDiscNumber(a.versionLabel, a.item.title);
        const bDisc = parseDiscNumber(b.versionLabel, b.item.title);
        return aDisc - bDisc;
      });

      const retailEntries = entries.filter(
        (e) => !isPreReleaseLabel(e.versionLabel)
      );

      // Demos, Protos, Betas, and Samples are never considered for 1G1R at all
      if (!isCompletionist && retailEntries.length === 0) {
        return;
      }

      const discCount = entries.filter((e) => hasDiscTag(e.versionLabel, e.item.title)).length;
      const distinctVersionCount = new Set(entries.map((e) => parseCleanVersion(e.versionLabel))).size;
      const isMultiDisc = discCount > 1;
      const isMultiVersion = distinctVersionCount > 1;

      let alternateType: 'versions' | 'discs' | 'both' = 'versions';
      if (isMultiDisc && isMultiVersion) {
        alternateType = 'both';
      } else if (isMultiDisc) {
        alternateType = 'discs';
      } else {
        alternateType = 'versions';
      }

      const isGroupExpanded = Boolean(expandedGameGroups[groupKey]);
      const alternateVersionCount = entries.length > 1 ? entries.length - 1 : 0;

      if (isGroupExpanded) {
        entries.forEach((entry, idx) => {
          rows.push({
            item: {
              ...entry.item,
              title: entry.baseTitle,
            },
            versionLabel: entry.versionLabel,
            isAlternateGroup,
            alternateVersionCount: idx === 0 ? alternateVersionCount : 0,
            alternateType,
            isGroupExpanded,
            groupKey,
          });
        });
      } else {
        // Multi-disc entries must always use Disc 1 as primary
        const disc1Entry = retailEntries.find(
          (e) => parseDiscNumber(e.versionLabel, e.item.title) === 1
        );

        let primaryEntry: typeof entries[0];
        if (isMultiDisc && disc1Entry) {
          primaryEntry = disc1Entry;
        } else {
          const monitoredRetailEntry = retailEntries.find(
            (e) => e.item.monitored
          );
          const monitoredEntry =
            monitoredRetailEntry ?? entries.find((e) => e.item.monitored);
          const unencryptedRetailEntries = retailEntries.filter(
            (e) => !isEncryptedLabel(e.versionLabel)
          );
          const fallbackPool =
            unencryptedRetailEntries.length > 0
              ? unencryptedRetailEntries
              : retailEntries;
          const fallbackRetail =
            fallbackPool.length > 0
              ? preferOriginalRelease
                ? fallbackPool[0]
                : fallbackPool[fallbackPool.length - 1]
              : entries[0];
          primaryEntry = monitoredEntry ?? fallbackRetail;
        }

        rows.push({
          item: {
            ...primaryEntry.item,
            title: primaryEntry.baseTitle,
          },
          versionLabel: primaryEntry.versionLabel,
          isAlternateGroup,
          alternateVersionCount,
          alternateType,
          isGroupExpanded: false,
          groupKey,
        });
      }
    });

    return rows;
  }, [items, isCompletionist, preferOriginalRelease, expandedGameGroups]);

  const handleMonitorSeasonPress = useCallback(
    (value: boolean) => {
      toggleSeasonMonitored({
        seasonNumber,
        monitored: value,
      });
    },
    [seasonNumber, toggleSeasonMonitored]
  );

  const handleExpandPress = useCallback(() => {
    onExpandPress(seasonNumber, !isExpanded);
  }, [seasonNumber, isExpanded, onExpandPress]);

  const handleMonitorGamePress = useCallback(
    (
      gameId: number,
      value: boolean,
      { shiftKey }: { shiftKey: boolean }
    ) => {
      const lastToggled = lastToggledGame.current;
      const gameIds = new Set([gameId]);

      if (shiftKey && lastToggled) {
        const { lower, upper } = getToggledRange(items, gameId, lastToggled);

        for (let i = lower; i < upper; i++) {
          gameIds.add(items[i].id);
        }
      }

      lastToggledGame.current = gameId;

      toggleGamesMonitored({
        gameIds: Array.from(gameIds),
        monitored: value,
      });
    },
    [items, toggleGamesMonitored]
  );

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RegionSearch,
      seriesId,
      seasonNumber,
    });
  }, [seriesId, seasonNumber, executeCommand]);

  const handleOrganizePress = useCallback(() => {
    setIsOrganizeModalOpen(true);
  }, []);

  const handleOrganizeModalClose = useCallback(() => {
    setIsOrganizeModalOpen(false);
  }, []);

  const handleManageGamesPress = useCallback(() => {
    setIsManageGamesOpen(true);
  }, []);

  const handleManageGamesModalClose = useCallback(() => {
    setIsManageGamesOpen(false);
  }, []);

  const handleHistoryPress = useCallback(() => {
    setIsHistoryModalOpen(true);
  }, []);

  const handleHistoryModalClose = useCallback(() => {
    setIsHistoryModalOpen(false);
  }, []);

  const handleInteractiveSearchPress = useCallback(() => {
    setIsInteractiveSearchModalOpen(true);
  }, []);

  const handleInteractiveSearchModalClose = useCallback(() => {
    setIsInteractiveSearchModalOpen(false);
  }, []);

  const handleSortPress = useCallback(
    (sortKey: string, sortDirection?: SortDirection) => {
      setGameSort({
        sortKey,
        sortDirection,
      });
    },
    []
  );

  const handleTableOptionChange = useCallback(
    (payload: TableOptionsChangePayload) => {
      setGameOptions(payload);
    },
    []
  );

  useEffect(() => {
    if (hasSetInitalExpand.current || items.length === 0) {
      return;
    }

    hasSetInitalExpand.current = true;

    onExpandPress(seasonNumber, false);
  }, [items, seriesId, seasonNumber, onExpandPress]);

  useEffect(() => {
    if ((previousRomFileCount ?? 0) > 0 && romFileCount === 0) {
      setIsOrganizeModalOpen(false);
      setIsManageGamesOpen(false);
    }
  }, [romFileCount, previousRomFileCount]);

  const gamesContainerRef = useRef<HTMLDivElement>(null);
  const [windowRange, setWindowRange] = useState({ start: 0, end: 60 });

  useEffect(() => {
    if (!isExpanded || displayRows.length <= 50) {
      return;
    }

    const container = gamesContainerRef.current;
    if (!container) {
      return;
    }

    const getScrollParent = (
      node: HTMLElement | null
    ): HTMLElement | Window => {
      let current = node?.parentElement;
      while (current) {
        const { overflowY } = window.getComputedStyle(current);
        if (overflowY === 'auto' || overflowY === 'scroll') {
          return current;
        }
        current = current.parentElement;
      }
      return window;
    };

    const scrollParent = getScrollParent(container);

    const updateWindow = () => {
      if (!gamesContainerRef.current) {
        return;
      }

      const containerRect = gamesContainerRef.current.getBoundingClientRect();
      const parentRect =
        scrollParent instanceof HTMLElement
          ? scrollParent.getBoundingClientRect()
          : { top: 0, height: window.innerHeight };

      const offsetTop = parentRect.top - containerRect.top;
      const vHeight = parentRect.height;
      const ROW_HEIGHT = 42;
      const OVERSCAN = 25;

      const start = Math.max(0, Math.floor(offsetTop / ROW_HEIGHT) - OVERSCAN);
      const visibleCount = Math.ceil(vHeight / ROW_HEIGHT);
      const end = Math.min(
        displayRows.length,
        start + visibleCount + 2 * OVERSCAN
      );

      setWindowRange((prev) => {
        if (
          Math.abs(prev.start - start) < 6 &&
          Math.abs(prev.end - end) < 6 &&
          end <= displayRows.length
        ) {
          return prev;
        }
        return { start, end };
      });
    };

    updateWindow();

    let ticking = false;
    const handleScroll = () => {
      if (!ticking) {
        window.requestAnimationFrame(() => {
          updateWindow();
          ticking = false;
        });
        ticking = true;
      }
    };

    scrollParent.addEventListener('scroll', handleScroll, { passive: true });
    window.addEventListener('resize', handleScroll, { passive: true });

    return () => {
      scrollParent.removeEventListener('scroll', handleScroll);
      window.removeEventListener('resize', handleScroll);
    };
  }, [isExpanded, displayRows.length]);

  const isVirtual = isExpanded && displayRows.length > 50;
  const startIndex = isVirtual
    ? Math.min(windowRange.start, displayRows.length)
    : 0;
  const endIndex = isVirtual
    ? Math.min(Math.max(windowRange.end, startIndex + 1), displayRows.length)
    : displayRows.length;
  const visibleRows = isVirtual
    ? displayRows.slice(startIndex, endIndex)
    : displayRows;
  const topSpacerHeight = isVirtual ? startIndex * 42 : 0;
  const bottomSpacerHeight = isVirtual ? (displayRows.length - endIndex) * 42 : 0;
  const visibleColumnCount = columns.filter(
    (col) => col.isVisible !== false
  ).length;

  if (
    (items && items.length === 0) ||
    (statistics?.totalGameCount === 0 && (!items || items.length === 0))
  ) {
    return null;
  }

  return (
    <div className={styles.season}>
      <div className={styles.header}>
        <div className={styles.left}>
          <MonitorToggleButton
            monitored={monitored}
            isDisabled={false}
            isSaving={isTogglingSeasonMonitored}
            size={24}
            onPress={handleMonitorSeasonPress}
          />

          <div className={styles.seasonInfo}>
            <div className={styles.seasonNumber}>{seasonNumberTitle}</div>
            {title &&
            title.trim().toLowerCase() !== seasonNumberTitle.trim().toLowerCase() &&
            !seasonNumberTitle.toLowerCase().includes(title.trim().toLowerCase()) &&
            !title.trim().toLowerCase().includes(seasonNumberTitle.trim().toLowerCase()) ? (
              <div className={styles.seasonTitle} title={title}>
                {title}
              </div>
            ) : null}
          </div>

          <div className={styles.seasonStats}>
            <Popover
              className={styles.gameCountTooltip}
              canFlip={true}
              anchor={
                <RegionProgressLabel
                  className={styles.seasonStatsLabel}
                  seriesId={seriesId}
                  seasonNumber={seasonNumber}
                  monitored={monitored}
                  gameCount={gameCount}
                  romFileCount={romFileCount}
                />
              }
              title={translate('RegionInformation')}
              body={
                <div>
                  <RegionInfo
                    totalGameCount={totalGameCount}
                    monitoredGameCount={monitoredGameCount}
                    romFileCount={romFileCount}
                    sizeOnDisk={sizeOnDisk}
                  />
                </div>
              }
              position={tooltipPositions.BOTTOM}
            />

            {sizeOnDisk ? (
              <Label
                className={styles.seasonStatsLabel}
                kind="default"
                size="large"
              >
                {formatBytes(sizeOnDisk)}
              </Label>
            ) : null}
          </div>
        </div>

        <Link className={styles.expandButton} onPress={handleExpandPress}>
          <Icon
            className={styles.expandButtonIcon}
            name={isExpanded ? icons.COLLAPSE : icons.EXPAND}
            title={
              isExpanded ? translate('HideGames') : translate('ShowGames')
            }
            size={24}
          />
          {isSmallScreen ? null : <span>&nbsp;</span>}
        </Link>

        {isSmallScreen ? (
          <Menu
            className={styles.actionsMenu}
            alignMenu={align.RIGHT}
            enforceMaxHeight={false}
          >
            <MenuButton className={styles.actionsMenuButton}>
              <Icon name={icons.ACTIONS} size={22} />
            </MenuButton>

            <MenuContent className={styles.actionsMenuContent}>
              <MenuItem
                isDisabled={
                  isSearching || !hasMonitoredGames || !seriesMonitored
                }
                onPress={handleSearchPress}
              >
                <SpinnerIcon
                  className={styles.actionMenuIcon}
                  name={icons.SEARCH}
                  isSpinning={isSearching}
                />

                {translate('Search')}
              </MenuItem>

              <MenuItem
                isDisabled={!totalGameCount}
                onPress={handleInteractiveSearchPress}
              >
                <Icon
                  className={styles.actionMenuIcon}
                  name={icons.INTERACTIVE}
                />

                {translate('InteractiveSearch')}
              </MenuItem>

              <MenuItem
                isDisabled={!romFileCount}
                onPress={handleOrganizePress}
              >
                <Icon className={styles.actionMenuIcon} name={icons.ORGANIZE} />

                {translate('PreviewRename')}
              </MenuItem>

              <MenuItem
                isDisabled={!romFileCount}
                onPress={handleManageGamesPress}
              >
                <Icon
                  className={styles.actionMenuIcon}
                  name={icons.EPISODE_FILE}
                />

                {translate('ManageGames')}
              </MenuItem>

              <MenuItem
                isDisabled={!totalGameCount}
                onPress={handleHistoryPress}
              >
                <Icon className={styles.actionMenuIcon} name={icons.HISTORY} />

                {translate('History')}
              </MenuItem>
            </MenuContent>
          </Menu>
        ) : (
          <div className={styles.actions}>
            <SpinnerIconButton
              className={styles.actionButton}
              name={icons.SEARCH}
              title={
                hasMonitoredGames && seriesMonitored
                  ? translate('SearchForMonitoredGamesSeason')
                  : translate('NoMonitoredGamesSeason')
              }
              size={20}
              isSpinning={isSearching}
              isDisabled={
                isSearching || !hasMonitoredGames || !seriesMonitored
              }
              onPress={handleSearchPress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.INTERACTIVE}
              title={translate('InteractiveSearchSeason')}
              aria-label={translate('InteractiveSearchSeason')}
              size={20}
              isDisabled={!totalGameCount}
              onPress={handleInteractiveSearchPress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.ORGANIZE}
              title={translate('PreviewRenameSeason')}
              aria-label={translate('PreviewRenameSeason')}
              size={20}
              isDisabled={!romFileCount}
              onPress={handleOrganizePress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.EPISODE_FILE}
              title={translate('ManageGamesSeason')}
              aria-label={translate('ManageGamesSeason')}
              size={20}
              isDisabled={!romFileCount}
              onPress={handleManageGamesPress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.HISTORY}
              title={translate('HistorySeason')}
              aria-label={translate('HistorySeason')}
              size={20}
              isDisabled={!totalGameCount}
              onPress={handleHistoryPress}
            />
          </div>
        )}
      </div>

      <div>
        {isExpanded ? (
          <div ref={gamesContainerRef} className={styles.games}>
            {displayRows.length ? (
              <Table
                columns={columns}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={handleSortPress}
                onTableOptionChange={handleTableOptionChange}
              >
                <TableBody>
                  {topSpacerHeight > 0 ? (
                    <tr style={{ height: `${topSpacerHeight}px` }}>
                      <td
                        colSpan={visibleColumnCount}
                        style={{
                          padding: 0,
                          border: 'none',
                          height: `${topSpacerHeight}px`,
                        }}
                      />
                    </tr>
                  ) : null}

                  {visibleRows.map((row) => {
                    const {
                      item,
                      versionLabel,
                      isAlternateGroup,
                      alternateVersionCount,
                      alternateType,
                      isGroupExpanded,
                      groupKey,
                    } = row;

                    return (
                      <GameRow
                        key={item.id}
                        columns={columns}
                        {...item}
                        romVersionLabel={versionLabel}
                        isAlternateGroup={isAlternateGroup}
                        alternateVersionCount={alternateVersionCount}
                        alternateType={alternateType}
                        isGroupExpanded={isGroupExpanded}
                        groupKey={groupKey}
                        isSaving={
                          isToggling && togglingGameIds.includes(item.id)
                        }
                        onToggleGroupExpand={toggleGroupExpand}
                        onMonitorGamePress={handleMonitorGamePress}
                      />
                    );
                  })}

                  {bottomSpacerHeight > 0 ? (
                    <tr style={{ height: `${bottomSpacerHeight}px` }}>
                      <td
                        colSpan={visibleColumnCount}
                        style={{
                          padding: 0,
                          border: 'none',
                          height: `${bottomSpacerHeight}px`,
                        }}
                      />
                    </tr>
                  ) : null}
                </TableBody>
              </Table>
            ) : (
              <div className={styles.noGames}>
                {translate('NoGamesInThisSeason')}
              </div>
            )}

            <div className={styles.collapseButtonContainer}>
              <IconButton
                iconClassName={styles.collapseButtonIcon}
                name={icons.COLLAPSE}
                size={20}
                title={translate('HideGames')}
                aria-label={translate('HideGames')}
                onPress={handleExpandPress}
              />
            </div>
          </div>
        ) : null}
      </div>

      <OrganizePreviewModal
        isOpen={isOrganizeModalOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleOrganizeModalClose}
      />

      <InteractiveImportModal
        isOpen={isManageGamesOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        title={seasonNumberTitle}
        folder={path}
        initialSortKey="relativePath"
        initialSortDirection={sortDirections.DESCENDING}
        showSeries={false}
        allowSeriesChange={false}
        showDelete={true}
        showImportMode={false}
        modalTitle={translate('ManageGames')}
        onModalClose={handleManageGamesModalClose}
      />

      <PlatformHistoryModal
        isOpen={isHistoryModalOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleHistoryModalClose}
      />

      <RegionInteractiveSearchModal
        isOpen={isInteractiveSearchModalOpen}
        gameCount={totalGameCount}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleInteractiveSearchModalClose}
      />
    </div>
  );
}

export default PlatformDetailsSeason;
