import React, { useCallback, useEffect, useMemo, useState } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import GameLanguages from 'Game/GameLanguages';
import GameQuality from 'Game/GameQuality';
import usePrevious from 'Helpers/Hooks/usePrevious';
import SelectGameModal from 'InteractiveImport/Game/SelectGameModal';
import { SelectedGame } from 'InteractiveImport/Game/SelectGameModalContent';
import SelectLanguageModal from 'InteractiveImport/Language/SelectLanguageModal';
import SelectQualityModal from 'InteractiveImport/Quality/SelectQualityModal';
import SelectRegionModal from 'InteractiveImport/Region/SelectRegionModal';
import SelectPlatformModal from 'InteractiveImport/Platform/SelectPlatformModal';
import { ReleaseGame, useGrabRelease } from 'InteractiveSearch/useReleases';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import Series from 'Platform/Platform';
import { useSingleSeries } from 'Platform/usePlatform';
import { useEnabledDownloadClients } from 'Settings/DownloadClients/DownloadClients/useDownloadClients';
import translate from 'Utilities/String/translate';
import SelectDownloadClientModal from './DownloadClient/SelectDownloadClientModal';
import OverrideMatchData from './OverrideMatchData';
import styles from './OverrideMatchModalContent.module.css';

type SelectType =
  | 'select'
  | 'series'
  | 'season'
  | 'game'
  | 'quality'
  | 'language'
  | 'downloadClient';

export interface OverrideMatchModalContentProps {
  indexerId: number;
  title: string;
  guid: string;
  seriesId?: number;
  seasonNumber?: number;
  games: ReleaseGame[];
  languages: Language[];
  quality: QualityModel;
  protocol: DownloadProtocol;
  isGrabbing: boolean;
  grabError?: string;
  grabRelease: ReturnType<typeof useGrabRelease>['grabRelease'];
  missingCredentials?: boolean;
  onModalClose(): void;
}

function OverrideMatchModalContent(props: OverrideMatchModalContentProps) {
  const modalTitle = translate('ManualGrab');
  const {
    indexerId,
    title,
    guid,
    protocol,
    isGrabbing,
    grabError,
    grabRelease,
    missingCredentials,
    onModalClose,
  } = props;

  const [seriesId, setSeriesId] = useState(props.seriesId);
  const [seasonNumber, setSeasonNumber] = useState(props.seasonNumber);
  const [games, setGames] = useState(props.games);
  const [languages, setLanguages] = useState(props.languages);
  const [quality, setQuality] = useState(props.quality);
  const [downloadClientId, setDownloadClientId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectModalOpen, setSelectModalOpen] = useState<SelectType | null>(
    null
  );
  const previousIsGrabbing = usePrevious(isGrabbing);

  const series: Series | undefined = useSingleSeries(seriesId);
  const { data: downloadClients } = useEnabledDownloadClients(protocol);

  const gameInfo = useMemo(() => {
    return games.map((game) => {
      return (
        <div key={game.id}>
          {game.gameNumber}

          {series?.seriesType === 'anime' &&
          game.absoluteGameNumber != null
            ? ` (${game.absoluteGameNumber})`
            : ''}

          {` - ${game.title}`}
        </div>
      );
    });
  }, [series, games]);

  const onSelectModalClose = useCallback(() => {
    setSelectModalOpen(null);
  }, [setSelectModalOpen]);

  const onSelectSeriesPress = useCallback(() => {
    setSelectModalOpen('series');
  }, [setSelectModalOpen]);

  const onSeriesSelect = useCallback(
    (s: Series) => {
      setSeriesId(s.id);
      setSeasonNumber(undefined);
      setGames([]);
      setSelectModalOpen(null);
    },
    [setSeriesId, setSeasonNumber, setGames, setSelectModalOpen]
  );

  const onSelectSeasonPress = useCallback(() => {
    setSelectModalOpen('season');
  }, [setSelectModalOpen]);

  const onSeasonSelect = useCallback(
    (s: number) => {
      setSeasonNumber(s);
      setGames([]);
      setSelectModalOpen(null);
    },
    [setSeasonNumber, setGames, setSelectModalOpen]
  );

  const onSelectGamePress = useCallback(() => {
    setSelectModalOpen('game');
  }, [setSelectModalOpen]);

  const onGamesSelect = useCallback(
    (gameMap: SelectedGame[]) => {
      setGames(gameMap[0].games);
      setSelectModalOpen(null);
    },
    [setGames, setSelectModalOpen]
  );

  const onSelectQualityPress = useCallback(() => {
    setSelectModalOpen('quality');
  }, [setSelectModalOpen]);

  const onQualitySelect = useCallback(
    (quality: QualityModel) => {
      setQuality(quality);
      setSelectModalOpen(null);
    },
    [setQuality, setSelectModalOpen]
  );

  const onSelectLanguagesPress = useCallback(() => {
    setSelectModalOpen('language');
  }, [setSelectModalOpen]);

  const onLanguagesSelect = useCallback(
    (languages: Language[]) => {
      setLanguages(languages);
      setSelectModalOpen(null);
    },
    [setLanguages, setSelectModalOpen]
  );

  const onSelectDownloadClientPress = useCallback(() => {
    setSelectModalOpen('downloadClient');
  }, [setSelectModalOpen]);

  const onDownloadClientSelect = useCallback(
    (downloadClientId: number) => {
      setDownloadClientId(downloadClientId);
      setSelectModalOpen(null);
    },
    [setDownloadClientId, setSelectModalOpen]
  );

  const onGrabPress = useCallback(() => {
    if (missingCredentials) {
      setError(translate('CredentialsRequiredMissingHelpText'));
      return;
    }
    if (!seriesId) {
      setError(translate('OverrideGrabNoSeries'));
      return;
    } else if (!games.length) {
      setError(translate('OverrideGrabNoGame'));
      return;
    } else if (!quality) {
      setError(translate('OverrideGrabNoQuality'));
      return;
    } else if (!languages.length) {
      setError(translate('OverrideGrabNoLanguage'));
      return;
    }

    grabRelease({
      indexerId,
      guid,
      override: {
        seriesId,
        gameIds: games.map((e) => e.id),
        quality,
        languages,
        downloadClientId,
      },
    });
  }, [
    indexerId,
    guid,
    seriesId,
    games,
    quality,
    languages,
    downloadClientId,
    missingCredentials,
    setError,
    grabRelease,
  ]);

  useEffect(() => {
    if (!isGrabbing && previousIsGrabbing) {
      onModalClose();
    }
  }, [isGrabbing, previousIsGrabbing, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('OverrideGrabModalTitle', { title })}
      </ModalHeader>

      <ModalBody>
        <DescriptionList>
          <DescriptionListItem
            className={styles.item}
            title={translate('SeriesSingular')}
            data={
              <OverrideMatchData
                value={series?.title}
                onPress={onSelectSeriesPress}
              />
            }
          />

          <DescriptionListItem
            className={styles.item}
            title={translate('SeasonNumber')}
            data={
              <OverrideMatchData
                value={seasonNumber}
                isDisabled={!series}
                onPress={onSelectSeasonPress}
              />
            }
          />

          <DescriptionListItem
            className={styles.item}
            title={translate('Games')}
            data={
              <OverrideMatchData
                value={gameInfo}
                isDisabled={!series || isNaN(Number(seasonNumber))}
                onPress={onSelectGamePress}
              />
            }
          />

          <DescriptionListItem
            className={styles.item}
            title={translate('Quality')}
            data={
              <OverrideMatchData
                value={
                  <GameQuality className={styles.label} quality={quality} />
                }
                onPress={onSelectQualityPress}
              />
            }
          />

          <DescriptionListItem
            className={styles.item}
            title={translate('Languages')}
            data={
              <OverrideMatchData
                value={
                  <GameLanguages
                    className={styles.label}
                    languages={languages}
                  />
                }
                onPress={onSelectLanguagesPress}
              />
            }
          />

          {downloadClients.length > 1 ? (
            <DescriptionListItem
              className={styles.item}
              title={translate('DownloadClient')}
              data={
                <OverrideMatchData
                  value={
                    downloadClients.find(
                      (downloadClient) => downloadClient.id === downloadClientId
                    )?.name ?? translate('Default')
                  }
                  onPress={onSelectDownloadClientPress}
                />
              }
            />
          ) : null}
        </DescriptionList>
      </ModalBody>

      <ModalFooter className={styles.footer}>
        <div className={styles.error}>{error || grabError || (missingCredentials ? translate('CredentialsRequiredMissingHelpText') : null)}</div>

        <div className={styles.buttons}>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerErrorButton
            isSpinning={isGrabbing}
            isDisabled={isGrabbing || missingCredentials}
            error={grabError}
            onPress={onGrabPress}
          >
            {translate('GrabRelease')}
          </SpinnerErrorButton>
        </div>
      </ModalFooter>

      <SelectPlatformModal
        isOpen={selectModalOpen === 'series'}
        modalTitle={modalTitle}
        onSeriesSelect={onSeriesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectRegionModal
        isOpen={selectModalOpen === 'season'}
        modalTitle={modalTitle}
        seriesId={seriesId}
        onSeasonSelect={onSeasonSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectGameModal
        isOpen={selectModalOpen === 'game'}
        selectedIds={[guid]}
        seriesId={seriesId}
        isAnime={series?.seriesType === 'anime'}
        seasonNumber={seasonNumber}
        selectedDetails={title}
        modalTitle={modalTitle}
        onGamesSelect={onGamesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectQualityModal
        isOpen={selectModalOpen === 'quality'}
        qualityId={quality ? quality.quality.id : 0}
        proper={quality ? quality.revision.version > 1 : false}
        real={quality ? quality.revision.real > 0 : false}
        modalTitle={modalTitle}
        onQualitySelect={onQualitySelect}
        onModalClose={onSelectModalClose}
      />

      <SelectLanguageModal
        isOpen={selectModalOpen === 'language'}
        languageIds={languages ? languages.map((l) => l.id) : []}
        modalTitle={modalTitle}
        onLanguagesSelect={onLanguagesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectDownloadClientModal
        isOpen={selectModalOpen === 'downloadClient'}
        protocol={protocol}
        modalTitle={modalTitle}
        onDownloadClientSelect={onDownloadClientSelect}
        onModalClose={onSelectModalClose}
      />
    </ModalContent>
  );
}

export default OverrideMatchModalContent;
