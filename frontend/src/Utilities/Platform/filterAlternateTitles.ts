import { AlternateTitle } from 'Platform/Platform';

function filterAlternateTitles(
  alternateTitles: AlternateTitle[],
  platformTitle: string | null,
  useSceneNumbering: boolean,
  seasonNumber?: number,
  sceneSeasonNumber?: number
) {
  const globalTitles: AlternateTitle[] = [];
  const seasonTitles: AlternateTitle[] = [];

  if (alternateTitles) {
    alternateTitles.forEach((alternateTitle) => {
      if (
        alternateTitle.sceneOrigin === 'unknown' ||
        alternateTitle.sceneOrigin === 'unknown:tvdb'
      ) {
        return;
      }

      if (alternateTitle.sceneOrigin === 'mixed') {
        return;
      }

      const hasAltSeasonNumber =
        alternateTitle.seasonNumber !== -1 &&
        alternateTitle.seasonNumber !== undefined;
      const hasAltSceneSeasonNumber =
        alternateTitle.sceneSeasonNumber !== -1 &&
        alternateTitle.sceneSeasonNumber !== undefined;

      if (
        !hasAltSeasonNumber &&
        !hasAltSceneSeasonNumber &&
        alternateTitle.title !== platformTitle &&
        (!alternateTitle.sceneOrigin || !useSceneNumbering)
      ) {
        globalTitles.push(alternateTitle);
        return;
      }

      if (
        !hasAltSeasonNumber &&
        !hasAltSceneSeasonNumber &&
        alternateTitle.sceneOrigin &&
        useSceneNumbering
      ) {
        seasonTitles.push(alternateTitle);
        return;
      }

      const mappedAltSeasonNumber = hasAltSeasonNumber
        ? alternateTitle.seasonNumber
        : alternateTitle.sceneSeasonNumber;
      const mappedSeasonNumber =
        alternateTitle.sceneOrigin !== 'tvdb' && sceneSeasonNumber !== undefined
          ? sceneSeasonNumber
          : seasonNumber;

      if (
        mappedSeasonNumber !== undefined &&
        mappedSeasonNumber === mappedAltSeasonNumber
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
