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
  { key: 0, value: '1G1R (Single Preferred Region)' },
  { key: 2, value: '1G1R Multi-Region (1 Per Monitored Region)' },
  { key: 1, value: 'Completionist (All Revisions, Regions & Variants)' },
];

const REVISION_PREFERENCE_OPTIONS = [
  { key: 0, value: 'Newest / Latest Revision (Rev A, v1.1, v1.2)' },
  { key: 1, value: 'Original Retail Release (Rev 0 / v1.0)' },
];

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
        collectionStrategy: series.collectionStrategy ?? 0,
        revisionPreference: series.revisionPreference ?? 0,
        pinnedVersion: series.pinnedVersion ?? '',
      },
      pendingChanges,
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
          {Number((settings as any).collectionStrategy?.value) !== 1 && (
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>1G1R Revision Preference</FormLabel>
              <FormInputHelpText text="Which ROM revision to monitor in 1G1R mode when multiple revisions exist" />
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
            <FormLabel>{translate('UseSeasonFolder')}</FormLabel>

            <FormInputHelpText text="Organize ROMs into regional subfolders (USA, Europe, Japan, Homebrew, ROM Hacks)" />
            <FormInput
              type={inputTypes.CHECK}
              name="seasonFolder"
              {...settings.seasonFolder}
              onChange={handleInputChange}
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
