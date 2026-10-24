import { useEffect, useLayoutEffect, useState } from 'react';
import { ThemeName, useUiSettingsValues } from 'Settings/UI/useUiSettings';

const useTheme = (): Exclude<ThemeName, 'auto'> => {
  const { theme } = useUiSettingsValues();
  const selectedTheme = (theme ?? window.Controllarr.theme) as ThemeName;
  const [resolvedTheme, setResolvedTheme] = useState<
    Exclude<ThemeName, 'auto'>
  >(() => {
    if (selectedTheme === 'auto') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light';
    }

    return selectedTheme;
  });

  useEffect(() => {
    if (selectedTheme !== 'auto') {
      setResolvedTheme(selectedTheme);
      return;
    }

    const applySystemTheme = () => {
      setResolvedTheme(
        window.matchMedia('(prefers-color-scheme: dark)').matches
          ? 'dark'
          : 'light'
      );
    };

    applySystemTheme();

    window
      .matchMedia('(prefers-color-scheme: dark)')
      .addEventListener('change', applySystemTheme);

    return () => {
      window
        .matchMedia('(prefers-color-scheme: dark)')
        .removeEventListener('change', applySystemTheme);
    };
  }, [selectedTheme]);

  return resolvedTheme;
};

export default useTheme;

const readThemeVar = (color: string) =>
  getComputedStyle(document.documentElement)
    .getPropertyValue(`--${color}`)
    .trim();

export const useThemeColor = (color: string) => {
  const theme = useTheme();
  const [value, setValue] = useState(() => readThemeVar(color));

  useLayoutEffect(() => {
    setValue(readThemeVar(color));
  }, [color, theme]);

  return value;
};
