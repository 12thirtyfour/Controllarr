import React, { useCallback, useRef, useState } from 'react';
import {
  setAddSeriesOption,
  useAddSeriesOptions,
} from 'AddSeries/addSeriesOptionsStore';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import SectionHeading from 'Components/SectionHeading';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import RootFolders from 'RootFolder/RootFolders';
import { useShowAdvancedSettings } from 'Settings/advancedSettingsStore';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import { SettingsStateChange } from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import Naming from './Naming/Naming';
import RegionOrderingSection from './RegionOrderingSection';
import AddRootFolder from './RootFolder/AddRootFolder';
import {
  MediaManagementSettingsModel,
  useManageMediaManagementSettings,
} from './useMediaManagementSettings';

const defaultLibraryStyleOptions: SelectInputOption[] = [
  { key: '1g1r', value: '1G1R (1 Game 1 ROM)' },
  {
    key: 'completionist',
    value: 'Completionist (All Revisions, Regions & Variants)',
  },
];

const defaultRevisionPreferenceOptions: SelectInputOption[] = [
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

const episodeTitleRequiredOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'always',
    get value() {
      return translate('Always');
    },
  },
  {
    key: 'bulkSeasonReleases',
    get value() {
      return translate('OnlyForBulkSeasonReleases');
    },
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    },
  },
];

const rescanAfterRefreshOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'always',
    get value() {
      return translate('Always');
    },
  },
  {
    key: 'afterManual',
    get value() {
      return translate('AfterManualRefresh');
    },
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    },
  },
];

const downloadPropersAndRepacksOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'preferAndUpgrade',
    get value() {
      return translate('PreferAndUpgrade');
    },
  },
  {
    key: 'doNotUpgrade',
    get value() {
      return translate('DoNotUpgradeAutomatically');
    },
  },
  {
    key: 'doNotPrefer',
    get value() {
      return translate('DoNotPrefer');
    },
  },
];

const fileDateOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'none',
    get value() {
      return translate('None');
    },
  },
  {
    key: 'localAirDate',
    get value() {
      return translate('LocalAirDate');
    },
  },
  {
    key: 'utcAirDate',
    get value() {
      return translate('UtcAirDate');
    },
  },
];

const seasonPackUpgradeOptions: EnhancedSelectInputValue<string>[] = [
  {
    key: 'all',
    get value() {
      return translate('All');
    },
  },
  {
    key: 'threshold',
    get value() {
      return translate('Threshold');
    },
  },
  {
    key: 'any',
    get value() {
      return translate('Any');
    },
  },
];

function MediaManagement() {
  const showAdvancedSettings = useShowAdvancedSettings();
  const isWindows = useIsWindows();
  const addOptions = useAddSeriesOptions();
  const [onePerRegion1g1r, setOnePerRegion1g1r] = useState<boolean>(
    () =>
      String(addOptions.collectionStrategy) === 'oneGamePerRegionPerConsole' ||
      String(addOptions.collectionStrategy) === '2'
  );

  const {
    isFetching,
    isFetched: isPopulated,
    isSaving,
    error,
    settings,
    hasSettings,
    hasPendingChanges,
    validationErrors,
    validationWarnings,
    saveSettings: saveMediaManagementSettings,
    updateSetting,
  } = useManageMediaManagementSettings();

  const [naming, setNaming] = useState<SettingsStateChange>({
    isSaving: false,
    hasPendingChanges: false,
  });

  const saveSettings = useRef<{
    naming: () => void;
  }>({
    naming: () => {},
  });

  const handleSetNamingSave = useCallback((saveCallback: () => void) => {
    saveSettings.current.naming = saveCallback;
  }, []);

  const handleSavePress = useCallback(() => {
    saveMediaManagementSettings();
    saveSettings.current.naming();
  }, [saveMediaManagementSettings]);

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      updateSetting(
        change.name as keyof MediaManagementSettingsModel,
        change.value as MediaManagementSettingsModel[keyof MediaManagementSettingsModel]
      );
    },
    [updateSetting]
  );

  const isCompletionistDefault =
    String(addOptions.collectionStrategy) === 'preservationist' ||
    String(addOptions.collectionStrategy) === '1';

  const handleDefaultLibraryStyleChange = useCallback(
    ({ value }: InputChanged<string>) => {
      if (value === 'completionist') {
        setAddSeriesOption('collectionStrategy', 'preservationist');
      } else {
        setAddSeriesOption(
          'collectionStrategy',
          onePerRegion1g1r ? 'oneGamePerRegionPerConsole' : 'oneGameOneRom'
        );
      }
    },
    [onePerRegion1g1r]
  );

  const handleOnePerGameCheck = useCallback(
    ({ value }: InputChanged<boolean>) => {
      const useSingle = Boolean(value);
      setOnePerRegion1g1r(!useSingle);
      if (
        String(addOptions.collectionStrategy) !== 'preservationist' &&
        String(addOptions.collectionStrategy) !== '1'
      ) {
        setAddSeriesOption(
          'collectionStrategy',
          useSingle ? 'oneGameOneRom' : 'oneGamePerRegionPerConsole'
        );
      }
    },
    [addOptions.collectionStrategy]
  );

  const handleOnePerRegionCheck = useCallback(
    ({ value }: InputChanged<boolean>) => {
      const useMulti = Boolean(value);
      setOnePerRegion1g1r(useMulti);
      if (
        String(addOptions.collectionStrategy) !== 'preservationist' &&
        String(addOptions.collectionStrategy) !== '1'
      ) {
        setAddSeriesOption(
          'collectionStrategy',
          useMulti ? 'oneGamePerRegionPerConsole' : 'oneGameOneRom'
        );
      }
    },
    [addOptions.collectionStrategy]
  );

  const handleDefaultRevisionChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setAddSeriesOption('revisionPreference', String(value));
    },
    []
  );

  return (
    <SettingsPage
      title={translate('MediaManagementSettings')}
      isSaving={isSaving || naming.isSaving}
      hasPendingChanges={naming.hasPendingChanges || hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('MediaManagement')}
          />

          <Naming
            setChildSave={handleSetNamingSave}
            onChildStateChange={setNaming}
          />

          <FieldSet
            legend="Default Library Curation (1G1R & Completionist)"
            caption="Set your global library curation defaults. Even if you select Completionist globally, your 1G1R preferences below will be inherited whenever you override a specific console to 1G1R."
          >
            <Form>
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Default Library Style</FormLabel>
                <FormInputHelpText text="Default collection strategy applied when adding new consoles/platforms" />
                <SelectInput<string>
                  name="defaultLibraryStyle"
                  value={isCompletionistDefault ? 'completionist' : '1g1r'}
                  values={defaultLibraryStyleOptions}
                  onChange={handleDefaultLibraryStyleChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>1G1R Default Version Preference</FormLabel>
                <FormInputHelpText text="Which ROM version/revision to prefer in 1G1R mode (used globally or when overriding a console to 1G1R)" />
                <SelectInput<string>
                  name="defaultRevisionPreference"
                  value={
                    String(addOptions.revisionPreference) === '1'
                      ? 'initialRelease'
                      : String(addOptions.revisionPreference) === '2'
                        ? 'pinned'
                        : String(
                            addOptions.revisionPreference || 'latestRevision'
                          )
                  }
                  values={defaultRevisionPreferenceOptions}
                  onChange={handleDefaultRevisionChange}
                />
              </FormRow>

              {String(addOptions.revisionPreference) === 'pinned' ||
              String(addOptions.revisionPreference) === '2' ? (
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Default Pinned Version Tag</FormLabel>
                  <FormInputHelpText text="Revision tag to pin when Pinned Version is selected (e.g. Rev A, Rev 1, v1.1)" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="pinnedVersion"
                    value={addOptions.pinnedVersion ?? ''}
                    placeholder="e.g. Rev A, Rev 1, v1.1"
                    onChange={({ value }: InputChanged<string>) =>
                      setAddSeriesOption('pinnedVersion', value)
                    }
                  />
                </FormRow>
              ) : null}

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>1G1R Scope: 1 ROM Per Game (Single Region)</FormLabel>
                <FormInputHelpText text="Keep only 1 preferred ROM per title across the entire console following your region priority" />
                <CheckInput
                  name="onePerGame1g1r"
                  value={!onePerRegion1g1r}
                  onChange={handleOnePerGameCheck}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  1G1R Scope: 1 ROM Per Monitored Region (Multi-Region 1G1R)
                </FormLabel>
                <FormInputHelpText text="Keep 1 preferred ROM per title inside each monitored region (e.g. 1 USA + 1 Japan)" />
                <CheckInput
                  name="onePerRegion1g1r"
                  value={onePerRegion1g1r}
                  onChange={handleOnePerRegionCheck}
                />
              </FormRow>
            </Form>
          </FieldSet>

          <RegionOrderingSection />

          {isFetching ? (
            <FieldSet legend={translate('NamingSettings')}>
              <LoadingIndicator />
            </FieldSet>
          ) : null}

          {!isFetching && error ? (
            <FieldSet legend={translate('NamingSettings')}>
              <Alert kind={kinds.DANGER}>
                {translate('MediaManagementSettingsLoadError')}
              </Alert>
            </FieldSet>
          ) : null}

          {hasSettings && isPopulated && !error ? (
            <Form
              id="mediaManagementSettings"
              validationErrors={validationErrors}
              validationWarnings={validationWarnings}
            >
              {showAdvancedSettings ? (
                <FieldSet
                  legend={translate('Folders')}
                  caption={translate('FoldersCaption')}
                >
                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>
                      {translate('CreateEmptySeriesFolders')}
                    </FormLabel>
                    <FormInputHelpText
                      text={translate('CreateEmptySeriesFoldersHelpText')}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="createEmptySeriesFolders"
                      onChange={handleInputChange}
                      {...settings.createEmptySeriesFolders}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('DeleteEmptyFolders')}</FormLabel>
                    <FormInputHelpText
                      text={translate('DeleteEmptySeriesFoldersHelpText')}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="deleteEmptyFolders"
                      onChange={handleInputChange}
                      {...settings.deleteEmptyFolders}
                    />
                  </FormRow>
                </FieldSet>
              ) : null}

              {showAdvancedSettings ? (
                <FieldSet
                  legend={translate('Importing')}
                  caption={translate('ImportingCaption')}
                >
                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.SMALL}
                  >
                    <FormLabel>{translate('EpisodeTitleRequired')}</FormLabel>
                    <FormInputHelpText
                      text={translate('EpisodeTitleRequiredHelpText')}
                    />
                    <FormInput
                      type={inputTypes.SELECT}
                      name="episodeTitleRequired"
                      values={episodeTitleRequiredOptions}
                      onChange={handleInputChange}
                      {...settings.episodeTitleRequired}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>
                      {translate('SkipFreeSpaceCheckWhenGrabbing')}
                    </FormLabel>
                    <FormInputHelpText
                      text={translate('SkipFreeSpaceCheckWhenGrabbingHelpText')}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="skipFreeSpaceCheckWhenGrabbing"
                      onChange={handleInputChange}
                      {...settings.skipFreeSpaceCheckWhenGrabbing}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>
                      {translate('SkipFreeSpaceCheckWhenImporting')}
                    </FormLabel>
                    <FormInputHelpText
                      text={translate(
                        'SkipFreeSpaceCheckWhenImportingHelpText'
                      )}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="skipFreeSpaceCheckWhenImporting"
                      onChange={handleInputChange}
                      {...settings.skipFreeSpaceCheckWhenImporting}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('MinimumFreeSpace')}</FormLabel>
                    <FormInputHelpText
                      text={translate('MinimumFreeSpaceHelpText')}
                    />
                    <FormInput
                      type={inputTypes.NUMBER}
                      unit="MB"
                      name="minimumFreeSpaceWhenImporting"
                      onChange={handleInputChange}
                      {...settings.minimumFreeSpaceWhenImporting}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>
                      {translate('UseHardlinksInsteadOfCopy')}
                    </FormLabel>
                    <FormInputHelpText
                      text={translate('CopyUsingHardlinksSeriesHelpText')}
                    />
                    <FormInputHelpText
                      text={translate('CopyUsingHardlinksHelpTextWarning')}
                      isWarning={true}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="copyUsingHardlinks"
                      onChange={handleInputChange}
                      {...settings.copyUsingHardlinks}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('ImportUsingScript')}</FormLabel>
                    <FormInputHelpText
                      text={translate('ImportUsingScriptHelpText')}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="useScriptImport"
                      onChange={handleInputChange}
                      {...settings.useScriptImport}
                    />
                  </FormRow>

                  {settings.useScriptImport.value ? (
                    <FormRow
                      advancedSettings={showAdvancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ImportScriptPath')}</FormLabel>
                      <FormInputHelpText
                        text={translate('ImportScriptPathHelpText')}
                      />
                      <FormInput
                        type={inputTypes.PATH}
                        includeFiles={true}
                        name="scriptImportPath"
                        onChange={handleInputChange}
                        {...settings.scriptImportPath}
                      />
                    </FormRow>
                  ) : null}

                  <FormRow size={sizes.MEDIUM}>
                    <FormLabel>{translate('ImportExtraFiles')}</FormLabel>
                    <FormInputHelpText
                      text={translate('ImportExtraFilesEpisodeHelpText')}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="importExtraFiles"
                      onChange={handleInputChange}
                      {...settings.importExtraFiles}
                    />
                  </FormRow>

                  {settings.importExtraFiles.value ? (
                    <FormRow
                      advancedSettings={showAdvancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ImportExtraFiles')}</FormLabel>
                      <FormInputHelpText
                        text={translate('ExtraFileExtensionsHelpText')}
                      />
                      <FormInputHelpText
                        text={translate('ExtraFileExtensionsHelpTextsExamples')}
                      />
                      <FormInput
                        type={inputTypes.TEXT}
                        name="extraFileExtensions"
                        onChange={handleInputChange}
                        {...settings.extraFileExtensions}
                      />
                    </FormRow>
                  ) : null}

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('UserRejectedExtensions')}</FormLabel>
                    <FormInputHelpText
                      text={translate('UserRejectedExtensionsHelpText')}
                    />
                    <FormInputHelpText
                      text={translate('UserRejectedExtensionsTextsExamples')}
                    />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="userRejectedExtensions"
                      onChange={handleInputChange}
                      {...settings.userRejectedExtensions}
                    />
                  </FormRow>

                  {showAdvancedSettings ? (
                    <>
                      <FormRow
                        advancedSettings={showAdvancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>
                          {translate('SeasonPackUpgradeAllowLabel')}
                        </FormLabel>
                        <FormInputHelpText
                          text={translate('SeasonPackUpgradeAllowHelpText')}
                        />
                        <FormInputHelpText
                          text={
                            settings.seasonPackUpgrade.value === 'any'
                              ? translate('SeasonPackUpgradeAllowAnyWarning')
                              : undefined
                          }
                          isWarning={true}
                        />
                        <FormInput
                          type={inputTypes.SELECT}
                          name="seasonPackUpgrade"
                          values={seasonPackUpgradeOptions}
                          onChange={handleInputChange}
                          {...settings.seasonPackUpgrade}
                        />
                      </FormRow>

                      {settings.seasonPackUpgrade.value === 'threshold' ? (
                        <FormRow
                          advancedSettings={showAdvancedSettings}
                          isAdvanced={true}
                          size={sizes.MEDIUM}
                        >
                          <FormLabel>
                            {translate('SeasonPackUpgradeThresholdLabel')}
                          </FormLabel>
                          <FormInputHelpText
                            text={translate(
                              'SeasonPackUpgradeThresholdHelpText'
                            )}
                          />
                          <FormInput
                            type={inputTypes.FLOAT}
                            name="seasonPackUpgradeThreshold"
                            unit="%"
                            step={0.01}
                            min={0}
                            max={100}
                            onChange={handleInputChange}
                            {...settings.seasonPackUpgradeThreshold}
                          />
                        </FormRow>
                      ) : null}
                    </>
                  ) : null}
                </FieldSet>
              ) : null}

              <FieldSet
                legend={translate('FileManagement')}
                caption={translate('FileManagementCaption')}
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>{translate('UnmonitorDeletedEpisodes')}</FormLabel>
                  <FormInputHelpText
                    text={translate('UnmonitorDeletedEpisodesHelpText')}
                  />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="autoUnmonitorPreviouslyDownloadedEpisodes"
                    onChange={handleInputChange}
                    {...settings.autoUnmonitorPreviouslyDownloadedEpisodes}
                  />
                </FormRow>

                <FormRow
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>
                    {translate('DownloadPropersAndRepacks')}
                  </FormLabel>
                  <FormInputHelpText
                    text={translate('DownloadPropersAndRepacksHelpText')}
                  />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="downloadPropersAndRepacks"
                    values={downloadPropersAndRepacksOptions}
                    onChange={handleInputChange}
                    {...settings.downloadPropersAndRepacks}
                  />
                </FormRow>

                <FormRow
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel>
                    {translate('RescanSeriesFolderAfterRefresh')}
                  </FormLabel>
                  <FormInputHelpText
                    text={translate('RescanAfterRefreshSeriesHelpText')}
                  />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="rescanAfterRefresh"
                    values={rescanAfterRefreshOptions}
                    onChange={handleInputChange}
                    {...settings.rescanAfterRefresh}
                  />
                </FormRow>

                <FormRow
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel>{translate('ChangeFileDate')}</FormLabel>
                  <FormInputHelpText
                    text={translate('ChangeFileDateHelpText')}
                  />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="fileDate"
                    values={fileDateOptions}
                    onChange={handleInputChange}
                    {...settings.fileDate}
                  />
                </FormRow>

                <FormRow
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel>{translate('RecyclingBin')}</FormLabel>
                  <FormInputHelpText text={translate('RecyclingBinHelpText')} />
                  <FormInput
                    type={inputTypes.PATH}
                    name="recycleBin"
                    includeFiles={false}
                    onChange={handleInputChange}
                    {...settings.recycleBin}
                  />
                </FormRow>

                <FormRow
                  advancedSettings={showAdvancedSettings}
                  isAdvanced={true}
                >
                  <FormLabel>{translate('RecyclingBinCleanup')}</FormLabel>
                  <FormInputHelpText
                    text={translate('RecyclingBinCleanupHelpText')}
                  />
                  <FormInput
                    type={inputTypes.NUMBER}
                    name="recycleBinCleanupDays"
                    min={0}
                    onChange={handleInputChange}
                    {...settings.recycleBinCleanupDays}
                  />
                </FormRow>
              </FieldSet>

              {showAdvancedSettings && !isWindows ? (
                <FieldSet
                  legend={translate('Permissions')}
                  caption={translate('PermissionsCaption')}
                >
                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('SetPermissions')}</FormLabel>
                    <FormInputHelpText
                      text={translate('SetPermissionsLinuxHelpText')}
                    />
                    <FormInput
                      type={inputTypes.CHECK}
                      name="setPermissionsLinux"
                      onChange={handleInputChange}
                      {...settings.setPermissionsLinux}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('ChmodFolder')}</FormLabel>
                    <FormInputHelpText
                      text={translate('ChmodFolderHelpText')}
                    />
                    <FormInput
                      type={inputTypes.UMASK}
                      name="chmodFolder"
                      onChange={handleInputChange}
                      {...settings.chmodFolder}
                    />
                  </FormRow>

                  <FormRow
                    advancedSettings={showAdvancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('ChownGroup')}</FormLabel>
                    <FormInputHelpText text={translate('ChownGroupHelpText')} />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="chownGroup"
                      onChange={handleInputChange}
                      {...settings.chownGroup}
                    />
                  </FormRow>
                </FieldSet>
              ) : null}
            </Form>
          ) : null}

          <SectionHeading
            title={translate('RootFolders')}
            description={translate('RootFoldersCaption')}
          />

          <RootFolders />

          <AddRootFolder />
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default MediaManagement;
