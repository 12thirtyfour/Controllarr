import classNames from 'classnames';
import moment from 'moment-timezone';
import React, {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useNavigate } from 'react-router-dom';
import {
  normalizeRegionOrder,
  useAddPlatformOption,
} from 'AddPlatform/addPlatformOptionsStore';
import CommandNames from 'Commands/CommandNames';
import { useCommands, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MetadataAttribution from 'Components/MetadataAttribution';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { OverflowDivider } from 'Components/Page/Toolbar/Overflow';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import useGames from 'Game/useGames';
import useRomFiles from 'RomFile/useRomFiles';
import usePrevious from 'Helpers/Hooks/usePrevious';
import {
  resolveConsoleThemeFromSlug,
  setActivePlatformTheme,
} from 'Helpers/Hooks/useTheme';
import {
  icons,
  kinds,
  sizes,
  sortDirections,
  tooltipPositions,
} from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import useCountryName from 'Internationalization/useCountryName';
import OrganizePreviewModal from 'Organize/OrganizePreviewModal';
import DeletePlatformModal from 'Platform/Delete/DeletePlatformModal';
import EditPlatformModal from 'Platform/Edit/EditPlatformModal';
import SdCardExportModal from 'Platform/Export/SdCardExportModal';
import PlatformHistoryModal from 'Platform/History/PlatformHistoryModal';
import MonitoringOptionsModal from 'Platform/MonitoringOptions/MonitoringOptionsModal';
import { Image, PlatformStatus, Statistics } from 'Platform/Platform';
import PlatformGenres from 'Platform/PlatformGenres';
import { getPlatformStatusDetails } from 'Platform/PlatformStatus';
import usePlatform, {
  useSingleSeries,
  useToggleSeriesMonitored,
} from 'Platform/usePlatform';
import { loadLocalLibretroConfig } from 'Settings/Libretro/LibretroSettings';
import QualityProfileName from 'Settings/Profiles/Quality/QualityProfileName';
import sortByProp from 'Utilities/Array/sortByProp';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import formatBytes from 'Utilities/Number/formatBytes';
import {
  registerPagePopulator,
  unregisterPagePopulator,
} from 'Utilities/pagePopulator';
import filterAlternateTitles from 'Utilities/Platform/filterAlternateTitles';
import translate from 'Utilities/String/translate';
import toggleSelected from 'Utilities/Table/toggleSelected';
import PlatformBiosSection from './PlatformBiosSection';
import RetroAchievementsShowcaseModal from './RetroAchievementsShowcaseModal';
import SeriesAlternateTitles from './PlatformAlternateTitles';
import PlatformDetailsLinks from './PlatformDetailsLinks';
import PlatformDetailsPath from './PlatformDetailsPath';
import PlatformDetailsProvider from './PlatformDetailsProvider';
import PlatformDetailsSeason from './PlatformDetailsRegion';
import SeriesProgressLabel from './PlatformProgressLabel';
import SeriesTags from './PlatformTags';
import styles from './PlatformDetails.module.css';

function getFanartUrl(images: Image[]) {
  return images.find((image) => image.coverType === 'fanart')?.url;
}

function getConsoleArtUrl(images: Image[], titleSlug?: string) {
  const poster = images.find((image) => image.coverType === 'poster');

  if (
    poster?.remoteUrl &&
    poster.remoteUrl.startsWith('/Content/Images/Consoles/')
  ) {
    return poster.remoteUrl;
  }

  if (poster?.url && poster.url.startsWith('/Content/Images/Consoles/')) {
    return poster.url;
  }

  if (titleSlug) {
    return `/Content/Images/Consoles/${titleSlug}.png`;
  }

  return poster?.remoteUrl || poster?.url;
}

function getDateYear(date: string) {
  return moment.utc(date).format('YYYY');
}

function getRunningYears(
  status: PlatformStatus,
  year: number,
  lastAired: string | undefined
) {
  if (year === 0) {
    return null;
  }

  return status === 'ended' && lastAired
    ? `${year}-${getDateYear(lastAired)}`
    : `${year}-`;
}

interface ExpandedState {
  allExpanded: boolean;
  allCollapsed: boolean;
  seasons: Record<number, boolean>;
}

interface PlatformDetailsProps {
  seriesId: number;
}

function PlatformDetails({ seriesId }: PlatformDetailsProps) {
  const navigate = useNavigate();
  const executeCommand = useExecuteCommand();

  const series = useSingleSeries(seriesId);
  const { toggleSeriesMonitored, isTogglingSeriesMonitored } =
    useToggleSeriesMonitored(seriesId);
  const { data: allSeries } = usePlatform();

  useLayoutEffect(() => {
    const consoleTheme = resolveConsoleThemeFromSlug(
      series?.titleSlug || series?.title
    );
    setActivePlatformTheme(consoleTheme);

    return () => {
      setActivePlatformTheme(null);
    };
  }, [series?.titleSlug, series?.title]);

  const monitored = series?.monitored ?? false;

  const {
    isFetching: isGamesFetching,
    isFetched: isGamesFetched,
    error: gamesError,
    data,
    refetch: refetchGames,
  } = useGames({ seriesId });

  const { hasGames, hasMonitoredGames } = useMemo(() => {
    return {
      hasGames: data.length > 0,
      hasMonitoredGames: data.some((e) => e.monitored),
    };
  }, [data]);

  const {
    isFetching: isRomFilesFetching,
    isFetched: isRomFilesFetched,
    error: romFilesError,
    hasRomFiles,
    refetch: refetchRomFiles,
  } = useRomFiles({ seriesId });

  const { data: commands } = useCommands();

  const { isRefreshing, isRenaming, isSearching, isSearchingCollection } = useMemo(() => {
    const seriesRefreshingCommand = findCommand(commands, {
      name: CommandNames.RefreshSeries,
    });

    const isSeriesRefreshingCommandExecuting = isCommandExecuting(
      seriesRefreshingCommand
    );

    const allSeriesRefreshing =
      isSeriesRefreshingCommandExecuting &&
      seriesRefreshingCommand &&
      (!('seriesIds' in seriesRefreshingCommand.body) ||
        seriesRefreshingCommand.body.seriesIds.length === 0);

    const isSeriesRefreshing =
      isSeriesRefreshingCommandExecuting &&
      seriesRefreshingCommand &&
      'seriesIds' in seriesRefreshingCommand.body &&
      seriesRefreshingCommand.body.seriesIds.includes(seriesId);

    const isSearchingExecuting = isCommandExecuting(
      findCommand(commands, {
        name: CommandNames.SeriesSearch,
        seriesId,
      })
    );

    const isSearchingCollectionExecuting = isCommandExecuting(
      findCommand(commands, {
        name: CommandNames.PlatformCollectionSearch,
        seriesId,
      })
    );

    const isRenamingFiles = isCommandExecuting(
      findCommand(commands, {
        name: CommandNames.RenameFiles,
        seriesId,
      })
    );

    const isRenamingSeriesCommand = findCommand(commands, {
      name: CommandNames.RenameSeries,
    });

    const isRenamingSeries =
      isCommandExecuting(isRenamingSeriesCommand) &&
      isRenamingSeriesCommand &&
      'seriesIds' in isRenamingSeriesCommand.body &&
      isRenamingSeriesCommand.body.seriesIds.includes(seriesId);

    return {
      isRefreshing: isSeriesRefreshing || allSeriesRefreshing,
      isRenaming: isRenamingFiles || isRenamingSeries,
      isSearching: isSearchingExecuting,
      isSearchingCollection: isSearchingCollectionExecuting,
    };
  }, [seriesId, commands]);

  const { nextSeries, previousSeries } = useMemo(() => {
    const sortedSeries = [...allSeries].sort(sortByProp('sortTitle'));
    const platformIndex = sortedSeries.findIndex(
      (series) => series.id === seriesId
    );

    if (platformIndex === -1) {
      return {
        nextSeries: undefined,
        previousSeries: undefined,
      };
    }

    const nextSeries = sortedSeries[platformIndex + 1] ?? sortedSeries[0];
    const previousSeries =
      sortedSeries[platformIndex - 1] ?? sortedSeries[sortedSeries.length - 1];

    return {
      nextSeries: {
        title: nextSeries.title,
        titleSlug: nextSeries.titleSlug,
      },
      previousSeries: {
        title: previousSeries.title,
        titleSlug: previousSeries.titleSlug,
      },
    };
  }, [seriesId, allSeries]);

  const [isOrganizeModalOpen, setIsOrganizeModalOpen] = useState(false);
  const [isManageGamesOpen, setIsManageGamesOpen] = useState(false);
  const [isEditPlatformModalOpen, setIsEditPlatformModalOpen] = useState(false);
  const [isDeletePlatformModalOpen, setIsDeletePlatformModalOpen] = useState(false);
  const [isOverviewExpanded, setIsOverviewExpanded] = useState(false);

  const handleToggleOverview = useCallback(() => {
    setIsOverviewExpanded((value) => !value);
  }, []);
  const [isPlatformHistoryModalOpen, setIsPlatformHistoryModalOpen] =
    useState(false);
  const [isExportModalOpen, setIsExportModalOpen] = useState(false);
  const [isRetroAchievementsModalOpen, setIsRetroAchievementsModalOpen] =
    useState(false);
  const [isMonitorOptionsModalOpen, setIsMonitorOptionsModalOpen] =
    useState(false);
  const [expandedState, setExpandedState] = useState<ExpandedState>({
    allExpanded: false,
    allCollapsed: true,
    seasons: {},
  });
  const storedRegionOrder = useAddPlatformOption('regionOrder');
  const orderedRegionIds = useMemo(
    () => normalizeRegionOrder(storedRegionOrder),
    [storedRegionOrder]
  );
  const wasRefreshing = usePrevious(isRefreshing);
  const wasRenaming = usePrevious(isRenaming);

  const alternateTitles = useMemo(() => {
    if (!series) {
      return [];
    }

    return filterAlternateTitles(
      series.alternateTitles,
      series.title,
      series.useSceneNumbering
    );
  }, [series]);

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

  const handleEditPlatformPress = useCallback(() => {
    setIsEditPlatformModalOpen(true);
  }, []);

  const handleEditPlatformModalClose = useCallback(() => {
    setIsEditPlatformModalOpen(false);
  }, []);

  const handleDeletePlatformPress = useCallback(() => {
    setIsEditPlatformModalOpen(false);
    setIsDeletePlatformModalOpen(true);
  }, []);

  const handleDeletePlatformModalClose = useCallback(() => {
    setIsDeletePlatformModalOpen(false);
  }, []);

  const handleSeriesHistoryPress = useCallback(() => {
    setIsPlatformHistoryModalOpen(true);
  }, []);

  const handlePlayNowPress = useCallback(() => {
    const config = loadLocalLibretroConfig();

    if (config.preferredLaunchMode === 'protocol') {
      window.location.href = `controllarr://play/${seriesId}`;
      return;
    }

    navigate(`/webplayer?gameId=${seriesId}`);
  }, [navigate, seriesId]);

  const handlePlatformHistoryModalClose = useCallback(() => {
    setIsPlatformHistoryModalOpen(false);
  }, []);

  const handleExportPress = useCallback(() => {
    setIsExportModalOpen(true);
  }, []);

  const handleExportModalClose = useCallback(() => {
    setIsExportModalOpen(false);
  }, []);

  const handleRetroAchievementsPress = useCallback(() => {
    setIsRetroAchievementsModalOpen(true);
  }, []);

  const handleRetroAchievementsModalClose = useCallback(() => {
    setIsRetroAchievementsModalOpen(false);
  }, []);

  const handleMonitorOptionsPress = useCallback(() => {
    setIsMonitorOptionsModalOpen(true);
  }, []);

  const handleMonitorOptionsClose = useCallback(() => {
    setIsMonitorOptionsModalOpen(false);
  }, []);

  const handleExpandAllPress = useCallback(() => {
    const expandAll = !expandedState.allExpanded;

    const newSeasons = Object.keys(expandedState.seasons).reduce<
      Record<number | string, boolean>
    >((acc, item) => {
      acc[item] = expandAll;
      return acc;
    }, {});

    setExpandedState({
      allExpanded: expandAll,
      allCollapsed: !expandAll,
      seasons: newSeasons,
    });
  }, [expandedState]);

  const handleExpandPress = useCallback(
    (seasonNumber: number, isExpanded: boolean) => {
      setExpandedState((state) => {
        const { allExpanded, allCollapsed } = state;

        const convertedState = {
          allSelected: allExpanded,
          allUnselected: allCollapsed,
          selectedState: state.seasons,
          lastToggled: null,
        };

        const newState = toggleSelected(
          convertedState,
          [],
          seasonNumber,
          isExpanded,
          false
        );

        return {
          allExpanded: newState.allSelected,
          allCollapsed: newState.allUnselected,
          seasons: newState.selectedState,
        };
      });
    },
    []
  );

  const handleMonitorToggle = useCallback(() => {
    toggleSeriesMonitored({
      monitored: !monitored,
    });
  }, [toggleSeriesMonitored, monitored]);

  const handleRefreshPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RefreshSeries,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const handleSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SeriesSearch,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const handleSearchCollectionPress = useCallback(() => {
    executeCommand({
      name: CommandNames.PlatformCollectionSearch,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const populate = useCallback(() => {
    refetchGames();
    refetchRomFiles();
  }, [refetchGames, refetchRomFiles]);

  const originalCountryName = useCountryName(series?.originalCountry);

  useEffect(() => {
    populate();
  }, [populate]);

  useEffect(() => {
    registerPagePopulator(populate, ['seriesUpdated']);

    return () => {
      unregisterPagePopulator(populate);
    };
  }, [populate]);

  useEffect(() => {
    if ((!isRefreshing && wasRefreshing) || (!isRenaming && wasRenaming)) {
      populate();
    }
  }, [isRefreshing, wasRefreshing, isRenaming, wasRenaming, populate]);

  const overviewRef = useRef<HTMLParagraphElement>(null);
  const [isOverviewClamped, setIsOverviewClamped] = useState(false);

  useLayoutEffect(() => {
    const element = overviewRef.current;

    if (!element) {
      return;
    }

    const compute = () =>
      setIsOverviewClamped(
        !isOverviewExpanded && element.scrollHeight > element.clientHeight
      );

    compute();

    const observer = new ResizeObserver(compute);
    observer.observe(element);

    return () => observer.disconnect();
  }, [series?.overview, isOverviewExpanded]);

  let expandIcon = icons.EXPAND_INDETERMINATE;

  if (expandedState.allExpanded) {
    expandIcon = icons.COLLAPSE;
  } else if (expandedState.allCollapsed) {
    expandIcon = icons.EXPAND;
  }

  if (!series) {
    return null;
  }

  const {
    tvdbId,
    tvMazeId,
    imdbId,
    tmdbId,
    title,
    titleSlug,
    runtime,
    ratings,
    path,
    pendingPath,
    statistics = {} as Statistics,
    qualityProfileId,
    language,
    status,
    network,
    originalLanguage,
    overview,
    images,
    seasons,
    genres,
    tags,
    year,
    lastAired,
    collectionStrategy,
    revisionPreference,
    pinnedVersion,
  } = series;

  const { gameCount = 0, romFileCount = 0, sizeOnDisk = 0 } = statistics;

  const statusDetails = getPlatformStatusDetails(status);
  const runningYears = getRunningYears(status, year, lastAired);

  let romFilesCountMessage = translate('PlatformDetailsNoRomFiles');

  if (romFileCount === 1) {
    romFilesCountMessage = translate('PlatformDetailsOneRomFile');
  } else if (romFileCount > 1) {
    romFilesCountMessage = translate('PlatformDetailsCountRomFiles', {
      romFileCount,
    });
  }

  const fanartUrl = getFanartUrl(images);
  const consoleArtUrl = getConsoleArtUrl(images, titleSlug);
  let collectionStrategyLabel = '1G1R';

  if (
    collectionStrategy === 1 ||
    String(collectionStrategy) === 'preservationist'
  ) {
    collectionStrategyLabel = 'Completionist';
  } else if (
    collectionStrategy === 2 ||
    String(collectionStrategy) === 'oneGamePerRegionPerConsole'
  ) {
    collectionStrategyLabel = '1G1R Multi-Region';
  }

  let revisionPreferenceLabel = ' (Latest Rev)';

  if (pinnedVersion) {
    revisionPreferenceLabel = ` (Pinned: ${pinnedVersion})`;
  } else if (collectionStrategyLabel === 'Completionist') {
    revisionPreferenceLabel = '';
  } else if (
    revisionPreference === 1 ||
    String(revisionPreference) === 'initialRelease'
  ) {
    revisionPreferenceLabel = ' (Original Release)';
  }

  const isFetching = isGamesFetching || isRomFilesFetching;
  const isPopulated = isGamesFetched && isRomFilesFetched;

  return (
    <PlatformDetailsProvider seriesId={seriesId}>
      <PageContent title={title}>
        <PageToolbar>
          <ToolbarItem
            id="refresh"
            priority={1}
            groupId="left-a"
            label={translate('RefreshAndScan')}
            iconName={icons.REFRESH}
            spinningName={icons.REFRESH}
            title={translate('RefreshAndScanTooltip')}
            isSpinning={isRefreshing}
            onPress={handleRefreshPress}
          />

          <ToolbarItem
            id="search-monitored"
            priority={1}
            groupId="left-a"
            label={translate('SearchMonitored')}
            iconName={icons.SEARCH}
            isDisabled={!monitored || !hasMonitoredGames || !hasGames}
            isSpinning={isSearching}
            title={
              hasMonitoredGames
                ? undefined
                : translate('NoMonitoredGames')
            }
            onPress={handleSearchPress}
          />

          <ToolbarItem
            id="search-collection"
            priority={1}
            groupId="left-a"
            label={translate('SearchForCollection')}
            iconName={icons.SEARCH}
            isDisabled={!monitored || !hasGames}
            isSpinning={isSearchingCollection}
            title={translate('SearchForCollectionHelpText')}
            onPress={handleSearchCollectionPress}
          />

          <OverflowDivider groupId="left-a">
            <PageToolbarSeparator />
          </OverflowDivider>

          <ToolbarItem
            id="preview-rename"
            priority={1}
            groupId="left-b"
            label={translate('PreviewRename')}
            iconName={icons.ORGANIZE}
            isDisabled={!hasRomFiles}
            onPress={handleOrganizePress}
          />

          <ToolbarItem
            id="manage-games"
            priority={1}
            groupId="left-b"
            label={translate('ManageGames')}
            iconName={icons.EPISODE_FILE}
            onPress={handleManageGamesPress}
          />

          <ToolbarItem
            id="export-sd-card"
            priority={1}
            groupId="left-b"
            label="Export to SD Card"
            iconName={icons.EXPORT}
            isDisabled={!hasRomFiles}
            onPress={handleExportPress}
          />

          <ToolbarItem
            id="play-now"
            priority={1}
            groupId="left-b"
            label="Play Now"
            iconName={icons.QUICK}
            isDisabled={!hasRomFiles}
            onPress={handlePlayNowPress}
          />

          <ToolbarItem
            id="history"
            priority={1}
            groupId="left-b"
            label={translate('History')}
            iconName={icons.HISTORY}
            isDisabled={!hasGames}
            onPress={handleSeriesHistoryPress}
          />

          <OverflowDivider groupId="left-b">
            <PageToolbarSeparator />
          </OverflowDivider>

          <ToolbarItem
            id="game-monitoring"
            priority={1}
            groupId="left-c"
            label={translate('GameMonitoring')}
            iconName={icons.MONITORED}
            onPress={handleMonitorOptionsPress}
          />

          <ToolbarItem
            id="edit"
            priority={1}
            groupId="left-c"
            label={translate('Edit')}
            iconName={icons.EDIT}
            onPress={handleEditPlatformPress}
          />

          <ToolbarItem
            id="delete"
            priority={1}
            groupId="left-c"
            label={translate('Delete')}
            iconName={icons.DELETE}
            onPress={handleDeletePlatformPress}
          />

          <PageToolbarSpacer />

          <ToolbarItem
            id="expand-all"
            priority={2}
            groupId="right"
            label={
              expandedState.allExpanded
                ? translate('CollapseAll')
                : translate('ExpandAll')
            }
            iconName={expandIcon}
            onPress={handleExpandAllPress}
          />
        </PageToolbar>

        <PageContentBody innerClassName={styles.innerContentBody}>
          <div className={styles.header}>
            <div
              className={styles.backdrop}
              style={
                fanartUrl ? { backgroundImage: `url(${fanartUrl})` } : undefined
              }
            >
              {consoleArtUrl ? (
                <img
                  className={styles.consoleArtBackground}
                  src={consoleArtUrl}
                  alt=""
                  aria-hidden="true"
                />
              ) : null}

              <div className={styles.backdropOverlay} />
            </div>

            <div className={styles.headerContent}>
              <div className={styles.info}>
                <div className={styles.titleRow}>
                  <div className={styles.titleContainer}>
                    <div className={styles.title}>{title}</div>

                    {alternateTitles.length ? (
                      <div className={styles.alternateTitlesIconContainer}>
                        <Popover
                          anchor={
                            <Icon name={icons.ALTERNATE_TITLES} size={18} />
                          }
                          title={translate('AlternateTitles')}
                          body={
                            <SeriesAlternateTitles
                              alternateTitles={alternateTitles}
                            />
                          }
                          position={tooltipPositions.BOTTOM}
                        />
                      </div>
                    ) : null}
                  </div>

                  <div className={styles.seriesNavigationButtons}>
                    {previousSeries ? (
                      <IconButton
                        className={styles.seriesNavigationButton}
                        name={icons.ARROW_LEFT}
                        size={24}
                        title={translate('PlatformDetailsGoTo', {
                          title: previousSeries.title,
                        })}
                        aria-label={translate('PlatformDetailsGoTo', {
                          title: previousSeries.title,
                        })}
                        to={`/platforms/${previousSeries.titleSlug}`}
                      />
                    ) : null}

                    {nextSeries ? (
                      <IconButton
                        className={styles.seriesNavigationButton}
                        name={icons.ARROW_RIGHT}
                        size={24}
                        title={translate('PlatformDetailsGoTo', {
                          title: nextSeries.title,
                        })}
                        aria-label={translate('PlatformDetailsGoTo', {
                          title: nextSeries.title,
                        })}
                        to={`/platforms/${nextSeries.titleSlug}`}
                      />
                    ) : null}
                  </div>
                </div>

                <div className={styles.details}>
                  {runtime ? (
                    <span className={styles.runtime}>
                      {translate('PlatformDetailsRuntime', { runtime })}
                    </span>
                  ) : null}

                  {ratings.value ? (
                    <HeartRating
                      rating={ratings.value}
                      votes={ratings.votes}
                      iconSize={14}
                    />
                  ) : null}

                  {genres.length ? (
                    <PlatformGenres className={styles.genres} genres={genres} />
                  ) : null}

                  {runningYears ? <span>{runningYears}</span> : null}
                </div>

                <div className={styles.detailsChips}>
                  <div className={styles.chipsRow}>
                    <Link
                      className={styles.monitoredToggle}
                      isDisabled={isTogglingSeriesMonitored}
                      aria-pressed={monitored}
                      onPress={handleMonitorToggle}
                    >
                      <Label
                        className={styles.detailsLabel}
                        size={sizes.LARGE}
                        icon={icons.MONITORED}
                        iconFilled={monitored}
                        interactive={true}
                      >
                        <span className={styles.monitoredText}>
                          {monitored
                            ? translate('Monitored')
                            : translate('Unmonitored')}
                        </span>
                      </Label>
                    </Link>

                    <Tooltip
                      anchor={
                        <Label
                          className={styles.detailsLabel}
                          size={sizes.LARGE}
                          icon={icons.EXTERNAL_LINK}
                          interactive={true}
                        >
                          <span className={styles.links}>
                            {translate('Links')}
                          </span>
                        </Label>
                      }
                      contentRole="dialog"
                      tooltip={
                        <PlatformDetailsLinks
                          tvdbId={tvdbId}
                          tvMazeId={tvMazeId}
                          imdbId={imdbId}
                          tmdbId={tmdbId}
                        />
                      }
                      kind={kinds.DEFAULT}
                      position={tooltipPositions.BOTTOM}
                    />

                    {tags.length ? (
                      <Tooltip
                        anchor={
                          <Label
                            className={styles.detailsLabel}
                            size={sizes.LARGE}
                            icon={icons.TAGS}
                            interactive={true}
                          >
                            <span className={styles.tags}>
                              {translate('Tags')}
                            </span>
                          </Label>
                        }
                        tooltip={<SeriesTags seriesId={seriesId} />}
                        kind={kinds.DEFAULT}
                        position={tooltipPositions.BOTTOM}
                      />
                    ) : null}
                  </div>

                  <div className={styles.chipsRow}>
                    <SeriesProgressLabel
                      className={styles.seriesProgressLabel}
                      seriesId={seriesId}
                      monitored={monitored}
                      gameCount={gameCount}
                      romFileCount={romFileCount}
                    />

                    <Tooltip
                      accessibleLabel={translate('SizeOnDisk')}
                      anchor={
                        <Label
                          className={styles.detailsLabel}
                          size={sizes.LARGE}
                          icon={icons.DRIVE}
                        >
                          <span className={styles.sizeOnDisk}>
                            {formatBytes(sizeOnDisk)}
                          </span>
                        </Label>
                      }
                      tooltip={<span>{romFilesCountMessage}</span>}
                      kind={kinds.DEFAULT}
                      position={tooltipPositions.BOTTOM}
                    />

                    <Label
                      className={styles.detailsLabel}
                      title={translate('QualityProfile')}
                      size={sizes.LARGE}
                      icon={icons.PROFILE}
                    >
                      <span className={styles.qualityProfileName}>
                        <QualityProfileName
                          qualityProfileId={qualityProfileId}
                        />
                      </span>
                    </Label>

                    <Link onPress={handleRetroAchievementsPress}>
                      <Label
                        className={styles.detailsLabel}
                        title={translate('RetroAchievements')}
                        size={sizes.LARGE}
                        icon={icons.SCORE}
                        interactive={true}
                      >
                        <span>RA Ready</span>
                      </Label>
                    </Link>

                    <Label
                      className={styles.detailsLabel}
                      title={translate('Collection Strategy')}
                      size={sizes.LARGE}
                      icon={icons.ORGANIZE}
                    >
                      <span>
                        {collectionStrategyLabel}
                        {revisionPreferenceLabel}
                      </span>
                    </Label>

                    <Label
                      className={styles.detailsLabel}
                      title={translate('Language')}
                      size={sizes.LARGE}
                      icon={icons.LANGUAGE}
                    >
                      <span className={styles.languageName}>
                        {language.name}
                      </span>
                    </Label>

                    <Label
                      className={styles.detailsLabel}
                      title={statusDetails.message}
                      size={sizes.LARGE}
                      kind={status === 'deleted' ? kinds.INVERSE : undefined}
                      icon={statusDetails.icon}
                    >
                      <span className={styles.statusName}>
                        {statusDetails.title}
                      </span>
                    </Label>

                    {originalLanguage?.name ? (
                      <Label
                        className={styles.detailsLabel}
                        title={translate('OriginalLanguage')}
                        size={sizes.LARGE}
                        icon={icons.LANGUAGE}
                      >
                        <span className={styles.originalLanguageName}>
                          {originalLanguage.name}
                        </span>
                      </Label>
                    ) : null}

                    {originalCountryName ? (
                      <Label
                        className={styles.detailsLabel}
                        title={translate('OriginalCountry')}
                        size={sizes.LARGE}
                        icon={icons.GLOBE}
                      >
                        <span className={styles.originalCountry}>
                          {originalCountryName}
                        </span>
                      </Label>
                    ) : null}

                    {network ? (
                      <Label
                        className={styles.detailsLabel}
                        title={translate('Network')}
                        size={sizes.LARGE}
                        icon={icons.NETWORK}
                      >
                        <span className={styles.network}>{network}</span>
                      </Label>
                    ) : null}
                  </div>
                </div>

                <div className={styles.heroFooter}>
                  <PlatformDetailsPath path={path} pendingPath={pendingPath} />

                  <MetadataAttribution />
                </div>
              </div>
            </div>
          </div>

          <div className={styles.contentContainer}>
            {overview ? (
              <section className={styles.overviewSection}>
                <div className={styles.overviewFrame} aria-hidden="true">
                  <span className={styles.overviewLabel}>
                    {translate('Overview')}
                  </span>
                </div>

                <p
                  ref={overviewRef}
                  className={classNames(
                    styles.overview,
                    !isOverviewExpanded && styles.overviewClamped
                  )}
                >
                  {overview}
                </p>

                {isOverviewClamped || isOverviewExpanded ? (
                  <Link
                    className={styles.overviewToggle}
                    onPress={handleToggleOverview}
                  >
                    {isOverviewExpanded ? translate('Less') : translate('More')}
                  </Link>
                ) : null}
              </section>
            ) : null}

            {!isPopulated && !gamesError && !romFilesError ? (
              <LoadingIndicator />
            ) : null}

            {!isFetching && gamesError ? (
              <Alert kind={kinds.DANGER}>
                {translate('GamesLoadError')}
              </Alert>
            ) : null}

            {!isFetching && romFilesError ? (
              <Alert kind={kinds.DANGER}>
                {translate('RomFilesLoadError')}
              </Alert>
            ) : null}

            {isPopulated && !!seasons.length ? (
              <div>
                {orderedRegionIds.map((regionId) => {
                  if (regionId === -1) {
                    return (
                      <PlatformBiosSection
                        key="bios"
                        platformSlug={titleSlug}
                      />
                    );
                  }

                  const season = seasons.find(
                    (s) => s.seasonNumber === regionId
                  );

                  if (!season) {
                    return null;
                  }

                  return (
                    <PlatformDetailsSeason
                      key={season.seasonNumber}
                      seriesId={seriesId}
                      {...season}
                      isExpanded={expandedState.seasons[season.seasonNumber]}
                      onExpandPress={handleExpandPress}
                    />
                  );
                })}
                {seasons
                  .filter((s) => !orderedRegionIds.includes(s.seasonNumber))
                  .map((season) => (
                    <PlatformDetailsSeason
                      key={season.seasonNumber}
                      seriesId={seriesId}
                      {...season}
                      isExpanded={expandedState.seasons[season.seasonNumber]}
                      onExpandPress={handleExpandPress}
                    />
                  ))}
              </div>
            ) : null}

            {isPopulated && !seasons.length ? (
              <Alert kind={kinds.WARNING}>
                {translate('NoGameInformation')}
              </Alert>
            ) : null}
          </div>

          <OrganizePreviewModal
            isOpen={isOrganizeModalOpen}
            seriesId={seriesId}
            onModalClose={handleOrganizeModalClose}
          />

          <InteractiveImportModal
            isOpen={isManageGamesOpen}
            seriesId={seriesId}
            title={title}
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
            isOpen={isPlatformHistoryModalOpen}
            seriesId={seriesId}
            onModalClose={handlePlatformHistoryModalClose}
          />

          <EditPlatformModal
            isOpen={isEditPlatformModalOpen}
            seriesId={seriesId}
            onModalClose={handleEditPlatformModalClose}
            onDeletePlatformPress={handleDeletePlatformPress}
          />

          <DeletePlatformModal
            isOpen={isDeletePlatformModalOpen}
            seriesId={seriesId}
            onModalClose={handleDeletePlatformModalClose}
          />

          <MonitoringOptionsModal
            isOpen={isMonitorOptionsModalOpen}
            seriesId={seriesId}
            onModalClose={handleMonitorOptionsClose}
          />

          <SdCardExportModal
            isOpen={isExportModalOpen}
            onModalClose={handleExportModalClose}
          />

          <RetroAchievementsShowcaseModal
            isOpen={isRetroAchievementsModalOpen}
            gameTitle={title}
            platformTitle={title}
            onModalClose={handleRetroAchievementsModalClose}
          />
        </PageContentBody>
      </PageContent>
    </PlatformDetailsProvider>
  );
}

export default PlatformDetails;
