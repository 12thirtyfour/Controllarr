import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import translate from 'Utilities/String/translate';

function PlatformTypePopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title={translate('Anime')}
        data={translate('AnimeGameTypeDescription')}
      />

      <DescriptionListItem
        title={translate('Daily')}
        data={translate('DailyGameTypeDescription')}
      />

      <DescriptionListItem
        title={translate('Standard')}
        data={translate('StandardGameTypeDescription')}
      />
    </DescriptionList>
  );
}

export default PlatformTypePopoverContent;
