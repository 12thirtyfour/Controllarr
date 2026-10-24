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
  { key: 0, value: '1G1R (Single Preferred Region)' },
  { key: 2, value: '1G1R Multi-Region (1 Per Monitored Region)' },
  { key: 1, value: 'Completionist (All Revisions, Regions & Variants)' },
];

const REVISION_PREFERENCE_OPTIONS = [
  { key: 0, value: 'Newest / Latest Revision (Rev A, v1.1, v1.2)' },
  { key: 1, value: 'Original Retail Release (Rev 0 / v1.0)' },
  { key: 2, value: 'Pinned Version / Tag' },
];

const COMPRESSION_FORMAT_OPTIONS = [
  { key: 'zip', value: 'Compressed Archive (.zip / .7z)' },
  { key: 'chd', value: 'Compressed Disc / CHD (.chd / .rvz / .cso)' },
  { key: 'raw', value: 'Uncompressed / Raw ROM (.nes, .sfc, .gba, .iso, .bin/.cue)' },
];

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
  const [seasonType] = useState(
    () =>
      seasonTypes.find((s) => s.type === 'official')?.type ??
      seasonTypes[0]?.type ??
      'official'
  );

  const {
    monitor,
    qualityProfileId,
    rootFolderPath,
    searchForCutoffUnmetEpisodes,
    searchForMissingEpisodes,
    seasonFolder,
    collectionStrategy,
    revisionPreference,
    pinnedVersion,
    compressionFormat,
    trackHomebrew,
    trackRomHacks,
    trackPrototypes,
    seriesType: seriesTypeSetting,
    tags,
  } = settings;

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
    const updatedSeasons = (series.seasons ?? []).map((s) => {
      if (s.seasonNumber === 5) {
        return { ...s, monitored: Boolean(trackHomebrew?.value) };
      }
      if (s.seasonNumber === 6) {
        return { ...s, monitored: Boolean(trackRomHacks?.value) };
      }
      if (s.seasonNumber === 0) {
        return { ...s, monitored: Boolean(trackPrototypes?.value) };
      }
      return s;
    });

    addSeries({
      ...series,
      seasons: updatedSeasons,
      rootFolderPath: rootFolderPath.value,
      addOptions: {
        monitor: monitor.value,
        searchForMissingEpisodes: searchForMissingEpisodes.value,
        searchForCutoffUnmetEpisodes: searchForCutoffUnmetEpisodes.value,
      },
      qualityProfileId: qualityProfileId.value || 1,
      collectionStrategy: Number(collectionStrategy?.value ?? 0),
      revisionPreference:
        Number(revisionPreference?.value ?? 0) === 2
          ? 0
          : Number(revisionPreference?.value ?? 0),
      pinnedVersion:
        Number(revisionPreference?.value ?? 0) === 2
          ? String(pinnedVersion?.value ?? '')
          : '',
      seriesType,
      seasonType,
      language,
      seasonFolder: seasonFolder.value,
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
    collectionStrategy,
    revisionPreference,
    pinnedVersion,
    trackHomebrew,
    trackRomHacks,
    trackPrototypes,
    seasonFolder,
    searchForMissingEpisodes,
    searchForCutoffUnmetEpisodes,
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
                      <Icon className={styles.labelIcon} name={icons.INFO} />
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
                />
              </FormRow>

              {Number(collectionStrategy?.value ?? 0) !== 1 ? (
                <FormRow>
                  <FormLabel>Version / Revision Preference</FormLabel>
                  <FormInputHelpText text="Which ROM revision to monitor in 1G1R mode when multiple revisions exist" />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="revisionPreference"
                    values={REVISION_PREFERENCE_OPTIONS}
                    onChange={handleInputChange}
                    {...revisionPreference}
                  />
                </FormRow>
              ) : null}

              {Number(revisionPreference?.value ?? 0) === 2 ? (
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

              <FormRow>
                <FormLabel>Compression / Container Format</FormLabel>
                <FormInputHelpText text="Preferred storage container for ROMs and disc images on this platform" />
                <FormInput
                  type={inputTypes.SELECT}
                  name="compressionFormat"
                  values={COMPRESSION_FORMAT_OPTIONS}
                  onChange={handleInputChange}
                  {...compressionFormat}
                />
              </FormRow>

              <FormRow>
                <FormLabel>{translate('SeasonFolder')}</FormLabel>
                <FormInputHelpText text="Organize ROMs into regional subfolders (USA, Europe, Japan, Homebrew, ROM Hacks)" />
                <FormInput
                  type={inputTypes.CHECK}
                  name="seasonFolder"
                  onChange={handleInputChange}
                  {...seasonFolder}
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
                />
              </FormRow>

              <FormRow>
                <FormLabel>Track Prototypes, Betas & Unlicensed</FormLabel>
                <FormInputHelpText text="Enable monitoring of the Prototypes, Betas & Unlicensed region" />
                <FormInput
                  type={inputTypes.CHECK}
                  name="trackPrototypes"
                  onChange={handleInputChange}
                  {...trackPrototypes}
                />
              </FormRow>

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

          <CheckInput
            containerClassName={styles.searchOption}
            name="searchForCutoffUnmetEpisodes"
            helpText={translate('CutoffUnmet')}
            onChange={handleInputChange}
            {...searchForCutoffUnmetEpisodes}
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
