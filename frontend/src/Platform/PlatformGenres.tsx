import React from 'react';
import Label from 'Components/Label';
import Tooltip from 'Components/Tooltip/Tooltip';
import { kinds, sizes, tooltipPositions } from 'Helpers/Props';

interface PlatformGenresProps {
  className?: string;
  genres: string[];
}

function PlatformGenres({ className, genres }: PlatformGenresProps) {
  const [firstGenre, ...otherGenres] = genres;

  if (otherGenres.length) {
    return (
      <Tooltip
        anchor={<span className={className}>{firstGenre}</span>}
        tooltip={
          <div>
            {otherGenres.map((tag) => {
              return (
                <Label key={tag} kind={kinds.INFO} size={sizes.LARGE}>
                  {tag}
                </Label>
              );
            })}
          </div>
        }
        kind={kinds.DEFAULT}
        position={tooltipPositions.TOP}
      />
    );
  }

  return <span className={className}>{firstGenre}</span>;
}

export default PlatformGenres;
