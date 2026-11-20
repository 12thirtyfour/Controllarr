import React from 'react';
import GameLanguages from 'Game/GameLanguages';
import { useRomFile } from './RomFileProvider';

interface RomFileLanguagesProps {
  romFileId: number | undefined;
}

function RomFileLanguages({ romFileId }: RomFileLanguagesProps) {
  const romFile = useRomFile(romFileId);

  return <GameLanguages languages={romFile?.languages ?? []} />;
}

export default RomFileLanguages;
