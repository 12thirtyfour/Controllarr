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
