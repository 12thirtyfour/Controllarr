import React, { useRef } from 'react';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { scrollDirections } from 'Helpers/Props';
import InteractiveSearch from 'InteractiveSearch/InteractiveSearch';
import { useClearReleasesOnUnmount } from 'InteractiveSearch/useReleases';
import formatSeason from 'Region/formatRegion';
import translate from 'Utilities/String/translate';
import styles from './RegionInteractiveSearchModalContent.module.css';

export interface RegionInteractiveSearchModalContentProps {
  gameCount: number;
  seriesId: number;
  seasonNumber: number;
  onModalClose(): void;
}

function RegionInteractiveSearchModalContent({
  gameCount,
  seriesId,
  seasonNumber,
  onModalClose,
}: RegionInteractiveSearchModalContentProps) {
  useClearReleasesOnUnmount({ seriesId, seasonNumber });
  const modalBodyRef = useRef<HTMLDivElement>(null);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {seasonNumber === null
          ? translate('InteractiveSearchModalHeader')
          : translate('InteractiveSearchModalHeaderSeason', {
              season: formatSeason(seasonNumber) as string,
            })}
      </ModalHeader>

      <ModalBody ref={modalBodyRef} scrollDirection={scrollDirections.BOTH}>
        <InteractiveSearch
          type="season"
          searchPayload={{
            seriesId,
            seasonNumber,
          }}
          scrollerRef={modalBodyRef}
        />
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <div>
          {translate('GamesInSeason', {
            gameCount,
          })}
        </div>

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default RegionInteractiveSearchModalContent;
