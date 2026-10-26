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
  seriesFolderFormat: string;
  seasonFolderFormat: string;
  standardGameFormat: string;
} {
  const segments = rawPath
    .split('/')
    .map((s) => s.trim())
    .filter(Boolean);

  if (segments.length === 0) {
    return {
      seriesFolderFormat: '{Manufacturer}/{Platform}',
      seasonFolderFormat: '{Region}',
      standardGameFormat: '{Game Title}',
    };
  }

  if (segments.length === 1) {
    return {
      seriesFolderFormat: '{Manufacturer}/{Platform}',
      seasonFolderFormat: '{Region}',
      standardGameFormat: segments[0],
    };
  }

  if (segments.length === 2) {
    return {
      seriesFolderFormat: segments[0],
      seasonFolderFormat: '{Region}',
      standardGameFormat: segments[1],
    };
  }

  const regionIdx = segments.findIndex(
    (s, idx) =>
      idx >= 1 &&
      idx < segments.length - 1 &&
      (/\{Region/i.test(s) || /\{Season/i.test(s))
  );

  if (regionIdx >= 1) {
    return {
      seriesFolderFormat: segments.slice(0, regionIdx).join('/'),
      seasonFolderFormat: segments[regionIdx],
      standardGameFormat: segments.slice(regionIdx + 1).join('/'),
    };
  }

  return {
    seriesFolderFormat: segments[0],
    seasonFolderFormat: segments[1],
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

    const seriesValue = settings.seriesFolderFormat?.value ?? '';
    const seasonValue = settings.seasonFolderFormat?.value ?? '';
    const gameValue =
      settings.standardGameFormat?.value ??
      settings.standardEpisodeFormat?.value ??
      '';
    const rawDaily =
      settings.dailyGameFormat?.value ??
      settings.dailyEpisodeFormat?.value ??
      '';

    const normalizedSeries = normalizeSegmentToConsoleVariables(
      seriesValue,
      '{Manufacturer}/{Platform}'
    );
    const normalizedSeason = normalizeSegmentToConsoleVariables(
      seasonValue,
      '{Region}'
    );
    const normalizedGame = normalizeSegmentToConsoleVariables(
      gameValue,
      '{Game Title}'
    );
    const normalizedDiscGame =
      !rawDaily ||
      rawDaily === '{Game Title}' ||
      rawDaily.includes('Air-Date') ||
      rawDaily.includes('air-date')
        ? '{Game Title}/{Game Title}'
        : normalizeSegmentToConsoleVariables(
            rawDaily,
            '{Game Title}/{Game Title}'
          );

    if (settings.seriesFolderFormat && settings.seriesFolderFormat.value !== normalizedSeries) {
      updateSetting('seriesFolderFormat', normalizedSeries);
    }

    if (settings.seasonFolderFormat && settings.seasonFolderFormat.value !== normalizedSeason) {
      updateSetting('seasonFolderFormat', normalizedSeason);
    }

    if (
      (settings.standardGameFormat && settings.standardGameFormat.value !== normalizedGame) ||
      (settings.standardEpisodeFormat && settings.standardEpisodeFormat.value !== normalizedGame)
    ) {
      updateSetting('standardGameFormat', normalizedGame);
      updateSetting('standardEpisodeFormat', normalizedGame);
    }

    if (
      (settings.dailyGameFormat && settings.dailyGameFormat.value !== normalizedDiscGame) ||
      (settings.dailyEpisodeFormat && settings.dailyEpisodeFormat.value !== normalizedDiscGame)
    ) {
      updateSetting('dailyGameFormat', normalizedDiscGame);
      updateSetting('dailyEpisodeFormat', normalizedDiscGame);
    }

    setUnifiedPathFormat(
      `${normalizedSeries}/${normalizedSeason}/${normalizedGame}`
    );
    setDiscPathFormat(
      `${normalizedSeries}/${normalizedSeason}/${normalizedDiscGame}`
    );
    setInitializedFromSettings(true);
  }, [hasSettings, initializedFromSettings, settings, updateSetting]);

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      if (change.name === 'unifiedPathFormat') {
        const nextPath = String(change.value ?? '');
        setUnifiedPathFormat(nextPath);

        const {
          seriesFolderFormat,
          seasonFolderFormat,
          standardGameFormat,
        } = splitUnifiedPathIntoBackendFields(nextPath);

        updateSetting('seriesFolderFormat', seriesFolderFormat);
        updateSetting('seasonFolderFormat', seasonFolderFormat);
        updateSetting('standardGameFormat', standardGameFormat);
        updateSetting('standardEpisodeFormat', standardGameFormat);
        return;
      }

      if (change.name === 'discPathFormat') {
        const nextDiscPath = String(change.value ?? '');
        setDiscPathFormat(nextDiscPath);

        const { standardGameFormat: discGameFormat } =
          splitUnifiedPathIntoBackendFields(nextDiscPath);

        updateSetting('dailyGameFormat', discGameFormat);
        updateSetting('dailyEpisodeFormat', discGameFormat);
        return;
      }

      if (change.name === 'renameGames') {
        const val = change.value as boolean;
        updateSetting('renameGames', val);
        updateSetting('renameEpisodes', val);
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

  const formatErrors = [
    ...(settings.seriesFolderFormat?.errors ?? []),
    ...(settings.seasonFolderFormat?.errors ?? []),
    ...(settings.standardGameFormat?.errors ?? settings.standardEpisodeFormat?.errors ?? []),
  ];

  const formatWarnings = [
    ...(settings.seriesFolderFormat?.warnings ?? []),
    ...(settings.seasonFolderFormat?.warnings ?? []),
    ...(settings.standardGameFormat?.warnings ?? settings.standardEpisodeFormat?.warnings ?? []),
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
              {...(settings.renameGames ?? settings.renameEpisodes)}
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
              errors={settings.dailyGameFormat?.errors ?? settings.dailyEpisodeFormat?.errors ?? []}
              warnings={settings.dailyGameFormat?.warnings ?? settings.dailyEpisodeFormat?.warnings ?? []}
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
