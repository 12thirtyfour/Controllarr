import React from 'react';
import FieldSet from 'Components/FieldSet';
import GameFormats from 'Game/GameFormats';
import PlatformTitleLink from 'Platform/PlatformTitleLink';
import translate from 'Utilities/String/translate';
import { ParseModel } from './ParseModel';
import ParseResultItem from './ParseResultItem';
import styles from './ParseResult.module.css';

interface ParseResultProps {
  item: ParseModel;
}

function ParseResult(props: ParseResultProps) {
  const { item } = props;
  const {
    customFormats,
    customFormatScore,
    games,
    languages,
    parsedGameInfo,
    series,
  } = item;

  const {
    releaseTitle,
    platformTitle,
    platformTitleInfo,
    releaseGroup,
    releaseHash,
    seasonNumber,
    gameNumbers,
    absoluteGameNumbers,
    special,
    fullSeason,
    isMultiSeason,
    isPartialSeason,
    isDaily,
    airDate,
    quality,
  } = parsedGameInfo;

  const finalLanguages = languages ?? parsedGameInfo.languages;

  return (
    <div>
      <FieldSet legend={translate('Release')}>
        <ParseResultItem
          title={translate('ReleaseTitle')}
          data={releaseTitle}
        />

        <ParseResultItem title={translate('PlatformTitle')} data={platformTitle} />

        <ParseResultItem
          title={translate('Year')}
          data={platformTitleInfo.year > 0 ? platformTitleInfo.year : '-'}
        />

        <ParseResultItem
          title={translate('AllTitles')}
          data={
            platformTitleInfo.allTitles?.length > 0
              ? platformTitleInfo.allTitles.join(', ')
              : '-'
          }
        />

        <ParseResultItem
          title={translate('ReleaseGroup')}
          data={releaseGroup ?? '-'}
        />

        <ParseResultItem
          title={translate('ReleaseHash')}
          data={releaseHash ? releaseHash : '-'}
        />
      </FieldSet>

      <FieldSet legend={translate('GameInfo')}>
        <div className={styles.container}>
          <div className={styles.column}>
            <ParseResultItem
              title={translate('SeasonNumber')}
              data={
                seasonNumber == null ||
                (seasonNumber === 0 && absoluteGameNumbers.length)
                  ? '-'
                  : seasonNumber
              }
            />

            <ParseResultItem
              title={translate('GameNumbers')}
              data={gameNumbers.join(', ') || '-'}
            />

            <ParseResultItem
              title={translate('AbsoluteGameNumbers')}
              data={
                absoluteGameNumbers.length
                  ? absoluteGameNumbers.join(', ')
                  : '-'
              }
            />

            <ParseResultItem
              title={translate('Daily')}
              data={isDaily ? 'True' : 'False'}
            />

            <ParseResultItem
              title={translate('AirDate')}
              data={airDate ?? '-'}
            />
          </div>

          <div className={styles.column}>
            <ParseResultItem
              title={translate('Special')}
              data={special ? translate('True') : translate('False')}
            />

            <ParseResultItem
              title={translate('FullSeason')}
              data={fullSeason ? translate('True') : translate('False')}
            />

            <ParseResultItem
              title={translate('MultiSeason')}
              data={isMultiSeason ? translate('True') : translate('False')}
            />

            <ParseResultItem
              title={translate('PartialSeason')}
              data={isPartialSeason ? translate('True') : translate('False')}
            />
          </div>
        </div>
      </FieldSet>

      <FieldSet legend={translate('Quality')}>
        <div className={styles.container}>
          <div className={styles.column}>
            <ParseResultItem
              title={translate('Quality')}
              data={quality.quality.name}
            />
            <ParseResultItem
              title={translate('Proper')}
              data={
                quality.revision.version > 1 && !quality.revision.isRepack
                  ? translate('True')
                  : '-'
              }
            />

            <ParseResultItem
              title={translate('Repack')}
              data={quality.revision.isRepack ? translate('True') : '-'}
            />
          </div>

          <div className={styles.column}>
            <ParseResultItem
              title={translate('Version')}
              data={
                quality.revision.version > 1 ? quality.revision.version : '-'
              }
            />

            <ParseResultItem
              title={translate('Real')}
              data={quality.revision.real ? translate('True') : '-'}
            />
          </div>
        </div>
      </FieldSet>

      <FieldSet legend={translate('Languages')}>
        <ParseResultItem
          title={translate('Languages')}
          data={finalLanguages.map((l) => l.name).join(', ')}
        />
      </FieldSet>

      <FieldSet legend={translate('Details')}>
        <ParseResultItem
          title={translate('MatchedToSeries')}
          data={
            series ? (
              <PlatformTitleLink
                titleSlug={series.titleSlug}
                title={series.title}
              />
            ) : (
              '-'
            )
          }
        />

        <ParseResultItem
          title={translate('MatchedToSeason')}
          data={games.length ? games[0].seasonNumber : '-'}
        />

        <ParseResultItem
          title={translate('MatchedToGames')}
          data={
            games.length ? (
              <div>
                {games.map((e) => {
                  return (
                    <div key={e.id}>
                      {e.gameNumber}
                      {series?.seriesType === 'anime' && e.absoluteGameNumber
                        ? ` (${e.absoluteGameNumber})`
                        : ''}{' '}
                      {` - ${e.title}`}
                    </div>
                  );
                })}
              </div>
            ) : (
              '-'
            )
          }
        />

        <ParseResultItem
          title={translate('CustomFormats')}
          data={
            customFormats?.length ? (
              <GameFormats formats={customFormats} />
            ) : (
              '-'
            )
          }
        />

        <ParseResultItem
          title={translate('CustomFormatScore')}
          data={customFormatScore}
        />
      </FieldSet>
    </div>
  );
}

export default ParseResult;
