import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import CheckInput from 'Components/Form/CheckInput';
import HeartRating from 'Components/HeartRating';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import PlatformTagList from 'Components/PlatformTagList';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import Column from 'Components/Table/Column';
import getReleaseTypeName from 'Game/getReleaseTypeName';
import { icons } from 'Helpers/Props';
import useCountryName from 'Internationalization/useCountryName';
import DeletePlatformModal from 'Platform/Delete/DeletePlatformModal';
import EditPlatformModal from 'Platform/Edit/EditPlatformModal';
import { Statistics } from 'Platform/Platform';
import PlatformBanner from 'Platform/PlatformBanner';
import SeriesMovePending from 'Platform/PlatformMovePending';
import { usePlatformTableOptions } from 'Platform/platformOptionsStore';
import PlatformTitleLink from 'Platform/PlatformTitleLink';
import { SelectStateInputProps } from 'typings/props';
import formatBytes from 'Utilities/Number/formatBytes';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import PlatformIndexProgressBar from '../ProgressBar/PlatformIndexProgressBar';
import usePlatformIndexItem from '../usePlatformIndexItem';
import hasGrowableColumns from './hasGrowableColumns';
import SeasonsCell from './RegionsCell';
import PlatformStatusCell from './PlatformStatusCell';
import styles from './PlatformIndexRow.module.css';

interface PlatformIndexRowProps {
  seriesId: number;
  sortKey: string;
  columns: Column[];
  isSelectMode: boolean;
}

function PlatformIndexRow(props: PlatformIndexRowProps) {
  const { seriesId, columns, isSelectMode } = props;

  const {
    series,
    qualityProfile,
    latestSeason,
    isRefreshingSeries,
    isSearchingSeries,
  } = usePlatformIndexItem(seriesId);

  const { showBanners, showSearchAction } = usePlatformTableOptions();

  const executeCommand = useExecuteCommand();
  const [hasBannerError, setHasBannerError] = useState(false);
  const [isEditPlatformModalOpen, setIsEditPlatformModalOpen] = useState(false);
  const [isDeletePlatformModalOpen, setIsDeletePlatformModalOpen] = useState(false);
  const { getIsSelected, toggleSelected } = useSelect();
  const originalCountryName = useCountryName(series?.originalCountry);

  const onRefreshPress = useCallback(() => {
    executeCommand({
      name: CommandNames.RefreshSeries,
      seriesIds: [seriesId],
    });
  }, [seriesId, executeCommand]);

  const onSearchPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SeriesSearch,
      seriesId,
    });
  }, [seriesId, executeCommand]);

  const onBannerLoadError = useCallback(() => {
    setHasBannerError(true);
  }, [setHasBannerError]);

  const onBannerLoad = useCallback(() => {
    setHasBannerError(false);
  }, [setHasBannerError]);

  const onEditPlatformPress = useCallback(() => {
    setIsEditPlatformModalOpen(true);
  }, [setIsEditPlatformModalOpen]);

  const onEditPlatformModalClose = useCallback(() => {
    setIsEditPlatformModalOpen(false);
  }, [setIsEditPlatformModalOpen]);

  const onDeletePlatformPress = useCallback(() => {
    setIsEditPlatformModalOpen(false);
    setIsDeletePlatformModalOpen(true);
  }, [setIsDeletePlatformModalOpen]);

  const onDeletePlatformModalClose = useCallback(() => {
    setIsDeletePlatformModalOpen(false);
  }, [setIsDeletePlatformModalOpen]);

  const checkInputCallback = useCallback(() => {
    // Mock handler to satisfy `onChange` being required for `CheckInput`.
  }, []);

  const onSelectedChange = useCallback(
    ({ id, value, shiftKey }: SelectStateInputProps) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [toggleSelected]
  );

  const nextAiring = series?.nextAiring;

  if (!series) {
    return null;
  }

  const {
    title,
    monitored,
    monitorNewItems,
    status,
    path,
    titleSlug,
    pendingPath,
    previousAiring,
    added,
    statistics = {} as Statistics,
    seasonFolder,
    images,
    seriesType,
    network,
    originalLanguage,
    certification,
    year,
    useSceneNumbering,
    language,
    seasonType,
    seasonTypes,
    genres = [],
    ratings,
    seasons = [],
    tags = [],
  } = series;

  const {
    seasonCount = 0,
    gameCount = 0,
    romFileCount = 0,
    totalGameCount = 0,
    sizeOnDisk = 0,
    releaseGroups = [],
    releaseTypes = [],
    romFileQualities = [],
  } = statistics;

  return (
    <>
      {isSelectMode ? (
        <VirtualTableSelectCell
          id={seriesId}
          isSelected={getIsSelected(seriesId)}
          isDisabled={false}
          onSelectedChange={onSelectedChange}
        />
      ) : null}

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'status') {
          return (
            <PlatformStatusCell
              key={name}
              className={styles[name]}
              seriesId={seriesId}
              monitored={monitored}
              status={status}
              isSelectMode={isSelectMode}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'sortTitle') {
          return (
            <VirtualTableRowCell
              key={name}
              className={classNames(
                styles[name],
                showBanners && styles.banner,
                showBanners && !hasGrowableColumns(columns) && styles.bannerGrow
              )}
            >
              {showBanners ? (
                <Link className={styles.link} to={`/platforms/${titleSlug}`}>
                  <PlatformBanner
                    className={styles.bannerImage}
                    images={images}
                    lazy={false}
                    overflow={true}
                    title={title}
                    onError={onBannerLoadError}
                    onLoad={onBannerLoad}
                  />

                  {hasBannerError ? (
                    <div className={styles.overlayTitle}>{title}</div>
                  ) : null}
                </Link>
              ) : (
                <PlatformTitleLink titleSlug={titleSlug} title={title} />
              )}
            </VirtualTableRowCell>
          );
        }

        if (name === 'seriesType') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {titleCase(seriesType)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'network') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {network}
            </VirtualTableRowCell>
          );
        }

        if (name === 'originalCountry') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {originalCountryName}
            </VirtualTableRowCell>
          );
        }

        if (name === 'originalLanguage') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {originalLanguage.name}
            </VirtualTableRowCell>
          );
        }

        if (name === 'qualityProfileId') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span className={styles.qualityChip}>
                {qualityProfile?.name ?? ''}
              </span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'nextAiring') {
          if (!nextAiring) {
            return (
              <VirtualTableRowCell key={name} className={styles[name]}>
                <span className={styles.nextAiringEmpty}>—</span>
              </VirtualTableRowCell>
            );
          }

          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={classNames(styles[name], styles.nextAiringMono)}
              date={nextAiring}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'previousAiring') {
          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={styles[name]}
              date={previousAiring}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'added') {
          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={styles[name]}
              date={added}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'seasonCount') {
          return (
            <SeasonsCell
              key={name}
              className={styles[name]}
              seriesId={seriesId}
              seasonCount={seasonCount}
              seasons={seasons}
              isSelectMode={isSelectMode}
            />
          );
        }

        if (name === 'seasonFolder') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <CheckInput
                name="seasonFolder"
                value={seasonFolder}
                isDisabled={true}
                onChange={checkInputCallback}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'gameProgress') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <PlatformIndexProgressBar
                seriesId={seriesId}
                monitored={monitored}
                status={status}
                gameCount={gameCount}
                romFileCount={romFileCount}
                totalGameCount={totalGameCount}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'latestSeason') {
          if (!latestSeason) {
            return <VirtualTableRowCell key={name} className={styles[name]} />;
          }

          const seasonStatistics = latestSeason.statistics || {};

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <PlatformIndexProgressBar
                seriesId={seriesId}
                seasonNumber={latestSeason.seasonNumber}
                monitored={monitored}
                status={status}
                gameCount={seasonStatistics.gameCount}
                romFileCount={seasonStatistics.romFileCount}
                totalGameCount={seasonStatistics.totalGameCount}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'gameCount') {
          const isComplete =
            totalGameCount > 0 && romFileCount >= totalGameCount;
          const fillPercent =
            totalGameCount > 0
              ? Math.min(100, (romFileCount / totalGameCount) * 100)
              : 0;

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span>
                {romFileCount} / {totalGameCount}
              </span>
              <div className={styles.gameBar}>
                <div
                  className={classNames(
                    styles.gameBarFill,
                    isComplete && styles.gameBarFillComplete
                  )}
                  style={{ width: `${fillPercent}%` }}
                />
              </div>
            </VirtualTableRowCell>
          );
        }

        if (name === 'year') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {year}
            </VirtualTableRowCell>
          );
        }

        if (name === 'path') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {path}

              {pendingPath ? (
                <SeriesMovePending
                  className={styles.movePendingIcon}
                  pendingPath={pendingPath}
                />
              ) : null}
            </VirtualTableRowCell>
          );
        }

        if (name === 'sizeOnDisk') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {formatBytes(sizeOnDisk)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'averageSizePerGame') {
          const averageSize =
            totalGameCount > 0 ? sizeOnDisk / totalGameCount : 0;

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {averageSize ? formatBytes(averageSize) : null}
            </VirtualTableRowCell>
          );
        }

        if (name === 'genres') {
          const joinedGenres = genres.join(', ');

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span title={joinedGenres}>{joinedGenres}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'ratings') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <HeartRating rating={ratings.value} votes={ratings.votes} />
            </VirtualTableRowCell>
          );
        }

        if (name === 'certification') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {certification}
            </VirtualTableRowCell>
          );
        }

        if (name === 'releaseGroups') {
          const joinedReleaseGroups = releaseGroups.join(', ');
          const truncatedReleaseGroups =
            releaseGroups.length > 3
              ? `${releaseGroups.slice(0, 3).join(', ')}...`
              : joinedReleaseGroups;

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span title={joinedReleaseGroups}>{truncatedReleaseGroups}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'releaseTypes') {
          const joinedReleaseTypes = releaseTypes
            .map(getReleaseTypeName)
            .join(', ');
          const truncatedReleaseTypes =
            releaseTypes.length > 3
              ? `${releaseTypes
                  .slice(0, 3)
                  .map(getReleaseTypeName)
                  .join(', ')}...`
              : joinedReleaseTypes;

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span title={joinedReleaseTypes}>{truncatedReleaseTypes}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'romFileQualities') {
          const joinedQualities = romFileQualities
            .map((q) => q.name)
            .join(', ');
          const truncatedQualities =
            romFileQualities.length > 3
              ? `${romFileQualities
                  .slice(0, 3)
                  .map((q) => q.name)
                  .join(', ')}...`
              : joinedQualities;

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span title={joinedQualities}>{truncatedQualities}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'tags') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <PlatformTagList tags={tags} />
            </VirtualTableRowCell>
          );
        }

        if (name === 'useSceneNumbering') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <CheckInput
                className={styles.checkInput}
                name="useSceneNumbering"
                value={useSceneNumbering}
                isDisabled={true}
                onChange={checkInputCallback}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'monitorNewItems') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {monitorNewItems === 'all'
                ? translate('SeasonsMonitoredAll')
                : translate('SeasonsMonitoredNone')}
            </VirtualTableRowCell>
          );
        }

        if (name === 'language') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {language.name}
            </VirtualTableRowCell>
          );
        }

        if (name === 'seasonType') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {seasonTypes.find((s) => s.type === seasonType)?.name ??
                seasonType}
            </VirtualTableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <SpinnerIconButton
                name={icons.REFRESH}
                title={translate('RefreshSeries')}
                isSpinning={isRefreshingSeries}
                onPress={onRefreshPress}
              />

              {showSearchAction ? (
                <SpinnerIconButton
                  name={icons.SEARCH}
                  title={translate('SearchForMonitoredGames')}
                  isSpinning={isSearchingSeries}
                  onPress={onSearchPress}
                />
              ) : null}

              <IconButton
                name={icons.EDIT}
                title={translate('EditPlatform')}
                aria-label={translate('EditPlatform')}
                onPress={onEditPlatformPress}
              />
            </VirtualTableRowCell>
          );
        }

        return null;
      })}

      <EditPlatformModal
        isOpen={isEditPlatformModalOpen}
        seriesId={seriesId}
        onModalClose={onEditPlatformModalClose}
        onDeletePlatformPress={onDeletePlatformPress}
      />

      <DeletePlatformModal
        isOpen={isDeletePlatformModalOpen}
        seriesId={seriesId}
        onModalClose={onDeletePlatformModalClose}
      />
    </>
  );
}

export default PlatformIndexRow;
