import React, { useMemo } from 'react';
import { SeasonType } from 'Platform/Platform';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import EnhancedSelectInput, {
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';

export interface RegionTypeSelectInputProps {
  name: string;
  value: string;
  seasonTypes: SeasonType[];
  modalTitle?: string;
  isDisabled?: boolean;
  includeNoChange: boolean;
  includeNoChangeDisabled?: boolean;
  includeMixed: boolean;
  onChange: (payload: InputChanged<string>) => void;
}

function getSeasonTypeHint(seasonCount: number, gameCount: number) {
  if (!gameCount) {
    return seasonCount === 1
      ? translate('OneSeason')
      : translate('CountSeasons', { count: seasonCount });
  }

  if (seasonCount === 1) {
    return gameCount === 1
      ? translate('OneSeasonOneGame')
      : translate('OneSeasonCountGames', { gameCount });
  }

  return gameCount === 1
    ? translate('CountSeasonsOneGame', { seasonCount })
    : translate('CountSeasonsCountGames', { seasonCount, gameCount });
}

export default function RegionTypeSelectInput(
  props: RegionTypeSelectInputProps
) {
  const {
    value,
    seasonTypes,
    includeNoChange,
    includeNoChangeDisabled,
    includeMixed,
    onChange,
  } = props;

  const values = useMemo(() => {
    const result: EnhancedSelectInputValue<string>[] = seasonTypes.map(
      (seasonType) => {
        const seasonCount = seasonType.seasonNumbers.filter(
          (seasonNumber) => seasonNumber > 0
        ).length;
        const { gameCount } = seasonType;

        return {
          key: seasonType.type,
          value: seasonType.name,
          hint: getSeasonTypeHint(seasonCount, gameCount),
        };
      }
    );

    if (includeNoChange) {
      result.unshift({
        key: 'noChange',
        value: translate('NoChange'),
        isDisabled: includeNoChangeDisabled,
      });
    }

    if (includeMixed) {
      result.unshift({
        key: 'mixed',
        value: `(${translate('Mixed')})`,
        isDisabled: true,
      });
    }

    return result;
  }, [seasonTypes, includeNoChange, includeNoChangeDisabled, includeMixed]);

  return (
    <EnhancedSelectInput
      {...props}
      value={value}
      values={values}
      onChange={onChange}
    />
  );
}
