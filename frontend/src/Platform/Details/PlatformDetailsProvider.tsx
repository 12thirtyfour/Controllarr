import React, { PropsWithChildren } from 'react';
import QueueDetailsProvider from 'Activity/Queue/Details/QueueDetailsProvider';
import { RomFileContext } from 'RomFile/RomFileProvider';
import useRomFiles from 'RomFile/useRomFiles';

function PlatformDetailsProvider({
  seriesId,
  children,
}: PropsWithChildren<{ seriesId: number }>) {
  const { data } = useRomFiles({ seriesId });

  return (
    <QueueDetailsProvider seriesId={seriesId}>
      <RomFileContext.Provider value={data}>
        {children}
      </RomFileContext.Provider>
    </QueueDetailsProvider>
  );
}

export default PlatformDetailsProvider;
