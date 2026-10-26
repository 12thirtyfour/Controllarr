import React, { useMemo } from 'react';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { Season } from 'Platform/Platform';
import { useSingleSeries } from 'Platform/usePlatform';
import translate from 'Utilities/String/translate';
import SelectRegionRow from './SelectRegionRow';

interface SelectRegionModalContentProps {
  seriesId?: number;
  modalTitle: string;
  onSeasonSelect(seasonNumber: number): void;
  onModalClose(): void;
}

function SelectRegionModalContent(props: SelectRegionModalContentProps) {
  const { seriesId, modalTitle, onSeasonSelect, onModalClose } = props;
  const series = useSingleSeries(seriesId);
  const seasons = useMemo<Season[]>(() => {
    return series?.seasons.slice(0).reverse() || [];
  }, [series]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('SelectRegionModalTitle', { modalTitle })}
      </ModalHeader>

      <ModalBody>
        {seasons.map((item) => {
          return (
            <SelectRegionRow
              key={item.seasonNumber}
              seasonNumber={item.seasonNumber}
              onSeasonSelect={onSeasonSelect}
            />
          );
        })}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectRegionModalContent;
