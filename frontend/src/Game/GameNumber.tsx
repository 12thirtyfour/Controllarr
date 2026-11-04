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
  seriesType: SeriesType | undefined,
  absoluteGameNumber: number | undefined
) {
  const messages = [];

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
    alternateTitles: seriesAlternateTitles = [],
    seriesType,
    showSeasonNumber = false,
  } = props;

  const alternateTitles = filterAlternateTitles(
    seriesAlternateTitles,
    null,
    seasonNumber
  );

  const hasAlternateTitles = !!alternateTitles.length;

  const warningMessage = getWarningMessage(
    seriesType,
    absoluteGameNumber
  );

  return (
    <span className={styles.gameNumber}>
      {hasAlternateTitles ? (
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
          title={translate('ReleaseInformation')}
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
