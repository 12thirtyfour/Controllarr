import React from 'react';
import HintedSelectInputSelectedValue from './HintedSelectInputSelectedValue';
import { ISeriesTypeOption } from './PlatformTypeSelectInput';

interface PlatformTypeSelectInputOptionProps {
  selectedValue: string;
  values: ISeriesTypeOption[];
  format: string;
}

function PlatformTypeSelectInputSelectedValue(
  props: PlatformTypeSelectInputOptionProps
) {
  const { selectedValue, values, ...otherProps } = props;
  const format = values.find((v) => v.key === selectedValue)?.format;

  return (
    <HintedSelectInputSelectedValue
      {...otherProps}
      selectedValue={selectedValue}
      values={values}
      hint={format}
    />
  );
}

export default PlatformTypeSelectInputSelectedValue;
