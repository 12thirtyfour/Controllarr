import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AddPlatformOptions,
  setAddPlatformOption,
  useAddPlatformOptions,
} from 'AddPlatform/addPlatformOptionsStore';
import PlatformMonitoringOptionsPopoverContent from 'AddPlatform/PlatformMonitoringOptionsPopoverContent';
import FieldSet from 'Components/FieldSet';
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
import { useMetadataSourceSettingsValues } from 'Settings/MetadataSource/useMetadataSourceSettings';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import selectSettings from 'Utilities/selectSettings';
import translate from 'Utilities/String/translate';
import { useAddAllPlatforms } from './useAddPlatform';
import styles from './AddNewPlatformModalContent.module.css';

export interface AddAllPlatformsModalContentProps {
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

function AddAllPlatformsModalContent({
  onModalClose,
}: AddAllPlatformsModalContentProps) {
  const options = useAddPlatformOptions();
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

  const isWindows = useIsWindows();
  const { isAddingAll, addAllError, addAllPlatforms } =
    useAddAllPlatforms(onModalClose);

  const { settings, validationErrors, validationWarnings } = useMemo(() => {
    return {
      ...selectSettings(options, {}),
      ...getValidationFailures(addAllError),
    };
  }, [options, addAllError]);

  const {
    monitor,
    qualityProfileId,
    rootFolderPath,
    searchForMissingGames,
    collectionStrategy,
    revisionPreference,
    pinnedVersion,
    enableCompression,
    trackUnlicensed,
    trackHomebrew,
    trackRomHacks,
    seriesType,
    tags,
  } = settings;

  const [discCompression, setDiscCompression] = useState<boolean>(
    Boolean(enableCompression?.value ?? true)
  );
  const [cartridgeCompression, setCartridgeCompression] = useState<boolean>(
    Boolean(enableCompression?.value ?? true)
  );
  const [ndsEncryptionPreference, setNdsEncryptionPreference] =
    useState<string>('notEncrypted');
  const [threeDsEncryptionPreference, setThreeDsEncryptionPreference] =
    useState<string>('notEncrypted');

  const normalizedStrategy = normalizeCollectionStrategy(
    collectionStrategy?.value
  );
  const normalizedRevision = normalizeRevisionPreference(
    revisionPreference?.value
  );

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged<string | number | boolean | number[]>) => {
      setAddPlatformOption(name as keyof AddPlatformOptions, value);
    },
    []
  );

  const handleDiscCompressionChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setDiscCompression(value);
    },
    []
  );

  const handleCartridgeCompressionChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setCartridgeCompression(value);
    },
    []
  );

  const handleNdsEncryptionChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setNdsEncryptionPreference(value);
    },
    []
  );

  const handleThreeDsEncryptionChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setThreeDsEncryptionPreference(value);
    },
    []
  );

  const handleAddAllPlatformsPress = useCallback(() => {
    addAllPlatforms({
      rootFolderPath: String(rootFolderPath.value ?? ''),
      qualityProfileId: Number(qualityProfileId.value) || 1,
      language,
      monitor: String(monitor.value ?? 'all'),
      collectionStrategy: normalizedStrategy,
      revisionPreference:
        normalizedRevision === 'pinned' ? 'latestRevision' : normalizedRevision,
      pinnedVersion:
        normalizedRevision === 'pinned'
          ? String(pinnedVersion?.value ?? '')
          : '',
      discCompression,
      cartridgeCompression,
      ndsEncryptionPreference,
      threeDsEncryptionPreference,
      trackUnlicensed: Boolean(trackUnlicensed?.value),
      trackRomHacks: Boolean(trackRomHacks?.value),
      trackHomebrew: Boolean(trackHomebrew?.value),
      searchForMissingGames: Boolean(searchForMissingGames?.value),
      searchForCutoffUnmetGames: false,
      seriesType: String(seriesType?.value ?? 'standard'),
      tags: (tags?.value as number[]) ?? [],
    });
  }, [
    addAllPlatforms,
    rootFolderPath,
    qualityProfileId,
    language,
    monitor,
    normalizedStrategy,
    normalizedRevision,
    pinnedVersion,
    discCompression,
    cartridgeCompression,
    ndsEncryptionPreference,
    threeDsEncryptionPreference,
    trackUnlicensed,
    trackRomHacks,
    trackHomebrew,
    searchForMissingGames,
    seriesType,
    tags,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>Add All Supported Platforms</ModalHeader>

      <ModalBody>
        <p className={styles.intro}>
          Add every supported platform in Controllarr&apos;s catalog to your
          library. Platforms already present in your library will be skipped
          automatically.
        </p>

        <Form
          validationErrors={validationErrors}
          validationWarnings={validationWarnings}
        >
          <FieldSet legend="General Library Options">
            <FormRow>
              <FormLabel>{translate('RootFolder')}</FormLabel>
              <FormInputHelpText text="Each platform will be created in its own subfolder inside this root directory." />
              <FormInput
                type={inputTypes.ROOT_FOLDER_SELECT}
                name="rootFolderPath"
                valueOptions={{
                  isWindows,
                }}
                selectedValueOptions={{
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
                  body={<PlatformMonitoringOptionsPopoverContent />}
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

            <FormRow>
              <FormLabel>{translate('Tags')}</FormLabel>

              <FormInput
                type={inputTypes.TAG}
                name="tags"
                onChange={handleInputChange}
                {...tags}
              />
            </FormRow>
          </FieldSet>

          <FieldSet legend="Platform-Specific Options: Compression">
            <FormRow>
              <FormLabel>Disc-Based Platforms Compression</FormLabel>
              <FormInputHelpText text="When enabled, disc platforms (PS1, PS2, PS3, PSP, GameCube, Wii, Wii U, Saturn, Dreamcast, Sega CD, TurboGrafx-CD, Xbox, Xbox 360, Jaguar CD, 3DO, CD-i, PC-FX, Neo Geo CD, Amiga CD32) automatically use lossless CHD / RVZ compression." />
              <FormInput
                type={inputTypes.CHECK}
                name="discCompression"
                value={discCompression}
                onChange={handleDiscCompressionChange}
              />
            </FormRow>

            <FormRow>
              <FormLabel>Cartridge & Handheld Compression</FormLabel>
              <FormInputHelpText text="When enabled, cartridge and handheld platforms automatically use ZIP / 7z archive compression." />
              <FormInput
                type={inputTypes.CHECK}
                name="cartridgeCompression"
                value={cartridgeCompression}
                onChange={handleCartridgeCompressionChange}
              />
            </FormRow>
          </FieldSet>

          <FieldSet legend="Platform-Specific Options: Nintendo DS & 3DS Encryption">
            {normalizedStrategy === 'preservationist' ? (
              <FormRow>
                <FormLabel>ROM Encryption Preference</FormLabel>
                <FormInputHelpText text="Completionist mode automatically monitors both Decrypted and Encrypted collections for Nintendo DS and Nintendo 3DS." />
              </FormRow>
            ) : (
              <>
                <FormRow>
                  <FormLabel>Nintendo DS (NDS) Encryption</FormLabel>
                  <FormInputHelpText text="Choose whether to monitor Decrypted or Encrypted Nintendo DS ROMs in 1G1R mode." />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="ndsEncryptionPreference"
                    values={ENCRYPTION_PREFERENCE_OPTIONS}
                    value={ndsEncryptionPreference}
                    onChange={handleNdsEncryptionChange}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>Nintendo 3DS (3DS) Encryption</FormLabel>
                  <FormInputHelpText text="Choose whether to monitor Decrypted or Encrypted Nintendo 3DS ROMs in 1G1R mode." />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="threeDsEncryptionPreference"
                    values={ENCRYPTION_PREFERENCE_OPTIONS}
                    value={threeDsEncryptionPreference}
                    onChange={handleThreeDsEncryptionChange}
                  />
                </FormRow>
              </>
            )}
          </FieldSet>
        </Form>
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <div className={styles.searchOptions}>
          <span className={styles.searchLabel}>{translate('Search')}</span>

          <CheckInput
            containerClassName={styles.searchOption}
            name="searchForMissingGames"
            helpText={translate('Missing')}
            onChange={handleInputChange}
            {...searchForMissingGames}
          />
        </div>

        <div className={styles.actions}>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.PRIMARY}
            isSpinning={isAddingAll}
            onPress={handleAddAllPlatformsPress}
          >
            Add All Supported Platforms
          </SpinnerButton>
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

export default AddAllPlatformsModalContent;
