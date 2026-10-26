import React from 'react';
import { SeriesType } from 'Platform/Platform';
import GameNumber, { GameNumberProps } from './GameNumber';

interface SeasonGameNumberProps extends GameNumberProps {
  airDate?: string;
  seriesType?: SeriesType;
}

function SeasonGameNumber(props: SeasonGameNumberProps) {
  const { airDate, seriesType, ...otherProps } = props;

  if (seriesType === 'daily' && airDate) {
    return <span>{airDate}</span>;
  }

  return (
    <GameNumber
      seriesType={seriesType}
      showSeasonNumber={true}
      {...otherProps}
    />
  );
}

export default SeasonGameNumber;
