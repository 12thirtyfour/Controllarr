import React, { useMemo } from 'react';
import * as platformTypes from 'Utilities/Platform/platformTypes';
import translate from 'Utilities/String/translate';
import EnhancedSelectInput, {
  EnhancedSelectInputProps,
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';
import PlatformTypeSelectInputOption from './PlatformTypeSelectInputOption';
import PlatformTypeSelectInputSelectedValue from './PlatformTypeSelectInputSelectedValue';

export interface PlatformTypeSelectInputProps
  extends Omit<
    EnhancedSelectInputProps<EnhancedSelectInputValue<string>, string>,
    'values'
  > {
  includeNoChange?: boolean;
  includeNoChangeDisabled?: boolean;
  includeMixed?: boolean;
}

export interface ISeriesTypeOption {
  key: string;
  value: string;
  format?: string;
  isDisabled?: boolean;
}

const seriesTypeOptions: ISeriesTypeOption[] = [
  {
    key: platformTypes.STANDARD,
    value: 'Standard (Regional)',
    get format() {
      return translate('StandardGameTypeFormat', { format: 'USA, Europe, Japan' });
    },
  },
  {
    key: platformTypes.ANIME,
    value: 'Single Collection (Flat)',
    get format() {
      return translate('AnimeGameTypeFormat', { format: 'Direct ROM Catalog' });
    },
  },
];

function PlatformTypeSelectInput(props: PlatformTypeSelectInputProps) {
  const {
    includeNoChange = false,
    includeNoChangeDisabled = true,
    includeMixed = false,
  } = props;

  const values = useMemo(() => {
    const result = [...seriesTypeOptions];

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
  }, [includeNoChange, includeNoChangeDisabled, includeMixed]);

  return (
    <EnhancedSelectInput
      {...props}
      values={values}
      optionComponent={PlatformTypeSelectInputOption}
      selectedValueComponent={PlatformTypeSelectInputSelectedValue}
    />
  );
}

export default PlatformTypeSelectInput;
