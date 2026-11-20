import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import RegionInteractiveSearchModalContent, {
  RegionInteractiveSearchModalContentProps,
} from './RegionInteractiveSearchModalContent';

interface RegionInteractiveSearchModalProps
  extends RegionInteractiveSearchModalContentProps {
  isOpen: boolean;
}

function RegionInteractiveSearchModal(
  props: RegionInteractiveSearchModalProps
) {
  const { isOpen, gameCount, seriesId, seasonNumber, onModalClose } = props;

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      closeOnBackgroundClick={false}
      onModalClose={onModalClose}
    >
      <RegionInteractiveSearchModalContent
        gameCount={gameCount}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default RegionInteractiveSearchModal;
