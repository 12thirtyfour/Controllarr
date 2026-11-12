import { useQueryClient } from '@tanstack/react-query';
import AddPlatform from 'AddPlatform/AddPlatform';
import { AddPlatformOptions } from 'AddPlatform/addPlatformOptionsStore';
import useApiMutation, {
  addOrUpdateQueryClientItem,
} from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import Language from 'Language/Language';
import Series from 'Platform/Platform';

interface AddPlatformPayload
  extends AddPlatform,
    Omit<
      AddPlatformOptions,
      | 'monitor'
      | 'searchForMissingGames'
      | 'searchForCutoffUnmetGames'
      | 'collectionStrategy'
      | 'revisionPreference'
      | 'encryptionPreference'
      | 'preferEncryptedRoms'
      | 'pinnedVersion'
      | 'enableCompression'
      | 'trackUnlicensed'
      | 'trackHomebrew'
      | 'trackRomHacks'
    > {
  language: Language;
}

const DEFAULT_SERIES: AddPlatform[] = [];

export const useLookupSeries = (
  query: string,
  language: Language,
  isEnabled = true
) => {
  const result = useApiQuery<AddPlatform[]>({
    path: '/series/lookup',
    queryParams: {
      term: query,
      language: language.id,
    },
    queryOptions: {
      enabled: isEnabled && !!query,
      // Disable refetch on window focus to prevent refetching when the user switch tabs
      refetchOnWindowFocus: false,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_SERIES,
  };
};

export const useAddPlatform = (onSuccess?: () => void) => {
  const queryClient = useQueryClient();

  const { isPending, error, mutate } = useApiMutation<Series, AddPlatformPayload>(
    {
      path: '/series',
      method: 'POST',
      mutationOptions: {
        onSuccess: (newSeries) => {
          queryClient.setQueryData<Series[]>(['/series'], (oldSeries = []) =>
            addOrUpdateQueryClientItem(oldSeries, newSeries, 'id')
          );
          queryClient.invalidateQueries({ queryKey: ['/series'] });
          queryClient.invalidateQueries({ queryKey: ['/episode'] });
          onSuccess?.();
        },
      },
    }
  );

  return {
    isAdding: isPending,
    addError: error,
    addPlatform: mutate,
  };
};

export interface AddAllPlatformsPayload {
  rootFolderPath: string;
  qualityProfileId: number;
  language: Language;
  monitor: string;
  collectionStrategy: string;
  revisionPreference: string;
  pinnedVersion: string;
  discCompression: boolean;
  cartridgeCompression: boolean;
  ndsEncryptionPreference: string;
  threeDsEncryptionPreference: string;
  trackUnlicensed: boolean;
  trackRomHacks: boolean;
  trackHomebrew: boolean;
  searchForMissingGames: boolean;
  searchForCutoffUnmetGames: boolean;
  seriesType: string;
  tags: number[];
}

export const useAddAllPlatforms = (onSuccess?: () => void) => {
  const queryClient = useQueryClient();

  const { isPending, error, mutate } = useApiMutation<
    Series[],
    AddAllPlatformsPayload
  >({
    path: '/series/addall',
    method: 'POST',
    mutationOptions: {
      onSuccess: (addedSeries) => {
        queryClient.setQueryData<Series[]>(['/series'], (oldSeries = []) => {
          let next = oldSeries;
          for (const item of addedSeries ?? []) {
            next = addOrUpdateQueryClientItem(next, item, 'id');
          }
          return next;
        });
        queryClient.invalidateQueries({ queryKey: ['/series'] });
        queryClient.invalidateQueries({ queryKey: ['/episode'] });
        onSuccess?.();
      },
    },
  });

  return {
    isAddingAll: isPending,
    addAllError: error,
    addAllPlatforms: mutate,
  };
};
