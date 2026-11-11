import React from 'react';
import { SeriesType } from 'Platform/Platform';
import GameNumber, { GameNumberProps } from './GameNumber';

interface RegionGameNumberProps extends GameNumberProps {
  airDate?: string;
  seriesType?: SeriesType;
}

function RegionGameNumber(props: RegionGameNumberProps) {
  const { airDate, seriesType, ...otherProps } = props;

  if (seriesType === 'daily' && airDate) {
    return <span>{airDate}</span>;
  }

  return (
    <GameNumber
      seriesType={seriesType}
      {...otherProps}
    />
  );
}

export default RegionGameNumber;
