import React, { useCallback, useEffect, useMemo, useState } from 'react';
import AddSeries from 'AddSeries/AddSeries';
import {
  AddSeriesOptions,
  setAddSeriesOption,
  useAddSeriesOptions,
} from 'AddSeries/addSeriesOptionsStore';
import SeriesMonitoringOptionsPopoverContent from 'AddSeries/SeriesMonitoringOptionsPopoverContent';
import { useAppDimension } from 'App/appStore';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { getValidationFailures } from 'Helpers/Hooks/useApiMutation';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import Language, { DEFAULT_LANGUAGE } from 'Language/Language';
import { useLanguageById } from 'Language/useLanguages';
import { SeriesType } from 'Series/Series';
import SeriesPoster from 'Series/SeriesPoster';
import { useMetadataSourceSettingsValues } from 'Settings/MetadataSource/useMetadataSourceSettings';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import selectSettings from 'Utilities/selectSettings';
import translate from 'Utilities/String/translate';
import { useAddSeries } from './useAddSeries';
import styles from './AddNewSeriesModalContent.module.css';

export interface AddNewSeriesModalContentProps {
  series: AddSeries;
  initialSeriesType: SeriesType;
  onModalClose: () => void;
}

const COLLECTION_STRATEGY_OPTIONS = [
  { key: 'oneGameOneRom', value: '1G1R (Single Preferred Region)' },
  {
    key: 'oneGamePerRegionPerConsole',
    value: '1G1R Multi-Region (1 Per Monitored Region)',
  },
  {
    key: 'preservationist',
    value: 'Completionist (All Revisions, Regions & Variants)',
  },
];

const REVISION_PREFERENCE_OPTIONS = [
  {
    key: 'latestRevision',
    value: 'Newest / Latest Revision (Rev A, v1.1, v1.2)',
  },
  {
    key: 'initialRelease',
    value: 'Original Retail Release (Rev 0 / v1.0)',
  },
  { key: 'pinned', value: 'Pinned Version / Tag' },
];

const ENCRYPTION_PREFERENCE_OPTIONS = [
  {
    key: 'notEncrypted',
    value: 'Decrypted',
  },
  {
    key: 'encrypted',
    value: 'Encrypted',
  },
];

const DISC_PLATFORMS = new Set([
  'ps1',
  'ps2',
  'ps3',
  'psp',
  'segacd',
  'saturn',
  'dreamcast',
  'tgcd',
  'gamecube',
  'wii',
  'wiiu',
  'xbox',
  'xbox360',
  'jaguarcd',
]);

function getPlatformCompressionFormat(slugOrTitle?: string): string {
  const normalized = (slugOrTitle ?? '').toLowerCase();

  if (
    DISC_PLATFORMS.has(normalized) ||
    normalized.includes('playstation') ||
    normalized.includes('saturn') ||
    normalized.includes('dreamcast') ||
    normalized.includes('sega cd') ||
    normalized.includes('turbografx-cd') ||
    normalized.includes('gamecube') ||
    normalized.includes('wii') ||
    normalized.includes('xbox') ||
    normalized.includes('jaguar cd')
  ) {
    return normalized.includes('gamecube') || normalized.includes('wii')
      ? 'RVZ / CHD lossless disc compression'
      : 'CHD lossless disc compression (.chd)';
  }

  return 'ZIP / 7z archive compression (.zip / .7z)';
}

function normalizeCollectionStrategy(val: unknown): string {
  if (val === 1 || val === '1' || val === 'preservationist') {
    return 'preservationist';
  }

  if (val === 2 || val === '2' || val === 'oneGamePerRegionPerConsole') {
    return 'oneGamePerRegionPerConsole';
  }

  return 'oneGameOneRom';
}

function normalizeRevisionPreference(val: unknown): string {
  if (val === 1 || val === '1' || val === 'initialRelease') {
    return 'initialRelease';
  }

  if (val === 2 || val === '2' || val === 'pinned') {
    return 'pinned';
  }

  return 'latestRevision';
}

function normalizeEncryptionPreference(
  val: unknown,
  preferEncryptedFallback?: boolean
): string {
  if (
    val === 1 ||
    val === '1' ||
    val === 'encrypted' ||
    (val === undefined && preferEncryptedFallback)
  ) {
    return 'encrypted';
  }

  return 'notEncrypted';
}

function hasEncryptedAndDecryptedCollections(series: {
  tvdbId?: number;
  titleSlug?: string;
}): boolean {
  const slug = (series.titleSlug ?? '').toLowerCase();
  return (
    series.tvdbId === 1012 ||
    series.tvdbId === 1013 ||
    slug === 'nds' ||
    slug === '3ds'
  );
}

function AddNewSeriesModalContent({
  series,
  initialSeriesType,
  onModalClose,
}: AddNewSeriesModalContentProps) {
  const {
    title: lookupTitle,
    originalTitle,
    year,
    overview: lookupOverview,
    images,
    seasonTypes,
    translations,
    folder: originalFolder,
    folders,
  } = series;
  const options = useAddSeriesOptions();
  const { preferredMetadataLanguage } = useMetadataSourceSettingsValues();
  const resolvedLanguage = useLanguageById(preferredMetadataLanguage);
  const [language, setLanguage] = useState<Language>(
    resolvedLanguage ?? DEFAULT_LANGUAGE
  );

  useEffect(() => {
    if (resolvedLanguage) {
      setLanguage(resolvedLanguage);
    }
  }, [resolvedLanguage]);

  const isSmallScreen = useAppDimension('isSmallScreen');
  const isWindows = useIsWindows();

  const { isAdding, addError, addSeries } = useAddSeries(onModalClose);

  const { settings, validationErrors, validationWarnings } = useMemo(() => {
    return {
      ...selectSettings(options, {}),
      ...getValidationFailures(addError),
    };
  }, [options, addError]);

  const [seriesType, setSeriesType] = useState<SeriesType>(
    initialSeriesType === 'standard'
      ? settings.seriesType.value
      : initialSeriesType
  );
  const seasonType = useMemo(
    () =>
      seasonTypes.find((s) => s.type === 'official')?.type ??
      seasonTypes[0]?.type ??
      'official',
    [seasonTypes]
  );

  const {
    monitor,
    qualityProfileId,
    rootFolderPath,
    searchForMissingEpisodes,
    collectionStrategy,
    revisionPreference,
    encryptionPreference,
    pinnedVersion,
    enableCompression,
    preferEncryptedRoms,
    trackUnlicensed,
    trackHomebrew,
    trackRomHacks,
    seriesType: seriesTypeSetting,
    tags,
  } = settings;

  const isIndividualGame = series.tvdbId >= 100000;
  const normalizedStrategy = normalizeCollectionStrategy(
    collectionStrategy?.value
  );
  const normalizedRevision = normalizeRevisionPreference(
    revisionPreference?.value
  );
  const normalizedEncryption = normalizeEncryptionPreference(
    encryptionPreference?.value,
    Boolean(preferEncryptedRoms?.value)
  );
  const autoCompressionLabel = getPlatformCompressionFormat(
    series.titleSlug || series.title
  );

  const translation = translations.find((t) => t.language.id === language.id);
  const title = translation?.title ?? originalTitle ?? lookupTitle;
  const overview = translation?.overview ?? lookupOverview;
  const folder =
    folders.find((f) => f.language.id === language.id)?.folder ??
    originalFolder;

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged<string | number | boolean | number[]>) => {
      setAddSeriesOption(name as keyof AddSeriesOptions, value);
    },
    []
  );

  const handleAddSeriesPress = useCallback(() => {
    const isMonitorNone = monitor.value === 'none';
    const effectiveEncryption =
      normalizedStrategy === 'preservationist' ? 'both' : normalizedEncryption;

    const updatedSeasons = (series.seasons ?? []).map((s) => {
      if (isMonitorNone) {
        return { ...s, monitored: false };
      }

      if (s.seasonNumber === 0) {
        return { ...s, monitored: Boolean(trackUnlicensed?.value) };
      }

      if (s.seasonNumber === 5) {
        return { ...s, monitored: Boolean(trackHomebrew?.value) };
      }

      if (s.seasonNumber === 6) {
        return { ...s, monitored: Boolean(trackRomHacks?.value) };
      }

      return s;
    });

    addSeries({
      ...series,
      monitored: !isMonitorNone,
      seasons: updatedSeasons,
      rootFolderPath: rootFolderPath.value,
      addOptions: {
        monitor: monitor.value,
        searchForMissingEpisodes: searchForMissingEpisodes.value,
        searchForCutoffUnmetEpisodes: false,
      },
      qualityProfileId: qualityProfileId.value || 1,
      collectionStrategy: normalizedStrategy,
      revisionPreference:
        normalizedRevision === 'pinned' ? 'latestRevision' : normalizedRevision,
      pinnedVersion:
        normalizedRevision === 'pinned'
          ? String(pinnedVersion?.value ?? '')
          : '',
      preferredArchiveFormat: Boolean(enableCompression?.value ?? true)
        ? 'auto'
        : 'none',
      encryptionPreference: hasEncryptedAndDecryptedCollections(series)
        ? effectiveEncryption
        : 'notEncrypted',
      preferEncryptedRoms:
        hasEncryptedAndDecryptedCollections(series) &&
        effectiveEncryption === 'encrypted',
      useSceneNumbering:
        hasEncryptedAndDecryptedCollections(series) &&
        effectiveEncryption === 'encrypted',
      seriesType,
      seasonType: hasEncryptedAndDecryptedCollections(series)
        ? effectiveEncryption
        : seasonType,
      language,
      seasonFolder: true,
      tags: tags.value,
    });
  }, [
    series,
    seriesType,
    seasonType,
    rootFolderPath,
    language,
    monitor,
    qualityProfileId,
    normalizedStrategy,
    normalizedRevision,
    normalizedEncryption,
    pinnedVersion,
    enableCompression,
    trackUnlicensed,
    trackHomebrew,
    trackRomHacks,
    searchForMissingEpisodes,
    tags,
    addSeries,
  ]);

  useEffect(() => {
    setSeriesType(seriesTypeSetting.value);
  }, [seriesTypeSetting]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {title}

        {!title.includes(String(year)) && year ? (
          <span className={styles.year}>({year})</span>
        ) : null}
      </ModalHeader>

      <ModalBody>
        <div className={styles.container}>
          {isSmallScreen ? null : (
            <SeriesPoster
              className={styles.poster}
              images={images}
              size={250}
              lazy={false}
              title={title}
            />
          )}

          <div className={styles.info}>
            {overview ? <p className={styles.intro}>{overview}</p> : null}

            <Form
              validationErrors={validationErrors}
              validationWarnings={validationWarnings}
            >
              {isIndividualGame ? (
                <FormRow>
                  <FormLabel>Platform Library Placement</FormLabel>
                  <FormInputHelpText text="This game will automatically be added and monitored under its parent platform folder using your configured Media Management naming structure." />
                </FormRow>
              ) : (
                <>
                  <FormRow>
                    <FormLabel>{translate('RootFolder')}</FormLabel>
                    <FormInputHelpText
                      text={translate('AddNewSeriesRootFolderHelpText', {
                        folder,
                      })}
                    />
                    <FormInput
                      type={inputTypes.ROOT_FOLDER_SELECT}
                      name="rootFolderPath"
                      valueOptions={{
                        seriesFolder: folder,
                        isWindows,
                      }}
                      selectedValueOptions={{
                        seriesFolder: folder,
                        isWindows,
                      }}
                      onChange={handleInputChange}
                      {...rootFolderPath}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>
                      {translate('Monitor')}

                      <Popover
                        anchor={
                          <Icon
                            className={styles.labelIcon}
                            name={icons.INFO}
                          />
                        }
                        title={translate('MonitoringOptions')}
                        body={<SeriesMonitoringOptionsPopoverContent />}
                        position={tooltipPositions.RIGHT}
                      />
                    </FormLabel>

                    <FormInput
                      type={inputTypes.MONITOR_EPISODES_SELECT}
                      name="monitor"
                      onChange={handleInputChange}
                      {...monitor}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>Collection Strategy</FormLabel>
                    <FormInputHelpText text="Choose 1G1R (single preferred ROM per game), 1G1R Multi-Region, or Completionist (all revisions and variants)" />
                    <FormInput
                      type={inputTypes.SELECT}
                      name="collectionStrategy"
                      values={COLLECTION_STRATEGY_OPTIONS}
                      onChange={handleInputChange}
                      {...collectionStrategy}
                      value={normalizedStrategy}
                    />
                  </FormRow>

                  {normalizedStrategy === 'preservationist' ? null : (
                    <FormRow>
                      <FormLabel>Version / Revision Preference</FormLabel>
                      <FormInputHelpText text="Which ROM revision to monitor in 1G1R mode when multiple revisions exist (Prototypes & Betas are never chosen as initial retail releases)" />
                      <FormInput
                        type={inputTypes.SELECT}
                        name="revisionPreference"
                        values={REVISION_PREFERENCE_OPTIONS}
                        onChange={handleInputChange}
                        {...revisionPreference}
                        value={normalizedRevision}
                      />
                    </FormRow>
                  )}

                  {normalizedStrategy !== 'preservationist' &&
                  normalizedRevision === 'pinned' ? (
                    <FormRow>
                      <FormLabel>Pinned Version Tag</FormLabel>
                      <FormInputHelpText text="Specify a specific revision tag to pin (e.g. Rev A, Rev 1, v1.1)" />
                      <FormInput
                        type={inputTypes.TEXT}
                        name="pinnedVersion"
                        onChange={handleInputChange}
                        {...pinnedVersion}
                      />
                    </FormRow>
                  ) : null}

                  {normalizedStrategy !== 'preservationist' &&
                  hasEncryptedAndDecryptedCollections(series) ? (
                    <FormRow>
                      <FormLabel>ROM Encryption Preference</FormLabel>
                      <FormInputHelpText text="Choose whether to monitor Decrypted or Encrypted ROMs in 1G1R mode (Completionist automatically monitors both)." />
                      <FormInput
                        type={inputTypes.SELECT}
                        name="encryptionPreference"
                        values={ENCRYPTION_PREFERENCE_OPTIONS}
                        onChange={handleInputChange}
                        {...encryptionPreference}
                        value={normalizedEncryption}
                      />
                    </FormRow>
                  ) : null}

                  <FormRow>
                    <FormLabel>Enable Compression</FormLabel>
                    <FormInputHelpText
                      text={`When enabled, Controllarr automatically uses ${autoCompressionLabel}. Leave unchecked to keep uncompressed raw files.`}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="enableCompression"
                      onChange={handleInputChange}
                      {...enableCompression}
                      value={Boolean(enableCompression?.value ?? true)}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>Track Unlicensed</FormLabel>
                    <FormInputHelpText text="Enable monitoring of the Unlicensed region" />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="trackUnlicensed"
                      onChange={handleInputChange}
                      {...trackUnlicensed}
                      value={Boolean(trackUnlicensed?.value)}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>Track ROM Hacks & Translations</FormLabel>
                    <FormInputHelpText text="Enable monitoring of the ROM Hacks & Translations region (hash-gated against HTGDB / SMDB)" />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="trackRomHacks"
                      onChange={handleInputChange}
                      {...trackRomHacks}
                      value={Boolean(trackRomHacks?.value)}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>Track Homebrew & Aftermarket</FormLabel>
                    <FormInputHelpText text="Enable monitoring of the Homebrew & Aftermarket region (hash-gated against HTGDB / SMDB)" />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="trackHomebrew"
                      onChange={handleInputChange}
                      {...trackHomebrew}
                      value={Boolean(trackHomebrew?.value)}
                    />
                  </FormRow>
                </>
              )}

              <FormRow>
                <FormLabel>{translate('Tags')}</FormLabel>

                <FormInput
                  type={inputTypes.TAG}
                  name="tags"
                  onChange={handleInputChange}
                  {...tags}
                />
              </FormRow>
            </Form>
          </div>
        </div>
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <div className={styles.searchOptions}>
          <span className={styles.searchLabel}>{translate('Search')}</span>

          <CheckInput
            containerClassName={styles.searchOption}
            name="searchForMissingEpisodes"
            helpText={translate('Missing')}
            onChange={handleInputChange}
            {...searchForMissingEpisodes}
          />
        </div>

        <div className={styles.actions}>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.PRIMARY}
            isSpinning={isAdding}
            onPress={handleAddSeriesPress}
          >
            {translate('AddSeriesWithTitle', { title })}
          </SpinnerButton>
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

export default AddNewSeriesModalContent;
