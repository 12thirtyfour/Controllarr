import React, { useCallback, useEffect, useMemo, useState } from 'react';
import SeriesMonitorNewItemsOptionsPopoverContent from 'AddSeries/SeriesMonitorNewItemsOptionsPopoverContent';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputButton from 'Components/Form/FormInputButton';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { usePendingChangesStore } from 'Helpers/Hooks/usePendingChangesStore';
import usePrevious from 'Helpers/Hooks/usePrevious';
import {
  icons,
  inputTypes,
  kinds,
  sizes,
  tooltipPositions,
} from 'Helpers/Props';
import MoveSeriesModal from 'Series/MoveSeries/MoveSeriesModal';
import Series from 'Series/Series';
import SeriesMovePending from 'Series/SeriesMovePending';
import { useSaveSeries, useSingleSeries } from 'Series/useSeries';
import { InputChanged } from 'typings/inputs';
import selectSettings from 'Utilities/selectSettings';
import translate from 'Utilities/String/translate';
import RootFolderModal from './RootFolder/RootFolderModal';
import { RootFolderUpdated } from './RootFolder/RootFolderModalContent';
import styles from './EditSeriesModalContent.module.css';

export interface EditSeriesModalContentProps {
  seriesId: number;
  onModalClose: () => void;
  onDeleteSeriesPress: () => void;
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
];

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

  return 'latestRevision';
}

function EditSeriesModalContent({
  seriesId,
  onModalClose,
  onDeleteSeriesPress,
}: EditSeriesModalContentProps) {
  const series = useSingleSeries(seriesId)!;

  const {
    title,
    monitored,
    monitorNewItems,
    seasonFolder,
    qualityProfileId,
    seriesType,
    seasonType,
    language,
    path,
    pendingPath,
    tags,
    rootFolderPath: initialRootFolderPath,
  } = series;

  const { pendingChanges, setPendingChange } = usePendingChangesStore<Series>(
    {}
  );

  const [isRootFolderModalOpen, setIsRootFolderModalOpen] = useState(false);
  const [rootFolderPath, setRootFolderPath] = useState(initialRootFolderPath);
  const isPathChanging = !!(
    pendingChanges.path && path !== pendingChanges.path
  );
  const [isConfirmMoveModalOpen, setIsConfirmMoveModalOpen] = useState(false);

  const { saveSeries, isSaving, saveError } = useSaveSeries(isPathChanging);
  const wasSaving = usePrevious(isSaving);

  const { settings, ...otherSettings } = useMemo(() => {
    return selectSettings(
      {
        monitored,
        monitorNewItems,
        seasonFolder,
        qualityProfileId,
        seriesType,
        seasonType,
        language,
        path,
        tags,
        collectionStrategy: normalizeCollectionStrategy(
          series.collectionStrategy
        ),
        revisionPreference: normalizeRevisionPreference(
          series.revisionPreference
        ),
        pinnedVersion: series.pinnedVersion ?? '',
      },
      {
        ...pendingChanges,
        ...(pendingChanges.collectionStrategy === undefined
          ? {}
          : {
              collectionStrategy: normalizeCollectionStrategy(
                pendingChanges.collectionStrategy
              ) as unknown as number,
            }),
        ...(pendingChanges.revisionPreference === undefined
          ? {}
          : {
              revisionPreference: normalizeRevisionPreference(
                pendingChanges.revisionPreference
              ) as unknown as number,
            }),
      },
      saveError
    );
  }, [
    monitored,
    monitorNewItems,
    seasonFolder,
    qualityProfileId,
    seriesType,
    seasonType,
    language,
    path,
    tags,
    series.collectionStrategy,
    series.revisionPreference,
    series.pinnedVersion,
    pendingChanges,
    saveError,
  ]);

  const currentSeasons = pendingChanges.seasons ?? series.seasons ?? [];
  const isUnlicensedMonitored = Boolean(
    currentSeasons.find((s) => s.seasonNumber === 0)?.monitored
  );
  const isHomebrewMonitored = Boolean(
    currentSeasons.find((s) => s.seasonNumber === 5)?.monitored
  );
  const isRomHacksMonitored = Boolean(
    currentSeasons.find((s) => s.seasonNumber === 6)?.monitored
  );

  const handleRegionMonitorToggle = useCallback(
    (seasonNumber: number, monitoredValue: boolean) => {
      const baseSeasons = pendingChanges.seasons ?? series.seasons ?? [];
      const updated = baseSeasons.map((s) =>
        s.seasonNumber === seasonNumber
          ? { ...s, monitored: monitoredValue }
          : s
      );
      setPendingChange('seasons', updated);
    },
    [pendingChanges.seasons, series.seasons, setPendingChange]
  );

  const handleUnlicensedMonitorChange = useCallback(
    ({ value }: InputChanged) => {
      handleRegionMonitorToggle(0, Boolean(value));
    },
    [handleRegionMonitorToggle]
  );

  const handleRomHacksMonitorChange = useCallback(
    ({ value }: InputChanged) => {
      handleRegionMonitorToggle(6, Boolean(value));
    },
    [handleRegionMonitorToggle]
  );

  const handleHomebrewMonitorChange = useCallback(
    ({ value }: InputChanged) => {
      handleRegionMonitorToggle(5, Boolean(value));
    },
    [handleRegionMonitorToggle]
  );

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      // @ts-expect-error name needs to be keyof Series
      setPendingChange(name, value);
    },
    [setPendingChange]
  );

  const handleRootFolderPress = useCallback(() => {
    setIsRootFolderModalOpen(true);
  }, []);

  const handleRootFolderModalClose = useCallback(() => {
    setIsRootFolderModalOpen(false);
  }, []);

  const handleRootFolderChange = useCallback(
    ({
      path: newPath,
      rootFolderPath: newRootFolderPath,
    }: RootFolderUpdated) => {
      setIsRootFolderModalOpen(false);
      setRootFolderPath(newRootFolderPath);
      handleInputChange({ name: 'path', value: newPath });
    },
    [handleInputChange]
  );

  const handleCancelPress = useCallback(() => {
    setIsConfirmMoveModalOpen(false);
  }, []);

  const handleSavePress = useCallback(() => {
    if (isPathChanging && !isConfirmMoveModalOpen) {
      setIsConfirmMoveModalOpen(true);
    } else {
      setIsConfirmMoveModalOpen(false);

      saveSeries({
        ...series,
        ...pendingChanges,
      });
    }
  }, [
    series,
    isPathChanging,
    isConfirmMoveModalOpen,
    pendingChanges,
    saveSeries,
  ]);

  const handleMoveSeriesPress = useCallback(() => {
    setIsConfirmMoveModalOpen(false);

    saveSeries({
      ...series,
      ...pendingChanges,
    });
  }, [series, pendingChanges, saveSeries]);

  useEffect(() => {
    if (!isSaving && wasSaving && !saveError) {
      onModalClose();
    }
  }, [isSaving, wasSaving, saveError, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('EditSeriesModalHeader', { title })}</ModalHeader>

      <ModalBody>
        <Form {...otherSettings}>
          <FormRow size={sizes.MEDIUM}>
            <FormLabel>Collection Strategy</FormLabel>
            <FormInputHelpText text="Choose 1G1R (single preferred ROM per game), 1G1R Multi-Region, or Completionist (all revisions and variants)" />
            <FormInput
              type={inputTypes.SELECT}
              name="collectionStrategy"
              values={COLLECTION_STRATEGY_OPTIONS}
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              {...(settings as any).collectionStrategy}
              onChange={handleInputChange}
            />
          </FormRow>

          {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
          {String((settings as any).collectionStrategy?.value) !==
            'preservationist' && (
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>1G1R Revision Preference</FormLabel>
              <FormInputHelpText text="Which ROM revision to monitor in 1G1R mode when multiple revisions exist (Prototypes & Betas are never chosen as initial retail releases)" />
              <FormInput
                type={inputTypes.SELECT}
                name="revisionPreference"
                values={REVISION_PREFERENCE_OPTIONS}
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                {...(settings as any).revisionPreference}
                onChange={handleInputChange}
              />
            </FormRow>
          )}

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>Pinned Version / Revision</FormLabel>
            <FormInputHelpText text="Optional revision or version tag to pin (e.g. Rev A, Rev 1, v1.1)" />
            <FormInput
              type={inputTypes.TEXT}
              name="pinnedVersion"
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              {...(settings as any).pinnedVersion}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('Monitored')}</FormLabel>

            <FormInputHelpText text={translate('MonitoredEpisodesHelpText')} />
            <FormInput
              type={inputTypes.CHECK}
              name="monitored"
              {...settings.monitored}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>
              {translate('MonitorNewSeasons')}
              <Popover
                anchor={<Icon className={styles.labelIcon} name={icons.INFO} />}
                title={translate('MonitorNewSeasons')}
                body={<SeriesMonitorNewItemsOptionsPopoverContent />}
                position={tooltipPositions.RIGHT}
              />
            </FormLabel>

            <FormInputHelpText text={translate('MonitorNewSeasonsHelpText')} />
            <FormInput
              type={inputTypes.MONITOR_NEW_ITEMS_SELECT}
              name="monitorNewItems"
              {...settings.monitorNewItems}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>Track Unlicensed</FormLabel>
            <FormInputHelpText text="Enable monitoring of the Unlicensed region" />
            <FormInput
              type={inputTypes.CHECK}
              name="trackUnlicensed"
              value={isUnlicensedMonitored}
              onChange={handleUnlicensedMonitorChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>Track ROM Hacks & Translations</FormLabel>
            <FormInputHelpText text="Enable monitoring of the ROM Hacks & Translations region" />
            <FormInput
              type={inputTypes.CHECK}
              name="trackRomHacks"
              value={isRomHacksMonitored}
              onChange={handleRomHacksMonitorChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>Track Homebrew & Aftermarket</FormLabel>
            <FormInputHelpText text="Enable monitoring of the Homebrew & Aftermarket region" />
            <FormInput
              type={inputTypes.CHECK}
              name="trackHomebrew"
              value={isHomebrewMonitored}
              onChange={handleHomebrewMonitorChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>
              {translate('Path')}

              {pendingPath ? (
                <SeriesMovePending
                  className={styles.labelIcon}
                  pendingPath={pendingPath}
                />
              ) : null}
            </FormLabel>

            <FormInput
              type={inputTypes.PATH}
              name="path"
              {...settings.path}
              buttons={[
                <FormInputButton
                  key="fileBrowser"
                  kind={kinds.DEFAULT}
                  title={translate('RootFolder')}
                  onPress={handleRootFolderPress}
                >
                  <Icon name={icons.ROOT_FOLDER} />
                </FormInputButton>,
              ]}
              includeFiles={false}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('Tags')}</FormLabel>

            <FormInput
              type={inputTypes.TAG}
              name="tags"
              {...settings.tags}
              onChange={handleInputChange}
            />
          </FormRow>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button
          className={styles.deleteButton}
          kind={kinds.DANGER}
          onPress={onDeleteSeriesPress}
        >
          {translate('Delete')}
        </Button>

        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerErrorButton
          error={saveError}
          isSpinning={isSaving}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>

      <RootFolderModal
        isOpen={isRootFolderModalOpen}
        seriesId={seriesId}
        languageId={settings.language.value.id}
        rootFolderPath={rootFolderPath}
        onSavePress={handleRootFolderChange}
        onModalClose={handleRootFolderModalClose}
      />

      <MoveSeriesModal
        originalPath={path}
        destinationPath={pendingChanges.path}
        isOpen={isConfirmMoveModalOpen}
        onModalClose={handleCancelPress}
        onSavePress={handleSavePress}
        onMoveSeriesPress={handleMoveSeriesPress}
      />
    </ModalContent>
  );
}

export default EditSeriesModalContent;
