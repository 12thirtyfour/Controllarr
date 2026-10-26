import React, { useCallback, useMemo } from 'react';
import {
  AddPlatformOptions,
  setAddPlatformOption,
  useAddPlatformOptions,
} from 'AddPlatform/addPlatformOptionsStore';
import EnhancedSelectInput, {
  EnhancedSelectInputValue,
} from 'Components/Form/Select/EnhancedSelectInput';
import MonitorGamesSelectInput from 'Components/Form/Select/MonitorGamesSelectInput';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import Language from 'Language/Language';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './ImportPlatformDefaults.module.css';

const COLLECTION_TYPE_OPTIONS: EnhancedSelectInputValue<string>[] = [
  { key: 'oneGameOneRom', value: '1G1R (Per Game)' },
  { key: 'oneGamePerRegionPerConsole', value: '1G1R (Per Region)' },
  { key: 'preservationist', value: 'Completionist (All Revisions)' },
];

interface ImportPlatformDefaultsProps {
  isApplyDisabled: boolean;
  onApplyDefaults: () => void;
}

function ImportPlatformDefaults({
  isApplyDisabled,
  onApplyDefaults,
}: ImportPlatformDefaultsProps) {
  const {
    monitor,
    collectionStrategy = 'oneGameOneRom',
    enableCompression = true,
  } = useAddPlatformOptions();

  const compressionOptions: EnhancedSelectInputValue<boolean>[] = useMemo(
    () => [
      { key: true, value: 'Enabled (Auto Format)' },
      { key: false, value: 'Disabled (Uncompressed)' },
    ],
    []
  );

  const handleInputChange = useCallback(
    ({
      name,
      value,
    }: InputChanged<string | number | boolean | number[] | Language>) => {
      setAddPlatformOption(name as keyof AddPlatformOptions, value);
    },
    []
  );

  return (
    <div className={styles.defaultsBar}>
      <div className={styles.defaultsContent}>
        <div className={styles.defaultsIdentity}>
          <div className={styles.defaultsTitle}>{translate('Defaults')}</div>
        </div>

        <div className={styles.setting}>
          <div className={styles.settingLabel}>{translate('Monitor')}</div>
          <MonitorGamesSelectInput
            name="monitor"
            value={monitor}
            modalTitle={translate('Monitor')}
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
            onChange={handleInputChange}
          />
        </div>

        <Button
          className={styles.applyButton}
          kind={kinds.DEFAULT}
          isDisabled={isApplyDisabled}
          onPress={onApplyDefaults}
        >
          {translate('ApplyToSelected')}
        </Button>
      </div>
    </div>
  );
}

export default ImportPlatformDefaults;
