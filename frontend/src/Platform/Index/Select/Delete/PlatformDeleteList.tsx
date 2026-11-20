import React from 'react';
import Series from 'Platform/Platform';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';

interface PlatformDeleteListStyles {
  pathContainer: string;
  path: string;
  statistics: string;
  deleteFilesMessage: string;
}

interface PlatformDeleteListProps {
  series: Series[];
  showFileDetails: boolean;
  totalRomFileCount: number;
  totalSizeOnDisk: number;
  styles: PlatformDeleteListStyles;
}

function PlatformDeleteList({
  series,
  showFileDetails,
  totalRomFileCount,
  totalSizeOnDisk,
  styles,
}: PlatformDeleteListProps) {
  return (
    <>
      <ul>
        {series.map(({ title, path, statistics = {} }) => {
          const { romFileCount = 0, sizeOnDisk = 0 } = statistics;

          return (
            <li key={title}>
              <span>{title}</span>

              {showFileDetails ? (
                <span>
                  <span className={styles.pathContainer}>
                    -<span className={styles.path}>{path}</span>
                  </span>

                  {romFileCount ? (
                    <span className={styles.statistics}>
                      (
                      {translate('DeletePlatformFolderGameCount', {
                        romFileCount,
                        size: formatBytes(sizeOnDisk),
                      })}
                      )
                    </span>
                  ) : null}
                </span>
              ) : null}
            </li>
          );
        })}
      </ul>

      {showFileDetails && totalRomFileCount ? (
        <div className={styles.deleteFilesMessage}>
          {translate('DeletePlatformFolderGameCount', {
            romFileCount: totalRomFileCount,
            size: formatBytes(totalSizeOnDisk),
          })}
        </div>
      ) : null}
    </>
  );
}

export default PlatformDeleteList;
