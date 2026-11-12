import React, { useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputButton from 'Components/Form/FormInputButton';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import NamingModal from './NamingModal';
import {
  NamingSettingsModel,
  useManageNamingSettings,
} from './useNamingSettings';
import styles from './Naming.module.css';

interface NamingProps {
  setChildSave: (saveCallback: () => void) => void;
  onChildStateChange: (state: {
    isSaving: boolean;
    hasPendingChanges: boolean;
  }) => void;
}

const DEFAULT_UNIFIED_PATH_FORMAT =
  '{Manufacturer}/{Platform}/{Region}/{Game Title}';
const DEFAULT_DISC_PATH_FORMAT =
  '{Manufacturer}/{Platform}/{Region}/{Game Title}/{Game Title}';

interface DatabaseNamingTemplate {
  key: string;
  label: string;
  format: string;
  hint: string;
}

const DATABASE_NAMING_TEMPLATES: DatabaseNamingTemplate[] = [
  {
    key: 'controllarr-default',
    label: 'Controllarr Default',
    format: '{Manufacturer}/{Platform}/{Region}/{Game Title}',
    hint: 'Nintendo/Nintendo Entertainment System/USA/Super Mario Bros. (USA) (Rev 1).nes',
  },
  {
    key: 'no-intro',
    label: 'No-Intro',
    format: '{Platform}/{Region}/{Game Title}',
    hint: 'Nintendo Entertainment System/USA/Super Mario Bros. (USA) (Rev 1).nes',
  },
  {
    key: 'smokemonster',
    label: 'SmokeMonster / EverDrive (SMDB)',
    format: '{Platform}/{Region}/{Game FirstLetter}/{Game Title}',
    hint: 'Nintendo Entertainment System/USA/S/Super Mario Bros. (USA) (Rev 1).nes',
  },
  {
    key: 'tosec',
    label: 'TOSEC',
    format:
      '{Platform}/{Region}/{Game CleanTitle} ({Release Year})({Publisher})({Region Code})({Language}){Dump Tags}',
    hint: 'Nintendo Entertainment System/USA/Super Mario Bros (1985)(Nintendo)(US)(en)[!].nes',
  },
  {
    key: 'redump',
    label: 'Redump',
    format: '{Platform}/{Region}/{Game Title}/{Game Title}',
    hint: 'PlayStation/USA/Final Fantasy VII (USA) (Disc 1)/Final Fantasy VII (USA) (Disc 1).chd',
  },
  {
    key: 'goodtools',
    label: 'GoodTools',
    format:
      '{Platform}/{Region}/{Game CleanTitle} ({Region Code}) {Dump Tags}',
    hint: 'Nintendo Entertainment System/USA/Super Mario Bros (US) [!].nes',
  },
  {
    key: 'htgdb',
    label: 'HTGDB / Flash-Cart Compact',
    format: '{Platform Short}/{Region}/{Game Title}',
    hint: 'NES/USA/Super Mario Bros. (USA) (Rev 1).nes',
  },
];

function normalizeSegmentToConsoleVariables(
  segment: string,
  fallback: string
): string {
  const trimmed = (segment ?? '').trim();

  if (!trimmed) {
    return fallback;
  }

  if (
    trimmed === '{Series Title}' ||
    trimmed === '{Series CleanTitle}' ||
    trimmed === '{Series.Title}'
  ) {
    return '{Manufacturer}/{Platform}';
  }

  if (
    trimmed === 'Season {season}' ||
    trimmed === 'Season {season:00}' ||
    trimmed === '{Season Title}' ||
    trimmed === '{Season CleanTitle}'
  ) {
    return '{Region}';
  }

  if (
    trimmed.includes('S{season') ||
    trimmed === '{Game Title}' ||
    trimmed === '{Game CleanTitle}'
  ) {
    return '{Game Title}';
  }

  return trimmed
    .replace(/\{Series Title\}/gi, '{Platform}')
    .replace(/\{Series CleanTitle\}/gi, '{Platform}')
    .replace(/\{Series Year\}/gi, '{Platform Year}')
    .replace(/\{Season Title\}/gi, '{Region}')
    .replace(/\{Season CleanTitle\}/gi, '{Region}')
    .replace(/\{Game Title\}/gi, '{Game Title}')
    .replace(/\{Game CleanTitle\}/gi, '{Game CleanTitle}');
}

function splitUnifiedPathIntoBackendFields(rawPath: string): {
  platformFolderFormat: string;
  regionFolderFormat: string;
  standardGameFormat: string;
} {
  const segments = rawPath
    .split('/')
    .map((s) => s.trim())
    .filter(Boolean);

  if (segments.length === 0) {
    return {
      platformFolderFormat: '{Manufacturer}/{Platform}',
      regionFolderFormat: '{Region}',
      standardGameFormat: '{Game Title}',
    };
  }

  if (segments.length === 1) {
    return {
      platformFolderFormat: '{Manufacturer}/{Platform}',
      regionFolderFormat: '{Region}',
      standardGameFormat: segments[0],
    };
  }

  if (segments.length === 2) {
    return {
      platformFolderFormat: segments[0],
      regionFolderFormat: '{Region}',
      standardGameFormat: segments[1],
    };
  }

  const regionIdx = segments.findIndex(
    (s, idx) =>
      idx >= 1 &&
      idx < segments.length - 1 &&
      /\{Region/i.test(s)
  );

  if (regionIdx >= 1) {
    return {
      platformFolderFormat: segments.slice(0, regionIdx).join('/'),
      regionFolderFormat: segments[regionIdx],
      standardGameFormat: segments.slice(regionIdx + 1).join('/'),
    };
  }

  return {
    platformFolderFormat: segments[0],
    regionFolderFormat: segments[1],
    standardGameFormat: segments.slice(2).join('/'),
  };
}

function Naming({ setChildSave, onChildStateChange }: NamingProps) {
  const {
    settings,
    updateSetting,
    isFetching,
    error,
    hasSettings,
    hasPendingChanges,
    isSaving,
    saveSettings,
  } = useManageNamingSettings();

  const [isNamingModalOpen, setNamingModalOpen, setNamingModalClosed] =
    useModalOpenState(false);
  const [activeModalTarget, setActiveModalTarget] = useState<
    'unifiedPathFormat' | 'discPathFormat'
  >('unifiedPathFormat');

  const [unifiedPathFormat, setUnifiedPathFormat] = useState<string>(
    DEFAULT_UNIFIED_PATH_FORMAT
  );
  const [discPathFormat, setDiscPathFormat] = useState<string>(
    DEFAULT_DISC_PATH_FORMAT
  );
  const [initializedFromSettings, setInitializedFromSettings] =
    useState<boolean>(false);

  useEffect(() => {
    if (!hasSettings || initializedFromSettings) {
      return;
    }

    const platformValue = settings.platformFolderFormat?.value ?? '';
    const regionValue = settings.regionFolderFormat?.value ?? '';
    const gameValue = settings.standardGameFormat?.value ?? '';
    const rawDisc = settings.discGameFormat?.value ?? '';

    const normalizedPlatform = normalizeSegmentToConsoleVariables(
      platformValue,
      '{Manufacturer}/{Platform}'
    );
    const normalizedRegion = normalizeSegmentToConsoleVariables(
      regionValue,
      '{Region}'
    );
    const normalizedGame = normalizeSegmentToConsoleVariables(
      gameValue,
      '{Game Title}'
    );
    const normalizedDiscGame =
      !rawDisc ||
      rawDisc === '{Game Title}' ||
      rawDisc.includes('Air-Date') ||
      rawDisc.includes('air-date')
        ? '{Game Title}/{Game Title}'
        : normalizeSegmentToConsoleVariables(
            rawDisc,
            '{Game Title}/{Game Title}'
          );

    if (
      settings.platformFolderFormat &&
      settings.platformFolderFormat.value !== normalizedPlatform
    ) {
      updateSetting('platformFolderFormat', normalizedPlatform);
    }

    if (
      settings.regionFolderFormat &&
      settings.regionFolderFormat.value !== normalizedRegion
    ) {
      updateSetting('regionFolderFormat', normalizedRegion);
    }

    if (
      settings.standardGameFormat &&
      settings.standardGameFormat.value !== normalizedGame
    ) {
      updateSetting('standardGameFormat', normalizedGame);
    }

    if (
      settings.discGameFormat &&
      settings.discGameFormat.value !== normalizedDiscGame
    ) {
      updateSetting('discGameFormat', normalizedDiscGame);
    }

    setUnifiedPathFormat(
      `${normalizedPlatform}/${normalizedRegion}/${normalizedGame}`
    );
    setDiscPathFormat(
      `${normalizedPlatform}/${normalizedRegion}/${normalizedDiscGame}`
    );
    setInitializedFromSettings(true);
  }, [hasSettings, initializedFromSettings, settings, updateSetting]);

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      if (change.name === 'unifiedNamingTemplate') {
        const templateKey = String(change.value ?? '');
        const matchedTemplate = DATABASE_NAMING_TEMPLATES.find(
          (t) => t.key === templateKey
        );

        if (!matchedTemplate) {
          return;
        }

        setUnifiedPathFormat(matchedTemplate.format);

        const {
          platformFolderFormat,
          regionFolderFormat,
          standardGameFormat,
        } = splitUnifiedPathIntoBackendFields(matchedTemplate.format);

        updateSetting('platformFolderFormat', platformFolderFormat);
        updateSetting('regionFolderFormat', regionFolderFormat);
        updateSetting('standardGameFormat', standardGameFormat);
        return;
      }

      if (change.name === 'unifiedPathFormat') {
        const nextPath = String(change.value ?? '');
        setUnifiedPathFormat(nextPath);

        const {
          platformFolderFormat,
          regionFolderFormat,
          standardGameFormat,
        } = splitUnifiedPathIntoBackendFields(nextPath);

        updateSetting('platformFolderFormat', platformFolderFormat);
        updateSetting('regionFolderFormat', regionFolderFormat);
        updateSetting('standardGameFormat', standardGameFormat);
        return;
      }

      if (change.name === 'discPathFormat') {
        const nextDiscPath = String(change.value ?? '');
        setDiscPathFormat(nextDiscPath);

        const { standardGameFormat: discGameFormat } =
          splitUnifiedPathIntoBackendFields(nextDiscPath);

        updateSetting('discGameFormat', discGameFormat);
        return;
      }

      const key = change.name as keyof NamingSettingsModel;

      updateSetting(key, change.value as NamingSettingsModel[typeof key]);
    },
    [updateSetting]
  );

  const handleUnifiedNamingModalOpenClick = useCallback(() => {
    setActiveModalTarget('unifiedPathFormat');
    setNamingModalOpen();
  }, [setNamingModalOpen]);

  const handleDiscNamingModalOpenClick = useCallback(() => {
    setActiveModalTarget('discPathFormat');
    setNamingModalOpen();
  }, [setNamingModalOpen]);

  const replaceIllegalCharacters = Boolean(
    hasSettings && settings.replaceIllegalCharacters?.value
  );

  const colonReplacementOptions: EnhancedSelectInputValue<number>[] = [
    { key: 0, value: translate('Delete') },
    { key: 1, value: translate('ReplaceWithDash') },
    { key: 2, value: translate('ReplaceWithSpaceDash') },
    { key: 3, value: translate('ReplaceWithSpaceDashSpace') },
    {
      key: 4,
      value: translate('SmartReplace'),
      hint: translate('SmartReplaceHint'),
    },
    {
      key: 5,
      value: translate('Custom'),
      hint: translate('CustomColonReplacementFormatHint'),
    },
  ];

  useEffect(() => {
    onChildStateChange({
      hasPendingChanges,
      isSaving,
    });
  }, [hasPendingChanges, isSaving, onChildStateChange]);

  useEffect(() => {
    setChildSave(saveSettings);
  }, [setChildSave, saveSettings]);

  const selectedTemplateKey =
    DATABASE_NAMING_TEMPLATES.find(
      (t) => t.format === unifiedPathFormat.trim()
    )?.key ?? 'custom';

  const databaseTemplateOptions: EnhancedSelectInputValue<string>[] = [
    {
      key: 'custom',
      value: 'Custom',
      hint: 'Modified or custom folder/filename format',
    },
    ...DATABASE_NAMING_TEMPLATES.map((t) => ({
      key: t.key,
      value: t.label,
      hint: t.hint,
    })),
  ];

  const formatErrors = [
    ...(settings.platformFolderFormat?.errors ?? []),
    ...(settings.regionFolderFormat?.errors ?? []),
    ...(settings.standardGameFormat?.errors ?? []),
  ];

  const formatWarnings = [
    ...(settings.platformFolderFormat?.warnings ?? []),
    ...(settings.regionFolderFormat?.warnings ?? []),
    ...(settings.standardGameFormat?.warnings ?? []),
  ];

  return (
    <FieldSet
      legend={translate('GameNaming')}
      caption="Configure your unified folder and ROM naming structure using manufacturer, platform, region, and game variables (Default: {Manufacturer}/{Platform}/{Region}/{Game Title})"
    >
      {isFetching ? <LoadingIndicator /> : null}
      {!isFetching && error ? (
        <Alert kind={kinds.DANGER}>
          {translate('NamingSettingsLoadError')}
        </Alert>
      ) : null}
      {hasSettings && !isFetching && !error ? (
        <Form>
          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('RenameGames')}</FormLabel>
            <FormInputHelpText text="Automatically rename imported ROMs and organize folders using your Library Path & ROM Naming Format" />
            <FormInput
              type={inputTypes.CHECK}
              name="renameGames"
              onChange={handleInputChange}
              {...settings.renameGames}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('ReplaceIllegalCharacters')}</FormLabel>
            <FormInputHelpText
              text={translate('ReplaceIllegalCharactersHelpText')}
            />
            <FormInput
              type={inputTypes.CHECK}
              name="replaceIllegalCharacters"
              onChange={handleInputChange}
              {...settings.replaceIllegalCharacters}
            />
          </FormRow>

          {replaceIllegalCharacters ? (
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>{translate('ColonReplacement')}</FormLabel>
              <FormInputHelpText
                text={translate('ColonReplacementFormatHelpText')}
              />
              <FormInput
                type={inputTypes.SELECT}
                name="colonReplacementFormat"
                values={colonReplacementOptions}
                onChange={handleInputChange}
                {...settings.colonReplacementFormat}
              />
            </FormRow>
          ) : null}

          {replaceIllegalCharacters &&
          settings.colonReplacementFormat?.value === 5 ? (
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>{translate('CustomColonReplacement')}</FormLabel>
              <FormInputHelpText
                text={translate('CustomColonReplacementFormatHelpText')}
              />
              <FormInput
                type={inputTypes.TEXT}
                name="customColonReplacementFormat"
                onChange={handleInputChange}
                {...settings.customColonReplacementFormat}
              />
            </FormRow>
          ) : null}

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>Verification Database Template</FormLabel>
            <FormInputHelpText text="Select a database verification naming template (No-Intro, SmokeMonster, TOSEC, Redump, GoodTools, HTGDB) to prepopulate the folder and ROM naming format below. You can still customize the format after selecting." />
            <FormInput
              type={inputTypes.SELECT}
              name="unifiedNamingTemplate"
              value={selectedTemplateKey}
              values={databaseTemplateOptions}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.LARGE}>
            <FormLabel>
              Library Path &amp; ROM Naming Format (Cartridge &amp; Handheld)
            </FormLabel>
            <FormInputHelpText text="Unified path format under your Root Folder for cartridge and handheld games. Click '?' to browse Platform, Region, and Game variables." />
            <FormInputHelpText text="Default Preview: {RootFolder}/Nintendo Entertainment System/USA/Super Mario Bros. (USA) (Rev 1).nes" />
            <FormInput
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="unifiedPathFormat"
              value={unifiedPathFormat}
              errors={formatErrors}
              warnings={formatWarnings}
              buttons={
                <FormInputButton onPress={handleUnifiedNamingModalOpenClick}>
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.LARGE}>
            <FormLabel>Disc-Based Games Path &amp; Naming Format</FormLabel>
            <FormInputHelpText text="Path format for optical disc platforms (.chd, .rvz, .iso, .cue/.bin). Houses single-disc and multi-disc games inside their own game-titled subfolder alongside .m3u playlists." />
            <FormInputHelpText text="Default Preview: {RootFolder}/PlayStation/USA/Castlevania - Symphony of the Night/Castlevania - Symphony of the Night (USA).chd" />
            <FormInput
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="discPathFormat"
              value={discPathFormat}
              errors={settings.discGameFormat?.errors ?? []}
              warnings={settings.discGameFormat?.warnings ?? []}
              buttons={
                <FormInputButton onPress={handleDiscNamingModalOpenClick}>
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
            />
          </FormRow>

          <NamingModal
            isOpen={isNamingModalOpen}
            name={activeModalTarget}
            value={
              activeModalTarget === 'discPathFormat'
                ? discPathFormat
                : unifiedPathFormat
            }
            onInputChange={handleInputChange}
            onModalClose={setNamingModalClosed}
          />
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default Naming;
