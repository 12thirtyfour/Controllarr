import React from 'react';
import Modal from 'Components/Modal/Modal';
import PlatformIndexOverviewOptionsModalContent from './PlatformIndexOverviewOptionsModalContent';

interface PlatformIndexOverviewOptionsModalProps {
  isOpen: boolean;
  onModalClose(...args: unknown[]): void;
}

function PlatformIndexOverviewOptionsModal({
  isOpen,
  onModalClose,
  ...otherProps
}: PlatformIndexOverviewOptionsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <PlatformIndexOverviewOptionsModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default PlatformIndexOverviewOptionsModal;
