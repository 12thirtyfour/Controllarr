import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import SelectInputStyle from 'Components/Form/SelectInput.module.css';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import settingsStyles from 'Settings/Settings.module.css';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

interface LibraryPlatformItem {
  id: number;
  title: string;
  titleSlug: string;
  fileCount: number;
}

interface LibraryProfileItem {
  profile: string | number;
  name: string;
  category: string;
  supportsZipPerRom: boolean;
  supportsChdForOpticalDiscs: boolean;
  supportsRvzForGameCubeWii: boolean;
  platforms: LibraryPlatformItem[];
}

interface LibraryExportRequest {
  profile: string | number;
  platformIds: number[];
  targetMountPath: string;
  overwrite: boolean;
  useCartridgeCompression: boolean;
  useDiscCompression: boolean;
}

interface LibraryExportResult {
  exportedCount: number;
  destinationPaths: string[];
  targetMountPath: string;
}

function ExportToSdPage() {
  const {
    data: libraryProfiles,
    isFetching: isProfilesFetching,
    error: profilesError,
  } = useApiQuery<LibraryProfileItem[]>({
    path: '/sdcardexport/library-profiles',
  });

  const {
    mutate: runExport,
    isPending: isExporting,
    error: exportError,
    isSuccess: isExportSuccess,
    data: exportResult,
  } = useApiMutation<LibraryExportResult, LibraryExportRequest>({
    path: '/sdcardexport/library',
    method: 'POST',
  });

  const [selectedProfileKey, setSelectedProfileKey] = useState<string>('');
  const [selectedPlatformIds, setSelectedPlatformIds] = useState<number[]>([]);
  const [useCartridgeCompression, setUseCartridgeCompression] = useState(false);
  const [useDiscCompression, setUseDiscCompression] = useState(false);
  const [targetMountPath, setTargetMountPath] = useState('');
  const [overwrite, setOverwrite] = useState(false);

  useEffect(() => {
    if (libraryProfiles && libraryProfiles.length > 0 && !selectedProfileKey) {
      const first = libraryProfiles[0];
      setSelectedProfileKey(String(first.profile));
      setSelectedPlatformIds(first.platforms.map((p) => p.id));
    }
  }, [libraryProfiles, selectedProfileKey]);

  const activeProfile = useMemo(() => {
    if (!libraryProfiles || libraryProfiles.length === 0) {
      return null;
    }

    return (
      libraryProfiles.find(
        (p) => String(p.profile) === selectedProfileKey
      ) ?? libraryProfiles[0]
    );
  }, [libraryProfiles, selectedProfileKey]);

  const groupedProfiles = useMemo(() => {
    if (!libraryProfiles) {
      return [];
    }

    const groups: { category: string; items: LibraryProfileItem[] }[] = [];
    libraryProfiles.forEach((p) => {
      let group = groups.find((g) => g.category === p.category);
      if (!group) {
        group = { category: p.category, items: [] };
        groups.push(group);
      }
      group.items.push(p);
    });
    return groups;
  }, [libraryProfiles]);

  const handleProfileChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      const nextKey = event.target.value;
      setSelectedProfileKey(nextKey);
      const nextProfile = libraryProfiles?.find(
        (p) => String(p.profile) === nextKey
      );
      setSelectedPlatformIds(
        nextProfile ? nextProfile.platforms.map((p) => p.id) : []
      );
    },
    [libraryProfiles]
  );

  const handleTogglePlatform = useCallback(
    (platformId: number, checked: boolean) => {
      setSelectedPlatformIds((prev) => {
        if (checked) {
          return prev.includes(platformId) ? prev : [...prev, platformId];
        }
        return prev.filter((id) => id !== platformId);
      });
    },
    []
  );

  const handleSelectAllPlatforms = useCallback(() => {
    if (!activeProfile) {
      return;
    }
    setSelectedPlatformIds(activeProfile.platforms.map((p) => p.id));
  }, [activeProfile]);

  const handleClearAllPlatforms = useCallback(() => {
    setSelectedPlatformIds([]);
  }, []);

  const handleCartridgeCompressionChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setUseCartridgeCompression(value);
    },
    []
  );

  const handleDiscCompressionChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setUseDiscCompression(value);
    },
    []
  );

  const handleTargetMountPathChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTargetMountPath(value);
    },
    []
  );

  const handleOverwriteChange = useCallback(({ value }: CheckInputChanged) => {
    setOverwrite(value);
  }, []);

  const handleExportPress = useCallback(() => {
    if (!activeProfile || !targetMountPath.trim()) {
      return;
    }

    runExport({
      profile: activeProfile.profile,
      platformIds: selectedPlatformIds,
      targetMountPath: targetMountPath.trim(),
      overwrite,
      useCartridgeCompression,
      useDiscCompression,
    });
  }, [
    activeProfile,
    targetMountPath,
    selectedPlatformIds,
    overwrite,
    useCartridgeCompression,
    useDiscCompression,
    runExport,
  ]);

  const canExport =
    Boolean(activeProfile) &&
    selectedPlatformIds.length > 0 &&
    Boolean(targetMountPath.trim()) &&
    !isExporting;

  return (
    <PageContent title="Export to SD Card">
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading scope={translate('Utilities')} title="Export to SD Card" />

          {isProfilesFetching ? <LoadingIndicator /> : null}

          {profilesError ? (
            <Alert kind={kinds.DANGER}>
              Unable to load library hardware export profiles.
            </Alert>
          ) : null}

          {!isProfilesFetching &&
          !profilesError &&
          (!libraryProfiles || libraryProfiles.length === 0) ? (
            <Alert kind={kinds.INFO}>
              No populated platforms were found in your active library. Import or
              download ROMs for at least one platform before exporting to an SD
              card.
            </Alert>
          ) : null}

          {activeProfile ? (
            <Form>
              <FieldSet
                legend="1. Target Hardware Profile"
                caption="Filtered to hardware profiles compatible with platforms currently populated in your library."
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Hardware Profile</FormLabel>
                  <FormInputHelpText text="Select the flash-cart, ODE, handheld OS, or emulation frontend for your SD card" />
                  <select
                    className={SelectInputStyle.select}
                    value={String(activeProfile.profile)}
                    onChange={handleProfileChange}
                  >
                    {groupedProfiles.map((group) => (
                      <optgroup key={group.category} label={group.category}>
                        {group.items.map((item) => (
                          <option
                            key={String(item.profile)}
                            value={String(item.profile)}
                          >
                            {item.name} ({item.platforms.length} populated{' '}
                            {item.platforms.length === 1
                              ? 'platform'
                              : 'platforms'}
                            )
                          </option>
                        ))}
                      </optgroup>
                    ))}
                  </select>
                </FormRow>
              </FieldSet>

              <FieldSet
                legend="2. Select Platforms to Export"
                caption="Choose which populated platforms from your library should be copied to the target SD card."
              >
                <div style={{ marginBottom: '12px', display: 'flex', gap: '8px' }}>
                  <Button size={sizes.SMALL} onPress={handleSelectAllPlatforms}>
                    Select All
                  </Button>
                  <Button size={sizes.SMALL} onPress={handleClearAllPlatforms}>
                    Clear All
                  </Button>
                </div>

                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
                    gap: '10px',
                  }}
                >
                  {activeProfile.platforms.map((platform) => {
                    const checked = selectedPlatformIds.includes(platform.id);
                    return (
                      <label
                        key={platform.id}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '8px',
                          padding: '8px 12px',
                          border: '1px solid var(--borderColor)',
                          borderRadius: '6px',
                          cursor: 'pointer',
                        }}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={(e) =>
                            handleTogglePlatform(platform.id, e.target.checked)
                          }
                        />
                        <span>
                          <strong>{platform.title}</strong> ({platform.fileCount}{' '}
                          {platform.fileCount === 1 ? 'file' : 'files'})
                        </span>
                      </label>
                    );
                  })}
                </div>
              </FieldSet>

              <FieldSet
                legend="3. Compression & Format Compatibility"
                caption="Configure cartridge and optical disc compression for the selected target hardware."
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Use Cartridge Compression</FormLabel>
                  <FormInputHelpText
                    text={
                      activeProfile.supportsZipPerRom
                        ? `${activeProfile.name} supports per-ROM .zip archives for cartridge ROMs.`
                        : `Warning: ${activeProfile.name} requires uncompressed raw cartridge ROMs — zipped ROMs will be extracted automatically.`
                    }
                  />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="useCartridgeCompression"
                    value={useCartridgeCompression}
                    onChange={handleCartridgeCompressionChange}
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Use Disc Compression</FormLabel>
                  <FormInputHelpText
                    text={
                      activeProfile.supportsChdForOpticalDiscs ||
                      activeProfile.supportsRvzForGameCubeWii
                        ? `${activeProfile.name} supports compressed disc formats (${[
                            activeProfile.supportsChdForOpticalDiscs ? 'CHD' : '',
                            activeProfile.supportsRvzForGameCubeWii ? 'RVZ' : '',
                          ]
                            .filter(Boolean)
                            .join(' / ')}).`
                        : `Warning: ${activeProfile.name} requires uncompressed optical disc images (.cue/.bin or .iso) — CHD/RVZ files will be converted automatically.`
                    }
                  />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="useDiscCompression"
                    value={useDiscCompression}
                    onChange={handleDiscCompressionChange}
                  />
                </FormRow>
              </FieldSet>

              <FieldSet
                legend="4. Destination SD Card / Export Path"
                caption="Specify the mounted SD card root path or local export folder."
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Destination Path</FormLabel>
                  <FormInputHelpText text="Mount point or directory where the hardware folder layout will be written (e.g. E:\\ or /mnt/sdcard)" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="targetMountPath"
                    value={targetMountPath}
                    placeholder="E:\\ or /media/sdcard"
                    onChange={handleTargetMountPathChange}
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Overwrite Existing Files</FormLabel>
                  <FormInputHelpText text="Replace existing files on the destination SD card if they already exist" />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="overwrite"
                    value={overwrite}
                    onChange={handleOverwriteChange}
                  />
                </FormRow>

                <div style={{ marginTop: '16px' }}>
                  <SpinnerButton
                    kind={kinds.PRIMARY}
                    size={sizes.MEDIUM}
                    isSpinning={isExporting}
                    isDisabled={!canExport}
                    onPress={handleExportPress}
                  >
                    Export to SD Card
                  </SpinnerButton>
                </div>
              </FieldSet>
            </Form>
          ) : null}

          {exportError ? (
            <div style={{ marginTop: '16px' }}>
              <Alert kind={kinds.DANGER}>
                Export failed. Verify the destination path is writable and try
                again.
              </Alert>
            </div>
          ) : null}

          {isExportSuccess && exportResult ? (
            <div style={{ marginTop: '16px' }}>
              <Alert kind={kinds.SUCCESS}>
                Exported {exportResult.exportedCount}{' '}
                {exportResult.exportedCount === 1 ? 'file' : 'files'} to{' '}
                <code>{exportResult.targetMountPath}</code>.
              </Alert>
            </div>
          ) : null}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default ExportToSdPage;
