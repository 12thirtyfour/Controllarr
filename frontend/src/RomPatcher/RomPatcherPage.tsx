import React, { ChangeEvent, useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import SelectInputStyle from 'Components/Form/SelectInput.module.css';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import usePlatform from 'Platform/usePlatform';
import useRomFiles from 'RomFile/useRomFiles';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

export interface RomPatchRequest {
  seriesId?: number;
  baseEpisodeFileId?: number;
  baseRomPath?: string;
  patchFilePath?: string;
  patchFileName?: string;
  patchBase64Content?: string;
  outputTitle?: string;
}

export interface RomPatchResult {
  success: boolean;
  message: string;
  format?: string;
  platformTitle?: string;
  baseRomName?: string;
  baseRomPath?: string;
  baseRomCrc32?: string;
  baseRomVerified?: boolean;
  outputRomPath?: string;
  outputCrc32?: string;
  outputSha1?: string;
  outputSha256?: string;
  verifiedInCatalog?: boolean;
  matchedCatalogName?: string;
}

export interface RomPatchInspectResult {
  success: boolean;
  message: string;
  format?: string;
  patchFileName?: string;
  hasEmbeddedSourceChecksum: boolean;
  expectedSourceCrc32?: string;
  expectedTargetCrc32?: string;
  expectedSourceSize?: number;
  expectedTargetSize?: number;
  maxPatchEndOffset?: number;
  requiresManualBaseRom: boolean;
  autoMatchedSeriesId?: number;
  autoMatchedPlatformTitle?: string;
  autoMatchedBaseEpisodeFileId?: number;
  autoMatchedBaseRomPath?: string;
  autoMatchedBaseRomName?: string;
}

const IPS_INCOMPATIBLE_EXTENSIONS = new Set([
  '.chd',
  '.rvz',
  '.cso',
  '.wbfs',
  '.pbp',
  '.iso',
  '.cue',
  '.gdi',
  '.cdi',
  '.wud',
  '.wux',
  '.wua',
  '.nsp',
  '.xci',
  '.nsz',
  '.pkg',
  '.xiso',
]);

function getExtension(filePath: string) {
  const idx = filePath.lastIndexOf('.');

  return idx >= 0 ? filePath.slice(idx).toLowerCase() : '';
}

function getFileNameFromPath(filePath: string) {
  const parts = filePath.replace(/\\/g, '/').split('/');

  return parts[parts.length - 1] || filePath;
}

function formatByteSize(bytes?: number) {
  if (!bytes || bytes <= 0) {
    return null;
  }

  if (bytes >= 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(2)} MiB`;
  }

  if (bytes >= 1024) {
    return `${(bytes / 1024).toFixed(1)} KiB`;
  }

  return `${bytes} B`;
}

function RomPatcherPage() {
  const [stagedPatchFileName, setStagedPatchFileName] = useState('');
  const [stagedPatchBase64, setStagedPatchBase64] = useState('');
  const [patchServerPath, setPatchServerPath] = useState('');
  const [selectedSeriesId, setSelectedSeriesId] = useState<number | undefined>(
    undefined
  );
  const [selectedBaseFileId, setSelectedBaseFileId] = useState<
    number | undefined
  >(undefined);
  const [baseRomFilter, setBaseRomFilter] = useState('');
  const [baseRomServerPath, setBaseRomServerPath] = useState('');
  const [patchOutputTitle, setPatchOutputTitle] = useState('');
  const [inspectResult, setInspectResult] =
    useState<RomPatchInspectResult | null>(null);
  const [patchStatus, setPatchStatus] = useState<RomPatchResult | null>(null);

  const { data: platforms = [] } = usePlatform();
  const { data: platformFiles = [], isFetching: isPlatformFilesFetching } =
    useRomFiles(
      selectedSeriesId ? { seriesId: selectedSeriesId } : { romFileIds: [] }
    );

  const sortedPlatforms = useMemo(() => {
    return [...platforms].sort((a, b) => {
      const aCount = a.statistics?.romFileCount ?? 0;
      const bCount = b.statistics?.romFileCount ?? 0;
      const aHasFiles = aCount > 0;
      const bHasFiles = bCount > 0;

      if (aHasFiles !== bHasFiles) {
        return aHasFiles ? -1 : 1;
      }

      return a.title.localeCompare(b.title);
    });
  }, [platforms]);

  const isIpsPatch = inspectResult?.format?.toUpperCase() === 'IPS';

  const retailBaseRomFiles = useMemo(() => {
    const query = baseRomFilter.trim().toLowerCase();

    return platformFiles
      .filter((f) => f.seasonNumber >= 1 && f.seasonNumber <= 4)
      .filter((f) => {
        if (!isIpsPatch) {
          return true;
        }

        const ext = getExtension(f.relativePath || '');

        return !IPS_INCOMPATIBLE_EXTENSIONS.has(ext);
      })
      .filter((f) => {
        if (!query) {
          return true;
        }

        const target = (f.relativePath || '').toLowerCase();

        return target.includes(query);
      })
      .sort((a, b) =>
        (a.relativePath || '').localeCompare(b.relativePath || '')
      );
  }, [platformFiles, isIpsPatch, baseRomFilter]);

  const { mutate: inspectRomPatch, isPending: isInspectingPatch } =
    useApiMutation<RomPatchInspectResult, RomPatchRequest>({
      path: '/gamemetadata/patches/inspect',
      method: 'POST',
      mutationOptions: {
        onSuccess: (result) => {
          setInspectResult(result);

          if (result.autoMatchedSeriesId) {
            setSelectedSeriesId(result.autoMatchedSeriesId);
          }

          if (result.autoMatchedBaseEpisodeFileId) {
            setSelectedBaseFileId(result.autoMatchedBaseEpisodeFileId);
          }
        },
        onError: () => {
          setInspectResult({
            success: false,
            hasEmbeddedSourceChecksum: false,
            requiresManualBaseRom: true,
            message:
              'Unable to inspect patch file. Ensure it is a valid .ips, .bps, .xdelta, or .zip archive.',
          });
        },
      },
    });

  const { mutate: applyRomPatch, isPending: isApplyingPatch } = useApiMutation<
    RomPatchResult,
    RomPatchRequest
  >({
    path: '/gamemetadata/patches/apply',
    method: 'POST',
    mutationOptions: {
      onSuccess: (result) => {
        setPatchStatus(result);
      },
      onError: () => {
        setPatchStatus({
          success: false,
          message:
            'Failed to apply patch. Ensure the patch file (.ips, .bps, .xdelta) and matching retail base ROM are valid.',
        });
      },
    },
  });

  const handlePatchUploadChange = useCallback(
    (e: ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];

      if (!file) {
        return;
      }

      setPatchServerPath('');
      setPatchStatus(null);
      setInspectResult(null);
      setSelectedBaseFileId(undefined);

      const reader = new FileReader();

      reader.onload = () => {
        const resultStr =
          typeof reader.result === 'string' ? reader.result : '';
        const base64 = resultStr.split(',')[1] ?? '';

        setStagedPatchFileName(file.name);
        setStagedPatchBase64(base64);

        inspectRomPatch({
          patchFileName: file.name,
          patchBase64Content: base64,
          seriesId: selectedSeriesId,
        });
      };

      reader.readAsDataURL(file);
    },
    [inspectRomPatch, selectedSeriesId]
  );

  const handlePatchServerPathChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setPatchServerPath(value ?? '');

      if (value && value.trim().length > 0) {
        setStagedPatchFileName('');
        setStagedPatchBase64('');
        setInspectResult(null);
      }
    },
    []
  );

  const handleInspectServerPatchPress = useCallback(() => {
    if (!patchServerPath.trim()) {
      return;
    }

    setPatchStatus(null);
    inspectRomPatch({
      patchFilePath: patchServerPath.trim(),
      seriesId: selectedSeriesId,
    });
  }, [inspectRomPatch, patchServerPath, selectedSeriesId]);

  const handlePlatformSelectChange = useCallback(
    (event: ChangeEvent<HTMLSelectElement>) => {
      const raw = event.target.value;
      const nextId = raw ? Number(raw) : undefined;

      setSelectedSeriesId(nextId && !Number.isNaN(nextId) ? nextId : undefined);
      setSelectedBaseFileId(undefined);
      setBaseRomFilter('');
    },
    []
  );

  const handleBaseFileSelectChange = useCallback(
    (event: ChangeEvent<HTMLSelectElement>) => {
      const raw = event.target.value;
      const nextId = raw ? Number(raw) : undefined;

      setSelectedBaseFileId(
        nextId && !Number.isNaN(nextId) ? nextId : undefined
      );
    },
    []
  );

  const handleBaseRomFilterChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setBaseRomFilter(value ?? '');
    },
    []
  );

  const handleBaseRomServerPathChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setBaseRomServerPath(value ?? '');
    },
    []
  );

  const handlePatchOutputTitleChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setPatchOutputTitle(value ?? '');
    },
    []
  );

  const hasStagedPatch =
    Boolean(stagedPatchBase64) || Boolean(patchServerPath.trim());
  const hasExplicitBaseRom =
    Boolean(selectedBaseFileId) || Boolean(baseRomServerPath.trim());
  const canAutoMatch = Boolean(inspectResult?.hasEmbeddedSourceChecksum);
  const canApplyPatch =
    hasStagedPatch &&
    (hasExplicitBaseRom || canAutoMatch) &&
    !isApplyingPatch &&
    !isInspectingPatch;

  const handleApplyPatchPress = useCallback(() => {
    if (!canApplyPatch) {
      return;
    }

    setPatchStatus(null);
    applyRomPatch({
      seriesId: selectedSeriesId,
      baseEpisodeFileId: selectedBaseFileId,
      baseRomPath: baseRomServerPath.trim() || undefined,
      patchFileName: stagedPatchFileName || undefined,
      patchBase64Content: stagedPatchBase64 || undefined,
      patchFilePath: patchServerPath.trim() || undefined,
      outputTitle: patchOutputTitle.trim() || undefined,
    });
  }, [
    canApplyPatch,
    applyRomPatch,
    selectedSeriesId,
    selectedBaseFileId,
    baseRomServerPath,
    stagedPatchFileName,
    stagedPatchBase64,
    patchServerPath,
    patchOutputTitle,
  ]);

  const maxOffsetFormatted = formatByteSize(inspectResult?.maxPatchEndOffset);

  let inspectAlertKind: 'success' | 'danger' | 'info' | 'warning' =
    kinds.SUCCESS;

  if (inspectResult && !inspectResult.success) {
    inspectAlertKind = kinds.DANGER;
  } else if (inspectResult?.requiresManualBaseRom) {
    inspectAlertKind = kinds.WARNING;
  }

  return (
    <PageContent title="ROM Patcher">
      <PageContentBody>
        <PageHeading scope={translate('Library')} title="ROM Patcher" />

        <div
          style={{
            maxWidth: '960px',
            margin: '0 auto',
            padding: '16px',
            paddingBottom: '48px',
          }}
        >
          <Alert kind={kinds.INFO}>
            Controllarr&apos;s ROM Patcher inspects .ips, .bps, and .xdelta /
            VCDIFF patches before applying them non-destructively onto a retail
            ROM in your library. Patched outputs are written into the selected
            platform&apos;s ROM Hacks or Translations folder without modifying
            your retail base ROM.
          </Alert>

          {patchStatus ? (
            <div style={{ marginTop: '16px', marginBottom: '16px' }}>
              <Alert kind={patchStatus.success ? kinds.SUCCESS : kinds.DANGER}>
                <div>
                  <strong>{patchStatus.message}</strong>
                </div>
                {patchStatus.platformTitle ? (
                  <div style={{ marginTop: '8px' }}>
                    <strong>Platform:</strong> {patchStatus.platformTitle}
                  </div>
                ) : null}
                {patchStatus.baseRomPath || patchStatus.baseRomName ? (
                  <div style={{ marginTop: '4px' }}>
                    <strong>Base ROM Patched:</strong>{' '}
                    {patchStatus.baseRomPath || patchStatus.baseRomName}
                    {patchStatus.baseRomCrc32
                      ? ` (CRC32: ${patchStatus.baseRomCrc32})`
                      : ''}
                  </div>
                ) : null}
                {patchStatus.outputRomPath ? (
                  <div style={{ marginTop: '4px' }}>
                    <strong>Output File:</strong> {patchStatus.outputRomPath}
                  </div>
                ) : null}
                {patchStatus.outputCrc32 ? (
                  <div style={{ marginTop: '4px' }}>
                    <small>
                      CRC32: {patchStatus.outputCrc32} | SHA1:{' '}
                      {patchStatus.outputSha1}
                    </small>
                  </div>
                ) : null}
                {patchStatus.matchedCatalogName ? (
                  <div style={{ marginTop: '4px' }}>
                    <small>
                      Matched Catalog Entry: {patchStatus.matchedCatalogName}
                    </small>
                  </div>
                ) : null}
              </Alert>
            </div>
          ) : null}

          <div style={{ marginTop: '20px' }}>
            <Form>
              <FieldSet
                legend="1. Select Patch File"
                caption="Select a local patch file or enter a path on the Controllarr host server. Selecting a file stages and inspects it without modifying your library."
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>
                    Patch File (.ips / .bps / .xdelta / .zip)
                  </FormLabel>
                  <FormInputHelpText text="Browse for a local .ips, .bps, .xdelta, .vcdiff, or .zip patch file" />
                  <input
                    type="file"
                    accept=".ips,.bps,.xdelta,.vcdiff,.zip"
                    disabled={isApplyingPatch || isInspectingPatch}
                    onChange={handlePatchUploadChange}
                  />
                  {stagedPatchFileName ? (
                    <div style={{ marginTop: '6px' }}>
                      <small>
                        Staged patch: <strong>{stagedPatchFileName}</strong>
                      </small>
                    </div>
                  ) : null}
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Or Patch Path on Server</FormLabel>
                  <FormInputHelpText text="Full server path to a .ips, .bps, .xdelta, .vcdiff, or .zip patch file on the host machine" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="patchServerPath"
                    value={patchServerPath}
                    placeholder="/path/to/Super Mario Bros. 3Mix.bps"
                    onChange={handlePatchServerPathChange}
                  />
                  <div style={{ marginTop: '8px' }}>
                    <Button
                      kind={kinds.DEFAULT}
                      size={sizes.SMALL}
                      isDisabled={
                        isInspectingPatch ||
                        isApplyingPatch ||
                        !patchServerPath.trim()
                      }
                      onPress={handleInspectServerPatchPress}
                    >
                      {isInspectingPatch ? 'Inspecting...' : 'Inspect Patch'}
                    </Button>
                  </div>
                </FormRow>

                {inspectResult ? (
                  <div style={{ marginTop: '12px' }}>
                    <Alert kind={inspectAlertKind}>
                      <div>
                        <strong>{inspectResult.message}</strong>
                      </div>
                      {inspectResult.format ? (
                        <div style={{ marginTop: '4px' }}>
                          <small>
                            Format: {inspectResult.format}
                            {inspectResult.expectedSourceCrc32
                              ? ` | Expected Source CRC32: ${inspectResult.expectedSourceCrc32}`
                              : ''}
                            {maxOffsetFormatted
                              ? ` | Max Patch Offset: ${maxOffsetFormatted}`
                              : ''}
                            {inspectResult.autoMatchedBaseRomName
                              ? ` | Matched ROM: ${inspectResult.autoMatchedBaseRomName}`
                              : ''}
                          </small>
                        </div>
                      ) : null}
                    </Alert>
                  </div>
                ) : null}
              </FieldSet>

              <FieldSet
                legend="2. Select Target Platform & Retail Base ROM"
                caption="IPS and xdelta patches do not store source ROM checksums. Choose the exact Target Platform and Retail Base ROM from your library (or let BPS auto-match by source CRC32)."
              >
                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Target Platform</FormLabel>
                  <FormInputHelpText text="Select the console platform whose library contains the retail base ROM" />
                  <select
                    aria-label="Target Platform"
                    className={SelectInputStyle.select}
                    value={selectedSeriesId ? String(selectedSeriesId) : ''}
                    disabled={isApplyingPatch}
                    onChange={handlePlatformSelectChange}
                  >
                    <option value="">-- Select Target Platform --</option>
                    {sortedPlatforms.map((platform) => {
                      const fileCount = platform.statistics?.romFileCount ?? 0;

                      return (
                        <option key={platform.id} value={String(platform.id)}>
                          {platform.title} ({fileCount}{' '}
                          {fileCount === 1 ? 'file' : 'files'})
                        </option>
                      );
                    })}
                  </select>
                </FormRow>

                {selectedSeriesId ? (
                  <>
                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Filter Retail Base ROMs</FormLabel>
                      <FormInputHelpText text="Type part of the base game filename (for example: FireRed, Emerald, Super Mario World)" />
                      <FormInput
                        type={inputTypes.TEXT}
                        name="baseRomFilter"
                        value={baseRomFilter}
                        placeholder="Filter base ROMs in selected platform..."
                        onChange={handleBaseRomFilterChange}
                      />
                    </FormRow>

                    <FormRow size={sizes.MEDIUM}>
                      <FormLabel>Retail Base ROM</FormLabel>
                      <FormInputHelpText
                        text={
                          isPlatformFilesFetching
                            ? 'Loading retail ROM files for selected platform...'
                            : `${retailBaseRomFiles.length} compatible retail ${
                                retailBaseRomFiles.length === 1 ? 'ROM' : 'ROMs'
                              } available`
                        }
                      />
                      <select
                        aria-label="Retail Base ROM"
                        className={SelectInputStyle.select}
                        value={
                          selectedBaseFileId ? String(selectedBaseFileId) : ''
                        }
                        disabled={isApplyingPatch || isPlatformFilesFetching}
                        onChange={handleBaseFileSelectChange}
                      >
                        <option value="">-- Select Retail Base ROM --</option>
                        {retailBaseRomFiles.map((romFile) => (
                          <option key={romFile.id} value={String(romFile.id)}>
                            {getFileNameFromPath(romFile.relativePath)}
                          </option>
                        ))}
                      </select>
                    </FormRow>
                  </>
                ) : null}

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>Or Explicit Base Retail ROM Server Path</FormLabel>
                  <FormInputHelpText text="Optional explicit server path if the retail base ROM is not listed in the dropdown above" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="baseRomServerPath"
                    value={baseRomServerPath}
                    placeholder="/roms/Nintendo/Game Boy Advance/USA/Pokemon - FireRed Version (USA, Europe).gba"
                    onChange={handleBaseRomServerPathChange}
                  />
                </FormRow>

                <FormRow size={sizes.MEDIUM}>
                  <FormLabel>
                    Optional Output Hack / Translation Title
                  </FormLabel>
                  <FormInputHelpText text="Leave blank to use the matched DAT canonical name or the patch filename" />
                  <FormInput
                    type={inputTypes.TEXT}
                    name="patchOutputTitle"
                    value={patchOutputTitle}
                    placeholder="e.g. Pokemon - Orange Islands Beta 5.7 (Hack)"
                    onChange={handlePatchOutputTitleChange}
                  />
                </FormRow>

                <div style={{ marginTop: '16px' }}>
                  <SpinnerButton
                    kind={kinds.PRIMARY}
                    size={sizes.MEDIUM}
                    isSpinning={isApplyingPatch}
                    isDisabled={!canApplyPatch}
                    onPress={handleApplyPatchPress}
                  >
                    {isApplyingPatch
                      ? 'Applying Patch & Verifying Hashes...'
                      : 'Apply Patch to Retail Base ROM'}
                  </SpinnerButton>
                </div>
              </FieldSet>
            </Form>
          </div>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default RomPatcherPage;
