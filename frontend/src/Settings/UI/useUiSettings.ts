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
  | 'nes'
  | 'snes'
  | 'dmg'
  | 'genesis'
  | 'gamecube';

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
