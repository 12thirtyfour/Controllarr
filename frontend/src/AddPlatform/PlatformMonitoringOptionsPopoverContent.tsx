import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';

function PlatformMonitoringOptionsPopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title="All ROMs"
        data="Marks all ROMs in the platform (all regions, unlicensed, homebrew, and ROM hacks/translations) as wanted."
      />

      <DescriptionListItem
        title="Default Region"
        data="Marks only ROMs matching the Console Region configured in UI Settings."
      />

      <DescriptionListItem
        title="All Retail"
        data="Marks all official retail regions (USA, Europe, Japan, and World) and excludes unlicensed, homebrew, and ROM hacks/translations."
      />

      <DescriptionListItem
        title="Non-Retail"
        data="Marks only non-retail content (unlicensed, homebrew/aftermarket, and ROM hacks/translations) and excludes retail regions."
      />
    </DescriptionList>
  );
}

export default PlatformMonitoringOptionsPopoverContent;
