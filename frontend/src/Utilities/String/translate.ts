let translations: Record<string, string> = {};

export function setTranslations(translationData: Record<string, string>) {
  translations = translationData;
}

export function translate(
  key: string,
  tokens: Record<string, string | number | boolean> = {}
) {
  const { isProduction = true } = window.Controllarr;

  if (!isProduction && !(key in translations)) {
    console.warn(`Missing translation for key: ${key}`);
  }

  const translation = translations[key] || key;

  tokens.appName = 'Controllarr';
  tokens.romFileCount ??= tokens.episodeFileCount;
  tokens.episodeFileCount ??= tokens.romFileCount;
  tokens.gameCount ??= tokens.episodeCount;
  tokens.episodeCount ??= tokens.gameCount;
  tokens.totalGameCount ??= tokens.totalEpisodeCount;
  tokens.totalEpisodeCount ??= tokens.totalGameCount;
  tokens.regionCount ??= tokens.seasonCount;
  tokens.seasonCount ??= tokens.regionCount;

  return translation.replace(/\{([a-z0-9]+?)\}/gi, (match, tokenMatch) =>
    String(tokens[tokenMatch] ?? match)
  );
}

export default translate;
