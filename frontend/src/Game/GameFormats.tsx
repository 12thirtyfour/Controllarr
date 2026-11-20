import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';

interface GameFormatsProps {
  formats: CustomFormat[];
}

function GameFormats({ formats }: GameFormatsProps) {
  return (
    <div>
      {formats.map(({ id, name }) => (
        <Label key={id} kind={kinds.INFO}>
          {name}
        </Label>
      ))}
    </div>
  );
}

export default GameFormats;
