import classNames from 'classnames';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { icons, kinds, sizes } from 'Helpers/Props';
import {
  LibretroLauncherConfig,
  loadLocalLibretroConfig,
} from 'Settings/Libretro/LibretroSettings';
import styles from './WebPlayerPage.module.css';

export interface GameLaunchDescriptor {
  gameId: number;
  seriesId: number;
  gameTitle: string;
  platformTitle: string;
  platformFolder: string;
  romPath: string;
  romFileName: string;
  hasRomFile: boolean;
  libretroCore: string;
  emulatorJsCore: string;
  supportsWebAssembly: boolean;
  preferredLaunchMode: string;
  shader: string;
  emulatorJsDataPath: string;
  romStreamUrl: string;
  biosStreamUrl: string;
  desktopProtocolUri: string;
  WebPlayerUrl?: string;
  webPlayerUrl: string;
}

interface DesktopLaunchResult {
  success: boolean;
  launchedMode: string;
  message: string;
  descriptor: GameLaunchDescriptor;
}

const SHADER_CHOICES = [
  { key: 'none', label: 'None (Raw Pixels)' },
  { key: 'crt-mattias', label: 'CRT-Mattias' },
  { key: 'crt-easymode', label: 'CRT-EasyMode' },
  { key: 'crt-aperture', label: 'CRT-Aperture' },
  { key: 'crt-geom', label: 'CRT-Geom' },
  { key: 'lcd-grid-v2', label: 'LCD-Grid v2' },
  { key: '2xScaleHQ', label: '2xScaleHQ' },
  { key: 'sabr', label: 'SABR v3.0' },
];

function WebPlayerPage() {
  const location = useLocation();
  const searchParams = useMemo(
    () => new URLSearchParams(location.search),
    [location.search]
  );

  const gameIdParam = Number(searchParams.get('gameId') || '0');
  const coreQueryParam = searchParams.get('core') || '';
  const localConfig = useMemo(() => loadLocalLibretroConfig(), []);

  const [selectedCore, setSelectedCore] = useState<string>(coreQueryParam);
  const [selectedShader, setSelectedShader] = useState<string>(
    localConfig.defaultShader || 'none'
  );
  const [desktopLaunchMessage, setDesktopLaunchMessage] = useState<
    string | null
  >(null);

  const { data: descriptor, isLoading } = useApiQuery<GameLaunchDescriptor>({
    path: `/gamelauncher/${gameIdParam}`,
    queryOptions: {
      enabled: gameIdParam > 0,
    },
  });

  const { data: remoteConfig } = useApiQuery<LibretroLauncherConfig>({
    path: '/gamelauncher/config',
  });

  const effectiveConfig = remoteConfig ?? localConfig;

  useEffect(() => {
    if (descriptor) {
      if (!selectedCore) {
        setSelectedCore(descriptor.libretroCore || 'snes9x');
      }

      if (descriptor.shader && selectedShader === 'none') {
        setSelectedShader(descriptor.shader);
      }
    }
  }, [descriptor, selectedCore, selectedShader]);

  const { mutate: triggerDesktopLaunch, isPending: isLaunchingDesktop } =
    useApiMutation<DesktopLaunchResult, void>({
      path: `/gamelauncher/${descriptor?.gameId || gameIdParam}/launch`,
      method: 'POST',
      mutationOptions: {
        onSuccess: (res) => {
          setDesktopLaunchMessage(res.message);
        },
      },
    });

  const handleLaunchDesktopPress = useCallback(() => {
    triggerDesktopLaunch();
  }, [triggerDesktopLaunch]);

  const handleProtocolPress = useCallback(() => {
    if (descriptor?.desktopProtocolUri) {
      window.location.href = descriptor.desktopProtocolUri;
    }
  }, [descriptor]);

  const handleShaderSelectChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      setSelectedShader(e.target.value);
    },
    []
  );

  const apiKey = window.Controllarr?.apiKey ?? '';
  const urlBase = window.Controllarr?.urlBase ?? '';

  const romStreamUrl = useMemo(() => {
    if (!descriptor) {
      return '';
    }

    const baseStream =
      descriptor.romStreamUrl ||
      `/api/v3/gamelauncher/${descriptor.gameId}/rom`;
    const separator = baseStream.includes('?') ? '&' : '?';

    return `${urlBase}${baseStream}${
      apiKey ? `${separator}apikey=${encodeURIComponent(apiKey)}` : ''
    }`;
  }, [apiKey, descriptor, urlBase]);

  const playerSrcDoc = useMemo(() => {
    if (!descriptor || !descriptor.hasRomFile) {
      return '';
    }

    const dataPath =
      effectiveConfig.emulatorJsDataPath ||
      descriptor.emulatorJsDataPath ||
      'https://cdn.emulatorjs.org/stable/data/';
    const ejsCore = descriptor.emulatorJsCore || selectedCore || 'snes';
    const safeTitle = (descriptor.gameTitle || 'ROM').replace(/["<>]/g, '');
    const shaderValue = selectedShader === 'none' ? '' : selectedShader;
    const shaderLine = shaderValue
      ? `window.EJS_shader = ${JSON.stringify(shaderValue)};`
      : '';

    return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <title>${safeTitle} - Controllarr Libretro Player</title>
  <style>
    html, body {
      margin: 0;
      padding: 0;
      width: 100%;
      height: 100%;
      background-color: #050608;
      overflow: hidden;
    }
    #game {
      width: 100%;
      height: 100%;
    }
  </style>
</head>
<body>
  <div id="game"></div>
  <script>
    window.EJS_player = '#game';
    window.EJS_core = ${JSON.stringify(ejsCore)};
    window.EJS_gameName = ${JSON.stringify(safeTitle)};
    window.EJS_gameUrl = ${JSON.stringify(romStreamUrl)};
    window.EJS_pathtodata = ${JSON.stringify(dataPath)};
    window.EJS_color = '#00a8e8';
    window.EJS_startOnLoaded = true;
    ${shaderLine}
  </script>
  <script src="${dataPath.replace(/\/?$/, '/')}loader.js"></script>
</body>
</html>`;
  }, [
    descriptor,
    effectiveConfig.emulatorJsDataPath,
    romStreamUrl,
    selectedCore,
    selectedShader,
  ]);

  if (isLoading) {
    return (
      <PageContent title="Libretro Web Player">
        <PageContentBody>
          <LoadingIndicator />
        </PageContentBody>
      </PageContent>
    );
  }

  let bodyContent: React.ReactNode = null;

  if (!descriptor || !descriptor.hasRomFile) {
    bodyContent = (
      <div className={styles.emptyStateCard}>
        <Icon name={icons.WARNING} size={32} />
        <h2 className={styles.emptyStateTitle}>
          No Downloaded ROM File Available on Disk
        </h2>
        <p className={styles.emptyStateText}>
          This game does not have an imported ROM file on disk yet, or the file
          could not be located at{' '}
          <code>{descriptor?.romPath || 'the expected library path'}</code>.
          Download or import a verified ROM first to launch it in Libretro, or
          configure your local RetroArch paths in Libretro Settings.
        </p>
        <div className={styles.emptyStateActions}>
          <Link
            className={classNames(
              styles.actionButton,
              styles.primaryActionButton
            )}
            to="/platforms"
          >
            Return to Library
          </Link>
          <Link className={styles.actionButton} to="/settings/libretro">
            Configure Libretro &amp; RetroArch
          </Link>
        </div>
      </div>
    );
  } else if (
    descriptor.supportsWebAssembly &&
    effectiveConfig.enableWebPlayer
  ) {
    bodyContent = (
      <div className={styles.playerFrameWrapper}>
        <iframe
          key={`${descriptor.gameId}-${selectedCore}-${selectedShader}`}
          title={`${descriptor.gameTitle} Libretro WebPlayer`}
          className={styles.playerIframe}
          srcDoc={playerSrcDoc}
          allow="autoplay; fullscreen; gamepad"
        />
      </div>
    );
  } else {
    const desktopReasonMessage = effectiveConfig.enableWebPlayer
      ? `${descriptor.platformTitle} uses the native "${descriptor.libretroCore}" core, which requires Desktop RetroArch or a standalone hardware-accelerated emulator.`
      : 'The In-Browser Libretro Web Player is currently disabled in Settings -> Libretro.';

    bodyContent = (
      <div className={styles.emptyStateCard}>
        <Icon name={icons.QUICK} size={32} />
        <h2 className={styles.emptyStateTitle}>
          Desktop Libretro / RetroArch Core Required ({descriptor.libretroCore})
        </h2>
        <p className={styles.emptyStateText}>{desktopReasonMessage}</p>
        <div className={styles.emptyStateActions}>
          <button
            type="button"
            className={classNames(
              styles.actionButton,
              styles.primaryActionButton
            )}
            onClick={handleLaunchDesktopPress}
          >
            <Icon name={icons.SERIES_CONTINUING} size={13} />
            <span>Launch in Desktop RetroArch</span>
          </button>
          <button
            type="button"
            className={styles.actionButton}
            onClick={handleProtocolPress}
          >
            <span>Open via controllarr:// Protocol</span>
          </button>
          <Link className={styles.actionButton} to="/settings/libretro">
            Configure Libretro Settings
          </Link>
        </div>
      </div>
    );
  }

  return (
    <PageContent
      title={
        descriptor?.gameTitle
          ? `${descriptor.gameTitle} - Libretro Player`
          : 'Libretro Player'
      }
    >
      <PageContentBody>
        <div className={styles.container}>
          <div className={styles.headerBar}>
            <div className={styles.titleGroup}>
              <div className={styles.titleRow}>
                <Icon name={icons.SERIES_CONTINUING} size={18} />
                <h1 className={styles.gameTitle}>
                  {descriptor?.gameTitle || `Game #${gameIdParam}`}
                </h1>
                {descriptor?.platformTitle ? (
                  <Label kind={kinds.INFO} size={sizes.MEDIUM}>
                    {descriptor.platformTitle}
                  </Label>
                ) : null}
                <Label
                  kind={
                    descriptor?.supportsWebAssembly
                      ? kinds.SUCCESS
                      : kinds.WARNING
                  }
                  size={sizes.SMALL}
                >
                  Core: {selectedCore || descriptor?.libretroCore || 'auto'}
                </Label>
              </div>
              <div className={styles.subInfo}>
                {descriptor?.romFileName
                  ? `ROM File: ${descriptor.romFileName}`
                  : 'No local ROM file attached yet'}
              </div>
            </div>

            <div className={styles.controlsRow}>
              <div className={styles.controlItem}>
                <span className={styles.controlLabel}>Shader:</span>
                <select
                  className={styles.selectControl}
                  value={selectedShader}
                  onChange={handleShaderSelectChange}
                >
                  {SHADER_CHOICES.map((s) => (
                    <option key={s.key} value={s.key}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </div>

              <button
                type="button"
                className={classNames(
                  styles.actionButton,
                  styles.primaryActionButton
                )}
                disabled={isLaunchingDesktop}
                onClick={handleLaunchDesktopPress}
              >
                <Icon name={icons.EXTERNAL_LINK} size={13} />
                <span>Launch Desktop RetroArch</span>
              </button>

              {descriptor?.desktopProtocolUri ? (
                <button
                  type="button"
                  className={styles.actionButton}
                  onClick={handleProtocolPress}
                >
                  <Icon name={icons.QUICK} size={13} />
                  <span>controllarr:// URI</span>
                </button>
              ) : null}

              <Link className={styles.actionButton} to="/settings/libretro">
                <Icon name={icons.SETTINGS} size={13} />
                <span>Libretro Settings</span>
              </Link>

              <Link className={styles.actionButton} to="/platforms">
                <Icon name={icons.ARROW_LEFT} size={13} />
                <span>Back to Library</span>
              </Link>
            </div>
          </div>

          {desktopLaunchMessage ? (
            <Alert kind={kinds.INFO}>{desktopLaunchMessage}</Alert>
          ) : null}

          {bodyContent}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default WebPlayerPage;
