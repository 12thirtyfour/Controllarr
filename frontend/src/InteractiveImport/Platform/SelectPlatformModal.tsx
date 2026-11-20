import React from 'react';
import Modal from 'Components/Modal/Modal';
import Series from 'Platform/Platform';
import SelectPlatformModalContent from './SelectPlatformModalContent';

interface SelectPlatformModalProps {
  isOpen: boolean;
  modalTitle: string;
  onSeriesSelect(series: Series): void;
  onModalClose(): void;
}

function SelectPlatformModal(props: SelectPlatformModalProps) {
  const { isOpen, modalTitle, onSeriesSelect, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectPlatformModalContent
        modalTitle={modalTitle}
        onSeriesSelect={onSeriesSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectPlatformModal;
