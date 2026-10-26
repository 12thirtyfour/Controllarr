import classNames from 'classnames';
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useDebounce from 'Helpers/Hooks/useDebounce';
import useQueryParams from 'Helpers/Hooks/useQueryParams';
import { icons, kinds } from 'Helpers/Props';
import { usePreferredMetadataLanguage } from 'Language/useLanguages';
import { useHasSeries } from 'Platform/usePlatform';
import MetadataSettingsModal from 'Settings/MetadataSource/MetadataSettingsModal';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddNewPlatformSearchResult from './AddNewPlatformSearchResult';
import { useLookupSeries } from './useAddPlatform';
import styles from './AddNewPlatform.module.css';

type SearchFilterType = 'all' | 'platforms' | 'games';

function AddNewPlatform() {
  const { term: initialTerm = '' } = useQueryParams<{ term: string }>();
  const hasSeries = useHasSeries();
  const language = usePreferredMetadataLanguage();
  const [term, setTerm] = useState(initialTerm);
  const [filterType, setFilterType] = useState<SearchFilterType>('all');
  const searchInputRef = useRef<HTMLInputElement>(null);
  const [isFetching, setIsFetching] = useState(false);
  const [isMetadataSettingsModalOpen, setIsMetadataSettingsModalOpen] =
    useState(false);
  const query = useDebounce(term, term ? 300 : 0);

  const handleSearchInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTerm(value);
      setIsFetching(!!value.trim());
    },
    []
  );

  const handleMetadataSettingsPress = useCallback(() => {
    setIsMetadataSettingsModalOpen(true);
  }, []);

  const handleMetadataSettingsModalClose = useCallback(() => {
    setIsMetadataSettingsModalOpen(false);
  }, []);

  const handleClearSeriesLookupPress = useCallback(() => {
    setTerm('');
    setIsFetching(false);
    searchInputRef.current?.focus();
  }, []);

  const handleFilterAllPress = useCallback(() => {
    setFilterType('all');
  }, []);

  const handleFilterPlatformsPress = useCallback(() => {
    setFilterType('platforms');
  }, []);

  const handleFilterGamesPress = useCallback(() => {
    setFilterType('games');
  }, []);

  const {
    isFetching: isFetchingApi,
    error,
    data,
  } = useLookupSeries(query, language);

  const filteredData = useMemo(() => {
    if (filterType === 'platforms') {
      return data.filter((item) => item.tvdbId < 100000);
    }

    if (filterType === 'games') {
      return data.filter((item) => item.tvdbId >= 100000);
    }

    return data;
  }, [data, filterType]);

  const platformCount = useMemo(
    () => data.filter((item) => item.tvdbId < 100000).length,
    [data]
  );

  const gameCount = useMemo(
    () => data.filter((item) => item.tvdbId >= 100000).length,
    [data]
  );

  useEffect(() => {
    setIsFetching(isFetchingApi);
  }, [isFetchingApi]);

  useEffect(() => {
    setTerm(initialTerm);
  }, [initialTerm]);

  return (
    <PageContent title={translate('AddNewPlatform')}>
      <PageContentBody>
        <PageHeading
          scope={translate('Media')}
          title={translate('AddNewPlatform')}
          actions={
            <Button onPress={handleMetadataSettingsPress}>
              {translate('MetadataSettings')}
            </Button>
          }
        />

        <div className={styles.searchSticky}>
          <div className={styles.searchWrap}>
            <Icon className={styles.searchIcon} name={icons.SEARCH} size={18} />

            <TextInput
              ref={searchInputRef}
              className={styles.searchInput}
              name="seriesLookup"
              value={term}
              placeholder="e.g. Pokemon, Chrono Trigger, Super Nintendo, Game Boy Advance..."
              autoFocus={true}
              onChange={handleSearchInputChange}
            />

            {term ? (
              <Button
                className={styles.clearLookupButton}
                onPress={handleClearSeriesLookupPress}
              >
                <Icon name={icons.REMOVE} size={14} />
              </Button>
            ) : null}
          </div>

          <div className={styles.filterRow}>
            <span className={styles.filterLabel}>Show:</span>

            <button
              type="button"
              className={classNames(
                styles.filterPill,
                filterType === 'all' && styles.filterPillActive
              )}
              onClick={handleFilterAllPress}
            >
              All ({data.length})
            </button>

            <button
              type="button"
              className={classNames(
                styles.filterPill,
                filterType === 'platforms' && styles.filterPillActive
              )}
              onClick={handleFilterPlatformsPress}
            >
              Consoles / Platforms ({platformCount})
            </button>

            <button
              type="button"
              className={classNames(
                styles.filterPill,
                filterType === 'games' && styles.filterPillActive
              )}
              onClick={handleFilterGamesPress}
            >
              Games / ROMs ({gameCount})
            </button>
          </div>
        </div>

        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && !!error ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('AddNewPlatformError')}
            </div>
            <p className={styles.error}>{getErrorMessage(error)}</p>
          </div>
        ) : null}

        {!isFetching && !error && !!filteredData.length ? (
          <div className={styles.searchResults}>
            <div className={styles.resultsLabel}>
              {filteredData.length === 1
                ? translate('CountResult', { count: filteredData.length })
                : translate('CountResults', { count: filteredData.length })}
            </div>

            {filteredData.map((item) => (
              <AddNewPlatformSearchResult key={item.tvdbId} series={item} />
            ))}
          </div>
        ) : null}

        {!isFetching && !error && !filteredData.length && term ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('CouldNotFindResults', { term })}
            </div>
            <div className={styles.emptyBody}>
              {translate('SearchByTvdbId')}
            </div>
            <div>
              <Link className={styles.emptyLink} to="/settings/metadatasource">
                {translate('WhyCantIFindMyShow')}
              </Link>
            </div>
          </div>
        ) : null}

        {term ? null : (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('AddNewPlatformHelpText')}
            </div>
            <div className={styles.emptyBody}>
              {translate('SearchByTvdbId')}
            </div>

            {hasSeries ? null : (
              <>
                <div className={styles.emptyDivider} />
                <div className={styles.emptyNoLibrary}>
                  {translate('NoSeriesHaveBeenAdded')}
                </div>
                <Button to="/add/import" kind={kinds.PRIMARY}>
                  {translate('ImportExistingSeries')}
                </Button>
              </>
            )}
          </div>
        )}

        <MetadataSettingsModal
          isOpen={isMetadataSettingsModalOpen}
          onModalClose={handleMetadataSettingsModalClose}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default AddNewPlatform;
