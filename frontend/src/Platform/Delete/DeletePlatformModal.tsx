import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import DeletePlatformModalContent, {
  DeletePlatformModalContentProps,
} from './DeletePlatformModalContent';

interface DeletePlatformModalProps extends DeletePlatformModalContentProps {
  isOpen: boolean;
}

function DeletePlatformModal({
  isOpen,
  onModalClose,
  ...otherProps
}: DeletePlatformModalProps) {
  return (
    <Modal isOpen={isOpen} size={sizes.MEDIUM} onModalClose={onModalClose}>
      <DeletePlatformModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default DeletePlatformModal;
