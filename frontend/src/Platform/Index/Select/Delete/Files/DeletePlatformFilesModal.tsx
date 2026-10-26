import React from 'react';
import Modal from 'Components/Modal/Modal';
import DeletePlatformModalContent, {
  DeletePlatformFilesModalContentProps,
} from './DeletePlatformFilesModalContent';

interface DeletePlatformFilesModalProps
  extends DeletePlatformFilesModalContentProps {
  isOpen: boolean;
}

function DeletePlatformFilesModal(props: DeletePlatformFilesModalProps) {
  const { isOpen, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <DeletePlatformModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default DeletePlatformFilesModal;
