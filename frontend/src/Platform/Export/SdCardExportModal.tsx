import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import SdCardExportModalContent, {
  SdCardExportModalContentProps,
} from './SdCardExportModalContent';

export interface SdCardExportModalProps extends SdCardExportModalContentProps {
  isOpen: boolean;
}

function SdCardExportModal({
  isOpen,
  onModalClose,
  ...otherProps
}: SdCardExportModalProps) {
  return (
    <Modal isOpen={isOpen} size={sizes.LARGE} onModalClose={onModalClose}>
      {isOpen ? (
        <SdCardExportModalContent {...otherProps} onModalClose={onModalClose} />
      ) : null}
    </Modal>
  );
}

export default SdCardExportModal;
