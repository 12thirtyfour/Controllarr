import classNames from 'classnames';
import React, {
  useCallback,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import GameFormats from 'Game/GameFormats';
import GameLanguages from 'Game/GameLanguages';
import IndexerFlags from 'Game/IndexerFlags';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import type Series from 'Platform/Platform';
import { useSaveSeries } from 'Platform/usePlatform';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatAge from 'Utilities/Number/formatAge';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import OverrideMatchModal from './OverrideMatch/OverrideMatchModal';
import Peers from './Peers';
import ReleaseSceneIndicator from './ReleaseSceneIndicator';
import { Release, useGrabRelease } from './useReleases';
import styles from './InteractiveSearchRow.module.css';

function getDownloadIcon(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return icons.SPINNER;
  } else if (isGrabbed) {
    return icons.DOWNLOADING;
  } else if (grabError) {
    return icons.DOWNLOADING;
  }

  return icons.DOWNLOAD;
}

function getDownloadKind(isGrabbed: boolean, grabError?: string) {
  if (isGrabbed) {
    return kinds.SUCCESS;
  }

  if (grabError) {
    return kinds.DANGER;
  }

  return kinds.DEFAULT;
}

function getDownloadTooltip(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return '';
  } else if (isGrabbed) {
    return translate('AddedToDownloadQueue');
  } else if (grabError) {
    return grabError;
  }

  return translate('AddToDownloadQueue');
}

interface InteractiveSearchRowProps extends Release {
  index: number;
  style: React.CSSProperties;
  setRowHeight: (index: number, height: number) => void;
  searchPayload: InteractiveSearchPayload;
}

function InteractiveSearchRow(props: InteractiveSearchRowProps) {
  const { index, style, setRowHeight } = props;
  const {
    decision,
    history,
    parsedInfo,
    release,
    languages,
    customFormatScore,
    customFormats,
    sceneMapping,
    mappedSeriesId,
    mappedSeasonNumber,
    mappedGameNumbers,
    mappedAbsoluteGameNumbers,
    mappedGameInfo,
    gameRequested,
    searchPayload,
    downloadAllowed,
  } = props;

  const { rejections = [] } = decision;

  const {
    absoluteGameNumbers,
    gameNumbers,
    isDaily,
    seasonNumber,
    quality,
  } = parsedInfo;

  const {
    guid,
    indexerId,
    age,
    ageHours,
    ageMinutes,
    publishDate,
    title,
    infoUrl,
    indexer,
    size,
    seeders,
    leechers,
    protocol,
    indexerFlags = 0,
    requiresCredentials,
    missingCredentials,
  } = release;

  const { longDateFormat, timeFormat } = useUiSettingsValues();

  const [isConfirmGrabModalOpen, setIsConfirmGrabModalOpen] = useState(false);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState(false);
  const { isGrabbing, isGrabbed, grabError, grabRelease } = useGrabRelease();
  const { saveSeries } = useSaveSeries(false);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const series = (searchPayload as any)?.series as Series | undefined;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const parsedRevision = (parsedInfo as any)?.revision;
  const isPinned =
    !!series?.pinnedVersion && parsedRevision === series?.pinnedVersion;

  const handlePinToggle = useCallback(() => {
    if (!series) return;
    saveSeries({
      ...series,
      pinnedVersion: isPinned ? null : parsedRevision,
    });
  }, [series, parsedRevision, isPinned, saveSeries]);

  const rowRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const element = rowRef.current;

    if (!element) {
      return;
    }

    const measure = () => setRowHeight(index, element.offsetHeight);

    measure();

    const observer = new ResizeObserver(measure);
    observer.observe(element);

    return () => observer.disconnect();
  }, [index, setRowHeight]);

  const isBlocklisted = useMemo(() => {
    return (
      decision.rejections.findIndex((r) => r.reason === 'blocklisted') >= 0
    );
  }, [decision]);

  const handleGrabPress = useCallback(() => {
    if (missingCredentials) {
      return;
    }
    if (downloadAllowed) {
      grabRelease({
        guid,
        indexerId,
        searchInfo: searchPayload,
      });

      return;
    }

    setIsConfirmGrabModalOpen(true);
  }, [
    guid,
    indexerId,
    searchPayload,
    downloadAllowed,
    missingCredentials,
    grabRelease,
    setIsConfirmGrabModalOpen,
  ]);

  const onGrabConfirm = useCallback(() => {
    if (missingCredentials) {
      return;
    }
    setIsConfirmGrabModalOpen(false);

    grabRelease({
      guid,
      indexerId,
      searchInfo: searchPayload,
    });
  }, [guid, indexerId, searchPayload, missingCredentials, grabRelease, setIsConfirmGrabModalOpen]);

  const onGrabCancel = useCallback(() => {
    setIsConfirmGrabModalOpen(false);
  }, [setIsConfirmGrabModalOpen]);

  const onOverridePress = useCallback(() => {
    setIsOverrideModalOpen(true);
  }, [setIsOverrideModalOpen]);

  const onOverrideModalClose = useCallback(() => {
    setIsOverrideModalOpen(false);
  }, [setIsOverrideModalOpen]);

  const { height: _height, ...positionStyle } = style;

  return (
    <div ref={rowRef} className={styles.row} style={positionStyle}>
      <VirtualTableRowCell className={styles.protocol}>
        <ProtocolLabel protocol={protocol} />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.age}>
        <span
          title={formatDateTime(publishDate, longDateFormat, timeFormat, {
            includeSeconds: true,
          })}
        >
          {formatAge(age, ageHours, ageMinutes)}
        </span>
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.title}>
        <div className={styles.titleContent}>
          <Link to={infoUrl}>{title}</Link>
          {requiresCredentials && (
            <Label
              kind={missingCredentials ? kinds.DANGER : kinds.INFO}
              className={styles.credentialsLabel}
              title={
                missingCredentials
                  ? translate('CredentialsRequiredMissingHelpText')
                  : translate('CredentialsConfiguredHelpText')
              }
            >
              {translate(
                missingCredentials
                  ? 'RequiresCredentials'
                  : 'UsesCredentials'
              )}
            </Label>
          )}
          {isPinned && (
            <Label kind="info" className={styles.pinnedLabel}>
              Pinned
            </Label>
          )}
          <ReleaseSceneIndicator
            className={styles.sceneMapping}
            seasonNumber={mappedSeasonNumber}
            gameNumbers={mappedGameNumbers}
            absoluteGameNumbers={mappedAbsoluteGameNumbers}
            sceneSeasonNumber={seasonNumber}
            sceneGameNumbers={gameNumbers}
            sceneAbsoluteGameNumbers={absoluteGameNumbers}
            sceneMapping={sceneMapping}
            gameRequested={gameRequested}
            isDaily={isDaily}
          />
        </div>
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.indexer}>
        {indexer}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.history}>
        {history ? (
          <Icon
            name={icons.DOWNLOADING}
            kind={history.failed ? kinds.DANGER : kinds.DEFAULT}
            title={`${
              history.failed
                ? translate('FailedAt', {
                    date: formatDateTime(
                      history.failed,
                      longDateFormat,
                      timeFormat,
                      { includeSeconds: true }
                    ),
                  })
                : translate('GrabbedAt', {
                    date: formatDateTime(
                      history.grabbed,
                      longDateFormat,
                      timeFormat,
                      { includeSeconds: true }
                    ),
                  })
            }`}
          />
        ) : null}

        {isBlocklisted ? (
          <Icon
            titleWrapperClassName={
              history ? styles.blocklistIconContainer : undefined
            }
            name={icons.BLOCKLIST}
            kind={kinds.DANGER}
            title={
              history?.failed
                ? `${translate('BlocklistedAt', {
                    date: formatDateTime(
                      history.failed,
                      longDateFormat,
                      timeFormat,
                      { includeSeconds: true }
                    ),
                  })}`
                : translate('Blocklisted')
            }
          />
        ) : null}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.size}>
        {formatBytes(size)}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.peers}>
        {protocol === 'torrent' ? (
          <Peers seeders={seeders} leechers={leechers} />
        ) : null}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.languages}>
        <GameLanguages languages={languages} />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.format}>
        {(() => {
          const displayFormat =
            release.format ||
            (props as any).format ||
            (() => {
              const ext = title.match(/\.([A-Za-z0-9]{1,7})(?:\s*\[|\s*\(|\s*$)/)?.[1];
              if (ext) {
                return ext.toUpperCase();
              }
              const token = title.match(
                /\b(3DS|CIA|CCI|NDS|DSI|GBA|GBC|GB|NES|SNES|SFC|SMC|N64|Z64|V64|ISO|CISO|CHD|RVZ|GCM|WBFS|WUX|WUD|WUA|NSP|NSZ|XCI|PBP|CSO|VPK|PKG|BIN|CUE|GDI|CDI|ZIP|7Z)\b/i
              )?.[1];
              return token ? token.toUpperCase() : null;
            })();

          return displayFormat ? (
            <span className={styles.formatBadge}>{displayFormat}</span>
          ) : (
            <span className={styles.formatBadge}>-</span>
          );
        })()}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.customFormatScore}>
        <Tooltip
          anchor={formatCustomFormatScore(
            customFormatScore,
            customFormats.length
          )}
          tooltip={<GameFormats formats={customFormats} />}
          position={tooltipPositions.LEFT}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.indexerFlags}>
        {indexerFlags ? (
          <Popover
            anchor={<Icon name={icons.FLAG} />}
            title={translate('IndexerFlags')}
            body={<IndexerFlags indexerFlags={indexerFlags} />}
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.rejected}>
        {rejections.length ? (
          <Popover
            anchor={<Icon name={icons.DANGER} kind={kinds.DANGER} />}
            title={translate('ReleaseRejected')}
            body={
              <ul>
                {rejections.map((rejection, index) => {
                  return <li key={index}>{rejection.message}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.download}>
        <SpinnerIconButton
          name={getDownloadIcon(isGrabbing, isGrabbed, grabError)}
          kind={getDownloadKind(isGrabbed, grabError)}
          title={
            missingCredentials
              ? translate('CredentialsRequiredMissingHelpText')
              : getDownloadTooltip(isGrabbing, isGrabbed, grabError)
          }
          isDisabled={isGrabbing || isGrabbed || missingCredentials}
          isSpinning={isGrabbing}
          onPress={handleGrabPress}
        />

        {!missingCredentials && (
          <Link
          className={styles.manualDownloadContent}
          title={translate('OverrideAndAddToDownloadQueue')}
          onPress={onOverridePress}
        >
          <div className={styles.manualDownloadContent}>
            <Icon
              className={styles.interactiveIcon}
              name={icons.INTERACTIVE}
              size={12}
            />

            <Icon
              className={styles.downloadIcon}
              name={icons.CIRCLE_DOWN}
              size={10}
            />
          </div>
        </Link>
        )}
        {series && (
          <IconButton
            name={icons.PIN}
            className={classNames(styles.pinButton, {
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              [(styles as any).pinned]: isPinned,
            })}
            kind={isPinned ? kinds.WARNING : kinds.DEFAULT}
            title={
              isPinned
                ? translate('Unpin release')
                : translate('Pin this release as preferred (1G1R override)')
            }
            onPress={handlePinToggle}
          />
        )}
      </VirtualTableRowCell>

      <ConfirmModal
        isOpen={isConfirmGrabModalOpen}
        kind={kinds.WARNING}
        title={translate('GrabRelease')}
        message={translate('GrabReleaseUnknownSeriesOrGameMessageText', {
          title,
        })}
        confirmLabel={translate('Grab')}
        onConfirm={onGrabConfirm}
        onCancel={onGrabCancel}
      />

      <OverrideMatchModal
        isOpen={isOverrideModalOpen}
        title={title}
        indexerId={indexerId}
        guid={guid}
        seriesId={mappedSeriesId}
        seasonNumber={mappedSeasonNumber}
        games={mappedGameInfo}
        languages={languages}
        quality={quality}
        protocol={protocol}
        isGrabbing={isGrabbing}
        grabError={grabError}
        grabRelease={grabRelease}
        onModalClose={onOverrideModalClose}
        missingCredentials={missingCredentials}
      />
    </div>
  );
}

export default InteractiveSearchRow;
