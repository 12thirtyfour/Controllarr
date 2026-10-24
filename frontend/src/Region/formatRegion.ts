import formatSeason, { getRegionalConsoleName } from 'Season/formatSeason';

export { getRegionalConsoleName };

export default function formatRegion(
  regionNumber: number,
  shortFormat?: boolean,
  platformSlugOrTitle?: string
) {
  return formatSeason(regionNumber, shortFormat, platformSlugOrTitle);
}
