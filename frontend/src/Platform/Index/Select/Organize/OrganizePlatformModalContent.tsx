import { orderBy } from 'lodash';
import React, { useCallback, useMemo } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useExecuteCommand } from 'Commands/useCommands';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, kinds } from 'Helpers/Props';
import Series from 'Platform/Platform';
import usePlatform from 'Platform/usePlatform';
import translate from 'Utilities/String/translate';
import styles from './OrganizePlatformModalContent.module.css';

export interface OrganizePlatformModalContentProps {
  onModalClose: () => void;
}

function OrganizePlatformModalContent({
  onModalClose,
}: OrganizePlatformModalContentProps) {
  const { data: allSeries } = usePlatform();
  const executeCommand = useExecuteCommand();
  const { useSelectedIds } = useSelect<Series>();
  const seriesIds = useSelectedIds();

  const platformTitles = useMemo(() => {
    const series = seriesIds.reduce((acc: Series[], id) => {
      const s = allSeries.find((s) => s.id === id);

      if (s) {
        acc.push(s);
      }

      return acc;
    }, []);

    const sorted = orderBy(series, ['sortTitle']);

    return sorted.map((s) => s.title);
  }, [allSeries, seriesIds]);

  const onOrganizePress = useCallback(() => {
    executeCommand({
      name: CommandNames.RenameSeries,
      seriesIds,
    });

    onModalClose();
  }, [seriesIds, onModalClose, executeCommand]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('OrganizeSelectedSeriesModalHeader')}
      </ModalHeader>

      <ModalBody>
        <p className={styles.intro}>
          {translate('OrganizeSelectedSeriesModalAlert')}
          <Icon className={styles.renameIcon} name={icons.ORGANIZE} />
        </p>

        <div className={styles.message}>
          {translate('OrganizeSelectedSeriesModalConfirmation', {
            count: platformTitles.length,
          })}
        </div>

        <ul>
          {platformTitles.map((title) => {
            return <li key={title}>{title}</li>;
          })}
        </ul>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.DANGER} onPress={onOrganizePress}>
          {translate('Organize')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default OrganizePlatformModalContent;
