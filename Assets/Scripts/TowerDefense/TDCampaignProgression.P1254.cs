using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TD
{
    [Serializable]
    public sealed class TDP1254CloudMatrixRow
    {
        public int slotId;
        public int cloudLevel;
        public int localLevel;
        public bool conflictDetected;
        public bool keepLocalPassed;
        public bool useCloudPassed;
        public bool mergePassed;
        public bool tamperedEnvelopeRejected;
        public bool wrongSlotRejected;
        public bool legacyMigrationPassed;
        public bool futureVersionRejected;
        public bool revisionMonotonic;
        public string error;
        public bool passed;
    }

    [Serializable]
    public sealed class TDP1254CloudMatrixAudit
    {
        public string schemaVersion;
        public string generatedUtc;
        public int saveVersion;
        public int slotCount;
        public bool profileRestored;
        public string restoreError;
        public TDP1254CloudMatrixRow[] rows;
        public bool passed;
    }

    public static partial class TDCampaignProgression
    {
        public static TDP1254CloudMatrixAudit DebugAuditCloudConflictMatrixForTest(int totalLevels)
        {
            var safeTotal = Mathf.Clamp(totalLevels, 8, MaxSnapshotLevels);
            var originalActiveSlot = ActiveSaveSlot;
            var originalInitialized = new bool[MaxSaveSlots];
            var originalSnapshots = new string[MaxSaveSlots];
            var rows = new List<TDP1254CloudMatrixRow>(MaxSaveSlots);
            var audit = new TDP1254CloudMatrixAudit
            {
                schemaVersion = "p1254-cloud-conflict-matrix-v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                saveVersion = SaveVersion,
                slotCount = MaxSaveSlots
            };

            var summaries = GetSaveSlotSummaries(safeTotal);
            for (var slot = 1; slot <= MaxSaveSlots; slot++)
            {
                originalInitialized[slot - 1] = summaries[slot - 1].initialized;
                if (!SetActiveSaveSlot(slot, safeTotal, out var captureError))
                {
                    audit.restoreError = captureError;
                    audit.rows = rows.ToArray();
                    return audit;
                }

                originalSnapshots[slot - 1] = originalInitialized[slot - 1]
                    ? ExportSnapshot(safeTotal)
                    : string.Empty;
            }

            try
            {
                for (var slot = 1; slot <= MaxSaveSlots; slot++)
                {
                    rows.Add(RunCloudMatrixRow(slot, safeTotal));
                }
            }
            catch (Exception exception)
            {
                rows.Add(new TDP1254CloudMatrixRow
                {
                    error = exception.Message,
                    passed = false
                });
            }
            finally
            {
                audit.profileRestored = RestoreCloudMatrixProfiles(
                    originalActiveSlot,
                    originalInitialized,
                    originalSnapshots,
                    safeTotal,
                    out var restoreError);
                audit.restoreError = restoreError;
            }

            audit.rows = rows.ToArray();
            audit.passed = audit.profileRestored &&
                           audit.rows.Length == MaxSaveSlots &&
                           Array.TrueForAll(audit.rows, row => row != null && row.passed);
            return audit;
        }

        private static TDP1254CloudMatrixRow RunCloudMatrixRow(int slotId, int totalLevels)
        {
            var row = new TDP1254CloudMatrixRow
            {
                slotId = slotId,
                cloudLevel = slotId,
                localLevel = slotId + 4
            };

            try
            {
                SetActiveSaveSlot(slotId, totalLevels, out _);
                ResetProgress(totalLevels);
                RecordResult(row.cloudLevel, true, 3, 91, 18, totalLevels, true);
                var cloudRevision = ReadLong(RevisionKey);
                var cloudCode = ExportCloudEnvelope(totalLevels);

                ResetProgress(totalLevels);
                RecordResult(row.localLevel, true, 2, 77, 11, totalLevels);
                var localRevision = ReadLong(RevisionKey);
                row.conflictDetected =
                    TryPreviewCloudEnvelope(cloudCode, totalLevels, out var conflictPreview, out _) &&
                    conflictPreview.conflictsWithLocal;

                row.keepLocalPassed =
                    TryResolveCloudEnvelope(
                        cloudCode,
                        totalLevels,
                        TDCampaignCloudConflictResolution.KeepLocal,
                        out _,
                        out _) &&
                    GetLevelProgress(row.localLevel).cleared &&
                    !GetLevelProgress(row.cloudLevel).cleared;

                ResetProgress(totalLevels);
                RecordResult(row.localLevel, true, 2, 77, 11, totalLevels);
                row.useCloudPassed =
                    TryResolveCloudEnvelope(
                        cloudCode,
                        totalLevels,
                        TDCampaignCloudConflictResolution.UseCloud,
                        out _,
                        out _) &&
                    GetLevelProgress(row.cloudLevel).cleared &&
                    !GetLevelProgress(row.localLevel).cleared;

                ResetProgress(totalLevels);
                RecordResult(row.localLevel, true, 2, 77, 11, totalLevels);
                row.mergePassed =
                    TryResolveCloudEnvelope(
                        cloudCode,
                        totalLevels,
                        TDCampaignCloudConflictResolution.Merge,
                        out _,
                        out _) &&
                    GetLevelProgress(row.cloudLevel).cleared &&
                    GetLevelProgress(row.localLevel).cleared;

                row.tamperedEnvelopeRejected =
                    !TryPreviewCloudEnvelope(
                        TamperCloudCode(cloudCode),
                        totalLevels,
                        out _,
                        out _);

                var wrongSlot = slotId == MaxSaveSlots ? 1 : slotId + 1;
                SetActiveSaveSlot(wrongSlot, totalLevels, out _);
                row.wrongSlotRejected =
                    !TryPreviewCloudEnvelope(cloudCode, totalLevels, out _, out var wrongSlotError) &&
                    !string.IsNullOrWhiteSpace(wrongSlotError);
                SetActiveSaveSlot(slotId, totalLevels, out _);

                var legacyCode = DebugExportLegacyPortableSaveForTest(totalLevels);
                ResetProgress(totalLevels);
                row.legacyMigrationPassed =
                    TryImportPortableSave(legacyCode, totalLevels, out var legacyPreview, out _) &&
                    legacyPreview.saveVersion == SaveVersion &&
                    GetLevelProgress(row.cloudLevel).cleared &&
                    GetLevelProgress(row.localLevel).cleared;

                var futureEnvelope = BuildP1254CloudEnvelopeWithVersion(totalLevels, SaveVersion + 1);
                row.futureVersionRejected =
                    !TryPreviewCloudEnvelope(futureEnvelope, totalLevels, out _, out _);
                row.revisionMonotonic = ReadLong(RevisionKey) >=
                                        Math.Max(cloudRevision, localRevision);
                row.passed = row.conflictDetected &&
                             row.keepLocalPassed &&
                             row.useCloudPassed &&
                             row.mergePassed &&
                             row.tamperedEnvelopeRejected &&
                             row.wrongSlotRejected &&
                             row.legacyMigrationPassed &&
                             row.futureVersionRejected &&
                             row.revisionMonotonic;
            }
            catch (Exception exception)
            {
                row.error = exception.Message;
                row.passed = false;
            }

            return row;
        }

        private static string BuildP1254CloudEnvelopeWithVersion(int totalLevels, int saveVersion)
        {
            var envelope = new TDCampaignCloudEnvelope
            {
                schemaVersion = 1,
                saveVersion = saveVersion,
                slotId = ActiveSaveSlot,
                revision = ReadLong(RevisionKey),
                modifiedUtcTicks = ReadLong(ModifiedUtcKey),
                deviceId = GetOrCreateDeviceId(),
                portableSave = ExportPortableSave(totalLevels)
            };
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope)));
            return CloudSavePrefix + BuildPortableSaveFingerprint(payload) + ":" + payload;
        }

        private static string TamperCloudCode(string cloudCode)
        {
            if (string.IsNullOrEmpty(cloudCode))
            {
                return "invalid";
            }

            var chars = cloudCode.ToCharArray();
            var index = chars.Length - 1;
            chars[index] = chars[index] == 'A' ? 'B' : 'A';
            return new string(chars);
        }

        private static bool RestoreCloudMatrixProfiles(
            int activeSlot,
            bool[] initialized,
            string[] snapshots,
            int totalLevels,
            out string error)
        {
            error = string.Empty;
            try
            {
                for (var slot = 1; slot <= MaxSaveSlots; slot++)
                {
                    if (!SetActiveSaveSlot(slot, totalLevels, out error))
                    {
                        return false;
                    }

                    ImportSnapshot(initialized[slot - 1] ? snapshots[slot - 1] : string.Empty, totalLevels);
                }

                return SetActiveSaveSlot(activeSlot, totalLevels, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
