import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import TableRowButton from 'Components/Table/TableRowButton';
import Game from 'Game/Game';
import { SelectStateInputProps } from 'typings/props';


interface SelectGameRowProps {
  id: number;
  gameNumber: number;
  absoluteGameNumber: number | undefined;
  title: string;
  airDate: string;
  isAnime: boolean;
  isSelected?: boolean;
}

function SelectGameRow({
  id,
  title,
  airDate,

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
  return (
    <TableRowButton onPress={handlePress}>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={handleSelectedChange}
      />

      <TableRowCell>{title}</TableRowCell>

      <TableRowCell>{airDate}</TableRowCell>
    </TableRowButton>
  );
}

export default SelectGameRow;
