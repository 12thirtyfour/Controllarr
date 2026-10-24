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

const DEFAULT_UNIFIED_PATH_FORMAT = '{Platform}/{Region}/{Game Title}';

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
    return '{Platform}';
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
    trimmed === '{Episode Title}' ||
    trimmed === '{Episode CleanTitle}'
  ) {
    return '{Game Title}';
  }

  return trimmed
    .replace(/\{Series Title\}/gi, '{Platform}')
    .replace(/\{Series CleanTitle\}/gi, '{Platform}')
    .replace(/\{Series Year\}/gi, '{Platform Year}')
    .replace(/\{Season Title\}/gi, '{Region}')
    .replace(/\{Season CleanTitle\}/gi, '{Region}')
    .replace(/\{Episode Title\}/gi, '{Game Title}')
    .replace(/\{Episode CleanTitle\}/gi, '{Game CleanTitle}');
}

function splitUnifiedPathIntoBackendFields(rawPath: string): {
  seriesFolderFormat: string;
  seasonFolderFormat: string;
  standardEpisodeFormat: string;
} {
  const segments = rawPath
    .split('/')
    .map((s) => s.trim())
    .filter(Boolean);

  if (segments.length === 0) {
    return {
      seriesFolderFormat: '{Platform}',
      seasonFolderFormat: '{Region}',
      standardEpisodeFormat: '{Game Title}',
    };
  }

  if (segments.length === 1) {
    return {
      seriesFolderFormat: '{Platform}',
      seasonFolderFormat: '{Region}',
      standardEpisodeFormat: segments[0],
    };
  }

  if (segments.length === 2) {
    return {
      seriesFolderFormat: segments[0],
      seasonFolderFormat: '{Region}',
      standardEpisodeFormat: segments[1],
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
      standardEpisodeFormat: segments.slice(regionIdx + 1).join('/'),
    };
  }

  return {
    seriesFolderFormat: segments[0],
    seasonFolderFormat: segments[1],
    standardEpisodeFormat: segments.slice(2).join('/'),
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

  const [unifiedPathFormat, setUnifiedPathFormat] = useState<string>(
    DEFAULT_UNIFIED_PATH_FORMAT
  );
  const [initializedFromSettings, setInitializedFromSettings] =
    useState<boolean>(false);

  useEffect(() => {
    if (!hasSettings || initializedFromSettings) {
      return;
    }

    const normalizedSeries = normalizeSegmentToConsoleVariables(
      settings.seriesFolderFormat.value,
      '{Platform}'
    );
    const normalizedSeason = normalizeSegmentToConsoleVariables(
      settings.seasonFolderFormat.value,
      '{Region}'
    );
    const normalizedEpisode = normalizeSegmentToConsoleVariables(
      settings.standardEpisodeFormat.value,
      '{Game Title}'
    );

    if (settings.seriesFolderFormat.value !== normalizedSeries) {
      updateSetting('seriesFolderFormat', normalizedSeries);
    }

    if (settings.seasonFolderFormat.value !== normalizedSeason) {
      updateSetting('seasonFolderFormat', normalizedSeason);
    }

    if (settings.standardEpisodeFormat.value !== normalizedEpisode) {
      updateSetting('standardEpisodeFormat', normalizedEpisode);
    }

    setUnifiedPathFormat(
      `${normalizedSeries}/${normalizedSeason}/${normalizedEpisode}`
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
          standardEpisodeFormat,
        } = splitUnifiedPathIntoBackendFields(nextPath);

        updateSetting('seriesFolderFormat', seriesFolderFormat);
        updateSetting('seasonFolderFormat', seasonFolderFormat);
        updateSetting('standardEpisodeFormat', standardEpisodeFormat);
        return;
      }

      const key = change.name as keyof NamingSettingsModel;

      updateSetting(key, change.value as NamingSettingsModel[typeof key]);
    },
    [updateSetting]
  );

  const handleUnifiedNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();
  }, [setNamingModalOpen]);

  const replaceIllegalCharacters =
    hasSettings && settings.replaceIllegalCharacters.value;

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
    ...(settings.standardEpisodeFormat?.errors ?? []),
  ];

  const formatWarnings = [
    ...(settings.seriesFolderFormat?.warnings ?? []),
    ...(settings.seasonFolderFormat?.warnings ?? []),
    ...(settings.standardEpisodeFormat?.warnings ?? []),
  ];

  return (
    <FieldSet
      legend={translate('EpisodeNaming')}
      caption="Configure your unified folder and ROM naming structure using platform, region, and game variables (Default: {Platform}/{Region}/{Game Title})"
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
            <FormLabel>{translate('RenameEpisodes')}</FormLabel>
            <FormInputHelpText text="Automatically rename imported ROMs and organize folders using your Library Path & ROM Naming Format" />
            <FormInput
              type={inputTypes.CHECK}
              name="renameEpisodes"
              onChange={handleInputChange}
              {...settings.renameEpisodes}
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
          settings.colonReplacementFormat.value === 5 ? (
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
            <FormLabel>Library Path &amp; ROM Naming Format</FormLabel>
            <FormInputHelpText text="Unified path format under your Root Folder using forward slashes (/) for folders. Click '?' to browse Platform, Region, and Game variables." />
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

          <NamingModal
            isOpen={isNamingModalOpen}
            name="unifiedPathFormat"
            value={unifiedPathFormat}
            onInputChange={handleInputChange}
            onModalClose={setNamingModalClosed}
          />
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default Naming;
