import moment from 'moment-timezone';
import React, { useCallback } from 'react';
import { useQueueDetails } from 'Activity/Queue/Details/QueueDetailsProvider';
import { useCommands } from 'Commands/useCommands';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import { icons } from 'Helpers/Props';
import { isCommandExecuting } from 'Utilities/Command';
import isBefore from 'Utilities/Date/isBefore';
import translate from 'Utilities/String/translate';
import useCalendar, {
  useCalendarRange,
  useCalendarSearchMissingCommandId,
} from './useCalendar';

function useIsSearching(searchMissingCommandId: number | undefined) {
  const { data: commands } = useCommands();

  if (searchMissingCommandId == null) {
    return false;
  }

  return isCommandExecuting(
    commands.find((command) => {
      return command.id === searchMissingCommandId;
    })
  );
}

const useMissingGameIdsSelector = () => {
  const { start, end } = useCalendarRange();
  const { data } = useCalendar();
  const queueDetails = useQueueDetails();

  return data.reduce<number[]>((acc, game) => {
    const airDateUtc = game.airDateUtc;

    if (
      !game.romFileId &&
      moment(airDateUtc).isAfter(start) &&
      moment(airDateUtc).isBefore(end) &&
      isBefore(game.airDateUtc) &&
      !queueDetails.some((details) => details.gameIds?.includes(game.id))
    ) {
      acc.push(game.id);
    }

    return acc;
  }, []);
};

export default function CalendarMissingGameSearchButton() {
  const searchMissingCommandId = useCalendarSearchMissingCommandId();
  const missingGameIds = useMissingGameIdsSelector();
  const isSearchingForMissing = useIsSearching(searchMissingCommandId);

  const handlePress = useCallback(() => {}, []);

  return (
    <PageToolbarButton
      label={translate('SearchForMissing')}
      iconName={icons.SEARCH}
      isDisabled={!missingGameIds.length}
      isSpinning={isSearchingForMissing}
      onPress={handlePress}
    />
  );
}
