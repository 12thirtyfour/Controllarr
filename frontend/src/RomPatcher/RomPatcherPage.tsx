import React, { ChangeEvent, useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Button from 'Components/Link/Button';
import PageContent from 'Components/Page/PageContent';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

export interface RomPatchRequest {
  seriesId?: number;
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
  baseRomPath?: string;
  outputRomPath?: string;
  outputCrc32?: string;
  outputSha1?: string;
  outputSha256?: string;
  verifiedInCatalog?: boolean;
  matchedCatalogName?: string;
}

function RomPatcherPage() {
  const [baseRomServerPath, setBaseRomServerPath] = useState('');
  const [patchOutputTitle, setPatchOutputTitle] = useState('');
  const [patchServerPath, setPatchServerPath] = useState('');
  const [patchStatus, setPatchStatus] = useState<RomPatchResult | null>(null);

  const { mutate: applyRomPatch, isPending: isApplyingPatch } = useApiMutation<
    RomPatchResult,
    RomPatchRequest
  >({
    path: '/gamemetadata/patches/apply',
    method: 'POST',
    mutationOptions: {
      onSuccess: (result) => {
        setPatchStatus(result);
        setPatchServerPath('');
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

      const reader = new FileReader();
      reader.onload = () => {
        const base64 = (reader.result as string).split(',')[1];
        applyRomPatch({
          patchFileName: file.name,
          patchBase64Content: base64,
          baseRomPath: baseRomServerPath.trim() || undefined,
          outputTitle: patchOutputTitle.trim() || undefined,
        });
      };
      reader.readAsDataURL(file);
    },
    [applyRomPatch, baseRomServerPath, patchOutputTitle]
  );

  const handleApplyServerPatchPress = useCallback(() => {
    if (!patchServerPath.trim()) {
      return;
    }

    applyRomPatch({
      patchFilePath: patchServerPath.trim(),
      baseRomPath: baseRomServerPath.trim() || undefined,
      outputTitle: patchOutputTitle.trim() || undefined,
    });
  }, [applyRomPatch, patchServerPath, baseRomServerPath, patchOutputTitle]);

  return (
    <PageContent title="ROM Patcher">
      <PageHeading scope={translate('Library')} title="ROM Patcher" />

      <div style={{ maxWidth: '960px', margin: '0 auto', padding: '16px' }}>
        <Alert kind={kinds.INFO}>
          Controllarr&apos;s Cryptographic Hash-Gated ROM Patcher applies .ips,
          .bps, and .xdelta / VCDIFF patches directly onto verified retail base
          ROMs in your library. Patched outputs are placed in the ROM Hacks &amp;
          Translations region folder, leaving your clean retail 1G1R base ROM
          untouched and byte-identical.
        </Alert>

        {patchStatus ? (
          <div style={{ marginTop: '16px', marginBottom: '16px' }}>
            <Alert kind={patchStatus.success ? kinds.SUCCESS : kinds.DANGER}>
              <div>
                <strong>{patchStatus.message}</strong>
              </div>
              {patchStatus.outputRomPath ? (
                <div style={{ marginTop: '8px' }}>
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
            legend="Patch Configuration"
            caption="Specify an optional base retail ROM path or output title, or leave blank to automatically discover the base ROM and match the HTGDB / SMDB catalog."
          >
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>Optional Base Retail ROM Path</FormLabel>
              <FormInputHelpText text="Leave blank to automatically discover the matching retail base ROM in your library by BPS source CRC32 or title, or specify an explicit retail ROM path" />
              <FormInput
                type={inputTypes.TEXT}
                name="baseRomServerPath"
                value={baseRomServerPath}
                placeholder="(Auto-discover from platform library)"
                onChange={({ value }: InputChanged<string>) =>
                  setBaseRomServerPath(value ?? '')
                }
              />
            </FormRow>

            <FormRow size={sizes.MEDIUM}>
              <FormLabel>Optional Output Hack / Translation Title</FormLabel>
              <FormInputHelpText text="Leave blank to use the matched HTGDB / SMDB / DAT canonical name or patch filename" />
              <FormInput
                type={inputTypes.TEXT}
                name="patchOutputTitle"
                value={patchOutputTitle}
                placeholder="e.g. Mother 3 [T+Eng v1.3] (Hack)"
                onChange={({ value }: InputChanged<string>) =>
                  setPatchOutputTitle(value ?? '')
                }
              />
            </FormRow>
          </FieldSet>

          <FieldSet
            legend="Apply Patch File"
            caption="Upload a patch file from your browser or provide a path on the Controllarr host server."
          >
            <FormRow size={sizes.MEDIUM}>
              <FormLabel>
                Upload &amp; Apply Patch (.ips / .bps / .xdelta / .zip)
              </FormLabel>
              <FormInputHelpText text="Select a local .ips, .bps, .xdelta, .vcdiff, or .zip patch file to apply onto the verified retail base ROM" />
              <input
                type="file"
                accept=".ips,.bps,.xdelta,.vcdiff,.zip"
                disabled={isApplyingPatch}
                onChange={handlePatchUploadChange}
              />
            </FormRow>

            <FormRow size={sizes.MEDIUM}>
              <FormLabel>Or Apply Patch from Server Path</FormLabel>
              <FormInputHelpText text="Full server path to a .ips, .bps, .xdelta, .vcdiff, or .zip patch file on the host machine" />
              <FormInput
                type={inputTypes.TEXT}
                name="patchServerPath"
                value={patchServerPath}
                placeholder="/path/to/Super Mario Bros. 3Mix.bps"
                onChange={({ value }: InputChanged<string>) =>
                  setPatchServerPath(value ?? '')
                }
              />
              <div style={{ marginTop: '12px' }}>
                <Button
                  kind={kinds.PRIMARY}
                  size={sizes.MEDIUM}
                  isDisabled={isApplyingPatch || !patchServerPath.trim()}
                  onPress={handleApplyServerPatchPress}
                >
                  {isApplyingPatch
                    ? 'Applying Patch & Verifying Hashes...'
                    : 'Apply Patch to Retail Base ROM'}
                </Button>
              </div>
            </FormRow>
          </FieldSet>
        </Form>
      </div>
    </div>
    </PageContent>
  );
}

export default RomPatcherPage;
