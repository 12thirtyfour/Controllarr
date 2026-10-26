import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import AddPlatform from 'AddPlatform/AddPlatform';
import { useAppDimension } from 'App/appStore';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import MetadataAttribution from 'Components/MetadataAttribution';
import { icons } from 'Helpers/Props';
import { useLanguageById } from 'Language/useLanguages';
import { Statistics } from 'Platform/Platform';
import PlatformGenres from 'Platform/PlatformGenres';
import PlatformPoster from 'Platform/PlatformPoster';
import useExistingPlatform from 'Platform/useExistingPlatform';
import { useMetadataSourceSettingsValues } from 'Settings/MetadataSource/useMetadataSourceSettings';
import translate from 'Utilities/String/translate';
import AddNewPlatformModal from './AddNewPlatformModal';
import styles from './AddNewPlatformSearchResult.module.css';

function getStatusChip(status: string) {
  switch (status) {
    case 'continuing':
      return { dot: styles.chipDotAiring, label: translate('Continuing') };
    case 'upcoming':
      return { dot: styles.chipDotUpcoming, label: translate('Upcoming') };
    case 'ended':
      return { dot: styles.chipDotEnded, label: translate('Ended') };
    case 'deleted':
      return { dot: styles.chipDotFinished, label: translate('Deleted') };
    default:
      return null;
  }
}

interface AddNewPlatformSearchResultProps {
  series: AddPlatform;
}

function AddNewPlatformSearchResult({ series }: AddNewPlatformSearchResultProps) {
  const {
    tvdbId,
    titleSlug,
    title: lookupTitle,
    originalTitle,
    year,
    network,
    originalLanguage,
    genres = [],
    status,
    statistics = {} as Statistics,
    ratings,
    overview: lookupOverview,
    seriesType,
    translations,
    images,
    isExcluded,
  } = series;

  const { preferredMetadataLanguage } = useMetadataSourceSettingsValues();
  const language = useLanguageById(preferredMetadataLanguage);
  const isExistingSeries = useExistingPlatform(tvdbId);
  const isSmallScreen = useAppDimension('isSmallScreen');
  const [isNewAddPlatformModalOpen, setIsNewAddPlatformModalOpen] = useState(false);

  const seasonCount = statistics.seasonCount;
  const translation = translations.find((t) => t.language.id === language?.id);
  const title = translation?.title ?? originalTitle ?? lookupTitle;
  const overview = translation?.overview ?? lookupOverview;

  const handlePress = useCallback(() => {
    setIsNewAddPlatformModalOpen(true);
  }, []);

  const handleAddPlatformModalClose = useCallback(() => {
    setIsNewAddPlatformModalOpen(false);
  }, []);

  const handleTvdbLinkPress = useCallback((event: React.SyntheticEvent) => {
    event.stopPropagation();
  }, []);

  const linkProps = isExistingSeries
    ? { to: `/platforms/${titleSlug}` }
    : { onPress: handlePress };
  let seasons = translate('OneSeason');

  if (seasonCount > 1) {
    seasons = translate('CountSeasons', { count: seasonCount });
  }

  const statusChip = getStatusChip(status);

  return (
    <div
      className={classNames(
        styles.searchResult,
        isExcluded && styles.searchResultExcluded
      )}
    >
      <Link
        className={styles.underlay}
        aria-label={
          isExistingSeries ? title : translate('AddPlatformWithTitle', { title })
        }
        {...linkProps}
      />

      <div className={styles.overlay}>
        {isSmallScreen ? null : (
          <PlatformPoster
            className={styles.poster}
            images={images}
            size={250}
            overflow={true}
            lazy={false}
            title={title}
          />
        )}

        <div className={styles.content}>
          <div className={styles.titleRow}>
            <div className={styles.titleContainer}>
              <div className={styles.title}>
                {title}

                {!title.includes(String(year)) && year ? (
                  <span className={styles.year}>({year})</span>
                ) : null}
              </div>
            </div>

            <div className={styles.icons}>
              {isExistingSeries ? (
                <Icon
                  className={styles.alreadyExistsIcon}
                  name={icons.CHECK_CIRCLE}
                  size={24}
                  title={translate('AlreadyInYourLibrary')}
                />
              ) : null}

              {isExcluded ? (
                <Icon
                  className={styles.excludedIcon}
                  name={icons.DANGER}
                  size={24}
                  title={translate('SeriesInImportListExclusions')}
                />
              ) : null}

              <Link
                className={styles.tvdbLink}
                to={`https://www.igdb.com/search?q=${encodeURIComponent(
                  title
                )}`}
                aria-label={translate('ViewSeriesOnTvdb', { title })}
                onPress={handleTvdbLinkPress}
              >
                <Icon name={icons.EXTERNAL_LINK} size={16} aria-hidden={true} />
              </Link>
            </div>
          </div>

          <div className={styles.chips}>
            {ratings.votes > 0 ? (
              <span className={styles.chip}>
                <HeartRating
                  rating={ratings.value}
                  votes={ratings.votes}
                  iconSize={11}
                />
              </span>
            ) : null}

            {statusChip ? (
              <span className={styles.chip}>
                <span className={classNames(styles.chipDot, statusChip.dot)} />
                {statusChip.label}
              </span>
            ) : null}

            {network ? <span className={styles.chip}>{network}</span> : null}

            {originalLanguage?.name ? (
              <span className={styles.chip}>{originalLanguage.name}</span>
            ) : null}

            {genres.length > 0 ? (
              <span className={styles.chip}>
                <PlatformGenres className={styles.genres} genres={genres} />
              </span>
            ) : null}

            {seasonCount ? (
              <span className={styles.chip}>{seasons}</span>
            ) : null}
          </div>

          <div className={styles.overview}>{overview}</div>

          <MetadataAttribution />
        </div>
      </div>

      <AddNewPlatformModal
        isOpen={isNewAddPlatformModalOpen && !isExistingSeries}
        series={series}
        initialSeriesType={seriesType}
        onModalClose={handleAddPlatformModalClose}
      />
    </div>
  );
}

export default AddNewPlatformSearchResult;
