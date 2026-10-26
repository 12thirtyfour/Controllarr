using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Dat;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Patching
{
    public class RomPatchingService : IRomPatchingService
    {
        private static readonly HashSet<string> SupportedPatchExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ips",
            ".bps",
            ".xdelta",
            ".vcdiff"
        };

        private static readonly uint[] Crc32Table = BuildCrc32Table();
        private static readonly VcdiffInstructionPair[] DefaultVcdiffCodeTable = BuildDefaultVcdiffCodeTable();

        private readonly IDiskProvider _diskProvider;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IDatCacheService _datCacheService;
        private readonly Lazy<IDownloadedEpisodesImportService> _downloadedEpisodesImportService;
        private readonly Logger _logger;

        public RomPatchingService(
            IDiskProvider diskProvider = null,
            ISeriesService seriesService = null,
            IEpisodeService episodeService = null,
            IMediaFileService mediaFileService = null,
            IDatCacheService datCacheService = null,
            Lazy<IDownloadedEpisodesImportService> downloadedEpisodesImportService = null,
            Logger logger = null)
        {
            _diskProvider = diskProvider;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _mediaFileService = mediaFileService;
            _datCacheService = datCacheService;
            _downloadedEpisodesImportService = downloadedEpisodesImportService;
            _logger = logger;
        }

        public bool IsPatchFile(string filePathOrName)
        {
            if (string.IsNullOrWhiteSpace(filePathOrName))
            {
                return false;
            }

            var ext = Path.GetExtension(filePathOrName);
            return !string.IsNullOrWhiteSpace(ext) && SupportedPatchExtensions.Contains(ext);
        }

        public RomPatchFormat DetectFormat(byte[] patchBytes, string fileName = null)
        {
            if (patchBytes != null && patchBytes.Length >= 5 &&
                patchBytes[0] == (byte)'P' &&
                patchBytes[1] == (byte)'A' &&
                patchBytes[2] == (byte)'T' &&
                patchBytes[3] == (byte)'C' &&
                patchBytes[4] == (byte)'H')
            {
                return RomPatchFormat.Ips;
            }

            if (patchBytes != null && patchBytes.Length >= 4 &&
                patchBytes[0] == (byte)'B' &&
                patchBytes[1] == (byte)'P' &&
                patchBytes[2] == (byte)'S' &&
                patchBytes[3] == (byte)'1')
            {
                return RomPatchFormat.Bps;
            }

            if (patchBytes != null && patchBytes.Length >= 4 &&
                patchBytes[0] == 0xD6 &&
                patchBytes[1] == 0xC3 &&
                patchBytes[2] == 0xC4 &&
                patchBytes[3] == 0x00)
            {
                return RomPatchFormat.Xdelta;
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
                return ext switch
                {
                    ".ips" => RomPatchFormat.Ips,
                    ".bps" => RomPatchFormat.Bps,
                    ".xdelta" => RomPatchFormat.Xdelta,
                    ".vcdiff" => RomPatchFormat.Xdelta,
                    _ => RomPatchFormat.Unknown
                };
            }

            return RomPatchFormat.Unknown;
        }

        public byte[] ApplyPatch(byte[] sourceBytes, byte[] patchBytes, RomPatchFormat format = RomPatchFormat.Unknown)
        {
            if (sourceBytes == null)
            {
                throw new ArgumentNullException(nameof(sourceBytes));
            }

            if (patchBytes == null || patchBytes.Length == 0)
            {
                throw new ArgumentNullException(nameof(patchBytes));
            }

            if (format == RomPatchFormat.Unknown)
            {
                format = DetectFormat(patchBytes);
            }

            return format switch
            {
                RomPatchFormat.Ips => ApplyIpsPatch(sourceBytes, patchBytes),
                RomPatchFormat.Bps => ApplyBpsPatch(sourceBytes, patchBytes),
                RomPatchFormat.Xdelta => ApplyVcdiffPatch(sourceBytes, patchBytes),
                _ => throw new InvalidDataException("Unsupported or unrecognized ROM patch format.")
            };
        }

        public uint? TryReadBpsSourceCrc32(byte[] patchBytes)
        {
            if (patchBytes == null || patchBytes.Length < 16 || DetectFormat(patchBytes) != RomPatchFormat.Bps)
            {
                return null;
            }

            return BitConverter.ToUInt32(patchBytes, patchBytes.Length - 12);
        }

        public uint? TryReadBpsTargetCrc32(byte[] patchBytes)
        {
            if (patchBytes == null || patchBytes.Length < 16 || DetectFormat(patchBytes) != RomPatchFormat.Bps)
            {
                return null;
            }

            return BitConverter.ToUInt32(patchBytes, patchBytes.Length - 8);
        }

        public RomPatchResultModel TryApplyPatchFileForSeries(string patchOrArchivePath, Series series)
        {
            if (string.IsNullOrWhiteSpace(patchOrArchivePath) || series == null)
            {
                return new RomPatchResultModel
                {
                    Success = false,
                    Message = "Patch path and target platform are required."
                };
            }

            return ApplyPatchToLibrary(new RomPatchRequestModel
            {
                SeriesId = series.Id,
                PatchFilePath = patchOrArchivePath
            });
        }

        public RomPatchResultModel ApplyPatchToLibrary(RomPatchRequestModel request)
        {
            if (request == null)
            {
                return new RomPatchResultModel { Success = false, Message = "Patch request cannot be null." };
            }

            try
            {
                var (patchBytes, effectivePatchName) = LoadPatchBytes(request);
                if (patchBytes == null || patchBytes.Length == 0)
                {
                    return new RomPatchResultModel
                    {
                        Success = false,
                        Message = "Unable to read .ips, .bps, or .xdelta patch content."
                    };
                }

                var format = DetectFormat(patchBytes, effectivePatchName);
                if (format == RomPatchFormat.Unknown)
                {
                    return new RomPatchResultModel
                    {
                        Success = false,
                        Message = "File is not a valid .ips, .bps, or .xdelta / VCDIFF patch."
                    };
                }

                Series series = null;
                if (request.SeriesId.HasValue && request.SeriesId.Value > 0 && _seriesService != null)
                {
                    series = _seriesService.GetSeries(request.SeriesId.Value);
                }

                var expectedSourceCrc32 = format == RomPatchFormat.Bps ? TryReadBpsSourceCrc32(patchBytes) : null;
                var (baseBytes, baseRomPath, baseExtension, resolvedSeries) = ResolveRetailBaseRom(
                    request,
                    series,
                    effectivePatchName,
                    expectedSourceCrc32);

                series ??= resolvedSeries;

                if (baseBytes == null || baseBytes.Length == 0)
                {
                    return new RomPatchResultModel
                    {
                        Success = false,
                        Format = format.ToString().ToUpperInvariant(),
                        Message = expectedSourceCrc32.HasValue
                            ? $"No matching retail base ROM with CRC32 {expectedSourceCrc32.Value:x8} was found in the platform library."
                            : "No matching retail base ROM was found in the platform library to apply this patch."
                    };
                }

                var patchedBytes = ApplyPatch(baseBytes, patchBytes, format);
                var (crc32Hex, md5Hex, sha1Hex, sha256Hex) = ComputeAllHashes(patchedBytes);

                DatEntry matchedDat = null;
                if (_datCacheService != null)
                {
                    matchedDat = _datCacheService.FindBySha256(sha256Hex) ??
                                 _datCacheService.FindBySha1(sha1Hex) ??
                                 _datCacheService.FindByMd5(md5Hex) ??
                                 _datCacheService.FindByCrc32(crc32Hex);
                }

                var cleanPatchTitle = !string.IsNullOrWhiteSpace(request.OutputTitle)
                    ? request.OutputTitle.Trim()
                    : (!string.IsNullOrWhiteSpace(matchedDat?.GameName)
                        ? Path.GetFileNameWithoutExtension(matchedDat.GameName.Replace('\\', '/'))
                        : Path.GetFileNameWithoutExtension(effectivePatchName ?? "Patched Game"));

                if (!cleanPatchTitle.Contains("(Hack)", StringComparison.OrdinalIgnoreCase) &&
                    !cleanPatchTitle.Contains("[T+", StringComparison.OrdinalIgnoreCase) &&
                    !cleanPatchTitle.Contains("Translation", StringComparison.OrdinalIgnoreCase))
                {
                    cleanPatchTitle = $"{cleanPatchTitle} (Hack)";
                }

                var outputExt = string.IsNullOrWhiteSpace(baseExtension) ? ".bin" : baseExtension;
                string outputRomPath = null;
                int? createdEpisodeId = null;
                int? createdFileId = null;

                if (series != null && !string.IsNullOrWhiteSpace(series.Path))
                {
                    var romHacksFolder = Path.Combine(series.Path, "ROM Hacks");
                    Directory.CreateDirectory(romHacksFolder);

                    var safeFileName = SanitizeFileName(cleanPatchTitle) + outputExt;
                    outputRomPath = Path.Combine(romHacksFolder, safeFileName);
                    File.WriteAllBytes(outputRomPath, patchedBytes);

                    var importedThroughFlow = false;
                    if (_downloadedEpisodesImportService?.Value != null)
                    {
                        try
                        {
                            var importResults = _downloadedEpisodesImportService.Value.ProcessPath(outputRomPath, ImportMode.Auto, series);
                            var successfulImport = importResults?.FirstOrDefault(r => r.Result == ImportResultType.Imported);
                            if (successfulImport?.EpisodeFile != null)
                            {
                                createdFileId = successfulImport.EpisodeFile.Id;
                                createdEpisodeId = successfulImport.EpisodeFile.Episodes.Value?.FirstOrDefault()?.Id;
                                outputRomPath = Path.Combine(series.Path, successfulImport.EpisodeFile.RelativePath);
                                importedThroughFlow = true;
                            }
                        }
                        catch (Exception importEx)
                        {
                            _logger?.Debug(importEx, "Standard import flow skipped or encountered an error for {0}, falling back to direct registration.", outputRomPath);
                        }
                    }

                    if (!importedThroughFlow)
                    {
                        var (epId, fileId) = RegisterPatchedRomInLibrary(
                            series,
                            cleanPatchTitle,
                            outputRomPath,
                            patchedBytes.LongLength);
                        createdEpisodeId = epId;
                        createdFileId = fileId;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(baseRomPath))
                {
                    var parentDir = Path.GetDirectoryName(baseRomPath) ?? Directory.GetCurrentDirectory();
                    var romHacksFolder = Path.Combine(parentDir, "ROM Hacks");
                    Directory.CreateDirectory(romHacksFolder);

                    var safeFileName = SanitizeFileName(cleanPatchTitle) + outputExt;
                    outputRomPath = Path.Combine(romHacksFolder, safeFileName);
                    File.WriteAllBytes(outputRomPath, patchedBytes);
                }

                return new RomPatchResultModel
                {
                    Success = true,
                    Format = format.ToString().ToUpperInvariant(),
                    BaseRomPath = baseRomPath,
                    OutputRomPath = outputRomPath,
                    OutputCrc32 = crc32Hex,
                    OutputSha1 = sha1Hex,
                    OutputSha256 = sha256Hex,
                    VerifiedInCatalog = matchedDat != null || format == RomPatchFormat.Bps,
                    MatchedCatalogName = matchedDat?.GameName ?? (format == RomPatchFormat.Bps ? "BPS Target CRC32 Verified" : "Verified Retail Base ROM"),
                    EpisodeId = createdEpisodeId,
                    EpisodeFileId = createdFileId,
                    Message = $"Applied {format.ToString().ToUpperInvariant()} patch onto verified retail base ROM and created '{Path.GetFileName(outputRomPath)}' (CRC32: {crc32Hex}) in ROM Hacks without modifying the retail file."
                };
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "ROM patching failed");
                return new RomPatchResultModel
                {
                    Success = false,
                    Message = $"Patch application failed: {ex.Message}"
                };
            }
        }

        private static byte[] ApplyIpsPatch(byte[] sourceBytes, byte[] patchBytes)
        {
            if (patchBytes.Length < 8 ||
                patchBytes[0] != (byte)'P' ||
                patchBytes[1] != (byte)'A' ||
                patchBytes[2] != (byte)'T' ||
                patchBytes[3] != (byte)'C' ||
                patchBytes[4] != (byte)'H')
            {
                throw new InvalidDataException("Invalid IPS header: missing 'PATCH' signature.");
            }

            var output = new List<byte>(sourceBytes);
            var pos = 5;

            while (pos + 3 <= patchBytes.Length)
            {
                if (patchBytes[pos] == (byte)'E' &&
                    patchBytes[pos + 1] == (byte)'O' &&
                    patchBytes[pos + 2] == (byte)'F')
                {
                    pos += 3;

                    // Optional 3-byte truncation length after EOF
                    if (pos + 3 <= patchBytes.Length)
                    {
                        var truncateLen = (patchBytes[pos] << 16) | (patchBytes[pos + 1] << 8) | patchBytes[pos + 2];
                        if (truncateLen >= 0 && truncateLen < output.Count)
                        {
                            output.RemoveRange(truncateLen, output.Count - truncateLen);
                        }
                    }

                    return output.ToArray();
                }

                if (pos + 5 > patchBytes.Length)
                {
                    throw new InvalidDataException("Truncated IPS record header.");
                }

                var offset = (patchBytes[pos] << 16) | (patchBytes[pos + 1] << 8) | patchBytes[pos + 2];
                var size = (patchBytes[pos + 3] << 8) | patchBytes[pos + 4];
                pos += 5;

                if (size == 0)
                {
                    if (pos + 3 > patchBytes.Length)
                    {
                        throw new InvalidDataException("Truncated IPS RLE record.");
                    }

                    var rleCount = (patchBytes[pos] << 8) | patchBytes[pos + 1];
                    var rleByte = patchBytes[pos + 2];
                    pos += 3;

                    EnsureCapacity(output, offset + rleCount);
                    for (var i = 0; i < rleCount; i++)
                    {
                        output[offset + i] = rleByte;
                    }
                }
                else
                {
                    if (pos + size > patchBytes.Length)
                    {
                        throw new InvalidDataException("Truncated IPS data payload.");
                    }

                    EnsureCapacity(output, offset + size);
                    for (var i = 0; i < size; i++)
                    {
                        output[offset + i] = patchBytes[pos + i];
                    }

                    pos += size;
                }
            }

            throw new InvalidDataException("Invalid IPS patch: missing 'EOF' marker.");
        }

        private static byte[] ApplyBpsPatch(byte[] sourceBytes, byte[] patchBytes)
        {
            if (patchBytes.Length < 16 ||
                patchBytes[0] != (byte)'B' ||
                patchBytes[1] != (byte)'P' ||
                patchBytes[2] != (byte)'S' ||
                patchBytes[3] != (byte)'1')
            {
                throw new InvalidDataException("Invalid BPS header: missing 'BPS1' signature.");
            }

            var expectedSourceCrc = BitConverter.ToUInt32(patchBytes, patchBytes.Length - 12);
            var expectedTargetCrc = BitConverter.ToUInt32(patchBytes, patchBytes.Length - 8);
            var expectedPatchCrc = BitConverter.ToUInt32(patchBytes, patchBytes.Length - 4);

            var actualPatchCrc = ComputeCrc32Uint(patchBytes, 0, patchBytes.Length - 4);
            if (actualPatchCrc != expectedPatchCrc)
            {
                throw new InvalidDataException($"BPS patch CRC32 mismatch (expected {expectedPatchCrc:x8}, got {actualPatchCrc:x8}).");
            }

            var actualSourceCrc = ComputeCrc32Uint(sourceBytes, 0, sourceBytes.Length);
            if (actualSourceCrc != expectedSourceCrc)
            {
                throw new InvalidDataException($"BPS source ROM CRC32 mismatch (expected {expectedSourceCrc:x8}, got {actualSourceCrc:x8}).");
            }

            var pos = 4;
            var sourceSize = ReadBpsVarInt(patchBytes, ref pos);
            var targetSize = ReadBpsVarInt(patchBytes, ref pos);
            var metadataSize = ReadBpsVarInt(patchBytes, ref pos);

            if (sourceSize != sourceBytes.LongLength)
            {
                throw new InvalidDataException($"BPS source ROM size mismatch (expected {sourceSize}, got {sourceBytes.LongLength}).");
            }

            pos += (int)metadataSize;

            var targetBytes = new byte[targetSize];
            var outputOffset = 0;
            var sourceRelativeOffset = 0;
            var targetRelativeOffset = 0;
            var bodyEnd = patchBytes.Length - 12;

            while (pos < bodyEnd)
            {
                var action = ReadBpsVarInt(patchBytes, ref pos);
                var length = (int)(action >> 2) + 1;
                var mode = (int)(action & 3);

                switch (mode)
                {
                    case 0: // SourceRead
                        for (var i = 0; i < length; i++)
                        {
                            targetBytes[outputOffset] = sourceBytes[outputOffset];
                            outputOffset++;
                        }

                        break;
                    case 1: // TargetRead
                        for (var i = 0; i < length; i++)
                        {
                            targetBytes[outputOffset++] = patchBytes[pos++];
                        }

                        break;
                    case 2: // SourceCopy
                    {
                        var offsetData = ReadBpsVarInt(patchBytes, ref pos);
                        var negative = (offsetData & 1) != 0;
                        var delta = (int)(offsetData >> 1);
                        sourceRelativeOffset += negative ? -delta : delta;

                        for (var i = 0; i < length; i++)
                        {
                            targetBytes[outputOffset++] = sourceBytes[sourceRelativeOffset++];
                        }

                        break;
                    }

                    case 3: // TargetCopy
                    {
                        var offsetData = ReadBpsVarInt(patchBytes, ref pos);
                        var negative = (offsetData & 1) != 0;
                        var delta = (int)(offsetData >> 1);
                        targetRelativeOffset += negative ? -delta : delta;

                        for (var i = 0; i < length; i++)
                        {
                            targetBytes[outputOffset++] = targetBytes[targetRelativeOffset++];
                        }

                        break;
                    }
                }
            }

            var actualTargetCrc = ComputeCrc32Uint(targetBytes, 0, targetBytes.Length);
            if (actualTargetCrc != expectedTargetCrc)
            {
                throw new InvalidDataException($"BPS target ROM CRC32 mismatch after patching (expected {expectedTargetCrc:x8}, got {actualTargetCrc:x8}).");
            }

            return targetBytes;
        }

        private static byte[] ApplyVcdiffPatch(byte[] sourceBytes, byte[] patchBytes)
        {
            if (patchBytes.Length < 5 ||
                patchBytes[0] != 0xD6 ||
                patchBytes[1] != 0xC3 ||
                patchBytes[2] != 0xC4 ||
                patchBytes[3] != 0x00)
            {
                throw new InvalidDataException("Invalid xdelta/VCDIFF header: missing VCDIFF magic signature.");
            }

            var pos = 4;
            var hdrIndicator = patchBytes[pos++];

            if ((hdrIndicator & 0x01) != 0)
            {
                throw new InvalidDataException("Secondary VCDIFF compressors are not supported.");
            }

            if ((hdrIndicator & 0x02) != 0)
            {
                var codeTableLen = ReadVcdiffVarInt(patchBytes, ref pos);
                pos += codeTableLen;
            }

            if ((hdrIndicator & 0x04) != 0)
            {
                var appHeaderLen = ReadVcdiffVarInt(patchBytes, ref pos);
                pos += appHeaderLen;
            }

            using (var outputStream = new MemoryStream())
            {
                while (pos < patchBytes.Length)
                {
                    var winIndicator = patchBytes[pos++];
                    var hasSourceSegment = (winIndicator & 0x01) != 0;
                    var hasTargetSegment = (winIndicator & 0x02) != 0;
                    var hasAdler32 = (winIndicator & 0x04) != 0;

                    var segmentLength = 0;
                    var segmentPosition = 0;

                    if (hasSourceSegment || hasTargetSegment)
                    {
                        segmentLength = ReadVcdiffVarInt(patchBytes, ref pos);
                        segmentPosition = ReadVcdiffVarInt(patchBytes, ref pos);
                    }

                    var deltaEncodingLength = ReadVcdiffVarInt(patchBytes, ref pos);
                    var windowEnd = pos + deltaEncodingLength;

                    var targetWindowLength = ReadVcdiffVarInt(patchBytes, ref pos);
                    var deltaIndicator = patchBytes[pos++];
                    if (deltaIndicator != 0)
                    {
                        throw new InvalidDataException("Compressed VCDIFF delta sections are not supported.");
                    }

                    var dataLen = ReadVcdiffVarInt(patchBytes, ref pos);
                    var instLen = ReadVcdiffVarInt(patchBytes, ref pos);
                    var addrLen = ReadVcdiffVarInt(patchBytes, ref pos);

                    uint? expectedAdler = null;
                    if (hasAdler32)
                    {
                        expectedAdler = ((uint)patchBytes[pos] << 24) |
                                        ((uint)patchBytes[pos + 1] << 16) |
                                        ((uint)patchBytes[pos + 2] << 8) |
                                        patchBytes[pos + 3];
                        pos += 4;
                    }

                    var dataPos = pos;
                    var instPos = dataPos + dataLen;
                    var instEnd = instPos + instLen;
                    var addrPos = instEnd;

                    var targetWindow = new byte[targetWindowLength];
                    var targetOffset = 0;
                    var cache = new VcdiffAddressCache();

                    var currentOutBytes = hasTargetSegment ? outputStream.ToArray() : null;

                    while (instPos < instEnd)
                    {
                        var opcode = patchBytes[instPos++];
                        var pair = DefaultVcdiffCodeTable[opcode];

                        ExecuteVcdiffInstruction(
                            pair.First,
                            patchBytes,
                            sourceBytes,
                            currentOutBytes,
                            hasSourceSegment,
                            hasTargetSegment,
                            segmentLength,
                            segmentPosition,
                            targetWindow,
                            ref targetOffset,
                            ref dataPos,
                            ref instPos,
                            ref addrPos,
                            cache);

                        if (pair.Second.Type != VcdiffOpType.Noop)
                        {
                            ExecuteVcdiffInstruction(
                                pair.Second,
                                patchBytes,
                                sourceBytes,
                                currentOutBytes,
                                hasSourceSegment,
                                hasTargetSegment,
                                segmentLength,
                                segmentPosition,
                                targetWindow,
                                ref targetOffset,
                                ref dataPos,
                                ref instPos,
                                ref addrPos,
                                cache);
                        }
                    }

                    if (expectedAdler.HasValue)
                    {
                        var actualAdler = ComputeAdler32(targetWindow);
                        if (actualAdler != expectedAdler.Value)
                        {
                            throw new InvalidDataException($"xdelta/VCDIFF window Adler32 mismatch (expected {expectedAdler.Value:x8}, got {actualAdler:x8}).");
                        }
                    }

                    outputStream.Write(targetWindow, 0, targetWindow.Length);
                    pos = windowEnd;
                }

                return outputStream.ToArray();
            }
        }

        private static void ExecuteVcdiffInstruction(
            VcdiffInstruction inst,
            byte[] patchBytes,
            byte[] sourceBytes,
            byte[] currentOutputBytes,
            bool hasSourceSegment,
            bool hasTargetSegment,
            int segmentLength,
            int segmentPosition,
            byte[] targetWindow,
            ref int targetOffset,
            ref int dataPos,
            ref int instPos,
            ref int addrPos,
            VcdiffAddressCache cache)
        {
            if (inst.Type == VcdiffOpType.Noop)
            {
                return;
            }

            var size = inst.Size == 0 ? ReadVcdiffVarInt(patchBytes, ref instPos) : inst.Size;

            switch (inst.Type)
            {
                case VcdiffOpType.Add:
                    Buffer.BlockCopy(patchBytes, dataPos, targetWindow, targetOffset, size);
                    dataPos += size;
                    targetOffset += size;
                    break;

                case VcdiffOpType.Run:
                {
                    var runByte = patchBytes[dataPos++];
                    for (var i = 0; i < size; i++)
                    {
                        targetWindow[targetOffset++] = runByte;
                    }

                    break;
                }

                case VcdiffOpType.Copy:
                {
                    var here = segmentLength + targetOffset;
                    var addr = cache.DecodeAddress(here, inst.Mode, patchBytes, ref addrPos);

                    for (var i = 0; i < size; i++)
                    {
                        var copyIndex = addr + i;
                        if (copyIndex < segmentLength)
                        {
                            if (hasSourceSegment)
                            {
                                targetWindow[targetOffset++] = sourceBytes[segmentPosition + copyIndex];
                            }
                            else if (hasTargetSegment && currentOutputBytes != null)
                            {
                                targetWindow[targetOffset++] = currentOutputBytes[segmentPosition + copyIndex];
                            }
                        }
                        else
                        {
                            targetWindow[targetOffset++] = targetWindow[copyIndex - segmentLength];
                        }
                    }

                    break;
                }
            }
        }

        private (byte[] PatchBytes, string PatchName) LoadPatchBytes(RomPatchRequestModel request)
        {
            if (!string.IsNullOrWhiteSpace(request.PatchBase64Content))
            {
                var rawBase64 = request.PatchBase64Content.Trim();
                var commaIdx = rawBase64.IndexOf(',');
                if (commaIdx >= 0)
                {
                    rawBase64 = rawBase64.Substring(commaIdx + 1);
                }

                var decoded = Convert.FromBase64String(rawBase64);
                return ExtractPatchIfZip(decoded, request.PatchFileName ?? "patch.bps");
            }

            if (!string.IsNullOrWhiteSpace(request.PatchFilePath) && File.Exists(request.PatchFilePath))
            {
                var rawBytes = File.ReadAllBytes(request.PatchFilePath);
                return ExtractPatchIfZip(rawBytes, Path.GetFileName(request.PatchFilePath));
            }

            return (null, null);
        }

        private (byte[] PatchBytes, string PatchName) ExtractPatchIfZip(byte[] rawBytes, string fileName)
        {
            if (rawBytes != null && rawBytes.Length >= 4 &&
                rawBytes[0] == 0x50 && rawBytes[1] == 0x4B && rawBytes[2] == 0x03 && rawBytes[3] == 0x04)
            {
                using (var ms = new MemoryStream(rawBytes))
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
                {
                    var patchEntry = archive.Entries.FirstOrDefault(e => IsPatchFile(e.Name));
                    if (patchEntry != null)
                    {
                        using (var entryStream = patchEntry.Open())
                        using (var outMs = new MemoryStream())
                        {
                            entryStream.CopyTo(outMs);
                            return (outMs.ToArray(), patchEntry.Name);
                        }
                    }
                }
            }

            return (rawBytes, fileName);
        }

        private (byte[] BaseBytes, string BaseRomPath, string BaseExtension, Series ResolvedSeries) ResolveRetailBaseRom(
            RomPatchRequestModel request,
            Series series,
            string patchFileName,
            uint? expectedSourceCrc32)
        {
            if (!string.IsNullOrWhiteSpace(request.BaseRomPath) && File.Exists(request.BaseRomPath))
            {
                var (bytes, ext) = ReadRomBytesAndExtension(request.BaseRomPath);
                return (bytes, request.BaseRomPath, ext, series);
            }

            var candidateSeriesList = new List<Series>();
            if (series != null)
            {
                candidateSeriesList.Add(series);
            }
            else if (_seriesService != null)
            {
                candidateSeriesList.AddRange(_seriesService.GetAllSeries());
            }

            foreach (var candidateSeries in candidateSeriesList)
            {
                if (candidateSeries == null || string.IsNullOrWhiteSpace(candidateSeries.Path))
                {
                    continue;
                }

                var files = _mediaFileService?.GetFilesBySeries(candidateSeries.Id) ?? new List<EpisodeFile>();
                var retailFiles = files
                    .Where(f => f.SeasonNumber >= 1 && f.SeasonNumber <= 4)
                    .ToList();

                if (request.BaseEpisodeFileId.HasValue && request.BaseEpisodeFileId.Value > 0)
                {
                    var explicitFile = files.FirstOrDefault(f => f.Id == request.BaseEpisodeFileId.Value);
                    if (explicitFile != null)
                    {
                        var fullPath = Path.Combine(candidateSeries.Path, explicitFile.RelativePath);
                        if (File.Exists(fullPath))
                        {
                            var (bytes, ext) = ReadRomBytesAndExtension(fullPath);
                            return (bytes, fullPath, ext, candidateSeries);
                        }
                    }
                }

                // 1. Match by BPS source CRC32 against retail library files
                if (expectedSourceCrc32.HasValue)
                {
                    foreach (var retailFile in retailFiles)
                    {
                        var fullPath = Path.Combine(candidateSeries.Path, retailFile.RelativePath);
                        if (!File.Exists(fullPath))
                        {
                            continue;
                        }

                        var (bytes, ext) = ReadRomBytesAndExtension(fullPath);
                        if (bytes != null && ComputeCrc32Uint(bytes, 0, bytes.Length) == expectedSourceCrc32.Value)
                        {
                            return (bytes, fullPath, ext, candidateSeries);
                        }
                    }
                }

                // 2. Match by normalized title prefix
                var normalizedPatchStem = NormalizeMatchKey(Path.GetFileNameWithoutExtension(patchFileName ?? string.Empty));
                foreach (var retailFile in retailFiles)
                {
                    var fullPath = Path.Combine(candidateSeries.Path, retailFile.RelativePath);
                    if (!File.Exists(fullPath))
                    {
                        continue;
                    }

                    var fileStem = NormalizeMatchKey(Path.GetFileNameWithoutExtension(retailFile.RelativePath));
                    if (!string.IsNullOrWhiteSpace(fileStem) &&
                        !string.IsNullOrWhiteSpace(normalizedPatchStem) &&
                        (normalizedPatchStem.Contains(fileStem, StringComparison.OrdinalIgnoreCase) ||
                         fileStem.Contains(normalizedPatchStem, StringComparison.OrdinalIgnoreCase)))
                    {
                        var (bytes, ext) = ReadRomBytesAndExtension(fullPath);
                        if (!expectedSourceCrc32.HasValue ||
                            (bytes != null && ComputeCrc32Uint(bytes, 0, bytes.Length) == expectedSourceCrc32.Value))
                        {
                            return (bytes, fullPath, ext, candidateSeries);
                        }
                    }
                }

                // 3. Fallback when a single retail ROM exists in the platform and no CRC mismatch
                if (!expectedSourceCrc32.HasValue && retailFiles.Count == 1)
                {
                    var singlePath = Path.Combine(candidateSeries.Path, retailFiles[0].RelativePath);
                    if (File.Exists(singlePath))
                    {
                        var (bytes, ext) = ReadRomBytesAndExtension(singlePath);
                        return (bytes, singlePath, ext, candidateSeries);
                    }
                }
            }

            return (null, null, null, series);
        }

        private static (byte[] RomBytes, string Extension) ReadRomBytesAndExtension(string path)
        {
            var ext = Path.GetExtension(path);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.Length > 0);
                    if (entry != null)
                    {
                        using (var s = entry.Open())
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            return (ms.ToArray(), Path.GetExtension(entry.Name));
                        }
                    }
                }
            }

            return (File.ReadAllBytes(path), ext);
        }

        private (int? EpisodeId, int? EpisodeFileId) RegisterPatchedRomInLibrary(
            Series series,
            string hackTitle,
            string outputRomPath,
            long byteSize)
        {
            if (_episodeService == null || _mediaFileService == null)
            {
                return (null, null);
            }

            try
            {
                var existingEpisodes = _episodeService.GetEpisodeBySeries(series.Id) ?? new List<Episode>();
                var hackEpisode = existingEpisodes.FirstOrDefault(e =>
                    e.SeasonNumber == GameCatalogProxy.RegionRomHacksSeason &&
                    string.Equals(e.Title, hackTitle, StringComparison.OrdinalIgnoreCase));

                if (hackEpisode == null)
                {
                    var nextEpNum = existingEpisodes
                        .Where(e => e.SeasonNumber == GameCatalogProxy.RegionRomHacksSeason)
                        .Select(e => e.EpisodeNumber)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    hackEpisode = new Episode
                    {
                        SeriesId = series.Id,
                        TvdbId = Math.Abs(HashCode.Combine(series.TvdbId, hackTitle.ToLowerInvariant())) + 500000,
                        SeasonNumber = GameCatalogProxy.RegionRomHacksSeason,
                        EpisodeNumber = nextEpNum,
                        AbsoluteEpisodeNumber = nextEpNum,
                        Title = hackTitle,
                        FinaleType = "ROM Hack",
                        Overview = $"Patched ROM hack / translation generated onto verified {series.Title} retail base ROM.",
                        AirDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        AirDateUtc = DateTime.UtcNow,
                        Monitored = true,
                        Ratings = new Ratings { Votes = 100, Value = 9.0m }
                    };

                    _episodeService.InsertMany(new List<Episode> { hackEpisode });
                }

                var relativePath = series.Path.GetRelativePath(outputRomPath);
                var existingFiles = _mediaFileService.GetFilesWithRelativePath(series.Id, relativePath);
                foreach (var old in existingFiles)
                {
                    _mediaFileService.Delete(old, DeleteMediaFileReason.Upgrade);
                }

                var episodeFile = new EpisodeFile
                {
                    SeriesId = series.Id,
                    SeasonNumber = GameCatalogProxy.RegionRomHacksSeason,
                    RelativePath = relativePath,
                    Size = byteSize,
                    DateAdded = DateTime.UtcNow,
                    Quality = new QualityModel(Quality.Unknown),
                    Episodes = new List<Episode> { hackEpisode }
                };

                var savedFile = _mediaFileService.Add(episodeFile);
                return (hackEpisode.Id, savedFile?.Id);
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Unable to register patched ROM in media file database for {0}", hackTitle);
                return (null, null);
            }
        }

        private static string NormalizeMatchKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var idx = input.IndexOf('(');
            var basePart = idx > 0 ? input.Substring(0, idx) : input;
            var bracketIdx = basePart.IndexOf('[');
            if (bracketIdx > 0)
            {
                basePart = basePart.Substring(0, bracketIdx);
            }

            return new string(basePart.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        }

        private static void EnsureCapacity(List<byte> buffer, int requiredSize)
        {
            while (buffer.Count < requiredSize)
            {
                buffer.Add(0);
            }
        }

        private static long ReadBpsVarInt(byte[] data, ref int pos)
        {
            long result = 0;
            long shift = 1;

            while (true)
            {
                if (pos >= data.Length)
                {
                    throw new InvalidDataException("Unexpected end of BPS stream while reading variable-length integer.");
                }

                var b = data[pos++];
                result += (b & 0x7F) * shift;
                if ((b & 0x80) != 0)
                {
                    break;
                }

                shift <<= 7;
                result += shift;
            }

            return result;
        }

        private static int ReadVcdiffVarInt(byte[] data, ref int pos)
        {
            var result = 0;
            while (true)
            {
                if (pos >= data.Length)
                {
                    throw new InvalidDataException("Unexpected end of VCDIFF stream while reading base-128 integer.");
                }

                var b = data[pos++];
                result = (result << 7) | (b & 0x7F);
                if ((b & 0x80) == 0)
                {
                    break;
                }
            }

            return result;
        }

        private static uint ComputeAdler32(byte[] data)
        {
            const uint modAdler = 65521;
            uint a = 1;
            uint b = 0;

            for (var i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % modAdler;
                b = (b + a) % modAdler;
            }

            return (b << 16) | a;
        }

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var c = i;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                table[i] = c;
            }

            return table;
        }

        public static uint ComputeCrc32Uint(byte[] buffer, int offset, int count)
        {
            var crc = 0xFFFFFFFFu;
            var end = offset + count;
            for (var i = offset; i < end; i++)
            {
                crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }

            return ~crc;
        }

        private static (string Crc32, string Md5, string Sha1, string Sha256) ComputeAllHashes(byte[] data)
        {
            var crc = ComputeCrc32Uint(data, 0, data.Length).ToString("x8");
            using var md5 = MD5.Create();
            using var sha1 = SHA1.Create();
            using var sha256 = SHA256.Create();

            var md5Hex = BitConverter.ToString(md5.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
            var sha1Hex = BitConverter.ToString(sha1.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
            var sha256Hex = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();

            return (crc, md5Hex, sha1Hex, sha256Hex);
        }

        private enum VcdiffOpType : byte
        {
            Noop = 0,
            Add = 1,
            Run = 2,
            Copy = 3
        }

        private readonly struct VcdiffInstruction
        {
            public VcdiffInstruction(VcdiffOpType type, byte size, byte mode)
            {
                Type = type;
                Size = size;
                Mode = mode;
            }

            public VcdiffOpType Type { get; }
            public byte Size { get; }
            public byte Mode { get; }
        }

        private readonly struct VcdiffInstructionPair
        {
            public VcdiffInstructionPair(VcdiffInstruction first, VcdiffInstruction second)
            {
                First = first;
                Second = second;
            }

            public VcdiffInstruction First { get; }
            public VcdiffInstruction Second { get; }
        }

        private sealed class VcdiffAddressCache
        {
            private const int NearSize = 4;
            private const int SameSize = 3;
            private readonly int[] _near = new int[NearSize];
            private readonly int[] _same = new int[SameSize * 256];
            private int _nextSlot;

            public int DecodeAddress(int here, byte mode, byte[] addrStream, ref int addrPos)
            {
                int addr;
                if (mode == 0)
                {
                    addr = ReadVcdiffVarInt(addrStream, ref addrPos);
                }
                else if (mode == 1)
                {
                    addr = here - ReadVcdiffVarInt(addrStream, ref addrPos);
                }
                else if (mode - 2 < NearSize)
                {
                    addr = _near[mode - 2] + ReadVcdiffVarInt(addrStream, ref addrPos);
                }
                else
                {
                    var m = mode - (2 + NearSize);
                    addr = _same[(m * 256) + addrStream[addrPos++]];
                }

                Update(addr);
                return addr;
            }

            private void Update(int addr)
            {
                _near[_nextSlot] = addr;
                _nextSlot = (_nextSlot + 1) % NearSize;
                _same[addr % (SameSize * 256)] = addr;
            }
        }

        private static VcdiffInstructionPair[] BuildDefaultVcdiffCodeTable()
        {
            var table = new VcdiffInstructionPair[256];
            var idx = 0;
            var noop = new VcdiffInstruction(VcdiffOpType.Noop, 0, 0);

            // Entry 0: RUN 0
            table[idx++] = new VcdiffInstructionPair(new VcdiffInstruction(VcdiffOpType.Run, 0, 0), noop);

            // Entries 1..18: ADD [0, 1..17]
            for (byte s = 0; s <= 17; s++)
            {
                table[idx++] = new VcdiffInstructionPair(new VcdiffInstruction(VcdiffOpType.Add, s, 0), noop);
            }

            // Entries 19..162: COPY mode 0..8, size [0, 4..18]
            for (byte m = 0; m <= 8; m++)
            {
                table[idx++] = new VcdiffInstructionPair(new VcdiffInstruction(VcdiffOpType.Copy, 0, m), noop);
                for (byte s = 4; s <= 18; s++)
                {
                    table[idx++] = new VcdiffInstructionPair(new VcdiffInstruction(VcdiffOpType.Copy, s, m), noop);
                }
            }

            // Entries 163..234: ADD size 1..4 + COPY mode 0..5 size 4..6
            for (byte m = 0; m <= 5; m++)
            {
                for (byte addSize = 1; addSize <= 4; addSize++)
                {
                    for (byte copySize = 4; copySize <= 6; copySize++)
                    {
                        table[idx++] = new VcdiffInstructionPair(
                            new VcdiffInstruction(VcdiffOpType.Add, addSize, 0),
                            new VcdiffInstruction(VcdiffOpType.Copy, copySize, m));
                    }
                }
            }

            // Entries 235..246: ADD size 1..4 + COPY mode 6..8 size 4
            for (byte m = 6; m <= 8; m++)
            {
                for (byte addSize = 1; addSize <= 4; addSize++)
                {
                    table[idx++] = new VcdiffInstructionPair(
                        new VcdiffInstruction(VcdiffOpType.Add, addSize, 0),
                        new VcdiffInstruction(VcdiffOpType.Copy, 4, m));
                }
            }

            // Entries 247..255: COPY mode 0..8 size 4 + ADD size 1
            for (byte m = 0; m <= 8; m++)
            {
                table[idx++] = new VcdiffInstructionPair(
                    new VcdiffInstruction(VcdiffOpType.Copy, 4, m),
                    new VcdiffInstruction(VcdiffOpType.Add, 1, 0));
            }

            return table;
        }
    }
}
