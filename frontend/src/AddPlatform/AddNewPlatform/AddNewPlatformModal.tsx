import React from 'react';
import Modal from 'Components/Modal/Modal';
import AddNewPlatformModalContent, {
  AddNewPlatformModalContentProps,
} from './AddNewPlatformModalContent';

interface AddNewPlatformModalProps extends AddNewPlatformModalContentProps {
  isOpen: boolean;
}

function AddNewPlatformModal({
  isOpen,
  onModalClose,
  ...otherProps
}: AddNewPlatformModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <AddNewPlatformModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default AddNewPlatformModal;
