import React from 'react';
import Modal from 'Components/Modal/Modal';
import AddAllPlatformsModalContent, {
  AddAllPlatformsModalContentProps,
} from './AddAllPlatformsModalContent';

interface AddAllPlatformsModalProps extends AddAllPlatformsModalContentProps {
  isOpen: boolean;
}

function AddAllPlatformsModal({
  isOpen,
  onModalClose,
}: AddAllPlatformsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <AddAllPlatformsModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default AddAllPlatformsModal;
