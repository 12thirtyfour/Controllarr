import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import TableRowButton from 'Components/Table/TableRowButton';
import Game from 'Game/Game';
import { icons, kinds } from 'Helpers/Props';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import Icon from '../../Components/Icon';
import styles from './SelectGameRow.module.css';

function getWarningMessage(
  unverifiedSceneNumbering: boolean,
  isAnime: boolean,
  absoluteGameNumber: number | undefined
) {
  const messages = [];

  if (unverifiedSceneNumbering) {
    messages.push(translate('SceneNumberNotVerified'));
  }

  if (isAnime && !absoluteGameNumber) {
    messages.push(translate('GameMissingAbsoluteNumber'));
  }

  return messages.join('\n');
}

interface SelectGameRowProps {
  id: number;
  gameNumber: number;
  absoluteGameNumber: number | undefined;
  title: string;
  airDate: string;
  isAnime: boolean;
  isSelected?: boolean;
  unverifiedSceneNumbering?: boolean;
}

function SelectGameRow({
  id,
  gameNumber,
  absoluteGameNumber,
  title,
  airDate,
  isAnime,
  unverifiedSceneNumbering = false,
}: SelectGameRowProps) {
  const { toggleSelected, useIsSelected } = useSelect<Game>();
  const isSelected = useIsSelected(id);

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [toggleSelected]
  );

  const handlePress = useCallback(() => {
    handleSelectedChange({ id, value: !isSelected, shiftKey: false });
  }, [id, isSelected, handleSelectedChange]);

  const warningMessage = getWarningMessage(
    unverifiedSceneNumbering,
    isAnime,
    absoluteGameNumber
  );

  return (
    <TableRowButton onPress={handlePress}>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={handleSelectedChange}
      />

      <TableRowCell>
        {gameNumber}
        {isAnime && !!absoluteGameNumber
          ? ` (${absoluteGameNumber})`
          : ''}
        {warningMessage ? (
          <Icon
            className={styles.warning}
            name={icons.WARNING}
            kind={kinds.WARNING}
            title={warningMessage}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell>{title}</TableRowCell>

      <TableRowCell>{airDate}</TableRowCell>
    </TableRowButton>
  );
}

export default SelectGameRow;
