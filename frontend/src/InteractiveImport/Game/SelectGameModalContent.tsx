import React, { useCallback, useMemo, useState } from 'react';
import { SelectProvider, useSelect } from 'App/Select/SelectContext';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Scroller from 'Components/Scroller/Scroller';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Game from 'Game/Game';
import {
  setGameSelectionSort,
  useGameSelectionOptions,
} from 'Game/gameSelectionOptionsStore';
import useGames from 'Game/useGames';
import { kinds, scrollDirections } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import clientSideFilterAndSort from 'Utilities/Filter/clientSideFilterAndSort';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import SelectGameRow from './SelectGameRow';
import styles from './SelectGameModalContent.module.css';

const columns = [
  {
    name: 'gameNumber',
    label: '#',
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'title',
    label: () => translate('Title'),
    isVisible: true,
  },
  {
    name: 'airDate',
    label: () => translate('AirDate'),
    isVisible: true,
  },
];

export interface SelectedGame {
  id: number;
  games: Game[];
}

interface SelectGameModalContentProps {
  selectedIds: number[] | string[];
  seriesId?: number;
  seasonNumber?: number;
  selectedDetails?: string;
  isAnime: boolean;
  modalTitle: string;
  onGamesSelect(selectedGames: SelectedGame[]): unknown;
  onModalClose(): unknown;
}

function SelectGameModalContentInner(props: SelectGameModalContentProps) {
  const {
    selectedIds,
    seriesId,
    seasonNumber,
    selectedDetails,
    isAnime,
    modalTitle,
    onGamesSelect,
    onModalClose,
  } = props;

  const [filter, setFilter] = useState('');

  const { isFetching, isFetched, data, error } = useGames({
    seriesId,
    seasonNumber,
    isSelection: true,
  });

  const { sortKey, sortDirection } = useGameSelectionOptions();

  const {
    allSelected,
    allUnselected,
    selectedCount: selectedGamesCount,
    getSelectedIds,
    selectAll,
    unselectAll,
  } = useSelect<Game>();

  const filterGameNumber = parseInt(filter);
  const errorMessage = getErrorMessage(error, translate('GamesLoadError'));
  const selectedCount = selectedIds.length;
  const selectionIsValid =
    selectedGamesCount > 0 && selectedGamesCount % selectedCount === 0;

  const onFilterChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setFilter(value.toLowerCase());
    },
    [setFilter]
  );

  const onSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      if (value) {
        selectAll();
      } else {
        unselectAll();
      }
    },
    [selectAll, unselectAll]
  );

  const onSortPress = useCallback(
    (newSortKey: string, newSortDirection?: SortDirection) => {
      setGameSelectionSort({
        sortKey: newSortKey,
        sortDirection: newSortDirection,
      });
    },
    []
  );

  const onGamesSelectWrapper = useCallback(() => {
    const gameIds: number[] = getSelectedIds();

    const selectedGames = data.reduce((acc: Game[], item) => {
      if (gameIds.indexOf(item.id) > -1) {
        acc.push(item);
      }

      return acc;
    }, []);

    const gamesPerFile = selectedGames.length / selectedIds.length;
    const sortedGames = selectedGames.sort((a, b) => {
      return a.seasonNumber - b.seasonNumber;
    });

    const mappedGames = selectedIds.map((id, index): SelectedGame => {
      const startingIndex = index * gamesPerFile;
      const games = sortedGames.slice(
        startingIndex,
        startingIndex + gamesPerFile
      );

      return {
        id: id as number,
        games,
      };
    });

    onGamesSelect(mappedGames);
  }, [selectedIds, data, getSelectedIds, onGamesSelect]);

  let details = selectedDetails;

  if (!details) {
    details =
      selectedCount > 1
        ? translate('CountSelectedFiles', { selectedCount })
        : translate('CountSelectedFile', { selectedCount });
  }

  const { data: items } = useMemo(() => {
    return clientSideFilterAndSort<Game>(data, {
      sortKey,
      sortDirection,
    });
  }, [data, sortKey, sortDirection]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('SelectGamesModalTitle', { modalTitle })}
      </ModalHeader>

      <ModalBody
        className={styles.modalBody}
        scrollDirection={scrollDirections.NONE}
      >
        <TextInput
          className={styles.filterInput}
          placeholder={translate('FilterGamesPlaceholder')}
          name="filter"
          value={filter}
          autoFocus={true}
          onChange={onFilterChange}
        />

        <Scroller className={styles.scroller} autoFocus={false}>
          {isFetching ? <LoadingIndicator /> : null}

          {error ? <div>{errorMessage}</div> : null}

          {isFetched && !!items.length ? (
            <Table
              columns={columns}
              selectAll={true}
              allSelected={allSelected}
              allUnselected={allUnselected}
              sortKey={sortKey}
              sortDirection={sortDirection}
              onSortPress={onSortPress}
              onSelectAllChange={onSelectAllChange}
            >
              <TableBody>
                {items.map((item) => {
                  return item.title.toLowerCase().includes(filter) ||
                    item.gameNumber === filterGameNumber ? (
                    <SelectGameRow
                      key={item.id}
                      id={item.id}
                      gameNumber={item.gameNumber}
                      absoluteGameNumber={item.absoluteGameNumber}
                      title={item.title}
                      airDate={item.airDate}
                      isAnime={isAnime}
                      unverifiedSceneNumbering={item.unverifiedSceneNumbering}
                    />
                  ) : null;
                })}
              </TableBody>
            </Table>
          ) : null}

          {isFetched && !data.length
            ? translate('NoGamesFoundForSelectedSeason')
            : null}
        </Scroller>
      </ModalBody>

      <ModalFooter className={styles.footer}>
        <div className={styles.details}>{details}</div>

        <div className={styles.buttons}>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <Button
            kind={kinds.SUCCESS}
            isDisabled={!selectionIsValid}
            onPress={onGamesSelectWrapper}
          >
            {translate('SelectGames')}
          </Button>
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

function SelectGameModalContent(props: SelectGameModalContentProps) {
  const { data } = useGames({
    seriesId: props.seriesId,
    seasonNumber: props.seasonNumber,
    isSelection: true,
  });

  return (
    <SelectProvider items={data}>
      <SelectGameModalContentInner {...props} />
    </SelectProvider>
  );
}

export default SelectGameModalContent;
