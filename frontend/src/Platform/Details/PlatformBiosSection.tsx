import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useMemo, useState } from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import SpinnerButton from 'Components/Link/SpinnerButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { icons, kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import {
  BiosFile,
  BiosVerificationStatus,
  getMd5Hash,
  getSha1Hash,
  getVerificationStatus,
} from 'Settings/Bios/BiosSettings';
import translate from 'Utilities/String/translate';
import styles from './PlatformBiosSection.module.css';

interface PlatformBiosSectionProps {
  platformSlug: string;
}

const COLUMNS: Column[] = [
  {
    name: 'fileName',
    label: () => translate('BiosFileName'),
    isVisible: true,
  },
  {
    name: 'isRequired',
    label: () => translate('Requirement'),
    isVisible: true,
  },
  {
    name: 'status',
    label: () => translate('VerificationStatus'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: 'Actions',
    isVisible: true,
  },
  {
    name: 'expectedHashes',
    label: () => translate('ExpectedHashes'),
    isVisible: true,
  },
];

interface BiosDownloadRowButtonProps {
  fileName: string;
  isDownloading: boolean;
  isSpinning: boolean;
  onDownload: (fileName: string) => void;
}

function BiosDownloadRowButton({
  fileName,
  isDownloading,
  isSpinning,
  onDownload,
}: BiosDownloadRowButtonProps) {
  const handlePress = useCallback(() => {
    onDownload(fileName);
  }, [fileName, onDownload]);

  return (
    <SpinnerButton
      size={sizes.SMALL}
      kind={kinds.DEFAULT}
      isSpinning={isSpinning}
      isDisabled={isDownloading}
      onPress={handlePress}
    >
      Download
    </SpinnerButton>
  );
}

function PlatformBiosSection({ platformSlug }: PlatformBiosSectionProps) {
  const queryClient = useQueryClient();
  const [isExpanded, setIsExpanded] = useState(false);
  const [downloadingFile, setDownloadingFile] = useState<string | null>(null);

  const queryParams = useMemo(() => {
    return {
      platformSlug,
    };
  }, [platformSlug]);

  const { data } = useApiQuery<BiosFile[]>({
    path: '/bios',
    queryParams,
  });

  const { mutate: downloadBios, isPending: isDownloading } = useApiMutation<
    BiosFile[],
    { platformSlug?: string; fileName?: string }
  >({
    path: '/bios/download',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        setDownloadingFile(null);
        queryClient.invalidateQueries({ queryKey: ['/bios'] });
        queryClient.invalidateQueries({ queryKey: ['/history'] });
        queryClient.invalidateQueries({ queryKey: ['/queue'] });
      },
      onError: () => {
        setDownloadingFile(null);
      },
    },
  });

  const handleToggleExpand = useCallback(() => {
    setIsExpanded((prev) => !prev);
  }, []);

  const handleDownloadAll = useCallback(() => {
    setDownloadingFile('__all__');
    downloadBios({ platformSlug });
  }, [downloadBios, platformSlug]);

  const handleDownloadSingle = useCallback(
    (fileName: string) => {
      setDownloadingFile(fileName);
      downloadBios({ platformSlug, fileName });
    },
    [downloadBios, platformSlug]
  );

  if (!data || data.length === 0) {
    return null;
  }

  const verifiedCount = data.filter(
    (item) => getVerificationStatus(item) === BiosVerificationStatus.Present
  ).length;
  const missingCount = data.length - verifiedCount;

  return (
    <div className={styles.biosSection}>
      <div className={styles.header}>
        <div className={styles.left}>
          <span className={styles.title}>System BIOS &amp; Firmware</span>

          <Label
            kind={missingCount === 0 ? kinds.SUCCESS : kinds.WARNING}
            size={sizes.MEDIUM}
          >
            {verifiedCount} / {data.length} Verified
          </Label>
        </div>

        <Link className={styles.expandButton} onPress={handleToggleExpand}>
          <Icon
            name={isExpanded ? icons.COLLAPSE : icons.EXPAND}
            title={
              isExpanded ? translate('HideGames') : translate('ShowGames')
            }
            size={24}
          />
        </Link>

        <div className={styles.actions}>
          {missingCount > 0 ? (
            <SpinnerButton
              size={sizes.SMALL}
              kind={kinds.PRIMARY}
              isSpinning={isDownloading && downloadingFile === '__all__'}
              isDisabled={isDownloading}
              onPress={handleDownloadAll}
            >
              Download Missing ({missingCount})
            </SpinnerButton>
          ) : null}
        </div>
      </div>

      {isExpanded ? (
        <div className={styles.body}>
          <Table columns={COLUMNS}>
            <TableBody>
              {data.map((item, idx) => {
                const md5 = getMd5Hash(item);
                const sha1 = getSha1Hash(item);
                const status = getVerificationStatus(item);
                let statusKind: Kind = kinds.WARNING;
                let statusText = translate('Missing');

                if (status === BiosVerificationStatus.Present) {
                  statusKind = kinds.SUCCESS;
                  statusText = translate('Present');
                } else if (status === BiosVerificationStatus.InvalidHash) {
                  statusKind = kinds.DANGER;
                  statusText = translate('InvalidHash');
                }

                return (
                  <TableRow key={`${item.fileName}-${idx}`}>
                    <TableRowCell>
                      <div className={styles.fileInfo}>
                        <span className={styles.fileName}>{item.fileName}</span>
                        {item.description ? (
                          <span className={styles.fileDescription}>
                            {item.description}
                          </span>
                        ) : null}
                      </div>
                    </TableRowCell>

                    <TableRowCell>
                      {item.isRequired ? (
                        <Label kind={kinds.PRIMARY}>
                          {translate('Required')}
                        </Label>
                      ) : (
                        <Label kind={kinds.DEFAULT}>
                          {translate('Optional')}
                        </Label>
                      )}
                    </TableRowCell>

                    <TableRowCell>
                      <Label kind={statusKind}>{statusText}</Label>
                    </TableRowCell>

                    <TableRowCell>
                      {status === BiosVerificationStatus.Present ? (
                        <Label kind={kinds.SUCCESS}>Ready</Label>
                      ) : (
                        <BiosDownloadRowButton
                          fileName={item.fileName}
                          isDownloading={isDownloading}
                          isSpinning={
                            isDownloading && downloadingFile === item.fileName
                          }
                          onDownload={handleDownloadSingle}
                        />
                      )}
                    </TableRowCell>

                    <TableRowCell>
                      <div className={styles.hashContainer}>
                        {md5 ? (
                          <div className={styles.hash}>MD5: {md5}</div>
                        ) : null}
                        {sha1 ? (
                          <div className={styles.hash}>SHA1: {sha1}</div>
                        ) : null}
                      </div>
                    </TableRowCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      ) : null}
    </div>
  );
}

export default PlatformBiosSection;
