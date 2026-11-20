import { AlternateTitle } from 'Platform/Platform';

function filterAlternateTitles(
  alternateTitles: AlternateTitle[],
  platformTitle: string | null,
  seasonNumber?: number
) {
  const globalTitles: AlternateTitle[] = [];
  const seasonTitles: AlternateTitle[] = [];

  if (alternateTitles) {
    alternateTitles.forEach((alternateTitle) => {
      const hasAltSeasonNumber =
        alternateTitle.seasonNumber !== -1 &&
        alternateTitle.seasonNumber !== undefined;

      if (!hasAltSeasonNumber && alternateTitle.title !== platformTitle) {
        globalTitles.push(alternateTitle);
        return;
      }

      if (
        seasonNumber !== undefined &&
        seasonNumber === alternateTitle.seasonNumber
      ) {
        seasonTitles.push(alternateTitle);
        return;
      }
    });
  }

  if (seasonNumber === undefined) {
    return globalTitles;
  }

  return seasonTitles;
}

export default filterAlternateTitles;
