import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { icons, kinds, sizes } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import { ImportFolder, useDeleteImportFolder } from './useImportFolders';
import styles from './ImportFolderRow.module.css';

interface ImportFolderRowProps extends ImportFolder {
  isSelectable: boolean;
}

function renderBadge(isUnavailable: boolean, isEmpty: boolean) {
  if (isUnavailable) {
    return (
      <Label kind={kinds.DANGER} dot={false}>
        {translate('Unavailable')}
      </Label>
    );
  }

  if (isEmpty) {
    return (
      <Label
        kind={kinds.WARNING}
        dot={false}
        title="Empty import source folder"
      >
        {translate('Empty')}
      </Label>
    );
  }

  return null;
}

function ImportFolderRow(props: ImportFolderRowProps) {
  const {
    id,
    path,
    accessible,
    isEmpty,
    freeSpace,
    isSelectable,
    containsRomFiles,
    unmappedFolders = [],
  } = props;

  const isUnavailable = !accessible;
  const importableCount = unmappedFolders.length;
  const hasFreeSpace = freeSpace != null && !isNaN(freeSpace);

  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const { deleteImportFolder } = useDeleteImportFolder(id);

  const handleDeletePress = useCallback(() => {
    setIsDeleteModalOpen(true);
  }, []);

  const handleDeleteModalClose = useCallback(() => {
    setIsDeleteModalOpen(false);
  }, []);

  const handleConfirmDelete = useCallback(() => {
    deleteImportFolder();
    setIsDeleteModalOpen(false);
  }, [deleteImportFolder]);

  return (
    <div
      className={classNames(
        styles.row,
        isSelectable && styles.selectable,
        isUnavailable && styles.unavailable
      )}
    >
      {isSelectable && !isUnavailable ? (
        <Link
          className={styles.underlay}
          to={`/add/import/${id}`}
          aria-label={`Import from source folder: ${path}`}
        />
      ) : null}

      <div
        className={classNames(styles.content, isSelectable && styles.overlay)}
      >
        <Icon
          className={styles.folderIcon}
          name={icons.DRIVE}
          size={20}
        />

        <div className={styles.info}>
          <span className={styles.path}>{path}</span>

          <div className={styles.meta}>
            {isUnavailable ? (
              <span>Import source folder is not accessible</span>
            ) : (
              <>
                <span className={styles.importable}>
                  {importableCount > 0
                    ? `${importableCount} platform${importableCount === 1 ? '' : 's'} to import`
                    : containsRomFiles
                    ? 'Contains Loose ROM Files'
                    : 'Nothing to import'}
                </span>

                {hasFreeSpace ? (
                  <>
                    <span className={styles.metaSep}>·</span>
                    <span className={styles.free}>
                      {translate('FreeSpace', {
                        freeSpace: formatBytes(freeSpace),
                      })}
                    </span>
                  </>
                ) : null}
              </>
            )}
          </div>
        </div>

        {renderBadge(isUnavailable, isEmpty)}

        {isSelectable || isUnavailable ? null : (
          <Button
            size={sizes.SMALL}
            to={`/add/import/${id}`}
            aria-label={`Import from source folder: ${path}`}
          >
            {translate('Select')}
          </Button>
        )}

        <IconButton
          title="Remove import source folder"
          aria-label="Remove import source folder"
          name={icons.REMOVE}
          onPress={handleDeletePress}
        />
      </div>

      <ConfirmModal
        isOpen={isDeleteModalOpen}
        kind={kinds.DANGER}
        title="Remove Import Source Folder"
        message={`Are you sure you want to remove '${path}' from the import sources list? (No files on disk will be deleted)`}
        confirmLabel={translate('Remove')}
        onConfirm={handleConfirmDelete}
        onCancel={handleDeleteModalClose}
      />
    </div>
  );
}

export default ImportFolderRow;
