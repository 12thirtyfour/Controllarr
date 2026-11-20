import React, { useMemo } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import { AlternateTitle } from 'Platform/Platform';
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
  const { alternateTitles } = props;

  const groupedAlternateTitles = useMemo(() => {
    return Object.values(
      alternateTitles.reduce(
        (
          acc: Record<string, { title: string; comment: string }>,
          alternateTitle
        ) => {
          const key = alternateTitle.title;
          const item = acc[key];

          if (item) {
            item.comment = alternateTitle.comment
              ? `${item.comment}/${alternateTitle.comment}`
              : item.comment;
          } else {
            acc[key] = {
              title: alternateTitle.title,
              comment: alternateTitle.comment ?? '',
            };
          }

          return acc;
        },
        {}
      )
    );
  }, [alternateTitles]);

  if (!alternateTitles.length) {
    return null;
  }

  return (
    <DescriptionList className={styles.descriptionList}>
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
            {groupedAlternateTitles.map(({ title, comment }) => {
              return (
                <div key={title}>
                  {title}
                  {comment ? (
                    <span className={styles.comment}> ({comment})</span>
                  ) : null}
                </div>
              );
            })}
          </div>
        }
      />
    </DescriptionList>
  );
}

export default SceneInfo;
