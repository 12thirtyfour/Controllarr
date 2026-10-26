import React from 'react';
import Modal from 'Components/Modal/Modal';
import OrganizePlatformModalContent, {
  OrganizePlatformModalContentProps,
} from './OrganizePlatformModalContent';

interface OrganizePlatformModalProps extends OrganizePlatformModalContentProps {
  isOpen: boolean;
}

function OrganizePlatformModal(props: OrganizePlatformModalProps) {
  const { isOpen, onModalClose, ...otherProps } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <OrganizePlatformModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default OrganizePlatformModal;
