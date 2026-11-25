import moment from 'moment-timezone';
import React, { useCallback, useEffect, useMemo } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import {
  setPreviewTheme,
  usePlatformThemeMatching,
} from 'Helpers/Hooks/useTheme';
import { inputTypes, kinds } from 'Helpers/Props';
import { useFilteredLanguages } from 'Language/useLanguages';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import timeZoneOptions from 'Utilities/Date/timeZoneOptions';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import {
  ThemeName,
  useManageUiSettings,
  useSaveUiSettings,
} from './useUiSettings';

const createDateFormatOption = (format: string) => ({
  key: format,
  get value() {
    return moment('2014-03-25').format(format);
  },
  hint: format,
});

export const firstDayOfWeekOptions: EnhancedSelectInputValue<number>[] = [
  {
    key: 0,
    get value() {
      return translate('Sunday');
    },
  },
  {
    key: 1,
    get value() {
      return translate('Monday');
    },
  },
];

export const weekColumnOptions: EnhancedSelectInputValue<string>[] = [
  createDateFormatOption('ddd M/D'),
  createDateFormatOption('ddd MM/DD'),
  createDateFormatOption('ddd D/M'),
  createDateFormatOption('ddd DD/MM'),
];

const shortDateFormatOptions: EnhancedSelectInputValue<string>[] = [
  createDateFormatOption('MMM D YYYY'),
  createDateFormatOption('DD MMM YYYY'),
  createDateFormatOption('MM/D/YYYY'),
  createDateFormatOption('MM/DD/YYYY'),
  createDateFormatOption('DD/MM/YYYY'),
  createDateFormatOption('YYYY-MM-DD'),
];

const longDateFormatOptions: EnhancedSelectInputValue<string>[] = [
  createDateFormatOption('dddd, MMMM D YYYY'),
  createDateFormatOption('dddd, D MMMM YYYY'),
];

export const timeFormatOptions: EnhancedSelectInputValue<string>[] = [
  { key: 'h(:mm)a', value: '5pm/5:30pm' },
  { key: 'HH:mm', value: '17:00/17:30' },
];

export const consoleRegionOptions: EnhancedSelectInputValue<string>[] = [
  { key: 'US', value: 'North America / USA (Genesis, NES, TurboGrafx-16)' },
  { key: 'EU', value: 'Europe / PAL (Mega Drive, NES, TurboGrafx)' },
  { key: 'JP', value: 'Japan / NTSC-J (Mega Drive, Famicom, PC Engine)' },
];

function UISettings() {
  const {
    data: languageItems = [],
    isFetching: isLanguagesFetching,
    isFetched: isLanguagesPopulated,
    error: languagesError,
  } = useFilteredLanguages({
    Any: true,
    Original: true,
    Unknown: true,
  });

  const {
    isFetching: isSettingsFetching,
    isFetched: isSettingsPopulated,
    error: settingsError,
    hasPendingChanges,
    hasSettings,
    settings,
    isSaving,
    validationErrors,
    validationWarnings,
    saveSettings,
    updateSetting,
  } = useManageUiSettings();

  const saveThemeSetting = useSaveUiSettings();

  const isFetching = isLanguagesFetching || isSettingsFetching;
  const isPopulated = isLanguagesPopulated && isSettingsPopulated;
  const error = languagesError || settingsError;

  const languages = useMemo(() => {
    return languageItems.map((item) => {
      return {
        key: item.id,
        value: item.name,
      };
    });
  }, [languageItems]);

  const [matchConsoleTheme, setMatchConsoleTheme] = usePlatformThemeMatching();

  useEffect(() => {
    return () => {
      setPreviewTheme(null);
    };
  }, []);

  // Must stay in sync with the theme selectors in frontend/src/Styles/Themes/themes.css.
  // Auto / Light / Dark first, followed by all console themes (Dark & Light) strictly in alphabetical order.
  const consoleBaseThemes: EnhancedSelectInputValue<string>[] = [
    { key: 'gx4000', value: 'Amstrad GX4000 (1990)' },
    { key: 'arduboy', value: 'Arduboy (2016)' },
    { key: 'atari2600', value: 'Atari 2600 Woodgrain (1977)' },
    { key: 'atari5200', value: 'Atari 5200 SuperSystem (1982)' },
    { key: 'atari7800', value: 'Atari 7800 ProSystem (1986)' },
    { key: 'jaguar', value: 'Atari Jaguar (1993)' },
    { key: 'jaguarcd', value: 'Atari Jaguar CD (1995)' },
    { key: 'lynx', value: 'Atari Lynx (1989)' },
    { key: 'sufamiturbo', value: 'Bandai Sufami Turbo (1996)' },
    { key: 'ws', value: 'Bandai WonderSwan (1999)' },
    { key: 'wonderswan', value: 'Bandai WonderSwan Color (2000)' },
    { key: 'pocketchallengev2', value: 'Benesse Pocket Challenge V2 (2000)' },
    { key: 'loopy', value: 'Casio Loopy (1995)' },
    { key: 'pv1000', value: 'Casio PV-1000 (1983)' },
    { key: 'colecovision', value: 'ColecoVision (1982)' },
    { key: 'cd32', value: 'Commodore Amiga CD32 (1993)' },
    { key: 'arcadia2001', value: 'Emerson Arcadia 2001 (1982)' },
    { key: 'adventurevision', value: 'Entex Adventure Vision (1982)' },
    { key: 'cassettevision', value: 'Epoch Cassette Vision (1981)' },
    { key: 'scv', value: 'Epoch Super Cassette Vision (1984)' },
    { key: 'channelf', value: 'Fairchild Channel F (1976)' },
    { key: 'famicom', value: 'Famicom (1983)' },
    { key: 'fds', value: 'Famicom Disk System (1986)' },
    { key: 'superacan', value: "Funtech Super A'Can (1995)" },
    { key: 'dmg', value: 'Game Boy DMG-01 (1989)' },
    { key: 'gba', value: 'Game Boy Advance (2001)' },
    { key: 'gbc', value: 'Game Boy Color (1998)' },
    { key: 'gamecube', value: 'GameCube Indigo (2001)' },
    { key: 'gp32', value: 'GamePark GP32 (2001)' },
    { key: 'gamemaster', value: 'Hartung Game Master (1990)' },
    { key: 'vc4000', value: 'Interton VC 4000 (1978)' },
    { key: 'picno', value: 'Konami Picno (1992)' },
    { key: 'leapster', value: 'LeapFrog Leapster (2003)' },
    { key: 'odyssey2', value: 'Magnavox Odyssey 2 (1978)' },
    { key: 'intellivision', value: 'Mattel Intellivision (1979)' },
    { key: 'xbox', value: 'Microsoft Xbox (2001)' },
    { key: 'xbox360', value: 'Microsoft Xbox 360 (2005)' },
    { key: 'xbox360digital', value: 'Microsoft Xbox 360 Digital (2005)' },
    { key: 'supergrafx', value: 'NEC PC Engine SuperGrafx (1989)' },
    { key: 'pcfx', value: 'NEC PC-FX (1994)' },
    { key: 'tg16', value: 'NEC TurboGrafx-16 (1989)' },
    { key: 'tgcd', value: 'NEC TurboGrafx-CD (1988)' },
    { key: 'nes', value: 'NES (1985)' },
    { key: '3ds', value: 'Nintendo 3DS (2011)' },
    { key: '3dseshop', value: 'Nintendo 3DS eShop (2011)' },
    { key: 'n64', value: 'Nintendo 64 (1996)' },
    { key: '64dd', value: 'Nintendo 64DD (1999)' },
    { key: 'nds', value: 'Nintendo DS (2004)' },
    { key: 'dsiware', value: 'Nintendo DSi Shop (2008)' },
    { key: 'ereader', value: 'Nintendo e-Reader (2001)' },
    { key: 'gameandwatch', value: 'Nintendo Game & Watch (1980)' },
    { key: 'satellaview', value: 'Nintendo Satellaview BS-X (1995)' },
    { key: 'switch', value: 'Nintendo Switch (2017)' },
    { key: 'switcheshop', value: 'Nintendo Switch eShop (2017)' },
    { key: 'vb', value: 'Nintendo Virtual Boy (1995)' },
    { key: 'wii', value: 'Nintendo Wii (2006)' },
    { key: 'wiiu', value: 'Nintendo Wii U (2012)' },
    { key: 'wiiueshop', value: 'Nintendo Wii U eShop (2012)' },
    { key: 'wiiware', value: 'Nintendo WiiWare (2008)' },
    { key: '3do', value: 'Panasonic 3DO FZ-1 (1993)' },
    { key: 'cdi', value: 'Philips CD-i (1991)' },
    { key: 'videopacplus', value: 'Philips Videopac+ G7400 (1983)' },
    { key: 'pokemonmini', value: 'Pokémon Mini (2001)' },
    { key: 'studio2', value: 'RCA Studio II (1977)' },
    { key: '32x', value: 'Sega 32X (1994)' },
    { key: 'beena', value: 'Sega Advanced Pico Beena (2005)' },
    { key: 'segacd', value: 'Sega CD (1991)' },
    { key: 'dreamcast', value: 'Sega Dreamcast (1998)' },
    { key: 'gg', value: 'Sega Game Gear (1990)' },
    { key: 'genesis', value: 'Sega Genesis (1989)' },
    { key: 'sms', value: 'Sega Master System (1985)' },
    { key: 'megadrive', value: 'Sega Mega Drive (1988)' },
    { key: 'pico', value: 'Sega Pico (1993)' },
    { key: 'saturn', value: 'Sega Saturn (1994)' },
    { key: 'sg1000', value: 'Sega SG-1000 (1983)' },
    { key: 'snes', value: 'SNES North America (1991)' },
    { key: 'neogeo', value: 'SNK Neo Geo AES (1990)' },
    { key: 'neogeocd', value: 'SNK Neo Geo CD (1994)' },
    { key: 'ngp', value: 'SNK Neo Geo Pocket (1998)' },
    { key: 'ngpc', value: 'SNK Neo Geo Pocket Color (1999)' },
    { key: 'ps1', value: 'Sony PlayStation (1994)' },
    { key: 'ps2', value: 'Sony PlayStation 2 (2000)' },
    { key: 'ps3', value: 'Sony PlayStation 3 (2006)' },
    { key: 'ps3psn', value: 'Sony PlayStation 3 PSN (2006)' },
    { key: 'psp', value: 'Sony PlayStation Portable (2004)' },
    { key: 'psppsn', value: 'Sony PlayStation Portable PSN (2008)' },
    { key: 'psvita', value: 'Sony PlayStation Vita (2011)' },
    { key: 'superfamicom', value: 'Super Famicom (1990)' },
    { key: 'gamecom', value: 'Tiger Game.com (1997)' },
    { key: 'vectrex', value: 'Vectrex (1982)' },
    { key: 'creativision', value: 'VTech CreatiVision (1981)' },
    { key: 'vsmile', value: 'VTech V.Smile (2004)' },
    { key: 'supervision', value: 'Watara Supervision (1992)' },
    { key: 'zeebo', value: 'Zeebo (2009)' },
  ].sort((a, b) => a.value.localeCompare(b.value));

  const consoleThemes: EnhancedSelectInputValue<string>[] =
    consoleBaseThemes.flatMap((c) => [
      { key: `${c.key}-dark`, value: `${c.value} (Dark)` },
      { key: `${c.key}-light`, value: `${c.value} (Light)` },
    ]);

  const themeOptions = [
    ...['auto', 'light', 'dark'].map((theme) => ({
      key: theme,
      value: titleCase(theme),
    })),
    ...consoleThemes,
  ];

  const handleInputChange = useCallback(
    (change: InputChanged) => {
      // @ts-expect-error name needs to be keyof UiSettingsModel
      updateSetting(change.name, change.value);

      if (change.name === 'theme') {
        // Themes apply and persist immediately on selection (no Save required).
        // The override keeps the UI in sync while the save request is in flight.
        setPreviewTheme(change.value as ThemeName);
        saveThemeSetting({ theme: change.value as ThemeName });
      }

      if (change.name === 'consoleRegion') {
        saveThemeSetting({ consoleRegion: change.value as string });
      }
    },
    [updateSetting, saveThemeSetting]
  );

  const handleMatchConsoleThemeChange = useCallback(
    (change: InputChanged) => {
      setMatchConsoleTheme(Boolean(change.value));
    },
    [setMatchConsoleTheme]
  );

  const handleSavePress = useCallback(() => {
    saveSettings();
    setPreviewTheme(null);
  }, [saveSettings]);

  return (
    <SettingsPage
      title={translate('UiSettings')}
      hasPendingChanges={hasPendingChanges}
      isSaving={isSaving}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading scope={translate('Settings')} title={translate('Ui')} />

          {isFetching && isPopulated ? <LoadingIndicator /> : null}

          {!isFetching && error ? (
            <Alert kind={kinds.DANGER}>
              {translate('UiSettingsLoadError')}
            </Alert>
          ) : null}

          {hasSettings && isPopulated && !error ? (
            <Form
              id="uiSettings"
              validationErrors={validationErrors}
              validationWarnings={validationWarnings}
            >
              <FieldSet
                legend={translate('Dates')}
                caption={translate('DatesCaption')}
              >
                <FormRow>
                  <FormLabel>{translate('ShortDateFormat')}</FormLabel>

                  <FormInput
                    type={inputTypes.SELECT}
                    name="shortDateFormat"
                    values={shortDateFormatOptions}
                    onChange={handleInputChange}
                    {...settings.shortDateFormat}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>{translate('LongDateFormat')}</FormLabel>

                  <FormInput
                    type={inputTypes.SELECT}
                    name="longDateFormat"
                    values={longDateFormatOptions}
                    onChange={handleInputChange}
                    {...settings.longDateFormat}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>{translate('TimeFormat')}</FormLabel>

                  <FormInput
                    type={inputTypes.SELECT}
                    name="timeFormat"
                    values={timeFormatOptions}
                    onChange={handleInputChange}
                    {...settings.timeFormat}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>{translate('TimeZone')}</FormLabel>

                  <FormInput
                    type={inputTypes.SELECT}
                    name="timeZone"
                    values={timeZoneOptions}
                    onChange={handleInputChange}
                    {...settings.timeZone}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>{translate('ShowRelativeDates')}</FormLabel>
                  <FormInputHelpText
                    text={translate('ShowRelativeDatesHelpText')}
                  />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="showRelativeDates"
                    onChange={handleInputChange}
                    {...settings.showRelativeDates}
                  />
                </FormRow>
              </FieldSet>

              <FieldSet
                legend={translate('Style')}
                caption={translate('StyleCaption')}
              >
                <FormRow>
                  <FormLabel>{translate('Theme')}</FormLabel>
                  <FormInputHelpText text="Select your default global theme (applies and saves immediately)" />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="theme"
                    values={themeOptions}
                    onChange={handleInputChange}
                    {...settings.theme}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>Match Console Theme on Platform Pages</FormLabel>
                  <FormInputHelpText text="Automatically display a console's hardware theme when viewing its platform page (any page without a console theme uses your default theme)" />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="matchConsoleTheme"
                    value={matchConsoleTheme}
                    onChange={handleMatchConsoleThemeChange}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>Console Region Naming</FormLabel>
                  <FormInputHelpText text="Default console titles across the library and region views to this specific region (e.g., Genesis vs. Mega Drive, NES vs. Famicom, TG-16 vs. PC Engine). This does not impact game search priority or region selection." />
                  <FormInput
                    type={inputTypes.SELECT}
                    name="consoleRegion"
                    values={consoleRegionOptions}
                    onChange={handleInputChange}
                    {...settings.consoleRegion}
                    value={settings.consoleRegion?.value ?? 'US'}
                  />
                </FormRow>

                <FormRow>
                  <FormLabel>{translate('EnableColorImpairedMode')}</FormLabel>
                  <FormInputHelpText
                    text={translate('EnableColorImpairedModeHelpText')}
                  />
                  <FormInput
                    type={inputTypes.CHECK}
                    name="enableColorImpairedMode"
                    onChange={handleInputChange}
                    {...settings.enableColorImpairedMode}
                  />
                </FormRow>
              </FieldSet>

              <FieldSet
                legend={translate('Language')}
                caption={translate('LanguageCaption')}
              >
                <FormRow>
                  <FormLabel>{translate('UiLanguage')}</FormLabel>
                  <FormInputHelpText text={translate('UiLanguageHelpText')} />
                  <FormInputHelpText
                    text={translate('BrowserReloadRequired')}
                    isWarning={true}
                  />
                  <FormInput
                    type={inputTypes.LANGUAGE_SELECT}
                    name="uiLanguage"
                    includeOriginal={false}
                    includeUnknown={false}
                    onChange={handleInputChange}
                    {...settings.uiLanguage}
                    errors={
                      languages.some(
                        (language) => language.key === settings.uiLanguage.value
                      )
                        ? settings.uiLanguage.errors
                        : [
                            ...settings.uiLanguage.errors,
                            { message: translate('InvalidUILanguage') },
                          ]
                    }
                  />
                </FormRow>
              </FieldSet>
            </Form>
          ) : null}
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default UISettings;
