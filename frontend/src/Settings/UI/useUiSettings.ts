import { useCallback } from 'react';
import {
  useManageSettings,
  useSaveSettings,
  useSettings,
} from 'Settings/useSettings';

export type ConsoleThemeBase =
  | '32x'
  | '3do'
  | '3ds'
  | '3dseshop'
  | '64dd'
  | 'adventurevision'
  | 'arcadia2001'
  | 'arduboy'
  | 'atari2600'
  | 'atari5200'
  | 'atari7800'
  | 'beena'
  | 'cassettevision'
  | 'cd32'
  | 'cdi'
  | 'channelf'
  | 'colecovision'
  | 'creativision'
  | 'dmg'
  | 'dreamcast'
  | 'dsiware'
  | 'ereader'
  | 'famicom'
  | 'fds'
  | 'gameandwatch'
  | 'gamecom'
  | 'gamecube'
  | 'gamemaster'
  | 'gba'
  | 'gbc'
  | 'genesis'
  | 'gg'
  | 'gp32'
  | 'gx4000'
  | 'intellivision'
  | 'jaguar'
  | 'jaguarcd'
  | 'leapster'
  | 'loopy'
  | 'lynx'
  | 'megadrive'
  | 'n64'
  | 'nds'
  | 'neogeo'
  | 'neogeocd'
  | 'nes'
  | 'ngp'
  | 'ngpc'
  | 'odyssey2'
  | 'pcfx'
  | 'picno'
  | 'pico'
  | 'pocketchallengev2'
  | 'pokemonmini'
  | 'ps1'
  | 'ps2'
  | 'ps3'
  | 'ps3psn'
  | 'psp'
  | 'psppsn'
  | 'psvita'
  | 'pv1000'
  | 'satellaview'
  | 'saturn'
  | 'scv'
  | 'segacd'
  | 'sg1000'
  | 'sms'
  | 'snes'
  | 'studio2'
  | 'sufamiturbo'
  | 'superacan'
  | 'superfamicom'
  | 'supergrafx'
  | 'supervision'
  | 'switch'
  | 'switcheshop'
  | 'tg16'
  | 'tgcd'
  | 'vb'
  | 'vc4000'
  | 'vectrex'
  | 'videopacplus'
  | 'vsmile'
  | 'wii'
  | 'wiiu'
  | 'wiiueshop'
  | 'wiiware'
  | 'wonderswan'
  | 'ws'
  | 'xbox'
  | 'xbox360'
  | 'xbox360digital'
  | 'zeebo';

export type ThemeName =
  | 'auto'
  | 'dark'
  | 'light'
  | ConsoleThemeBase
  | `${ConsoleThemeBase}-dark`
  | `${ConsoleThemeBase}-light`;

export interface UiSettingsModel {
  theme: ThemeName;
  showRelativeDates: boolean;
  shortDateFormat: string;
  longDateFormat: string;
  timeFormat: string;
  timeZone: string;
  firstDayOfWeek: number;
  enableColorImpairedMode: boolean;
  calendarWeekColumnHeader: string;
  uiLanguage: number;
  consoleRegion: string;
}

const PATH = '/settings/ui';

export const useUiSettingsValues = () => {
  const { data } = useSettings<UiSettingsModel>(PATH);

  return data;
};

export const useUiSettings = () => {
  return useSettings<UiSettingsModel>(PATH);
};

export const useManageUiSettings = () => {
  return useManageSettings<UiSettingsModel>(PATH);
};

export const useSaveUiSettings = () => {
  const { data } = useSettings<UiSettingsModel>(PATH);
  const { save } = useSaveSettings<UiSettingsModel>(PATH);

  const saveSettings = useCallback(
    (changes: Partial<UiSettingsModel>) => {
      const updatedSettings = { ...data, ...changes };

      save(updatedSettings);
    },
    [data, save]
  );

  return saveSettings;
};
