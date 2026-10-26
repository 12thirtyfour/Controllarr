import React from 'react';
import Modal from 'Components/Modal/Modal';
import EditPlatformModalContent, {
  EditPlatformModalContentProps,
} from './EditPlatformModalContent';

interface EditPlatformModalProps extends EditPlatformModalContentProps {
  isOpen: boolean;
}

function EditPlatformModal({
  isOpen,
  onModalClose,
  ...otherProps
}: EditPlatformModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <EditPlatformModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default EditPlatformModal;
