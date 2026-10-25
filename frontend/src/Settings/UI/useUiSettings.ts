import { useCallback } from 'react';
import {
  useManageSettings,
  useSaveSettings,
  useSettings,
} from 'Settings/useSettings';

export type ThemeName =
  | 'auto'
  | 'dark'
  | 'light'
  | '32x'
  | '3ds'
  | '64dd'
  | 'atari2600'
  | 'atari7800'
  | 'dmg'
  | 'dreamcast'
  | 'famicom'
  | 'fds'
  | 'gamecube'
  | 'gba'
  | 'gbc'
  | 'genesis'
  | 'gg'
  | 'jaguar'
  | 'jaguarcd'
  | 'lynx'
  | 'megadrive'
  | 'n64'
  | 'nds'
  | 'neogeo'
  | 'nes'
  | 'ngpc'
  | 'ps1'
  | 'ps2'
  | 'ps3'
  | 'psp'
  | 'psvita'
  | 'satellaview'
  | 'saturn'
  | 'segacd'
  | 'sms'
  | 'snes'
  | 'superfamicom'
  | 'switch'
  | 'tg16'
  | 'tgcd'
  | 'vb'
  | 'wii'
  | 'wiiu'
  | 'wonderswan'
  | 'xbox'
  | 'xbox360';

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
