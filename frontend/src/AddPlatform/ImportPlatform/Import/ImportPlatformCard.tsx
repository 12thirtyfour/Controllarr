import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CheckInput from 'Components/Form/CheckInput';
import EnhancedSelectInput, {
  EnhancedSelectInputValue,
} from 'Components/Form/Select/EnhancedSelectInput';
import MonitorGamesSelectInput from 'Components/Form/Select/MonitorGamesSelectInput';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import PlatformPoster from 'Platform/PlatformPoster';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import { ImportPlatformEligibility } from './importPlatformEligibility';
import {
  ImportPlatformItem,
  UnamppedFolderItem,
  updateImportPlatformItem,
  useImportPlatformItem,
} from './importPlatformStore';
import ImportPlatformSelectSeries from './SelectPlatform/ImportPlatformSelectPlatform';
import styles from './ImportPlatformCard.module.css';

const COLLECTION_TYPE_OPTIONS: EnhancedSelectInputValue<string>[] = [
  { key: 'oneGameOneRom', value: '1G1R (Per Game)' },
  { key: 'oneGamePerRegionPerConsole', value: '1G1R (Per Region)' },
  { key: 'preservationist', value: 'Completionist (All Revisions)' },
];

function getMatchStatusLabel(eligibility: ImportPlatformEligibility) {
  switch (eligibility) {
    case 'existing':
      return translate('AlreadyInLibrary');
    case 'duplicate':
      return translate('DuplicateMatch');
    case 'unmatched':
      return translate('Pending');
    case 'ready':
    default:
      return null;
  }
}

interface ImportPlatformCardProps {
  unmappedFolder: UnamppedFolderItem;
  duplicateIds: string[];
  eligibility: ImportPlatformEligibility;
  isCompact: boolean;
  isStacked: boolean;
}

function ImportPlatformCard({
  unmappedFolder,
  duplicateIds,
  eligibility,
  isCompact,
  isStacked,
}: ImportPlatformCardProps) {
  const id = unmappedFolder.id;

  const item = useImportPlatformItem(unmappedFolder.id);

  const {
    relativePath,
    monitor,
    collectionStrategy = 'oneGameOneRom',
    enableCompression = true,
    selectedSeries,
  } = item ?? {};

  const { setItemStates, toggleSelected, useIsSelected } =
    useSelect<ImportPlatformItem>();
  const isSelected = useIsSelected(id);
  const isBlocked = eligibility === 'unmatched' || eligibility === 'existing';
  const needsAttention =
    isBlocked || (eligibility === 'duplicate' && !isSelected);
  const matchStatusLabel = getMatchStatusLabel(eligibility);

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      updateImportPlatformItem({ id, [name]: value });
    },
    [id]
  );

  const handleSelectedChange = useCallback(
    ({ value, shiftKey }: CheckInputChanged) => {
      if (eligibility === 'duplicate') {
        setItemStates(
          value
            ? duplicateIds.map((duplicateId) => ({
                id: duplicateId,
                isSelected: duplicateId === id,
              }))
            : [{ id, isSelected: false }]
        );

        return;
      }

      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [id, duplicateIds, eligibility, setItemStates, toggleSelected]
  );

  const [isSearching, setIsSearching] = useState(false);

  const compressionOptions: EnhancedSelectInputValue<boolean>[] = useMemo(
    () => [
      { key: true, value: 'Enabled (Auto Format)' },
      { key: false, value: 'Disabled (Uncompressed)' },
    ],
    []
  );

  return (
    <div
      className={classNames(
        styles.rowLayout,
        isCompact && styles.compact,
        isStacked && styles.stacked,
        needsAttention && styles.needsAttention,
        selectedSeries && styles.matched,
        isSearching && styles.searching
      )}
    >
      <CheckInput
        className={styles.selectInput}
        containerClassName={styles.selectContainer}
        name={id}
        ariaLabel={translate('SelectRow')}
        value={isSelected}
        isDisabled={isBlocked}
        onChange={handleSelectedChange}
      />

      <div className={styles.poster}>
        {selectedSeries ? (
          <PlatformPoster
            title={selectedSeries.title}
            images={selectedSeries.images}
            size={250}
            lazy={false}
            overflow={true}
          />
        ) : (
          <div className={styles.posterPlaceholder}>?</div>
        )}
      </div>

      <div
        className={classNames(
          styles.seriesInfo,
          needsAttention && styles.attentionInfo
        )}
      >
        <div className={styles.identity}>
          {selectedSeries ? (
            <>
              <div className={styles.titleRow}>
                <ImportPlatformSelectSeries
                  id={id}
                  onInputChange={handleInputChange}
                  onEditingChange={setIsSearching}
                />
              </div>

              <div className={styles.source}>
                <span className={styles.folder}>{relativePath}</span>
                {selectedSeries.network ? ` · ${selectedSeries.network}` : null}
              </div>
            </>
          ) : (
            <>
              <div className={styles.folderName}>{relativePath}</div>
              <div className={styles.source}>
                <span className={styles.folder}>{unmappedFolder.path}</span>
              </div>
            </>
          )}
        </div>

        {needsAttention && matchStatusLabel ? (
          <>
            <Label
              className={classNames(
                styles.matchStatus,
                !selectedSeries && styles.matchStatusStackedHidden
              )}
              kind={kinds.WARNING}
            >
              {matchStatusLabel}
            </Label>

            {selectedSeries ? null : (
              <div className={styles.matchAction}>
                <ImportPlatformSelectSeries
                  id={id}
                  onInputChange={handleInputChange}
                  onEditingChange={setIsSearching}
                />
              </div>
            )}
          </>
        ) : (
          <div className={styles.settingsRow}>
            <div className={styles.setting}>
              <div className={styles.settingLabel}>{translate('Monitor')}</div>

              <MonitorGamesSelectInput
                name="monitor"
                value={monitor}
                modalTitle={translate('Monitor')}
                isDisabled={!selectedSeries}
                onChange={handleInputChange}
              />
            </div>

            <div className={styles.setting}>
              <div className={styles.settingLabel}>Collection Type</div>

              <EnhancedSelectInput
                name="collectionStrategy"
                value={collectionStrategy}
                modalTitle="Collection Type"
                values={COLLECTION_TYPE_OPTIONS}
                isDisabled={!selectedSeries}
                onChange={handleInputChange}
              />
            </div>

            <div className={styles.setting}>
              <div className={styles.settingLabel}>Compression</div>

              <EnhancedSelectInput
                name="enableCompression"
                value={enableCompression}
                modalTitle="Compression"
                values={compressionOptions}
                isDisabled={!selectedSeries}
                onChange={handleInputChange}
              />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default ImportPlatformCard;
