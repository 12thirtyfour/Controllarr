import React from 'react';
import Modal from 'Components/Modal/Modal';
import SelectGameModalContent, {
  SelectedGame,
} from './SelectGameModalContent';

interface SelectGameModalProps {
  isOpen: boolean;
  selectedIds: number[] | string[];
  seriesId?: number;
  seasonNumber?: number;
  selectedDetails?: string;
  isAnime: boolean;
  modalTitle: string;
  onGamesSelect(selectedGames: SelectedGame[]): void;
  onModalClose(): void;
}

function SelectGameModal(props: SelectGameModalProps) {
  const {
    isOpen,
    selectedIds,
    seriesId,
    seasonNumber,
    selectedDetails,
    isAnime,
    modalTitle,
    onGamesSelect,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectGameModalContent
        selectedIds={selectedIds}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        selectedDetails={selectedDetails}
        isAnime={isAnime}
        modalTitle={modalTitle}
        onGamesSelect={onGamesSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectGameModal;
