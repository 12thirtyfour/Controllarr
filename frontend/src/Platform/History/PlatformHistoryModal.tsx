import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import PlatformHistoryModalContent, {
  PlatformHistoryModalContentProps,
} from './PlatformHistoryModalContent';

interface PlatformHistoryModalProps extends PlatformHistoryModalContentProps {
  isOpen: boolean;
}

function PlatformHistoryModal({
  isOpen,
  onModalClose,
  ...otherProps
}: PlatformHistoryModalProps) {
  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      onModalClose={onModalClose}
    >
      <PlatformHistoryModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default PlatformHistoryModal;
