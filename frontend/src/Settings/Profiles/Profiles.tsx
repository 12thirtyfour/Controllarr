import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useState } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import PathInput from 'Components/Form/PathInput';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import SectionHeading from 'Components/SectionHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import fetchJson from 'Utilities/Fetch/fetchJson';
import getQueryPath from 'Utilities/Fetch/getQueryPath';
import translate from 'Utilities/String/translate';
import DelayProfiles from './Delay/DelayProfiles';
import ReleaseProfiles from './Release/ReleaseProfiles';
import styles from './Profiles.module.css';

interface VirtualLibraryProfile {
  id: number;
  name: string;
  targetPath: string;
  hardwarePreset: string;
  groupMultiDiscFolders: boolean;
  autoSyncOnRefresh: boolean;
  lastSyncedAt?: string;
  linkedConsoleCount: number;
  lastSyncMessage?: string;
}

const PRESET_OPTIONS: SelectInputOption[] = [
  { key: 'mister', value: 'MiSTer FPGA (/games/{Core}/)' },
  {
    key: 'retroarch',
    value: 'RetroArch / Libretro (Canonical System Folders)',
  },
  {
    key: 'analogue_pocket',
    value: 'Analogue Pocket openFPGA (/Assets/{core}/common/)',
  },
  { key: 'onionos', value: 'OnionOS / GarlicOS Handhelds (/Roms/{System}/)' },
  {
    key: 'batocera',
    value: 'Batocera / Knulli / ES-DE (/roms/{system}/)',
  },
  {
    key: 'everdrive',
    value: 'Krikzz EverDrive / Flash Carts & ODEs (/{Console}/)',
  },
];

const PRESET_LAYOUT_EXAMPLES: Record<string, string> = {
  mister:
    '{TargetFolder}/games/NES, {TargetFolder}/games/SNES, {TargetFolder}/games/Genesis, {TargetFolder}/games/PSX',
  retroarch:
    '{TargetFolder}/Nintendo - Nintendo Entertainment System, {TargetFolder}/Sega - Mega Drive - Genesis',
  analogue_pocket:
    '{TargetFolder}/Assets/nes/common, {TargetFolder}/Assets/snes/common, {TargetFolder}/Assets/genesis/common',
  onionos:
    '{TargetFolder}/Roms/FC, {TargetFolder}/Roms/SFC, {TargetFolder}/Roms/MD, {TargetFolder}/Roms/PS',
  batocera:
    '{TargetFolder}/roms/nes, {TargetFolder}/roms/snes, {TargetFolder}/roms/megadrive, {TargetFolder}/roms/psx',
  everdrive:
    '{TargetFolder}/NES, {TargetFolder}/SNES, {TargetFolder}/Genesis, {TargetFolder}/GBA',
};

interface VirtualLibraryActionButtonsProps {
  libraryId: number;
  isSyncing: boolean;
  onSyncNow: (id: number) => void;
  onDelete: (id: number) => void;
}

function VirtualLibraryActionButtons({
  libraryId,
  isSyncing,
  onSyncNow,
  onDelete,
}: VirtualLibraryActionButtonsProps) {
  const handleSyncPress = useCallback(() => {
    onSyncNow(libraryId);
  }, [libraryId, onSyncNow]);

  const handleDeletePress = useCallback(() => {
    onDelete(libraryId);
  }, [libraryId, onDelete]);

  return (
    <div className={styles.libraryActions}>
      <SpinnerButton
        size={sizes.SMALL}
        kind={kinds.PRIMARY}
        isSpinning={isSyncing}
        isDisabled={isSyncing}
        onPress={handleSyncPress}
      >
        Sync Now
      </SpinnerButton>

      <Button
        size={sizes.SMALL}
        kind={kinds.DANGER}
        onPress={handleDeletePress}
      >
        Delete
      </Button>
    </div>
  );
}

function Profiles() {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [targetPath, setTargetPath] = useState('');
  const [hardwarePreset, setHardwarePreset] = useState('mister');
  const [groupMultiDiscFolders, setGroupMultiDiscFolders] = useState(true);
  const [autoSyncOnRefresh, setAutoSyncOnRefresh] = useState(true);
  const [syncingId, setSyncingId] = useState<number | null>(null);

  const { data: virtualLibraries = [] } = useApiQuery<VirtualLibraryProfile[]>({
    path: '/virtuallibrary',
  });

  const { mutate: saveVirtualLibrary, isPending: isSaving } = useApiMutation<
    VirtualLibraryProfile,
    Partial<VirtualLibraryProfile>
  >({
    path: '/virtuallibrary',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        setName('');
        setTargetPath('');
        queryClient.invalidateQueries({ queryKey: ['/virtuallibrary'] });
      },
    },
  });

  const handleNameChange = useCallback(({ value }: InputChanged<string>) => {
    setName(value);
  }, []);

  const handleTargetPathChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTargetPath(value);
    },
    []
  );

  const handlePresetChange = useCallback(({ value }: InputChanged<string>) => {
    setHardwarePreset(value);
  }, []);

  const handleMultiDiscChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setGroupMultiDiscFolders(Boolean(value));
    },
    []
  );

  const handleAutoSyncChange = useCallback(
    ({ value }: InputChanged<boolean>) => {
      setAutoSyncOnRefresh(Boolean(value));
    },
    []
  );

  const handleCreatePress = useCallback(() => {
    if (!name.trim() || !targetPath.trim()) {
      return;
    }

    saveVirtualLibrary({
      name: name.trim(),
      targetPath: targetPath.trim(),
      hardwarePreset,
      groupMultiDiscFolders,
      autoSyncOnRefresh,
    });
  }, [
    name,
    targetPath,
    hardwarePreset,
    groupMultiDiscFolders,
    autoSyncOnRefresh,
    saveVirtualLibrary,
  ]);

  const handleSyncNow = useCallback(
    async (id: number) => {
      setSyncingId(id);

      try {
        await fetchJson({
          path: getQueryPath(`/virtuallibrary/${id}/sync`),
          method: 'POST',
          headers: {
            'X-Api-Key': window.Controllarr.apiKey,
          },
        });
        queryClient.invalidateQueries({ queryKey: ['/virtuallibrary'] });
      } finally {
        setSyncingId(null);
      }
    },
    [queryClient]
  );

  const handleDelete = useCallback(
    async (id: number) => {
      await fetchJson({
        path: getQueryPath(`/virtuallibrary/${id}`),
        method: 'DELETE',
        headers: {
          'X-Api-Key': window.Controllarr.apiKey,
        },
      });
      queryClient.invalidateQueries({ queryKey: ['/virtuallibrary'] });
    },
    [queryClient]
  );

  return (
    <SettingsPage title="Virtual Libraries" showSave={false}>
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title="Virtual Libraries"
          />

          <div className={settingsStyles.pageSection}>
            <SectionHeading
              title="Hardware & Frontend Virtual Libraries (Symlinks)"
              description="Expose your canonical ROM library in multiple hardware-specific folder structures simultaneously (e.g., MiSTer network share + local RetroArch folder) using console directory symlinks without duplicating storage."
            />

            <div className={styles.virtualLibCard}>
              <Form>
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Virtual Library Name</FormLabel>
                  <FormInputHelpText text="A friendly label for this virtual library (e.g. Living Room MiSTer Share, Local RetroArch)" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="name"
                    value={name}
                    placeholder="e.g. Living Room MiSTer Share"
                    onChange={handleNameChange}
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Target Folder Path</FormLabel>
                  <FormInputHelpText text="Browse to the folder where Controllarr should build the target directory structure and symlink your console folders" />
                  <PathInput
                    name="targetPath"
                    value={targetPath}
                    placeholder="/path/to/virtual/library"
                    includeFiles={false}
                    hasFileBrowser={true}
                    onChange={handleTargetPathChange}
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Target Platform / Folder Structure</FormLabel>
                  <FormInputHelpText text="Select the hardware or emulator frontend folder naming convention to mimic" />
                  <SelectInput<string>
                    name="hardwarePreset"
                    value={hardwarePreset}
                    values={PRESET_OPTIONS}
                    onChange={handlePresetChange}
                  />
                  <div className={styles.presetPreview}>
                    Structure Preview:{' '}
                    {PRESET_LAYOUT_EXAMPLES[hardwarePreset] ??
                      PRESET_LAYOUT_EXAMPLES.mister}
                  </div>
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Group Multi-Disc Games in Subfolders</FormLabel>
                  <FormInputHelpText text="Organize multi-disc games (Disc 1, Disc 2) into a subfolder named after the game title" />
                  <CheckInput
                    name="groupMultiDiscFolders"
                    value={groupMultiDiscFolders}
                    onChange={handleMultiDiscChange}
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Auto-Sync on Library Refresh</FormLabel>
                  <FormInputHelpText text="Automatically update console symlinks whenever your library refreshes or imports new platforms" />
                  <CheckInput
                    name="autoSyncOnRefresh"
                    value={autoSyncOnRefresh}
                    onChange={handleAutoSyncChange}
                  />
                </FormRow>

                <div className={styles.formActions}>
                  <SpinnerButton
                    kind={kinds.PRIMARY}
                    isSpinning={isSaving}
                    isDisabled={!name.trim() || !targetPath.trim() || isSaving}
                    onPress={handleCreatePress}
                  >
                    Create &amp; Sync Virtual Library
                  </SpinnerButton>
                </div>
              </Form>
            </div>

            {virtualLibraries.length > 0 ? (
              <div className={styles.libraryGrid}>
                {virtualLibraries.map((lib) => (
                  <div key={lib.id} className={styles.libraryItem}>
                    <div className={styles.libraryInfo}>
                      <div className={styles.libraryTitleRow}>
                        <span className={styles.libraryName}>{lib.name}</span>
                        <Label kind={kinds.PRIMARY}>{lib.hardwarePreset}</Label>
                        <Label kind={kinds.SUCCESS}>
                          {lib.linkedConsoleCount} Linked Console Folders
                        </Label>
                        {lib.autoSyncOnRefresh ? (
                          <Label kind={kinds.INFO}>Auto-Sync</Label>
                        ) : null}
                      </div>

                      <div className={styles.libraryPath}>{lib.targetPath}</div>

                      {lib.lastSyncMessage ? (
                        <div className={styles.libraryMeta}>
                          {lib.lastSyncMessage}
                        </div>
                      ) : null}
                    </div>

                    <VirtualLibraryActionButtons
                      libraryId={lib.id}
                      isSyncing={syncingId === lib.id}
                      onSyncNow={handleSyncNow}
                      onDelete={handleDelete}
                    />
                  </div>
                ))}
              </div>
            ) : null}
          </div>

          <div className={settingsStyles.pageSection}>
            <SectionHeading
              title={translate('DelayProfiles')}
              description={translate('DelayProfilesSectionDescription')}
            />

            <DelayProfiles />
          </div>

          <div className={settingsStyles.pageSection}>
            <SectionHeading
              title={translate('ReleaseProfiles')}
              description={translate('ReleaseProfilesSectionDescription')}
            />

            <ReleaseProfiles />
          </div>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default Profiles;
