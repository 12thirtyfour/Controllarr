import React, { useCallback, useEffect, useRef, useState } from 'react';
import ReactDOM from 'react-dom';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import AdvancedSettingsButton from './AdvancedSettingsButton';

interface SettingsSaveItemsProps {
  showSave?: boolean;
  isSaving?: boolean;
  hasPendingChanges?: boolean;
  onSavePress?: () => void;
}

function SettingsSaveItems({
  showSave = true,
  isSaving = false,
  hasPendingChanges = false,
  onSavePress,
}: SettingsSaveItemsProps) {
  const [showSavedToast, setShowSavedToast] = useState(false);
  const wasSavingRef = useRef(false);
  const timerRef = useRef<number | null>(null);

  const triggerSavedFeedback = useCallback(() => {
    setShowSavedToast(true);
    if (timerRef.current) {
      window.clearTimeout(timerRef.current);
    }
    timerRef.current = window.setTimeout(() => {
      setShowSavedToast(false);
    }, 2600);
  }, []);

  useEffect(() => {
    if (wasSavingRef.current && !isSaving && !hasPendingChanges) {
      triggerSavedFeedback();
    }
    wasSavingRef.current = isSaving;
  }, [isSaving, hasPendingChanges, triggerSavedFeedback]);

  useEffect(() => {
    return () => {
      if (timerRef.current) {
        window.clearTimeout(timerRef.current);
      }
    };
  }, []);

  const handlePress = useCallback(() => {
    onSavePress?.();
    triggerSavedFeedback();
  }, [onSavePress, triggerSavedFeedback]);

  return (
    <>
      <ToolbarItem id="advanced-settings" pinned={true}>
        <AdvancedSettingsButton showLabel={true} />
      </ToolbarItem>

      {showSave ? (
        <ToolbarItem
          id="save"
          priority={1}
          groupId="save"
          label={
            hasPendingChanges
              ? translate('SaveChanges')
              : showSavedToast
                ? 'Saved!'
                : translate('NoChanges')
          }
          iconName={
            showSavedToast && !hasPendingChanges ? icons.CHECK : icons.SAVE
          }
          isSpinning={isSaving}
          isDisabled={!hasPendingChanges}
          onPress={handlePress}
        />
      ) : null}

      {showSavedToast && typeof document !== 'undefined'
        ? ReactDOM.createPortal(
            <div
              style={{
                position: 'fixed',
                bottom: '24px',
                right: '24px',
                zIndex: 9999,
                display: 'flex',
                alignItems: 'center',
                gap: '10px',
                padding: '12px 18px',
                borderRadius: '8px',
                backgroundColor: 'var(--successBackgroundColor, #28a745)',
                color: '#fff',
                fontWeight: 600,
                fontSize: '14px',
                boxShadow: '0 8px 24px rgba(0, 0, 0, 0.35)',
                pointerEvents: 'none',
              }}
            >
              <span>✓</span>
              <span>Settings saved successfully</span>
            </div>,
            document.body
          )
        : null}
    </>
  );
}

export default SettingsSaveItems;
