import React, { useCallback, useState } from 'react';
import {
  setAddSeriesOption,
  useAddSeriesOptions,
} from 'AddSeries/addSeriesOptionsStore';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { inputTypes, sizes } from 'Helpers/Props';
import RegionOrderingSection from 'Settings/MediaManagement/RegionOrderingSection';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

const defaultLibraryStyleOptions: SelectInputOption[] = [
  { key: '1g1r', value: '1G1R (1 Game 1 ROM)' },
  {
    key: 'completionist',
    value: 'Completionist (All Revisions, Regions & Variants)',
  },
];

const default1g1rScopeOptions: SelectInputOption[] = [
  { key: 'oneGameOneRom', value: 'Per Game' },
  { key: 'oneGamePerRegionPerConsole', value: 'Per Region' },
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

function CollectionSettings() {
  const addOptions = useAddSeriesOptions();
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [onePerRegion1g1r, setOnePerRegion1g1r] = useState<boolean>(
    () =>
      String(addOptions.collectionStrategy) === 'oneGamePerRegionPerConsole' ||
      String(addOptions.collectionStrategy) === '2'
  );

  const isCompletionistDefault =
    String(addOptions.collectionStrategy) === 'preservationist' ||
    String(addOptions.collectionStrategy) === '1';

  const markDirty = useCallback(() => {
    setHasPendingChanges(true);
  }, []);

  const handleSavePress = useCallback(() => {
    setHasPendingChanges(false);
  }, []);

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
      setHasPendingChanges(true);
    },
    [onePerRegion1g1r]
  );

  const handle1g1rScopeChange = useCallback(
    ({ value }: InputChanged<string>) => {
      const useMulti = value === 'oneGamePerRegionPerConsole';
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
      setHasPendingChanges(true);
    },
    [addOptions.collectionStrategy]
  );

  const handleDefaultRevisionChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setAddSeriesOption('revisionPreference', String(value));
      setHasPendingChanges(true);
    },
    []
  );

  const handlePinnedVersionChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setAddSeriesOption('pinnedVersion', value);
      setHasPendingChanges(true);
    },
    []
  );

  const handleBooleanOptionChange = useCallback(
    ({ name, value }: InputChanged<boolean>) => {
      if (
        name === 'enableCompression' ||
        name === 'trackUnlicensed' ||
        name === 'trackRomHacks' ||
        name === 'trackHomebrew'
      ) {
        setAddSeriesOption(name, Boolean(value));
        setHasPendingChanges(true);
      }
    },
    []
  );

  let defaultRevisionValue = String(
    addOptions.revisionPreference || 'latestRevision'
  );

  if (String(addOptions.revisionPreference) === '1') {
    defaultRevisionValue = 'initialRelease';
  } else if (String(addOptions.revisionPreference) === '2') {
    defaultRevisionValue = 'pinned';
  }

  return (
    <SettingsPage
      title="Collection Settings"
      showSave={true}
      hasPendingChanges={hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title="Collection Settings"
          />

          <FieldSet
            legend="Default Library Curation (1G1R & Completionist)"
            caption="Set your global library curation defaults. Even if you select Completionist globally, your 1G1R preferences below will be inherited whenever you override a specific console to 1G1R."
          >
            <Form>
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Default Collection Strategy</FormLabel>
                <FormInputHelpText text="Default collection strategy applied when adding or importing consoles/platforms" />
                <SelectInput<string>
                  name="defaultLibraryStyle"
                  value={isCompletionistDefault ? 'completionist' : '1g1r'}
                  values={defaultLibraryStyleOptions}
                  onChange={handleDefaultLibraryStyleChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>1G1R Scope</FormLabel>
                <FormInputHelpText text="Choose whether 1G1R keeps 1 preferred ROM per game across the console (Per Game) or 1 preferred ROM inside each monitored region (Per Region)" />
                <SelectInput<string>
                  name="oneGameOneRomScope"
                  value={
                    onePerRegion1g1r
                      ? 'oneGamePerRegionPerConsole'
                      : 'oneGameOneRom'
                  }
                  values={default1g1rScopeOptions}
                  onChange={handle1g1rScopeChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>1G1R Default Version Preference</FormLabel>
                <FormInputHelpText text="Which ROM version/revision to prefer in 1G1R mode (used globally or when overriding a console to 1G1R)" />
                <SelectInput<string>
                  name="defaultRevisionPreference"
                  value={defaultRevisionValue}
                  values={defaultRevisionPreferenceOptions}
                  onChange={handleDefaultRevisionChange}
                />
              </FormRow>

              {defaultRevisionValue === 'pinned' ? (
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Default Pinned Version Tag</FormLabel>
                  <FormInputHelpText text="Revision tag to pin when Pinned Version is selected (e.g. Rev A, Rev 1, v1.1)" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="pinnedVersion"
                    value={addOptions.pinnedVersion ?? ''}
                    placeholder="e.g. Rev A, Rev 1, v1.1"
                    onChange={handlePinnedVersionChange}
                  />
                </FormRow>
              ) : null}

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Enable Compression by Default</FormLabel>
                <FormInputHelpText text="Automatically compress imported ROMs/discs using the optimal archival format for each platform (e.g. CHD for optical discs, ZIP/7z for cartridges)" />
                <FormInput
                  type={inputTypes.CHECK}
                  name="enableCompression"
                  value={addOptions.enableCompression ?? true}
                  onChange={handleBooleanOptionChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Track Unlicensed Games by Default</FormLabel>
                <FormInputHelpText text="Include unlicensed commercial and historical cartridge/disc releases when adding a new platform" />
                <FormInput
                  type={inputTypes.CHECK}
                  name="trackUnlicensed"
                  value={Boolean(addOptions.trackUnlicensed)}
                  onChange={handleBooleanOptionChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  Track ROM Hacks &amp; Translations by Default
                </FormLabel>
                <FormInputHelpText text="Include community ROM hacks and fan translations (verified via cryptographic hash-gating) when adding a new platform" />
                <FormInput
                  type={inputTypes.CHECK}
                  name="trackRomHacks"
                  value={Boolean(addOptions.trackRomHacks)}
                  onChange={handleBooleanOptionChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>
                  Track Homebrew &amp; Aftermarket by Default
                </FormLabel>
                <FormInputHelpText text="Include modern homebrew and indie aftermarket releases when adding a new platform" />
                <FormInput
                  type={inputTypes.CHECK}
                  name="trackHomebrew"
                  value={Boolean(addOptions.trackHomebrew)}
                  onChange={handleBooleanOptionChange}
                />
              </FormRow>
            </Form>
          </FieldSet>

          <RegionOrderingSection onOrderChange={markDirty} />
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default CollectionSettings;
