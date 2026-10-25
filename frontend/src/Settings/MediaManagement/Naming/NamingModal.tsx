import React, { useCallback, useState } from 'react';
import SelectInput, { SelectInputOption } from 'Components/Form/SelectInput';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import ModalSection from 'Components/ModalSection';
import { icons, sizes } from 'Helpers/Props';
import { Size } from 'Helpers/Props/sizes';
import translate from 'Utilities/String/translate';
import NamingOption from './NamingOption';
import TokenCase from './TokenCase';
import TokenSeparator from './TokenSeparator';
import { NamingSettingsModel } from './useNamingSettings';
import styles from './NamingModal.module.css';

interface Token {
  token: string;
  example: string;
  footNotes?: string;
  notes?: string[];
}

interface TokenGroup {
  legend: string;
  caption?: string;
  tokens: Token[];
  size?: Extract<Size, 'large'>;
  isFullFilename?: boolean;
}

type SeparatorInputOption = Omit<SelectInputOption, 'key'> & {
  key: TokenSeparator;
};

type CaseInputOption = Omit<SelectInputOption, 'key'> & {
  key: TokenCase;
};

const separatorOptions: SeparatorInputOption[] = [
  {
    key: ' ',
    get value() {
      return `${translate('Space')} ( )`;
    },
  },
  {
    key: '.',
    get value() {
      return `${translate('Period')} (.)`;
    },
  },
  {
    key: '_',
    get value() {
      return `${translate('Underscore')} (_)`;
    },
  },
  {
    key: '-',
    get value() {
      return `${translate('Dash')} (-)`;
    },
  },
];

const caseOptions: CaseInputOption[] = [
  {
    key: 'title',
    get value() {
      return translate('DefaultCase');
    },
  },
  {
    key: 'lower',
    get value() {
      return translate('Lowercase');
    },
  },
  {
    key: 'upper',
    get value() {
      return translate('Uppercase');
    },
  },
];

const romPathPresets: Token[] = [
  {
    token: '{Platform}/{Region}/{Game Title}',
    example:
      'Nintendo Entertainment System/USA/Super Mario Bros. (USA) (Rev 1)',
  },
  {
    token: '{Platform Short}/{Region Code}/{Game CleanTitle} ({Release Year})',
    example: 'NES/US/Super Mario Bros (1985)',
  },
  {
    token: '{Platform}/{Game Title}/{Game Title}',
    example:
      'PlayStation/Final Fantasy VII (USA) (Disc 1)/Final Fantasy VII (USA) (Disc 1)',
  },
  {
    token: '{Manufacturer}/{Platform}/{Region}/{Game Title}',
    example:
      'Nintendo/Nintendo Entertainment System/USA/The Legend of Zelda (USA) (Rev 1)',
  },
];

const platformTokens: Token[] = [
  {
    token: '{Platform}',
    example: 'Nintendo Entertainment System',
  },
  {
    token: '{Platform Short}',
    example: 'NES',
  },
  {
    token: '{Manufacturer}',
    example: 'Nintendo',
  },
  {
    token: '{Platform Year}',
    example: '1985',
  },
];

const regionTokens: Token[] = [
  {
    token: '{Region}',
    example: 'USA',
  },
  {
    token: '{Region Code}',
    example: 'US',
  },
];

const gameTitleTokens: Token[] = [
  {
    token: '{Game Title}',
    example: 'The Legend of Zelda (USA) (Rev 1)',
  },
  {
    token: '{Game CleanTitle}',
    example: 'The Legend of Zelda',
  },
  {
    token: '{Release Year}',
    example: '1986',
  },
  {
    token: '{Version}',
    example: 'Rev 1',
  },
  {
    token: '{Disc}',
    example: 'Disc 1',
  },
];

const originalTokens: Token[] = [
  {
    token: '{Original Title}',
    example: 'Legend of Zelda, The (USA) (Rev 1)',
  },
  {
    token: '{Original Filename}',
    example: 'Legend of Zelda, The (USA) (Rev 1).nes',
  },
];

function getNamingTokenGroups(): TokenGroup[] {
  return [
    {
      legend: 'Preset Path Templates',
      caption:
        'Click a preset to replace the full Library Path & ROM Naming Format',
      tokens: romPathPresets,
      size: sizes.LARGE,
      isFullFilename: true,
    },
    {
      legend: 'Platform / Console Variables',
      tokens: platformTokens,
    },
    {
      legend:
        'Region Variables (USA, Europe, Japan, World, Unlicensed, Hacks, Homebrew)',
      tokens: regionTokens,
    },
    {
      legend: 'Game & ROM Variables',
      tokens: gameTitleTokens,
    },
    {
      legend: 'Original Dump Filename Variables',
      tokens: originalTokens,
      size: sizes.LARGE,
    },
  ];
}

export interface NamingModalOptions {
  name:
    | keyof Pick<
        NamingSettingsModel,
        | 'standardEpisodeFormat'
        | 'dailyEpisodeFormat'
        | 'animeEpisodeFormat'
        | 'seriesFolderFormat'
        | 'seasonFolderFormat'
        | 'specialsFolderFormat'
      >
    | 'unifiedPathFormat'
    | 'discPathFormat';
  season?: boolean;
  seasonTitle?: boolean;
  episode?: boolean;
  daily?: boolean;
  anime?: boolean;
  additional?: boolean;
}

interface NamingModalProps extends NamingModalOptions {
  isOpen: boolean;
  value: string;
  onInputChange: ({ name, value }: { name: string; value: string }) => void;
  onModalClose: () => void;
}

function NamingModal(props: NamingModalProps) {
  const { isOpen, name, value, onInputChange, onModalClose } = props;

  const [tokenSeparator, setTokenSeparator] = useState<TokenSeparator>(' ');
  const [tokenCase, setTokenCase] = useState<TokenCase>('title');
  const [selectionStart, setSelectionStart] = useState<number | null>(null);
  const [selectionEnd, setSelectionEnd] = useState<number | null>(null);

  const handleTokenSeparatorChange = useCallback(
    ({ value }: { value: TokenSeparator }) => {
      setTokenSeparator(value);
    },
    [setTokenSeparator]
  );

  const handleTokenCaseChange = useCallback(
    ({ value }: { value: TokenCase }) => {
      setTokenCase(value);
    },
    [setTokenCase]
  );

  const handleInputSelectionChange = useCallback(
    (selectionStart: number | null, selectionEnd: number | null) => {
      setSelectionStart(selectionStart);
      setSelectionEnd(selectionEnd);
    },
    [setSelectionStart, setSelectionEnd]
  );

  const handleOptionPress = useCallback(
    ({
      isFullFilename,
      tokenValue,
    }: {
      isFullFilename: boolean;
      tokenValue: string;
    }) => {
      if (isFullFilename) {
        onInputChange({ name, value: tokenValue });
      } else if (selectionStart == null || selectionEnd == null) {
        onInputChange({
          name,
          value: `${value}${tokenValue}`,
        });
      } else {
        const start = value.substring(0, selectionStart);
        const end = value.substring(selectionEnd);
        const newValue = `${start}${tokenValue}${end}`;
        const caret = selectionStart + tokenValue.length;

        onInputChange({ name, value: newValue });

        setSelectionStart(caret);
        setSelectionEnd(caret);
      }
    },
    [name, value, selectionEnd, selectionStart, onInputChange]
  );

  const tokenGroups = getNamingTokenGroups();

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>Library Path &amp; ROM Naming Variables</ModalHeader>

        <ModalBody>
          <div className={styles.builder}>
            <TextInput
              className={styles.formatInput}
              name={name}
              value={value}
              onChange={onInputChange}
              onSelectionChange={handleInputSelectionChange}
            />

            <div className={styles.controls}>
              <div className={styles.control}>
                <span className={styles.controlLabel}>
                  {translate('Separator')}
                </span>

                <SelectInput
                  className={styles.namingSelect}
                  name="separator"
                  value={tokenSeparator}
                  values={separatorOptions}
                  onChange={handleTokenSeparatorChange}
                />
              </div>

              <div className={styles.control}>
                <span className={styles.controlLabel}>
                  {translate('Capitalization')}
                </span>

                <SelectInput
                  className={styles.namingSelect}
                  name="case"
                  value={tokenCase}
                  values={caseOptions}
                  onChange={handleTokenCaseChange}
                />
              </div>
            </div>
          </div>

          <div className={styles.instruction}>
            <Icon name={icons.INFO} size={14} />
            <span>
              Click a preset path template or insert individual console, region,
              and game variables below. Use forward slashes (/) to separate
              folders.
            </span>
          </div>

          {tokenGroups.map((group) => (
            <ModalSection key={group.legend} title={group.legend}>
              {group.caption ? (
                <p className={styles.caption}>{group.caption}</p>
              ) : null}

              <div className={styles.groups}>
                {group.tokens.map(({ token, example, notes }) => (
                  <NamingOption
                    key={token}
                    token={token}
                    example={example}
                    notes={notes}
                    isFullFilename={group.isFullFilename}
                    size={group.size}
                    tokenSeparator={tokenSeparator}
                    tokenCase={tokenCase}
                    onPress={handleOptionPress}
                  />
                ))}
              </div>
            </ModalSection>
          ))}
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Close')}</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default NamingModal;
