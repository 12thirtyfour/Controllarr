import React, { useCallback, useEffect, useState } from 'react';
import PlatformMonitoringOptionsPopoverContent from 'AddPlatform/PlatformMonitoringOptionsPopoverContent';
import Alert from 'Components/Alert';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import { useUpdateSeriesMonitor } from 'Platform/usePlatform';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './MonitoringOptionsModalContent.module.css';

const NO_CHANGE = 'noChange';

export interface MonitoringOptionsModalContentProps {
  seriesId: number;
  onModalClose: () => void;
}

function MonitoringOptionsModalContent({
  seriesId,
  onModalClose,
}: MonitoringOptionsModalContentProps) {
  const {
    updateSeriesMonitor,
    isUpdatingSeriesMonitor,
    updateSeriesMonitorError,
  } = useUpdateSeriesMonitor(true);

  const [monitor, setMonitor] = useState(NO_CHANGE);
  const wasSaving = usePrevious(isUpdatingSeriesMonitor);

  const handleMonitorChange = useCallback(({ value }: InputChanged<string>) => {
    setMonitor(value);
  }, []);

  const handleSavePress = useCallback(() => {
    if (monitor === NO_CHANGE) {
      return;
    }

    updateSeriesMonitor({
      series: [
        {
          id: seriesId,
        },
      ],
      monitoringOptions: { monitor },
    });
  }, [monitor, seriesId, updateSeriesMonitor]);

  useEffect(() => {
    if (!isUpdatingSeriesMonitor && wasSaving && !updateSeriesMonitorError) {
      onModalClose();
    }
  }, [
    isUpdatingSeriesMonitor,
    wasSaving,
    updateSeriesMonitorError,
    onModalClose,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('MonitorGames')}</ModalHeader>

      <ModalBody>
        <Alert className={styles.info} kind={kinds.INFO}>
          {translate('MonitorGamesModalInfo')}
        </Alert>

        <Form>
          <FormRow>
            <FormLabel>
              {translate('Monitoring')}

              <Popover
                anchor={<Icon className={styles.labelIcon} name={icons.INFO} />}
                title={translate('MonitoringOptions')}
                body={<PlatformMonitoringOptionsPopoverContent />}
                position={tooltipPositions.RIGHT}
              />
            </FormLabel>

            <FormInput
              type="monitorGamesSelect"
              name="monitor"
              value={monitor}
              includeNoChange={true}
              onChange={handleMonitorChange}
            />
          </FormRow>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerButton
          isSpinning={isUpdatingSeriesMonitor}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default MonitoringOptionsModalContent;
