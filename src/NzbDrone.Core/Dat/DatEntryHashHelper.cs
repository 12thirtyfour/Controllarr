using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Dat
{
    internal static class DatEntryHashHelper
    {
        public static string NormalizeHash(string value, bool isCrc32 = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim().ToLowerInvariant();
            if (isCrc32 && string.Equals(trimmed, "00000000", StringComparison.Ordinal))
            {
                return null;
            }

            return trimmed;
        }

        public static void NormalizeEntryHashes(DatEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.Sha256 = NormalizeHash(entry.Sha256);
            entry.Sha1 = NormalizeHash(entry.Sha1);
            entry.Md5 = NormalizeHash(entry.Md5);
            entry.Crc32 = NormalizeHash(entry.Crc32, isCrc32: true);
        }

        public static bool HasAnyHash(DatEntry entry)
        {
            return entry != null &&
                   (!string.IsNullOrWhiteSpace(entry.Sha256) ||
                    !string.IsNullOrWhiteSpace(entry.Sha1) ||
                    !string.IsNullOrWhiteSpace(entry.Md5) ||
                    !string.IsNullOrWhiteSpace(entry.Crc32));
        }

        public static bool TryFindMatchingEntry(IDictionary<string, DatEntry> hashIndex, DatEntry candidate, out DatEntry existing)
        {
            existing = null;
            if (hashIndex == null || candidate == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Sha256) &&
                hashIndex.TryGetValue("sha256:" + candidate.Sha256, out var match256) &&
                !HasConflictingHash(match256, candidate))
            {
                existing = match256;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Sha1) &&
                hashIndex.TryGetValue("sha1:" + candidate.Sha1, out var match1) &&
                !HasConflictingHash(match1, candidate))
            {
                existing = match1;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Md5) &&
                hashIndex.TryGetValue("md5:" + candidate.Md5, out var match5) &&
                !HasConflictingHash(match5, candidate))
            {
                existing = match5;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Crc32) &&
                hashIndex.TryGetValue("crc:" + candidate.Crc32, out var matchCrc) &&
                !HasConflictingHash(matchCrc, candidate))
            {
                existing = matchCrc;
                return true;
            }

            return false;
        }

        public static bool HasConflictingHash(DatEntry existing, DatEntry candidate)
        {
            if (existing == null || candidate == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(existing.Sha256) &&
                !string.IsNullOrWhiteSpace(candidate.Sha256) &&
                !string.Equals(existing.Sha256, candidate.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(existing.Sha1) &&
                !string.IsNullOrWhiteSpace(candidate.Sha1) &&
                !string.Equals(existing.Sha1, candidate.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(existing.Md5) &&
                !string.IsNullOrWhiteSpace(candidate.Md5) &&
                !string.Equals(existing.Md5, candidate.Md5, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(existing.Crc32) &&
                !string.IsNullOrWhiteSpace(candidate.Crc32) &&
                !string.Equals(existing.Crc32, candidate.Crc32, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool MergeDatEntry(DatEntry target, DatEntry source, IDictionary<string, DatEntry> hashIndex = null)
        {
            if (target == null || source == null)
            {
                return false;
            }

            var changed = false;

            if (string.IsNullOrWhiteSpace(target.Sha256) && !string.IsNullOrWhiteSpace(source.Sha256))
            {
                target.Sha256 = source.Sha256;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.Sha1) && !string.IsNullOrWhiteSpace(source.Sha1))
            {
                target.Sha1 = source.Sha1;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.Md5) && !string.IsNullOrWhiteSpace(source.Md5))
            {
                target.Md5 = source.Md5;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.Crc32) && !string.IsNullOrWhiteSpace(source.Crc32))
            {
                target.Crc32 = source.Crc32;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.SmdbPath) && !string.IsNullOrWhiteSpace(source.SmdbPath))
            {
                target.SmdbPath = source.SmdbPath;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.Region) && !string.IsNullOrWhiteSpace(source.Region))
            {
                target.Region = source.Region;
                changed = true;
            }

            if (target.Size <= 0 && source.Size > 0)
            {
                target.Size = source.Size;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.GameName) && !string.IsNullOrWhiteSpace(source.GameName))
            {
                target.GameName = source.GameName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.RomName) && !string.IsNullOrWhiteSpace(source.RomName))
            {
                target.RomName = source.RomName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(target.Status) && !string.IsNullOrWhiteSpace(source.Status))
            {
                target.Status = source.Status;
                changed = true;
            }

            if (hashIndex != null)
            {
                IndexEntryHashes(hashIndex, target);
            }

            return changed;
        }

        public static void IndexEntryHashes(IDictionary<string, DatEntry> hashIndex, DatEntry entry)
        {
            if (hashIndex == null || entry == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(entry.Sha256))
            {
                hashIndex.TryAdd("sha256:" + entry.Sha256, entry);
            }

            if (!string.IsNullOrWhiteSpace(entry.Sha1))
            {
                hashIndex.TryAdd("sha1:" + entry.Sha1, entry);
            }

            if (!string.IsNullOrWhiteSpace(entry.Md5))
            {
                hashIndex.TryAdd("md5:" + entry.Md5, entry);
            }

            if (!string.IsNullOrWhiteSpace(entry.Crc32))
            {
                hashIndex.TryAdd("crc:" + entry.Crc32, entry);
            }
        }
    }
}
