import type { ImportPlatformItem } from './importPlatformStore';

type ImportPlatformEligibilityItem = Pick<
  ImportPlatformItem,
  'id' | 'selectedSeries'
>;

export type ImportPlatformEligibility =
  | 'ready'
  | 'unmatched'
  | 'existing'
  | 'duplicate';

export interface ImportPlatformEligibilityResult {
  eligibilityById: Map<string, ImportPlatformEligibility>;
  duplicateIdsByTvdbId: Map<number, string[]>;
}

export interface ImportPlatformSelectionResult {
  importableIds: string[];
  duplicateIdsToDeselect: Set<string>;
}

export function getImportPlatformSelection(
  { eligibilityById }: ImportPlatformEligibilityResult,
  selectedIds: string[]
): ImportPlatformSelectionResult {
  const duplicateIdsToDeselect = new Set<string>();

  const importableIds = selectedIds.filter((id) => {
    const status = eligibilityById.get(id);

    return (
      status === 'ready' ||
      status === 'existing' ||
      status === 'duplicate'
    );
  });

  return { importableIds, duplicateIdsToDeselect };
}

export function getImportPlatformEligibility(
  items: ImportPlatformEligibilityItem[],
  existingTvdbIds: Set<number>
): ImportPlatformEligibilityResult {
  const eligibilityById = items.reduce<Map<string, ImportPlatformEligibility>>(
    (eligibility, item) => {
      const tvdbId = item.selectedSeries?.tvdbId;
      let status: ImportPlatformEligibility = 'unmatched';

      if (tvdbId != null && existingTvdbIds.has(tvdbId)) {
        status = 'existing';
      } else if (tvdbId != null) {
        status = 'ready';
      }

      eligibility.set(item.id, status);

      return eligibility;
    },
    new Map()
  );

  const duplicateIdsByTvdbId = new Map<number, string[]>();

  return {
    eligibilityById,
    duplicateIdsByTvdbId,
  };
}
