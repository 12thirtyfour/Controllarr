import React from 'react';
import Modal from 'Components/Modal/Modal';
import DeletePlatformModalContent, {
  DeletePlatformModalContentProps,
} from './DeletePlatformModalContent';

interface DeletePlatformModalProps extends DeletePlatformModalContentProps {
  isOpen: boolean;
}

function DeletePlatformModal(props: DeletePlatformModalProps) {
  const { isOpen, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <DeletePlatformModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default DeletePlatformModal;
