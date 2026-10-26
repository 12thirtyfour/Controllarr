import { SelectedFilterKey } from 'Components/Filter/Filter';
import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import dimensions from 'Styles/Variables/dimensions';
import { CalendarView } from './calendarViews';

export interface CalendarOptions {
  collapseMultipleGames: boolean;
  showCoverArt: boolean;
  showGameInformation: boolean;
  showFinaleIcon: boolean;
  showSpecialIcon: boolean;
  showCutoffUnmetIcon: boolean;
  fullColorEvents: boolean;
  selectedFilterKey: SelectedFilterKey;
  view: CalendarView;
}

const { useOptions, useOption, getOptions, getOption, setOptions, setOption } =
  createOptionsStore<CalendarOptions>('calendar_options', () => {
    return {
      collapseMultipleGames: false,
      showCoverArt: true,
      showGameInformation: true,
      showFinaleIcon: false,
      showSpecialIcon: false,
      showCutoffUnmetIcon: false,
      fullColorEvents: false,
      selectedFilterKey: 'monitored',
      view: window.innerWidth > dimensions.breakpointSmall ? 'week' : 'day',
    };
  });

export const useCalendarOptions = useOptions;
export const getCalendarOptions = getOptions;
export const setCalendarOptions = setOptions;
export const useCalendarOption = useOption;
export const getCalendarOption = getOption;
export const setCalendarOption = setOption;
