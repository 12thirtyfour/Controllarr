import { useQueryClient } from '@tanstack/react-query';
import ModelBase from 'App/ModelBase';
import useApiMutation, {
  addOrUpdateQueryClientItem,
} from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { UnmappedFolder } from 'RootFolder/useRootFolders';

export interface ImportFolder extends ModelBase {
  id: number;
  path: string;
  accessible: boolean;
  isEmpty: boolean;
  freeSpace?: number;
  totalSpace?: number;
  containsRomFiles?: boolean;
  unmappedFolders: UnmappedFolder[];
}

interface AddImportFolder {
  path: string;
}

const DEFAULT_IMPORT_FOLDERS: ImportFolder[] = [];

export const useImportFolders = () => {
  const result = useApiQuery<ImportFolder[]>({
    path: '/importfolder',
    queryOptions: {
      refetchOnMount: 'always',
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_IMPORT_FOLDERS,
  };
};

export const useImportFolder = (id: number, timeout: boolean) => {
  const result = useApiQuery<ImportFolder>({
    path: `/importfolder/${id}`,
    queryParams: { timeout },
    queryOptions: {
      refetchOnWindowFocus: false,
      refetchOnMount: 'always',
    },
  });

  return {
    ...result,
    data: result.data,
  };
};

export const useDeleteImportFolder = (id: number) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<unknown, void>({
    path: `/importfolder/${id}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ['/importfolder'] });
      },
    },
  });

  return {
    deleteImportFolder: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};

export const useAddImportFolder = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error, data } = useApiMutation<
    ImportFolder,
    AddImportFolder
  >({
    path: '/importfolder',
    method: 'POST',
    mutationOptions: {
      onSuccess: (newImportFolder) => {
        queryClient.setQueryData<ImportFolder[]>(
          ['/importfolder'],
          (old = []) => addOrUpdateQueryClientItem(old, newImportFolder, 'id')
        );
      },
    },
  });

  return {
    addImportFolder: mutate,
    isAdding: isPending,
    addError: error,
    newImportFolder: data,
  };
};

export default useImportFolders;
