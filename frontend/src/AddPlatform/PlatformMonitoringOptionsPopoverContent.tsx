import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import translate from 'Utilities/String/translate';

function PlatformMonitoringOptionsPopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title={translate('MonitorAllGames')}
        data={translate('MonitorAllGamesDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorFutureGames')}
        data={translate('MonitorFutureGamesDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorMissingGames')}
        data={translate('MonitorMissingGamesDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorExistingGames')}
        data={translate('MonitorExistingGamesDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorRecentGames')}
        data={translate('MonitorRecentGamesDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorPilotGame')}
        data={translate('MonitorPilotGameDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorFirstSeason')}
        data={translate('MonitorFirstSeasonDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorLastSeason')}
        data={translate('MonitorLastSeasonDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorSpecialGames')}
        data={translate('MonitorSpecialGamesDescription')}
      />

      <DescriptionListItem
        title={translate('UnmonitorSpecialGames')}
        data={translate('UnmonitorSpecialsGamesDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorNoGames')}
        data={translate('MonitorNoGamesDescription')}
      />
    </DescriptionList>
  );
}

export default PlatformMonitoringOptionsPopoverContent;
