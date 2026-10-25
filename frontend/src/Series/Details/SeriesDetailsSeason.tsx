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
import Episode from 'Episode/Episode';
import {
  setEpisodeOptions,
  setEpisodeSort,
  useEpisodeOptions,
} from 'Episode/episodeOptionsStore';
import { getQueryKey, useToggleEpisodesMonitored } from 'Episode/useEpisode';
import { useSeasonEpisodes } from 'Episode/useEpisodes';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { align, icons, sortDirections, tooltipPositions } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import OrganizePreviewModal from 'Organize/OrganizePreviewModal';
import formatSeason from 'Season/formatSeason';
import SeriesHistoryModal from 'Series/History/SeriesHistoryModal';
import SeasonInteractiveSearchModal from 'Series/Search/SeasonInteractiveSearchModal';
import { Statistics } from 'Series/Series';
import { useSingleSeries, useToggleSeasonMonitored } from 'Series/useSeries';
import { TableOptionsChangePayload } from 'typings/Table';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import isBefore from 'Utilities/Date/isBefore';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import getToggledRange from 'Utilities/Table/getToggledRange';
import EpisodeRow from './EpisodeRow';
import SeasonInfo from './SeasonInfo';
import SeasonProgressLabel from './SeasonProgressLabel';
import styles from './SeriesDetailsSeason.module.css';

function isPreReleaseLabel(label: string) {
  return /\b(proto|prototype|beta|demo|sample)\b/i.test(label);
}

function isEncryptedLabel(label: string) {
  return /\bencrypted\b/i.test(label);
}

function parseBaseGameAndVersion(item: Episode) {
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
    /^(.*?)\s*\((Rev\s*[A-Z0-9.]+|v\d+[.\dA-Za-z]*|Version\s*[\d.]+|Encrypted[^)]*|Beta[^)]*|Proto[^)]*|Prototype[^)]*|Demo[^)]*|Sample[^)]*|Virtual Console[^)]*|GameCube[^)]*|Classic NES[^)]*|Animal Crossing|e-Reader|Unlicensed|Unl|Pirate|Homebrew|Aftermarket|ROM Hack)\)$/i
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

function getSeasonStatistics(episodes: Episode[]) {
  let episodeCount = 0;
  let episodeFileCount = 0;
  let totalEpisodeCount = 0;
  let monitoredEpisodeCount = 0;
  let hasMonitoredEpisodes = false;
  const sizeOnDisk = 0;

  episodes.forEach((episode) => {
    if (episode.episodeFileId) {
      episodeCount++;
      episodeFileCount++;
    } else if (episode.monitored && isBefore(episode.airDateUtc)) {
      episodeCount++;
    }

    if (episode.monitored) {
      monitoredEpisodeCount++;
      hasMonitoredEpisodes = true;
    }

    totalEpisodeCount++;
  });

  return {
    episodeCount,
    episodeFileCount,
    totalEpisodeCount,
    monitoredEpisodeCount,
    hasMonitoredEpisodes,
    sizeOnDisk,
  };
}

function useIsSearching(seriesId: number, seasonNumber: number) {
  const { data: commands } = useCommands();
  return isCommandExecuting(
    findCommand(commands, {
      name: CommandNames.SeasonSearch,
      seriesId,
      seasonNumber,
    })
  );
}

interface SeriesDetailsSeasonProps {
  seriesId: number;
  monitored: boolean;
  seasonNumber: number;
  statistics?: Statistics;
  title?: string;
  isExpanded?: boolean;
  onExpandPress: (seasonNumber: number, isExpanded: boolean) => void;
}

function SeriesDetailsSeason({
  seriesId,
  monitored,
  seasonNumber,
  statistics = {} as Statistics,
  title,
  isExpanded,
  onExpandPress,
}: SeriesDetailsSeasonProps) {
  const executeCommand = useExecuteCommand();
  const {
    monitored: seriesMonitored,
    path,
    titleSlug,
    collectionStrategy = 0,
    revisionPreference = 0,
  } = useSingleSeries(seriesId)!;
  const { data: items } = useSeasonEpisodes(seriesId, seasonNumber);

  const { columns, sortKey, sortDirection } = useEpisodeOptions();

  const isSmallScreen = useAppDimension('isSmallScreen');
  const isSearching = useIsSearching(seriesId, seasonNumber);

  const { isTogglingSeasonMonitored, toggleSeasonMonitored } =
    useToggleSeasonMonitored(seriesId);

  const { isToggling, togglingEpisodeIds, toggleEpisodesMonitored } =
    useToggleEpisodesMonitored(getQueryKey('episodes')!);

  const { sizeOnDisk = 0 } = statistics;

  const {
    episodeCount,
    episodeFileCount,
    totalEpisodeCount,
    monitoredEpisodeCount,
    hasMonitoredEpisodes,
  } = getSeasonStatistics(items);

  const previousEpisodeFileCount = usePrevious(episodeFileCount);

  const [isOrganizeModalOpen, setIsOrganizeModalOpen] = useState(false);
  const [isManageEpisodesOpen, setIsManageEpisodesOpen] = useState(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [isInteractiveSearchModalOpen, setIsInteractiveSearchModalOpen] =
    useState(false);
  const [expandedGameGroups, setExpandedGameGroups] = useState<
    Record<string, boolean>
  >({});

  const lastToggledEpisode = useRef<number | null>(null);
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
      { item: Episode; versionLabel: string }[]
    >();

    items.forEach((item) => {
      const { baseTitle, versionLabel } = parseBaseGameAndVersion(item);
      const key = baseTitle.toLowerCase();
      const existing = groupsMap.get(key);

      if (existing) {
        existing.push({ item, versionLabel });
      } else {
        groupsMap.set(key, [{ item, versionLabel }]);
      }
    });

    const rows: {
      item: Episode;
      versionLabel: string;
      isAlternateGroup: boolean;
      alternateVersionCount: number;
      isGroupExpanded: boolean;
      groupKey: string;
    }[] = [];

    let groupIndex = 0;
    groupsMap.forEach((rawEntries, groupKey) => {
      const isAlternateGroup = groupIndex % 2 === 1;
      groupIndex++;

      // Sort unencrypted retail releases first, then encrypted retail releases, then Prototype/Beta/Demo/Sample pre-releases
      const entries = [...rawEntries].sort((a, b) => {
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
        return aBucket - bBucket;
      });

      const retailEntries = entries.filter(
        (e) => !isPreReleaseLabel(e.versionLabel)
      );

      // Demos, Protos, Betas, and Samples are never considered for 1G1R at all
      if (!isCompletionist && retailEntries.length === 0) {
        return;
      }

      const isGroupExpanded = Boolean(expandedGameGroups[groupKey]);
      const alternateVersionCount = entries.length > 1 ? entries.length - 1 : 0;

      if (isGroupExpanded) {
        entries.forEach((entry, idx) => {
          rows.push({
            item: entry.item,
            versionLabel: entry.versionLabel,
            isAlternateGroup,
            alternateVersionCount: idx === 0 ? alternateVersionCount : 0,
            isGroupExpanded,
            groupKey,
          });
        });
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
        const primaryEntry = monitoredEntry ?? fallbackRetail;

        rows.push({
          item: primaryEntry.item,
          versionLabel: primaryEntry.versionLabel,
          isAlternateGroup,
          alternateVersionCount,
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

  const handleMonitorEpisodePress = useCallback(
    (
      episodeId: number,
      value: boolean,
      { shiftKey }: { shiftKey: boolean }
    ) => {
      const lastToggled = lastToggledEpisode.current;
      const episodeIds = new Set([episodeId]);

      if (shiftKey && lastToggled) {
        const { lower, upper } = getToggledRange(items, episodeId, lastToggled);

        for (let i = lower; i < upper; i++) {
          episodeIds.add(items[i].id);
        }
      }

      lastToggledEpisode.current = episodeId;

      toggleEpisodesMonitored({
        episodeIds: Array.from(episodeIds),
        monitored: value,
      });
    },
    [items, toggleEpisodesMonitored]
  );

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SeasonSearch,
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

  const handleManageEpisodesPress = useCallback(() => {
    setIsManageEpisodesOpen(true);
  }, []);

  const handleManageEpisodesModalClose = useCallback(() => {
    setIsManageEpisodesOpen(false);
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
      setEpisodeSort({
        sortKey,
        sortDirection,
      });
    },
    []
  );

  const handleTableOptionChange = useCallback(
    (payload: TableOptionsChangePayload) => {
      setEpisodeOptions(payload);
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
    if ((previousEpisodeFileCount ?? 0) > 0 && episodeFileCount === 0) {
      setIsOrganizeModalOpen(false);
      setIsManageEpisodesOpen(false);
    }
  }, [episodeFileCount, previousEpisodeFileCount]);

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
            {title ? (
              <div className={styles.seasonTitle} title={title}>
                {title}
              </div>
            ) : null}
          </div>

          <div className={styles.seasonStats}>
            <Popover
              className={styles.episodeCountTooltip}
              canFlip={true}
              anchor={
                <SeasonProgressLabel
                  className={styles.seasonStatsLabel}
                  seriesId={seriesId}
                  seasonNumber={seasonNumber}
                  monitored={monitored}
                  episodeCount={episodeCount}
                  episodeFileCount={episodeFileCount}
                />
              }
              title={translate('SeasonInformation')}
              body={
                <div>
                  <SeasonInfo
                    totalEpisodeCount={totalEpisodeCount}
                    monitoredEpisodeCount={monitoredEpisodeCount}
                    episodeFileCount={episodeFileCount}
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
              isExpanded ? translate('HideEpisodes') : translate('ShowEpisodes')
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
                  isSearching || !hasMonitoredEpisodes || !seriesMonitored
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
                isDisabled={!totalEpisodeCount}
                onPress={handleInteractiveSearchPress}
              >
                <Icon
                  className={styles.actionMenuIcon}
                  name={icons.INTERACTIVE}
                />

                {translate('InteractiveSearch')}
              </MenuItem>

              <MenuItem
                isDisabled={!episodeFileCount}
                onPress={handleOrganizePress}
              >
                <Icon className={styles.actionMenuIcon} name={icons.ORGANIZE} />

                {translate('PreviewRename')}
              </MenuItem>

              <MenuItem
                isDisabled={!episodeFileCount}
                onPress={handleManageEpisodesPress}
              >
                <Icon
                  className={styles.actionMenuIcon}
                  name={icons.EPISODE_FILE}
                />

                {translate('ManageEpisodes')}
              </MenuItem>

              <MenuItem
                isDisabled={!totalEpisodeCount}
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
                hasMonitoredEpisodes && seriesMonitored
                  ? translate('SearchForMonitoredEpisodesSeason')
                  : translate('NoMonitoredEpisodesSeason')
              }
              size={20}
              isSpinning={isSearching}
              isDisabled={
                isSearching || !hasMonitoredEpisodes || !seriesMonitored
              }
              onPress={handleSearchPress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.INTERACTIVE}
              title={translate('InteractiveSearchSeason')}
              aria-label={translate('InteractiveSearchSeason')}
              size={20}
              isDisabled={!totalEpisodeCount}
              onPress={handleInteractiveSearchPress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.ORGANIZE}
              title={translate('PreviewRenameSeason')}
              aria-label={translate('PreviewRenameSeason')}
              size={20}
              isDisabled={!episodeFileCount}
              onPress={handleOrganizePress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.EPISODE_FILE}
              title={translate('ManageEpisodesSeason')}
              aria-label={translate('ManageEpisodesSeason')}
              size={20}
              isDisabled={!episodeFileCount}
              onPress={handleManageEpisodesPress}
            />

            <IconButton
              className={styles.actionButton}
              name={icons.HISTORY}
              title={translate('HistorySeason')}
              aria-label={translate('HistorySeason')}
              size={20}
              isDisabled={!totalEpisodeCount}
              onPress={handleHistoryPress}
            />
          </div>
        )}
      </div>

      <div>
        {isExpanded ? (
          <div className={styles.episodes}>
            {displayRows.length ? (
              <Table
                columns={columns}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={handleSortPress}
                onTableOptionChange={handleTableOptionChange}
              >
                <TableBody>
                  {displayRows.map((row) => {
                    const {
                      item,
                      versionLabel,
                      isAlternateGroup,
                      alternateVersionCount,
                      isGroupExpanded,
                      groupKey,
                    } = row;

                    return (
                      <EpisodeRow
                        key={item.id}
                        columns={columns}
                        {...item}
                        romVersionLabel={versionLabel}
                        isAlternateGroup={isAlternateGroup}
                        alternateVersionCount={alternateVersionCount}
                        isGroupExpanded={isGroupExpanded}
                        groupKey={groupKey}
                        isSaving={
                          isToggling && togglingEpisodeIds.includes(item.id)
                        }
                        onToggleGroupExpand={toggleGroupExpand}
                        onMonitorEpisodePress={handleMonitorEpisodePress}
                      />
                    );
                  })}
                </TableBody>
              </Table>
            ) : (
              <div className={styles.noEpisodes}>
                {translate('NoEpisodesInThisSeason')}
              </div>
            )}

            <div className={styles.collapseButtonContainer}>
              <IconButton
                iconClassName={styles.collapseButtonIcon}
                name={icons.COLLAPSE}
                size={20}
                title={translate('HideEpisodes')}
                aria-label={translate('HideEpisodes')}
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
        isOpen={isManageEpisodesOpen}
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
        modalTitle={translate('ManageEpisodes')}
        onModalClose={handleManageEpisodesModalClose}
      />

      <SeriesHistoryModal
        isOpen={isHistoryModalOpen}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleHistoryModalClose}
      />

      <SeasonInteractiveSearchModal
        isOpen={isInteractiveSearchModalOpen}
        episodeCount={totalEpisodeCount}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleInteractiveSearchModalClose}
      />
    </div>
  );
}

export default SeriesDetailsSeason;
