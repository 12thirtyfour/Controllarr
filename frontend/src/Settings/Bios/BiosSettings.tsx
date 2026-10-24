import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import Label from 'Components/Label';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './BiosSettings.module.css';

export enum BiosVerificationStatus {
  Missing = 0,
  Present = 1,
  InvalidHash = 2,
}

export interface BiosFile {
  fileName: string;
  platformSlug?: string;
  gamePlatform?: string;
  platformTitle?: string;
  description?: string;
  md5Hash?: string;
  expectedMd5?: string;
  sha1Hash?: string;
  expectedSha1?: string;
  isRequired?: boolean;
  status?: BiosVerificationStatus | number;
  isVerified?: boolean;
  resolvedPath?: string;
  downloadUrl?: string;
}

export function getPlatformSlug(item: BiosFile): string {
  return item.platformTitle || item.platformSlug || item.gamePlatform || '';
}

interface BiosDownloadButtonProps {
  platform?: string;
  fileName: string;
  isDownloading: boolean;
  isSpinning: boolean;
  onDownload: (platform: string | undefined, fileName: string) => void;
}

function BiosDownloadButton({
  platform,
  fileName,
  isDownloading,
  isSpinning,
  onDownload,
}: BiosDownloadButtonProps) {
  const handlePress = useCallback(() => {
    onDownload(platform, fileName);
  }, [platform, fileName, onDownload]);

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

export function getMd5Hash(item: BiosFile): string {
  return item.md5Hash || item.expectedMd5 || '';
}

export function getSha1Hash(item: BiosFile): string {
  return item.sha1Hash || item.expectedSha1 || '';
}

export function getVerificationStatus(item: BiosFile): BiosVerificationStatus {
  if (item.status !== undefined) {
    return item.status as BiosVerificationStatus;
  }

  return item.isVerified
    ? BiosVerificationStatus.Present
    : BiosVerificationStatus.Missing;
}

const columns: Column[] = [
  {
    name: 'platformSlug',
    label: () => translate('PlatformSlug'),
    isVisible: true,
  },
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
    name: 'expectedHashes',
    label: () => translate('ExpectedHashes'),
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
];

const PLATFORM_OPTIONS: SelectInputOption[] = [
  { key: '', value: () => translate('AllPlatforms') },
  { key: 'ps1', value: 'Sony PlayStation (PS1)' },
  { key: 'ps2', value: 'Sony PlayStation 2 (PS2)' },
  { key: 'psp', value: 'Sony PlayStation Portable (PSP)' },
  { key: 'segacd', value: 'Sega CD / Mega-CD' },
  { key: 'saturn', value: 'Sega Saturn' },
  { key: 'dreamcast', value: 'Sega Dreamcast' },
  { key: 'naomi', value: 'Sega NAOMI / Atomiswave' },
  { key: 'sms', value: 'Sega Master System / Mark III' },
  { key: 'gamegear', value: 'Sega Game Gear' },
  { key: 'fds', value: 'Nintendo Famicom Disk System' },
  { key: 'satellaview', value: 'Nintendo Satellaview (BS-X)' },
  { key: '64dd', value: 'Nintendo 64DD' },
  { key: 'gb', value: 'Nintendo Game Boy' },
  { key: 'gbc', value: 'Nintendo Game Boy Color' },
  { key: 'sgb', value: 'Super Game Boy' },
  { key: 'gba', value: 'Nintendo Game Boy Advance' },
  { key: 'nds', value: 'Nintendo DS' },
  { key: 'dsi', value: 'Nintendo DSi' },
  { key: 'pokemonmini', value: 'Nintendo Pokémon Mini' },
  { key: 'gamecube', value: 'Nintendo GameCube' },
  { key: 'tgcd', value: 'TurboGrafx-CD / PC Engine CD-ROM²' },
  { key: 'pcfx', value: 'NEC PC-FX' },
  { key: 'neogeo', value: 'SNK Neo Geo AES / MVS' },
  { key: 'neogeocd', value: 'SNK Neo Geo CD' },
  { key: '3do', value: '3DO Interactive Multiplayer' },
  { key: 'atari5200', value: 'Atari 5200' },
  { key: 'atari7800', value: 'Atari 7800' },
  { key: 'lynx', value: 'Atari Lynx' },
  { key: 'jaguarcd', value: 'Atari Jaguar CD' },
  { key: 'colecovision', value: 'ColecoVision' },
  { key: 'intellivision', value: 'Mattel Intellivision' },
  { key: 'odyssey2', value: 'Magnavox Odyssey² / Videopac' },
  { key: 'vectrex', value: 'GCE Vectrex' },
  { key: 'channelf', value: 'Fairchild Channel F' },
  { key: 'msx', value: 'MSX / MSX2 / MSX2+' },
  { key: 'amiga', value: 'Commodore Amiga / CD32 / CDTV' },
  { key: 'x68000', value: 'Sharp X68000' },
  { key: 'pc98', value: 'NEC PC-98' },
  { key: 'cdi', value: 'Philips CD-i' },
];

function BiosSettings() {
  const queryClient = useQueryClient();
  const [platformSlug, setPlatformSlug] = useState('');
  const [downloadingFile, setDownloadingFile] = useState<string | null>(null);

  const queryParams = useMemo(() => {
    return {
      platformSlug: platformSlug || undefined,
    };
  }, [platformSlug]);

  const { data, isFetching, error } = useApiQuery<BiosFile[]>({
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
      },
      onError: () => {
        setDownloadingFile(null);
      },
    },
  });

  const handlePlatformChange = useCallback((change: InputChanged<string>) => {
    setPlatformSlug(change.value);
  }, []);

  const handleDownloadAllMissing = useCallback(() => {
    setDownloadingFile('__all__');
    downloadBios({
      platformSlug: platformSlug || undefined,
    });
  }, [downloadBios, platformSlug]);

  const handleDownloadSingle = useCallback(
    (gamePlatform?: string, fileName?: string) => {
      setDownloadingFile(fileName ?? null);
      downloadBios({
        platformSlug: gamePlatform,
        fileName,
      });
    },
    [downloadBios]
  );

  const missingCount = useMemo(() => {
    return (data ?? []).filter(
      (item) => getVerificationStatus(item) !== BiosVerificationStatus.Present
    ).length;
  }, [data]);

  return (
    <SettingsPage title={translate('BiosSettings')} showSave={false}>
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('Bios')}
          />

          <div className={styles.filterContainer}>
            <div className={styles.filterGroup}>
              <label className={styles.filterLabel} htmlFor="platformSlug">
                {translate('PlatformSlug')}
              </label>
              <SelectInput<string>
                name="platformSlug"
                value={platformSlug}
                values={PLATFORM_OPTIONS}
                onChange={handlePlatformChange}
              />
              <span className={styles.storageNote}>
                BIOS files are stored automatically inside each platform&apos;s{' '}
                <code>bios/</code> subfolder (e.g.{' '}
                <code>{'{PlatformFolder}/bios/'}</code>).
              </span>
            </div>

            <div className={styles.actionGroup}>
              <SpinnerButton
                kind={kinds.PRIMARY}
                isSpinning={isDownloading && downloadingFile === '__all__'}
                isDisabled={isDownloading || missingCount === 0}
                onPress={handleDownloadAllMissing}
              >
                Download All Missing ({missingCount})
              </SpinnerButton>
            </div>
          </div>

          {isFetching ? <LoadingIndicator /> : null}

          {error ? (
            <Alert kind={kinds.DANGER}>
              {translate('BiosSettingsLoadError')}
            </Alert>
          ) : null}

          {!isFetching && !error && data ? (
            <Table columns={columns}>
              <TableBody>
                {data.map((item, index) => {
                  const platform = getPlatformSlug(item);
                  const md5 = getMd5Hash(item);
                  const sha1 = getSha1Hash(item);
                  const status = getVerificationStatus(item);
                  const key = `${item.gamePlatform}-${item.fileName}-${index}`;

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
                    <TableRow key={key}>
                      <TableRowCell>
                        <span className={styles.platformSlug}>{platform}</span>
                      </TableRowCell>

                      <TableRowCell>
                        <div className={styles.fileInfo}>
                          <span className={styles.fileName}>
                            {item.fileName}
                          </span>
                          {item.description ? (
                            <span className={styles.fileDescription}>
                              {item.description}
                            </span>
                          ) : null}
                          {item.resolvedPath ? (
                            <span className={styles.resolvedPath}>
                              {item.resolvedPath}
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
                        <div className={styles.hashContainer}>
                          {md5 ? (
                            <div className={styles.hash}>
                              <span className={styles.hashLabel}>
                                {translate('ExpectedMd5')}:
                              </span>
                              {md5}
                            </div>
                          ) : null}
                          {sha1 ? (
                            <div className={styles.hash}>
                              <span className={styles.hashLabel}>
                                {translate('ExpectedSha1')}:
                              </span>
                              {sha1}
                            </div>
                          ) : null}
                        </div>
                      </TableRowCell>

                      <TableRowCell>
                        <Label kind={statusKind}>{statusText}</Label>
                      </TableRowCell>

                      <TableRowCell>
                        {status === BiosVerificationStatus.Present ? (
                          <Label kind={kinds.SUCCESS}>Verified</Label>
                        ) : (
                          <BiosDownloadButton
                            platform={item.gamePlatform}
                            fileName={item.fileName}
                            isDownloading={isDownloading}
                            isSpinning={
                              isDownloading && downloadingFile === item.fileName
                            }
                            onDownload={handleDownloadSingle}
                          />
                        )}
                      </TableRowCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          ) : null}
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default BiosSettings;
