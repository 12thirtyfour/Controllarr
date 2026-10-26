import React from 'react';
import Label from 'Components/Label';
import GameQuality from 'Game/GameQuality';
import translate from 'Utilities/String/translate';
import { useRomFile } from './RomFileProvider';

interface RomFileQualityProps {
  romFileId: number | undefined;
}

function RomFileQuality({ romFileId }: RomFileQualityProps) {
  const romFile = useRomFile(romFileId);

  if (!romFile?.quality) {
    return <Label>{translate('Unknown')}</Label>;
  }

  return <GameQuality quality={romFile.quality} />;
}

export default RomFileQuality;
