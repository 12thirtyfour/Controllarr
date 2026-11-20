import React from 'react';
import Modal from 'Components/Modal/Modal';
import SelectRegionModalContent from './SelectRegionModalContent';

interface SelectRegionModalProps {
  isOpen: boolean;
  modalTitle: string;
  seriesId?: number;
  onSeasonSelect(seasonNumber: number): void;
  onModalClose(): void;
}

function SelectRegionModal(props: SelectRegionModalProps) {
  const { isOpen, modalTitle, seriesId, onSeasonSelect, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectRegionModalContent
        modalTitle={modalTitle}
        seriesId={seriesId}
        onSeasonSelect={onSeasonSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectRegionModal;
