import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import { SortDirection } from 'Helpers/Props/sortDirections';

interface GameSelectOptions {
  sortKey: string;
  sortDirection: SortDirection;
}

const { useOptions, useOption, setOptions, setOption, setSort } =
  createOptionsStore<GameSelectOptions>('game_selection_options', () => {
    return {
      sortKey: 'gameNumber',
      sortDirection: 'ascending',
    };
  });

export const useGameSelectionOptions = useOptions;
export const setGameSelectionOptions = setOptions;
export const useGameSelectionOption = useOption;
export const setGameSelectionOption = setOption;
export const setGameSelectionSort = setSort;
