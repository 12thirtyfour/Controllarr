import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import Link from 'Components/Link/Link';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import SettingsPage from './SettingsPage';
import styles from './Settings.module.css';

interface SectionEntry {
  to: string;
  iconName: IconName;
  title: string;
  summary: string;
}

function buildSections(): SectionEntry[] {
  return [
    {
      to: '/settings/general',
      iconName: icons.ADVANCED_SETTINGS,
      title: translate('General'),
      summary: translate('GeneralSettingsSummary'),
    },
    {
      to: '/settings/ui',
      iconName: icons.VIEW,
      title: translate('Ui'),
      summary: translate('UiSettingsSummary'),
    },
    {
      to: '/settings/mediamanagement',
      iconName: icons.DRIVE,
      title: translate('MediaManagement'),
      summary: translate('MediaManagementSettingsSummary'),
    },
    {
      to: '/settings/collection',
      iconName: icons.SERIES_CONTINUING,
      title: 'Collection Settings',
      summary:
        'Configure collection strategy (1G1R vs. Completionist), revision priority, unofficial content defaults, and region ordering',
    },
    {
      to: '/settings/profiles',
      iconName: icons.PROFILE,
      title: 'Virtual Libraries',
      summary:
        'Define hardware-specific virtual folders (MiSTer, RetroArch, Analogue Pocket, OnionOS, Batocera) via console directory symlinks',
    },
    {
      to: '/settings/indexers',
      iconName: icons.RSS,
      title: translate('Indexers'),
      summary: translate('IndexersSettingsSummary'),
    },
    {
      to: '/settings/downloadclients',
      iconName: icons.DOWNLOADING,
      title: translate('DownloadClients'),
      summary: translate('DownloadClientsSettingsSummary'),
    },
    {
      to: '/settings/metadata',
      iconName: icons.CLIPBOARD,
      title: translate('Metadata'),
      summary:
        'Configure metadata scrapers, priority cascading, artwork checkboxes, rate limits, and local DAT/SMDB verification catalogs',
    },
    {
      to: '/settings/connect',
      iconName: icons.NETWORK,
      title: translate('Connect'),
      summary: translate('ConnectSettingsSummary'),
    },
    {
      to: '/settings/importlists',
      iconName: icons.ROOT_FOLDER,
      title: translate('ImportLists'),
      summary: translate('ImportListsSettingsSummary'),
    },
    {
      to: '/settings/tags',
      iconName: icons.TAGS,
      title: translate('Tags'),
      summary: translate('TagsSettingsSummary'),
    },
    {
      to: '/settings/retroachievements',
      iconName: icons.SCORE,
      title: translate('RetroAchievements'),
      summary: translate('RetroAchievementsSettingsSummary'),
    },
    {
      to: '/settings/libretro',
      iconName: icons.SERIES_CONTINUING,
      title: translate('Libretro'),
      summary: translate('LibretroSettingsSummary'),
    },
  ];
}

function Settings() {
  const sections = buildSections();

  return (
    <SettingsPage title={translate('Settings')} showSave={false}>
      <PageContentBody className={styles.body}>
        <div className={styles.section}>
          <PageHeading title={translate('Settings')} />

          <div className={styles.grid}>
            {sections.map((section) => (
              <Link key={section.to} className={styles.card} to={section.to}>
                <div className={styles.cardHead}>
                  <span className={styles.cardIcon}>
                    <Icon name={section.iconName} size={16} />
                  </span>
                  <h2 className={styles.cardTitle} title={section.title}>{section.title}</h2>
                </div>
                <p className={styles.cardSummary}>{section.summary}</p>
              </Link>
            ))}
          </div>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default Settings;
