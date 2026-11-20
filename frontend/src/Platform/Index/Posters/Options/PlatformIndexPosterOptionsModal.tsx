import React from 'react';
import Modal from 'Components/Modal/Modal';
import PlatformIndexPosterOptionsModalContent from './PlatformIndexPosterOptionsModalContent';

interface PlatformIndexPosterOptionsModalProps {
  isOpen: boolean;
  onModalClose(...args: unknown[]): unknown;
}

function PlatformIndexPosterOptionsModal({
  isOpen,
  onModalClose,
}: PlatformIndexPosterOptionsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <PlatformIndexPosterOptionsModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default PlatformIndexPosterOptionsModal;
