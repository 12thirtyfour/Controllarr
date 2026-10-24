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
import NamingModal, { NamingModalOptions } from './NamingModal';
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
  const [namingModalOptions, setNamingModalOptions] =
    useState<NamingModalOptions | null>(null);

  // Prepopulate default Game/Platform/Region naming formats if still set to legacy TV defaults
  useEffect(() => {
    if (!hasSettings) {
      return;
    }

    if (
      settings.seasonFolderFormat.value === 'Season {season}' ||
      !settings.seasonFolderFormat.value
    ) {
      updateSetting('seasonFolderFormat', '{Season Title}');
    }

    if (
      settings.standardEpisodeFormat.value.includes(
        'S{season:00}E{episode:00}'
      ) ||
      !settings.standardEpisodeFormat.value
    ) {
      updateSetting('standardEpisodeFormat', '{Episode Title}');
    }

    if (!settings.seriesFolderFormat.value) {
      updateSetting('seriesFolderFormat', '{Series Title}');
    }
  }, [hasSettings, settings, updateSetting]);

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      const key = change.name as keyof NamingSettingsModel;

      updateSetting(key, change.value as NamingSettingsModel[typeof key]);
    },
    [updateSetting]
  );

  const handleStandardNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'standardEpisodeFormat',
      season: true,
      seasonTitle: true,
      episode: true,
      additional: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const handleSeriesFolderNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'seriesFolderFormat',
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const handleSeasonFolderNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'seasonFolderFormat',
      season: true,
      seasonTitle: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

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

  return (
    <FieldSet
      legend={translate('EpisodeNaming')}
      caption="Configure how Controllarr organizes Platform folders, Region folders, and ROM filenames (Default: {Platform}/{Region}/{Game})"
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
            <FormInputHelpText text="Automatically rename imported ROMs using your canonical DAT / No-Intro / Redump naming format" />
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

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('SeriesFolderFormat')}</FormLabel>
            <FormInputHelpText text="Top-level console directory under your Root Folder (Example: Nintendo Entertainment System)" />
            <FormInput
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="seriesFolderFormat"
              buttons={
                <FormInputButton
                  onPress={handleSeriesFolderNamingModalOpenClick}
                >
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
              {...settings.seriesFolderFormat}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('SeasonFolderFormat')}</FormLabel>
            <FormInputHelpText text="Regional subfolder inside the platform folder (Example: USA, Europe, Japan, Homebrew, ROM Hacks)" />
            <FormInput
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="seasonFolderFormat"
              buttons={
                <FormInputButton
                  onPress={handleSeasonFolderNamingModalOpenClick}
                >
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
              {...settings.seasonFolderFormat}
            />
          </FormRow>

          <FormRow size={sizes.LARGE}>
            <FormLabel>{translate('StandardEpisodeFormat')}</FormLabel>
            <FormInputHelpText text="ROM filename structure inside the region folder (Example: Super Mario Bros. (USA) (Rev 1).nes)" />
            <FormInputHelpText text="Full Path Preview: {RootFolder}/Nintendo Entertainment System/USA/Super Mario Bros. (USA) (Rev 1).nes" />
            <FormInput
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="standardEpisodeFormat"
              buttons={
                <FormInputButton onPress={handleStandardNamingModalOpenClick}>
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
              {...settings.standardEpisodeFormat}
            />
          </FormRow>

          {namingModalOptions ? (
            <NamingModal
              isOpen={isNamingModalOpen}
              {...namingModalOptions}
              value={settings[namingModalOptions.name].value}
              onInputChange={handleInputChange}
              onModalClose={setNamingModalClosed}
            />
          ) : null}
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default Naming;
