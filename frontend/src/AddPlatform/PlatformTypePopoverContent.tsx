import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import translate from 'Utilities/String/translate';

function PlatformTypePopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title={translate('Standard')}
        data={translate('StandardGameTypeDescription')}
      />

      <DescriptionListItem
        title={translate('Anime')}
        data={translate('AnimeGameTypeDescription')}
      />
    </DescriptionList>
  );
}

export default PlatformTypePopoverContent;
