import React, { useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Label from 'Components/Label';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { invalidateRetroAchievementsCache } from 'Platform/Details/gameAchievementsHelper';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './RetroAchievementsSettings.module.css';

export interface RetroAchievementsCredentialsRequest {
  username: string;
  webApiKey: string;
}

const STORAGE_USERNAME_KEY = 'controllarr_ra_username';
const STORAGE_APIKEY_KEY = 'controllarr_ra_apikey';

function RetroAchievementsSettings() {
  const [username, setUsername] = useState<string>(() => {
    return window.localStorage.getItem(STORAGE_USERNAME_KEY) ?? '';
  });
  const [webApiKey, setWebApiKey] = useState<string>(() => {
    return window.localStorage.getItem(STORAGE_APIKEY_KEY) ?? '';
  });
  const [validationStatus, setValidationStatus] = useState<
    'idle' | 'valid' | 'invalid'
  >(() => {
    const savedUser = window.localStorage.getItem(STORAGE_USERNAME_KEY);
    const savedKey = window.localStorage.getItem(STORAGE_APIKEY_KEY);
    return savedUser && savedKey ? 'valid' : 'idle';
  });

  const {
    mutate: validateCredentials,
    isPending: isValidating,
    error: validationError,
  } = useApiMutation<boolean, RetroAchievementsCredentialsRequest>({
    path: '/retroachievements/credentials',
    method: 'POST',
    mutationOptions: {
      onSuccess: (isValid) => {
        if (isValid) {
          window.localStorage.setItem(STORAGE_USERNAME_KEY, username.trim());
          window.localStorage.setItem(STORAGE_APIKEY_KEY, webApiKey.trim());
          invalidateRetroAchievementsCache();
          setValidationStatus('valid');
        } else {
          setValidationStatus('invalid');
        }
      },
      onError: () => {
        setValidationStatus('invalid');
      },
    },
  });

  const handleUsernameChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setUsername(value ?? '');
      setValidationStatus('idle');
    },
    []
  );

  const handleApiKeyChange = useCallback(({ value }: InputChanged<string>) => {
    setWebApiKey(value ?? '');
    setValidationStatus('idle');
  }, []);

  const handleTestAndSavePress = useCallback(() => {
    validateCredentials({
      username: username.trim(),
      webApiKey: webApiKey.trim(),
    });
  }, [username, webApiKey, validateCredentials]);

  return (
    <SettingsPage
      title={translate('RetroAchievementsSettings')}
      showSave={false}
    >
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('RetroAchievements')}
          />

          <div className={styles.card}>
            <Form>
              <FormRow size={sizes.MEDIUM}>
                <FormLabel>{translate('Username')}</FormLabel>
                <FormInputHelpText
                  text={translate('RetroAchievementsUsernameHelpText')}
                />
                <FormInput
                  type={inputTypes.TEXT}
                  name="username"
                  value={username}
                  placeholder="RetroAchievements username"
                  onChange={handleUsernameChange}
                />
              </FormRow>

              <FormRow size={sizes.MEDIUM}>
                <FormLabel>{translate('RetroAchievementsWebApiKey')}</FormLabel>
                <FormInputHelpText
                  text={translate('RetroAchievementsWebApiKeyHelpText')}
                />
                <FormInput
                  type={inputTypes.PASSWORD}
                  name="webApiKey"
                  value={webApiKey}
                  placeholder="16-64 character alphanumeric Web API Key"
                  onChange={handleApiKeyChange}
                />
              </FormRow>

              <div className={styles.actions}>
                <SpinnerErrorButton
                  isSpinning={isValidating}
                  error={validationError}
                  disabled={!username.trim() || !webApiKey.trim()}
                  onPress={handleTestAndSavePress}
                >
                  Test &amp; Save Credentials
                </SpinnerErrorButton>
              </div>
            </Form>

            {validationStatus === 'valid' ? (
              <div className={styles.statusAlert}>
                <Alert kind={kinds.SUCCESS}>
                  <strong>Connected as {username.trim()}!</strong>{' '}
                  {translate('RetroAchievementsCredentialsValid')}
                  <div style={{ marginTop: '8px' }}>
                    Badge Legend in Platform &amp; Game Views:{' '}
                    <Label outline={true} size={sizes.SMALL}>
                      ◇ Available (0 Earned)
                    </Label>{' '}
                    <Label kind={kinds.INFO} size={sizes.SMALL}>
                      🥈 Softcore Earned
                    </Label>{' '}
                    <Label kind={kinds.WARNING} size={sizes.SMALL}>
                      🥇 Hardcore Earned
                    </Label>{' '}
                    <Label kind={kinds.PRIMARY} size={sizes.SMALL}>
                      👑 100% Mastered
                    </Label>
                  </div>
                </Alert>
              </div>
            ) : null}

            {validationStatus === 'invalid' ? (
              <div className={styles.statusAlert}>
                <Alert kind={kinds.DANGER}>
                  {translate('RetroAchievementsCredentialsInvalid')}
                </Alert>
              </div>
            ) : null}
          </div>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default RetroAchievementsSettings;
