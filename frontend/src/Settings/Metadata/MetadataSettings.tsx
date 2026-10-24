import React, { useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { kinds, sizes } from 'Helpers/Props';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

interface GameMediaAssetSettings {
  boxArt2d: boolean;
  boxArt3d: boolean;
  titleScreens: boolean;
  screenshots: boolean;
  clearLogos: boolean;
  mediaScans: boolean;
  videoPreviews: boolean;
  manualsPdf: boolean;
  publishingMetadata: boolean;
  generateGamelistXml: boolean;
}

const STORAGE_KEY = 'controllarr_game_metadata_assets_v1';

const DEFAULT_SETTINGS: GameMediaAssetSettings = {
  boxArt2d: true,
  boxArt3d: true,
  titleScreens: true,
  screenshots: true,
  clearLogos: true,
  mediaScans: false,
  videoPreviews: false,
  manualsPdf: true,
  publishingMetadata: true,
  generateGamelistXml: true,
};

function loadInitialSettings(): GameMediaAssetSettings {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);

    if (raw) {
      return { ...DEFAULT_SETTINGS, ...JSON.parse(raw) };
    }
  } catch {
    // ignore storage errors
  }

  return DEFAULT_SETTINGS;
}

function MetadataSettings() {
  const [values, setValues] =
    useState<GameMediaAssetSettings>(loadInitialSettings);
  const [hasPendingChanges, setHasPendingChanges] = useState(false);
  const [savedNotice, setSavedNotice] = useState(false);

  const handleToggle = useCallback(({ name, value }: InputChanged<boolean>) => {
    setValues((prev) => ({
      ...prev,
      [name]: Boolean(value),
    }));
    setHasPendingChanges(true);
    setSavedNotice(false);
  }, []);

  const handleSavePress = useCallback(() => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(values));
    setHasPendingChanges(false);
    setSavedNotice(true);
  }, [values]);

  return (
    <SettingsPage
      title={translate('MetadataSettings')}
      showSave={true}
      hasPendingChanges={hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('Metadata')}
          />

          {savedNotice ? (
            <Alert kind={kinds.SUCCESS}>
              Game media asset and publishing metadata settings saved.
            </Alert>
          ) : null}

          <Form>
            <FieldSet
              legend="Artwork & Visual Media Assets"
              caption="Select which artwork types Controllarr should download and store alongside your platforms and ROMs"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>2D Box Art (Front &amp; Regional Covers)</FormLabel>
                <FormInputHelpText text="Download regional retail box art (USA, Europe, Japan / Famicom / Mega Drive)" />
                <CheckInput
                  name="boxArt2d"
                  value={values.boxArt2d}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>3D Box Art Renders</FormLabel>
                <FormInputHelpText text="Download angled 3D box art renders for emulation frontends" />
                <CheckInput
                  name="boxArt3d"
                  value={values.boxArt3d}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Title Screens</FormLabel>
                <FormInputHelpText text="Download in-game title screen captures" />
                <CheckInput
                  name="titleScreens"
                  value={values.titleScreens}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Gameplay Screenshots</FormLabel>
                <FormInputHelpText text="Download representative in-game action screenshots" />
                <CheckInput
                  name="screenshots"
                  value={values.screenshots}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Clear Logos / Wheel Art</FormLabel>
                <FormInputHelpText text="Download transparent game and console logos for BigBox, ES-DE, and Batocera" />
                <CheckInput
                  name="clearLogos"
                  value={values.clearLogos}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Cartridge &amp; Optical Disc Scans</FormLabel>
                <FormInputHelpText text="Download physical cartridge label and CD/DVD artwork scans" />
                <CheckInput
                  name="mediaScans"
                  value={values.mediaScans}
                  onChange={handleToggle}
                />
              </FormRow>
            </FieldSet>

            <FieldSet
              legend="Video Previews, Manuals & Publishing Data"
              caption="Configure video snaps, PDF manuals, and frontend metadata XML generation"
            >
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Gameplay Video Previews (.mp4)</FormLabel>
                <FormInputHelpText text="Download short gameplay video snaps when supported by an active metadata source (e.g. ScreenScraper)" />
                <CheckInput
                  name="videoPreviews"
                  value={values.videoPreviews}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Game Instruction Manuals (.pdf)</FormLabel>
                <FormInputHelpText text="Download scanned original retail instruction booklets" />
                <CheckInput
                  name="manualsPdf"
                  value={values.manualsPdf}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Publishing &amp; Release Metadata</FormLabel>
                <FormInputHelpText text="Store Developer, Publisher, Release Year, Genre, Player Count, and Region data" />
                <CheckInput
                  name="publishingMetadata"
                  value={values.publishingMetadata}
                  onChange={handleToggle}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>Generate EmulationStation gamelist.xml</FormLabel>
                <FormInputHelpText text="Write and update gamelist.xml inside each platform folder for ES-DE, Batocera, Knulli, and RetroBat" />
                <CheckInput
                  name="generateGamelistXml"
                  value={values.generateGamelistXml}
                  onChange={handleToggle}
                />
              </FormRow>
            </FieldSet>
          </Form>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default MetadataSettings;
