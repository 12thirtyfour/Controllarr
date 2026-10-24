import React from 'react';
import { CommandBody } from 'Commands/Command';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { useMultipleSeries } from 'Series/useSeries';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import styles from './QueuedTaskRowNameCell.module.css';

function formatTitles(titles: string[]) {
  if (!titles) {
    return null;
  }

  if (titles.length > 11) {
    return (
      <span title={titles.join(', ')}>
        {titles.slice(0, 10).join(', ')}, {titles.length - 10} more
      </span>
    );
  }

  return <span>{titles.join(', ')}</span>;
}

export interface QueuedTaskRowNameCellProps {
  commandName: string;
  body: CommandBody;
  clientUserAgent?: string;
}

export default function QueuedTaskRowNameCell(
  props: QueuedTaskRowNameCellProps
) {
  const { commandName, body, clientUserAgent } = props;
  const platformIds = 'seriesIds' in body ? [...body.seriesIds] : [];

  if ('seriesId' in body && body.seriesId) {
    platformIds.push(body.seriesId);
  }

  const platforms = useMultipleSeries(platformIds);
  const sortedPlatforms = platforms.sort(sortByProp('sortTitle'));

  return (
    <TableRowCell>
      <span className={styles.commandName}>
        {commandName}
        {sortedPlatforms.length ? (
          <span> - {formatTitles(sortedPlatforms.map((s) => s.title))}</span>
        ) : null}
        {'seasonNumber' in body && body.seasonNumber ? (
          <span>
            {' '}
            {translate('SeasonNumberToken', {
              seasonNumber: body.seasonNumber,
            })}
          </span>
        ) : null}
      </span>

      {clientUserAgent ? (
        <span
          className={styles.userAgent}
          title={translate('TaskUserAgentTooltip')}
        >
          {translate('From')}: {clientUserAgent}
        </span>
      ) : null}
    </TableRowCell>
  );
}
