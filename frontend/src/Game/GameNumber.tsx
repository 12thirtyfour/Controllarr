import React from 'react';
import Icon from 'Components/Icon';
import Popover from 'Components/Tooltip/Popover';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import { AlternateTitle, SeriesType } from 'Platform/Platform';
import padNumber from 'Utilities/Number/padNumber';
import filterAlternateTitles from 'Utilities/Platform/filterAlternateTitles';
import translate from 'Utilities/String/translate';
import SceneInfo from './SceneInfo';
import styles from './GameNumber.module.css';

function getWarningMessage(
  unverifiedSceneNumbering: boolean,
  seriesType: SeriesType | undefined,
  absoluteGameNumber: number | undefined
) {
  const messages = [];

  if (unverifiedSceneNumbering) {
    messages.push(translate('SceneNumberNotVerified'));
  }

  if (seriesType === 'anime' && !absoluteGameNumber) {
    messages.push(translate('GameMissingAbsoluteNumber'));
  }

  return messages.join('\n');
}

export interface GameNumberProps {
  seasonNumber: number;
  gameNumber: number;
  absoluteGameNumber?: number;
  sceneSeasonNumber?: number;
  sceneGameNumber?: number;
  sceneAbsoluteGameNumber?: number;
  useSceneNumbering?: boolean;
  unverifiedSceneNumbering?: boolean;
  alternateTitles?: AlternateTitle[];
  seriesType?: SeriesType;
  showSeasonNumber?: boolean;
}

function GameNumber(props: GameNumberProps) {
  const {
    seasonNumber,
    gameNumber,
    absoluteGameNumber,
    sceneSeasonNumber,
    sceneGameNumber,
    sceneAbsoluteGameNumber,
    useSceneNumbering = false,
    unverifiedSceneNumbering = false,
    alternateTitles: seriesAlternateTitles = [],
    seriesType,
    showSeasonNumber = false,
  } = props;

  const alternateTitles = filterAlternateTitles(
    seriesAlternateTitles,
    null,
    useSceneNumbering,
    seasonNumber,
    sceneSeasonNumber
  );

  const hasSceneInformation =
    sceneSeasonNumber !== undefined ||
    sceneGameNumber !== undefined ||
    (seriesType === 'anime' && sceneAbsoluteGameNumber !== undefined) ||
    !!alternateTitles.length;

  const warningMessage = getWarningMessage(
    unverifiedSceneNumbering,
    seriesType,
    absoluteGameNumber
  );

  return (
    <span className={styles.gameNumber}>
      {hasSceneInformation ? (
        <Popover
          anchor={
            <span>
              {showSeasonNumber && seasonNumber != null && (
                <>
                  {seasonNumber}
                  <span className={styles.separator}>x</span>
                </>
              )}

              {showSeasonNumber ? padNumber(gameNumber, 2) : gameNumber}

              {seriesType === 'anime' && !!absoluteGameNumber && (
                <span className={styles.absoluteGameNumber}>
                  ({absoluteGameNumber})
                </span>
              )}
            </span>
          }
          title={translate('SceneInformation')}
          body={
            <SceneInfo
              seasonNumber={seasonNumber}
              gameNumber={gameNumber}
              sceneSeasonNumber={sceneSeasonNumber}
              sceneGameNumber={sceneGameNumber}
              sceneAbsoluteGameNumber={sceneAbsoluteGameNumber}
              alternateTitles={alternateTitles}
              seriesType={seriesType}
            />
          }
          position={tooltipPositions.RIGHT}
        />
      ) : (
        <span>
          {showSeasonNumber && seasonNumber != null && (
            <>
              {seasonNumber}
              <span className={styles.separator}>x</span>
            </>
          )}

          {showSeasonNumber ? padNumber(gameNumber, 2) : gameNumber}

          {seriesType === 'anime' && !!absoluteGameNumber && (
            <span className={styles.absoluteGameNumber}>
              ({absoluteGameNumber})
            </span>
          )}
        </span>
      )}

      {warningMessage ? (
        <Icon
          titleWrapperClassName={styles.warning}
          name={icons.WARNING}
          kind={kinds.WARNING}
          title={warningMessage}
        />
      ) : null}
    </span>
  );
}

export default GameNumber;
