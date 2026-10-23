import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import SelectInputStyle from 'Components/Form/SelectInput.module.css';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './SdCardExportModalContent.module.css';

export interface SdCardExportItem {
  sourceFilePath: string;
  platformFolder: string;
  gameTitle: string;
  innerRomExtensionHint?: string;
}

export interface SdCardProfileDescriptor {
  profile: string | number;
  name: string;
  category: string;
  pathTemplate: string;
  usesFat32AlphabeticalBuckets: boolean;
  supportsZipPerRom: boolean;
  supportsChdForOpticalDiscs: boolean;
  supportsRvzForGameCubeWii: boolean;
  optimizationOptionLabel: string;
}

export interface SdCardExportRequestResource {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  profile: any;
  targetMountPath: string;
  overwrite: boolean;
  optimizeFormatsForDevice: boolean;
  items: SdCardExportItem[];
}

export interface SdCardExportResult {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  profile: any;
  targetMountPath: string;
  optimizeFormatsForDevice: boolean;
  exportedCount: number;
  destinationPaths: string[];
  appliedTransformations: number[];
}

export interface SdCardExportModalContentProps {
  items?: SdCardExportItem[];
  onModalClose: () => void;
}

function SdCardExportModalContent({
  items = [],
  onModalClose,
}: SdCardExportModalContentProps) {
  const {
    data: profiles,
    isFetching: isProfilesFetching,
    error: profilesError,
  } = useApiQuery<SdCardProfileDescriptor[]>({
    path: 'sdcardexport/profiles',
  });

  const {
    mutate: exportToSdCard,
    isPending: isExporting,
    error: exportError,
    isSuccess: isExportSuccess,
    data: exportResult,
  } = useApiMutation<SdCardExportResult, SdCardExportRequestResource>({
    path: 'sdcardexport',
    method: 'POST',
  });

  const [selectedProfile, setSelectedProfile] = useState<
    string | number | null
  >(null);
  const [targetMountPath, setTargetMountPath] = useState<string>('');
  const [overwrite, setOverwrite] = useState<boolean>(false);
  const [optimizeFormatsForDevice, setOptimizeFormatsForDevice] =
    useState<boolean>(false);

  useEffect(() => {
    if (profiles && profiles.length > 0 && selectedProfile === null) {
      setSelectedProfile(profiles[0].profile);
    }
  }, [profiles, selectedProfile]);

  const groupedProfiles = useMemo(() => {
    if (!profiles) {
      return [];
    }

    const groups: { category: string; items: SdCardProfileDescriptor[] }[] = [];

    profiles.forEach((profile) => {
      let group = groups.find((g) => g.category === profile.category);

      if (!group) {
        group = { category: profile.category, items: [] };
        groups.push(group);
      }

      group.items.push(profile);
    });

    return groups;
  }, [profiles]);

  const selectedProfileDescriptor = useMemo(() => {
    if (!profiles || selectedProfile === null) {
      return null;
    }

    return (
      profiles.find((p) => String(p.profile) === String(selectedProfile)) ||
      profiles[0]
    );
  }, [profiles, selectedProfile]);

  const handleProfileChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setSelectedProfile(event.target.value);
    },
    []
  );

  const handleTargetPathChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTargetMountPath(value ?? '');
    },
    []
  );

  const handleOptimizeChange = useCallback(({ value }: CheckInputChanged) => {
    setOptimizeFormatsForDevice(Boolean(value));
  }, []);

  const handleOverwriteChange = useCallback(({ value }: CheckInputChanged) => {
    setOverwrite(Boolean(value));
  }, []);

  const handleExport = useCallback(() => {
    if (selectedProfile === null || !targetMountPath.trim()) {
      return;
    }

    exportToSdCard({
      profile: selectedProfile,
      targetMountPath: targetMountPath.trim(),
      overwrite,
      optimizeFormatsForDevice,
      items,
    });
  }, [
    selectedProfile,
    targetMountPath,
    overwrite,
    optimizeFormatsForDevice,
    items,
    exportToSdCard,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('SD Card Multi-Device Exporter')}</ModalHeader>

      <ModalBody>
        {isProfilesFetching ? <LoadingIndicator /> : null}

        {!isProfilesFetching && profilesError ? (
          <Alert kind={kinds.DANGER}>
            {translate('Failed to load SD card device profiles.')}
          </Alert>
        ) : null}

        {!isProfilesFetching && profiles && profiles.length > 0 ? (
          <Form>
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>{translate('Target Device Profile')}</FormLabel>
              <select
                className={SelectInputStyle.select}
                name="profile"
                value={selectedProfile ?? ''}
                onChange={handleProfileChange}
              >
                {groupedProfiles.map((group) => (
                  <optgroup key={group.category} label={group.category}>
                    {group.items.map((p) => (
                      <option key={String(p.profile)} value={String(p.profile)}>
                        {p.name}
                      </option>
                    ))}
                  </optgroup>
                ))}
              </select>

              {selectedProfileDescriptor ? (
                <div className={styles.profileDetails}>
                  <div className={styles.pathTemplateContainer}>
                    <span>{translate('Path Template')}: </span>
                    <code className={styles.pathTemplate}>
                      {selectedProfileDescriptor.pathTemplate}
                    </code>
                  </div>

                  <div className={styles.chipsContainer}>
                    {selectedProfileDescriptor.usesFat32AlphabeticalBuckets ? (
                      <Label kind={kinds.INFO} size={sizes.SMALL}>
                        {translate('FAT32 Buckets')}
                      </Label>
                    ) : null}

                    {selectedProfileDescriptor.supportsZipPerRom ? (
                      <Label kind={kinds.SUCCESS} size={sizes.SMALL}>
                        {translate('Per-ROM ZIP')}
                      </Label>
                    ) : null}

                    {selectedProfileDescriptor.supportsChdForOpticalDiscs ? (
                      <Label kind={kinds.PURPLE} size={sizes.SMALL}>
                        {translate('CHD')}
                      </Label>
                    ) : null}

                    {selectedProfileDescriptor.supportsRvzForGameCubeWii ? (
                      <Label kind={kinds.PRIMARY} size={sizes.SMALL}>
                        {translate('RVZ')}
                      </Label>
                    ) : null}
                  </div>
                </div>
              ) : null}
            </FormRow>

            <FormRow size={sizes.MEDIUM}>
              <FormLabel>{translate('Target Mount Path')}</FormLabel>
              <FormInput
                type={inputTypes.PATH}
                name="targetMountPath"
                value={targetMountPath}
                placeholder={translate('/media/sdcard or E:\\')}
                includeFiles={false}
                onChange={handleTargetPathChange}
              />
            </FormRow>

            <FormRow size={sizes.MEDIUM}>
              <FormLabel>{translate('Format Optimization')}</FormLabel>
              <FormInput
                type={inputTypes.CHECK}
                name="optimizeFormatsForDevice"
                value={optimizeFormatsForDevice}
                helpText={
                  selectedProfileDescriptor?.optimizationOptionLabel ||
                  translate(
                    'To save room, compress or convert files to optimal formats for target device'
                  )
                }
                onChange={handleOptimizeChange}
              />

              <div className={styles.optimizationHelp}>
                <div>{translate('When enabled:')}</div>
                <ul className={styles.optimizationHelpList}>
                  <li>
                    {translate(
                      'Devices supporting compressed formats (e.g., MiSTer, RetroArch, OnionOS, Batocera) will automatically zip cartridge ROMs per-ROM and convert optical discs to .chd/.rvz.'
                    )}
                  </li>
                  <li>
                    {translate(
                      "Flashcarts/ODEs that do not support .chd/.zip (e.g., EverDrive, Analogue Pocket, FXPak Pro, xStation) will automatically extract .zip or convert .chd to .cue/.bin into the target device's folder layout."
                    )}
                  </li>
                </ul>
              </div>
            </FormRow>

            <FormRow size={sizes.MEDIUM}>
              <FormLabel>{translate('Overwrite Existing Files')}</FormLabel>
              <FormInput
                type={inputTypes.CHECK}
                name="overwrite"
                value={overwrite}
                helpText={translate(
                  'Overwrite files if they already exist on target SD card'
                )}
                onChange={handleOverwriteChange}
              />
            </FormRow>
          </Form>
        ) : null}

        {isExportSuccess && exportResult ? (
          <div className={styles.resultContainer}>
            <Alert kind={kinds.SUCCESS}>
              {translate(
                `Successfully exported ${exportResult.exportedCount} items to ${exportResult.targetMountPath}`
              )}
            </Alert>
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>
          {isExportSuccess ? translate('Close') : translate('Cancel')}
        </Button>

        {isExportSuccess ? null : (
          <SpinnerErrorButton
            isSpinning={isExporting}
            error={exportError}
            disabled={!targetMountPath.trim() || selectedProfile === null}
            onPress={handleExport}
          >
            {translate('Export to SD Card')}
          </SpinnerErrorButton>
        )}
      </ModalFooter>
    </ModalContent>
  );
}

export default SdCardExportModalContent;
