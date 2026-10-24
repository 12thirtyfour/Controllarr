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

interface TokenFlags {
  season: boolean;
  seasonTitle: boolean;
  episode: boolean;
  daily: boolean;
  anime: boolean;
  additional: boolean;
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

const romFilePresets: Token[] = [
  {
    token: '{Episode Title}',
    example: 'Super Mario Bros. (USA) (Rev 1)',
  },
  {
    token: '{Episode CleanTitle}',
    example: 'Super Mario Bros USA Rev 1',
  },
  {
    token: '{Series Title} - {Episode Title}',
    example: 'Nintendo Entertainment System - Super Mario Bros. (USA)',
  },
  {
    token: '{Episode Title}/{Episode Title}',
    example:
      'Final Fantasy VII (USA) (Disc 1)/Final Fantasy VII (USA) (Disc 1)',
  },
];

const platformTokens: Token[] = [
  {
    token: '{Series Title}',
    example: 'Nintendo Entertainment System',
  },
  {
    token: '{Series CleanTitle}',
    example: 'Nintendo Entertainment System',
  },
  {
    token: '{Series TitleYear}',
    example: 'Nintendo Entertainment System (1985)',
  },
  {
    token: '{Series TitleFirstCharacter}',
    example: 'N',
  },
  {
    token: '{Series Year}',
    example: '1985',
  },
];

const regionTokens: Token[] = [
  {
    token: '{Season Title}',
    example: 'USA',
  },
  {
    token: '{Season CleanTitle}',
    example: 'USA',
  },
  {
    token: '{season:0}',
    example: '1',
  },
  {
    token: '{season:00}',
    example: '01',
  },
];

const gameTitleTokens: Token[] = [
  {
    token: '{Episode Title}',
    example: 'The Legend of Zelda (USA) (Rev 1)',
  },
  {
    token: '{Episode CleanTitle}',
    example: 'The Legend of Zelda USA Rev 1',
  },
  {
    token: '{Air-Date}',
    example: '1986-02-21',
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

function getNamingTokenGroups({
  season,
  seasonTitle,
  episode,
  additional,
}: TokenFlags): TokenGroup[] {
  const groups: TokenGroup[] = [];

  if (episode) {
    groups.push({
      legend: 'ROM Naming Presets',
      caption: 'Click a preset to replace the full ROM naming pattern',
      tokens: romFilePresets,
      size: sizes.LARGE,
      isFullFilename: true,
    });
  }

  groups.push({
    legend: 'Platform / Console Tokens',
    tokens: platformTokens,
  });

  if (season || seasonTitle) {
    groups.push({
      legend: 'Region Folder Tokens (USA, Europe, Japan, Homebrew, ROM Hacks)',
      tokens: regionTokens,
    });
  }

  if (episode || additional) {
    groups.push({
      legend: 'Game / ROM Title & Release Date Tokens',
      tokens: gameTitleTokens,
    });

    groups.push({
      legend: 'Original Dump Filename Tokens',
      tokens: originalTokens,
      size: sizes.LARGE,
    });
  }

  return groups;
}

export interface NamingModalOptions {
  name: keyof Pick<
    NamingSettingsModel,
    | 'standardEpisodeFormat'
    | 'dailyEpisodeFormat'
    | 'animeEpisodeFormat'
    | 'seriesFolderFormat'
    | 'seasonFolderFormat'
    | 'specialsFolderFormat'
  >;
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
  const {
    isOpen,
    name,
    value,
    season = false,
    seasonTitle = false,
    episode = false,
    daily = false,
    anime = false,
    additional = false,
    onInputChange,
    onModalClose,
  } = props;

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

  const tokenGroups = getNamingTokenGroups({
    season,
    seasonTitle,
    episode,
    daily,
    anime,
    additional,
  });

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {episode
            ? translate('FileNameTokens')
            : translate('FolderNameTokens')}
        </ModalHeader>

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
            <span>{translate('NamingTokenSelectHelpText')}</span>
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
