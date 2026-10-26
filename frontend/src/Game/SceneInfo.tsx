import React, { useMemo } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import { AlternateTitle } from 'Platform/Platform';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import styles from './SceneInfo.module.css';

interface SceneInfoProps {
  seasonNumber?: number;
  gameNumber?: number;
  sceneSeasonNumber?: number;
  sceneGameNumber?: number;
  sceneAbsoluteGameNumber?: number;
  alternateTitles: AlternateTitle[];
  seriesType?: string;
}

function SceneInfo(props: SceneInfoProps) {
  const {
    seasonNumber,
    gameNumber,
    sceneSeasonNumber,
    sceneGameNumber,
    sceneAbsoluteGameNumber,
    alternateTitles,
    seriesType,
  } = props;

  const groupedAlternateTitles = useMemo(() => {
    const reducedAlternateTitles = alternateTitles.map((alternateTitle) => {
      let suffix = '';

      const altSceneSeasonNumber =
        sceneSeasonNumber === undefined ? seasonNumber : sceneSeasonNumber;
      const altSceneGameNumber =
        sceneGameNumber === undefined ? gameNumber : sceneGameNumber;

      const mappingSeasonNumber =
        alternateTitle.sceneOrigin === 'tvdb'
          ? seasonNumber
          : altSceneSeasonNumber;
      const altSeasonNumber =
        alternateTitle.sceneSeasonNumber !== -1 &&
        alternateTitle.sceneSeasonNumber !== undefined
          ? alternateTitle.sceneSeasonNumber
          : mappingSeasonNumber;
      const altGameNumber =
        alternateTitle.sceneOrigin === 'tvdb'
          ? gameNumber
          : altSceneGameNumber;

      if (altGameNumber !== altSceneGameNumber) {
        suffix = `S${padNumber(altSeasonNumber as number, 2)}E${padNumber(
          altGameNumber as number,
          2
        )}`;
      } else if (altSeasonNumber !== altSceneSeasonNumber) {
        suffix = `S${padNumber(altSeasonNumber as number, 2)}`;
      }

      return {
        alternateTitle,
        title: alternateTitle.title,
        suffix,
        comment: alternateTitle.comment,
      };
    });

    return Object.values(
      reducedAlternateTitles.reduce(
        (
          acc: Record<
            string,
            { title: string; suffix: string; comment: string }
          >,
          alternateTitle
        ) => {
          const key = alternateTitle.suffix
            ? `${alternateTitle.title} ${alternateTitle.suffix}`
            : alternateTitle.title;
          const item = acc[key];

          if (item) {
            item.comment = alternateTitle.comment
              ? `${item.comment}/${alternateTitle.comment}`
              : item.comment;
          } else {
            acc[key] = {
              title: alternateTitle.title,
              suffix: alternateTitle.suffix,
              comment: alternateTitle.comment ?? '',
            };
          }

          return acc;
        },
        {}
      )
    );
  }, [
    alternateTitles,
    seasonNumber,
    gameNumber,
    sceneSeasonNumber,
    sceneGameNumber,
  ]);

  return (
    <DescriptionList className={styles.descriptionList}>
      {sceneSeasonNumber === undefined ? null : (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('Season')}
          data={sceneSeasonNumber}
        />
      )}

      {sceneGameNumber === undefined ? null : (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('Game')}
          data={sceneGameNumber}
        />
      )}

      {seriesType === 'anime' && sceneAbsoluteGameNumber !== undefined ? (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={translate('Absolute')}
          data={sceneAbsoluteGameNumber}
        />
      ) : null}

      {alternateTitles.length ? (
        <DescriptionListItem
          titleClassName={styles.title}
          descriptionClassName={styles.description}
          title={
            groupedAlternateTitles.length === 1
              ? translate('Title')
              : translate('Titles')
          }
          data={
            <div>
              {groupedAlternateTitles.map(({ title, suffix, comment }) => {
                return (
                  <div key={`${title} ${suffix}`}>
                    {title}
                    {suffix && <span> ({suffix})</span>}
                    {comment ? (
                      <span className={styles.comment}> {comment}</span>
                    ) : null}
                  </div>
                );
              })}
            </div>
          }
        />
      ) : null}
    </DescriptionList>
  );
}

export default SceneInfo;
