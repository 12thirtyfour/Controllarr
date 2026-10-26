import classNames from 'classnames';
import _ from 'lodash';
import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import Icon from 'Components/Icon';
import Popover from 'Components/Tooltip/Popover';
import { icons, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './ReleaseSceneIndicator.module.css';

function formatReleaseNumber(
  seasonNumber: number | null | undefined,
  gameNumbers: number[] | undefined,
  absoluteGameNumbers: number[] | undefined
) {
  if (gameNumbers && gameNumbers.length) {
    if (gameNumbers.length > 1) {
      return `${seasonNumber}x${gameNumbers[0]}-${
        gameNumbers[gameNumbers.length - 1]
      }`;
    }

    return `${seasonNumber}x${gameNumbers[0]}`;
  }

  if (absoluteGameNumbers && absoluteGameNumbers.length) {
    if (absoluteGameNumbers.length > 1) {
      return `${absoluteGameNumbers[0]}-${
        absoluteGameNumbers[absoluteGameNumbers.length - 1]
      }`;
    }

    return absoluteGameNumbers[0];
  }

  if (seasonNumber != null) {
    return translate('SeasonNumberToken', { seasonNumber });
  }

  return null;
}

interface ReleaseSceneIndicatorProps {
  className: string;
  seasonNumber?: number;
  gameNumbers?: number[];
  absoluteGameNumbers?: number[];
  sceneSeasonNumber?: number | null;
  sceneGameNumbers?: number[];
  sceneAbsoluteGameNumbers?: number[];
  sceneMapping?: {
    sceneOrigin?: string;
    title?: string;
    comment?: string;
  };
  gameRequested: boolean;
  isDaily: boolean;
}

function ReleaseSceneIndicator(props: ReleaseSceneIndicatorProps) {
  const {
    className,
    seasonNumber,
    gameNumbers,
    absoluteGameNumbers,
    sceneSeasonNumber,
    sceneGameNumbers,
    sceneAbsoluteGameNumbers,
    sceneMapping = {},
    gameRequested,
    isDaily,
  } = props;

  const { sceneOrigin, title, comment } = sceneMapping;

  if (isDaily) {
    return null;
  }

  let mappingDifferent =
    sceneSeasonNumber !== undefined && seasonNumber !== sceneSeasonNumber;

  if (sceneGameNumbers !== undefined) {
    mappingDifferent =
      mappingDifferent || !_.isEqual(sceneGameNumbers, gameNumbers);
  } else if (sceneAbsoluteGameNumbers !== undefined) {
    mappingDifferent =
      mappingDifferent ||
      !_.isEqual(sceneAbsoluteGameNumbers, absoluteGameNumbers);
  }

  if (!sceneMapping && !mappingDifferent) {
    return null;
  }

  const releaseNumber = formatReleaseNumber(
    sceneSeasonNumber,
    sceneGameNumbers,
    sceneAbsoluteGameNumbers
  );
  const mappedNumber = formatReleaseNumber(
    seasonNumber,
    gameNumbers,
    absoluteGameNumbers
  );
  const messages = [];

  const isMixed = sceneOrigin === 'mixed';
  const isUnknown = sceneOrigin === 'unknown' || sceneOrigin === 'unknown:tvdb';

  let level = styles.levelNone;

  if (isMixed) {
    level = styles.levelMixed;
    messages.push(
      <div key="source">
        {translate('ReleaseSceneIndicatorSourceMessage', {
          message: comment ?? 'Source',
        })}
      </div>
    );
  } else if (isUnknown) {
    level = styles.levelUnknown;
    messages.push(
      <div key="unknown">
        {translate('ReleaseSceneIndicatorUnknownMessage')}
      </div>
    );

    if (sceneOrigin === 'unknown') {
      messages.push(
        <div key="origin">
          {translate('ReleaseSceneIndicatorAssumingScene')}.
        </div>
      );
    } else if (sceneOrigin === 'unknown:tvdb') {
      messages.push(
        <div key="origin">{translate('ReleaseSceneIndicatorAssumingTvdb')}</div>
      );
    }
  } else if (mappingDifferent) {
    level = styles.levelMapped;
  } else if (sceneOrigin) {
    level = styles.levelNormal;
  }

  if (!gameRequested) {
    if (!isMixed && !isUnknown) {
      level = styles.levelNotRequested;
    }

    if (mappedNumber) {
      messages.push(
        <div key="not-requested">
          {translate('ReleaseSceneIndicatorMappedNotRequested')}
        </div>
      );
    } else {
      messages.push(
        <div key="unknown-series">
          {translate('ReleaseSceneIndicatorUnknownSeries')}
        </div>
      );
    }
  }

  const table = (
    <DescriptionList className={styles.descriptionList}>
      {comment !== undefined && (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('Mapping')}
          data={comment}
        />
      )}

      {title !== undefined && (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('Title')}
          data={title}
        />
      )}

      {releaseNumber !== undefined && (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('Release')}
          data={releaseNumber ?? 'unknown'}
        />
      )}

      {releaseNumber !== undefined && (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('TheTvdb')}
          data={mappedNumber ?? 'unknown'}
        />
      )}
    </DescriptionList>
  );

  return (
    <Popover
      anchor={
        <div className={classNames(level, styles.container, className)}>
          <Icon name={icons.SCENE_MAPPING} />
        </div>
      }
      title={translate('SceneInfo')}
      body={
        <div>
          {table}
          {(messages.length && (
            <div className={styles.messages}>{messages}</div>
          )) ||
            null}
        </div>
      }
      position={tooltipPositions.RIGHT}
    />
  );
}

export default ReleaseSceneIndicator;
