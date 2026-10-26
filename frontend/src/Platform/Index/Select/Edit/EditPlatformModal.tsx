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
  onSavePress,
  onModalClose,
}: EditPlatformModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <EditPlatformModalContent
        onSavePress={onSavePress}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default EditPlatformModal;
