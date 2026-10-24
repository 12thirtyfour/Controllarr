import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItemDescription from 'Components/DescriptionList/DescriptionListItemDescription';
import DescriptionListItemTitle from 'Components/DescriptionList/DescriptionListItemTitle';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

function MoreInfo() {
  return (
    <FieldSet legend={translate('MoreInfo')}>
      <DescriptionList>
        <DescriptionListItemTitle>
          {translate('HomePage')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://controllarr.org/">controllarr.org</Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>{translate('Wiki')}</DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://wiki.servarr.com/controllarr">
            wiki.servarr.com/controllarr
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('Forums')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://forums.controllarr.org/">
            forums.controllarr.org
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('Twitter')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://twitter.com/controllarr">@controllarr</Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('Discord')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://discord.controllarr.org/">
            discord.controllarr.org
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>{translate('IRC')}</DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="irc://irc.libera.chat/#controllarr">
            {translate('IRCLinkText')}
          </Link>
        </DescriptionListItemDescription>
        <DescriptionListItemDescription>
          <Link to="https://web.libera.chat/?channels=#controllarr">
            {translate('LiberaWebchat')}
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('Donations')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://controllarr.org/donate">
            controllarr.org/donate
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('Source')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://github.com/12thirtyfour/Controllarr/">
            github.com/12thirtyfour/Controllarr
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('FeatureRequests')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://github.com/12thirtyfour/Controllarr/issues">
            github.com/12thirtyfour/Controllarr/issues
          </Link>
        </DescriptionListItemDescription>
      </DescriptionList>
    </FieldSet>
  );
}

export default MoreInfo;
