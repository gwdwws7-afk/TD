using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TD
{
    public enum TDResonanceDoctrine
    {
        Adaptive = 0,
        EmberSurge = 1,
        FractureMark = 2
    }

    public enum TDCampaignDifficultyTier
    {
        Standard = 0,
        Veteran = 1,
        EmberTrial = 2
    }

    [Serializable]
    public sealed class TDCampaignLevelProgress
    {
        public int levelIndex;
        public bool cleared;
        public int attempts;
        public int bestStars;
        public int bestTacticalScore;
        public int bestIntegrity;
        public bool contractCompleted;
        public string towerLoadout;
        public int resonanceDoctrine;
        public int difficultyPreference;
        public int highestDifficultyCleared;
    }

    public sealed class TDCampaignProgressSummary
    {
        public int totalLevels;
        public int clearedLevels;
        public int earnedStars;
        public int availableStars;
        public int completedContracts;
        public int availableContracts;
        public int highestUnlockedLevel;
        public int veteranClears;
        public int emberTrialClears;
    }

    public sealed class TDCampaignChapterProgressSummary
    {
        public string chapterId;
        public int totalLevels;
        public int clearedLevels;
        public int earnedStars;
        public int availableStars;
        public int completedContracts;
        public int availableContracts;
        public bool cleared;
        public bool mastered;
        public bool rewardClaimed;
        public int veteranClears;
        public int emberTrialClears;
    }

    public sealed class TDCampaignPortableSavePreview
    {
        public int saveVersion;
        public int codeLength;
        public string fingerprint;
        public int claimedChapterRewards;
        public string[] claimedRewardIds;
        public string[] claimedMetaRewardIds;
        public string[] unlockedProtocolIds;
        public TDCampaignProgressSummary progress;
    }

    public enum TDCampaignCloudConflictResolution
    {
        KeepLocal = 0,
        UseCloud = 1,
        Merge = 2
    }

    public sealed class TDCampaignSaveSlotSummary
    {
        public int slotId;
        public bool initialized;
        public long revision;
        public long modifiedUtcTicks;
        public string fingerprint;
        public TDCampaignProgressSummary progress;
    }

    public sealed class TDCampaignCloudPreview
    {
        public int slotId;
        public long revision;
        public long modifiedUtcTicks;
        public string deviceId;
        public string fingerprint;
        public bool conflictsWithLocal;
        public string[] claimedRewardIds;
        public string[] claimedMetaRewardIds;
        public string[] unlockedProtocolIds;
        public TDCampaignProgressSummary progress;
    }

    [Serializable]
    public sealed class TDCampaignRecoveryAudit
    {
        public string schemaVersion;
        public string generatedUtc;
        public int slotId;
        public int totalLevels;
        public bool baselineValid;
        public bool primaryRecoveryValid;
        public bool corruptionDetected;
        public bool recoverySucceeded;
        public bool snapshotRestored;
        public bool tamperedRecoveryRejected;
        public bool passed;
        public string recoverySource;
        public string recoveryPath;
        public string baselineFingerprint;
        public string restoredFingerprint;
        public string error;
    }

    public sealed class TDCampaignProgressUpdate
    {
        public int levelIndex;
        public bool victory;
        public bool firstClear;
        public bool newBestStars;
        public bool newBestScore;
        public bool firstContractCompletion;
        public bool contractCompleted;
        public bool nextLevelUnlocked;
        public int earnedStars;
        public int bestStars;
        public int bestTacticalScore;
        public int highestUnlockedLevel;
        public int highestDifficultyCleared;
    }

    [Serializable]
    public sealed class TDCampaignObservationRecord
    {
        public string entryId;
        public int flags;
    }

    [Serializable]
    public sealed class TDCampaignProtocolSelectionRecord
    {
        public int levelIndex;
        public string protocolId;
    }

    [Serializable]
    internal sealed class TDCampaignProgressSnapshot
    {
        public bool initialized;
        public int saveVersion;
        public int highestUnlockedLevel;
        public string[] claimedChapterRewards;
        public string[] claimedMetaRewards;
        public string[] unlockedProtocols;
        public TDCampaignObservationRecord[] enemyObservations;
        public TDCampaignObservationRecord[] towerObservations;
        public TDCampaignProtocolSelectionRecord[] protocolSelections;
        public TDCampaignLevelProgress[] levels;
    }

    [Serializable]
    internal sealed class TDCampaignCloudEnvelope
    {
        public int schemaVersion;
        public int saveVersion;
        public int slotId;
        public long revision;
        public long modifiedUtcTicks;
        public string deviceId;
        public string portableSave;
    }

    public static partial class TDCampaignProgression
    {
        public const int SaveVersion = 2;
        public const int MaxFormationTowers = 4;
        public const int MaxSaveSlots = 3;
        public const string PortableSavePrefix = "EMBERLINE-SAVE-2:";
        public const string CloudSavePrefix = "EMBERLINE-CLOUD-1:";

        private const string LegacyPortableSavePrefix = "EMBERLINE-SAVE-1:";
        private const string LegacyPrefix = "td_campaign_progress_v1";
        private const string SlotPrefix = "td_campaign_progress_v2_slot";
        private const string ActiveSlotKey = "td_campaign_progress_v2_active_slot";
        private const string SlotsMigratedKey = "td_campaign_progress_v2_slots_migrated";
        private const string DeviceIdKey = "td_campaign_progress_v2_device_id";
        private static string VersionKey => GetSlotKey("version");
        private static string HighestUnlockedKey => GetSlotKey("highest_unlocked");
        private static string ClaimedRewardsKey => GetSlotKey("claimed_chapter_rewards");
        private static string ClaimedMetaRewardsKey => GetSlotKey("claimed_meta_rewards");
        private static string UnlockedProtocolsKey => GetSlotKey("unlocked_protocols");
        private static string EnemyObservationsKey => GetSlotKey("enemy_observations");
        private static string TowerObservationsKey => GetSlotKey("tower_observations");
        private static string ProtocolSelectionsKey => GetSlotKey("protocol_selections");
        private static string RevisionKey => GetSlotKey("revision");
        private static string ModifiedUtcKey => GetSlotKey("modified_utc_ticks");
        private static string TotalLevelsKey => GetSlotKey("campaign_total_levels");
        private static string SnapshotChecksumKey => GetSlotKey("snapshot_checksum");
        private static string RecoveryCacheKey => GetSlotKey("recovery_snapshot");
        private const int MaxSnapshotLevels = 64;
        private const int MaxClaimedChapterRewards = 32;
        private const int MaxMetaRecords = 128;
        private static bool _recoveryWriteInProgress;

        public static int ActiveSaveSlot => Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 1), 1, MaxSaveSlots);

        public static void EnsureInitialized(int selectedLevel, int totalLevels)
        {
            var safeTotal = Mathf.Max(1, totalLevels);
            var safeSelected = Mathf.Clamp(selectedLevel, 1, safeTotal);
            var changed = false;

            EnsureSlotMigration(safeTotal);
            if (PlayerPrefs.GetInt(TotalLevelsKey, 0) != safeTotal)
            {
                PlayerPrefs.SetInt(TotalLevelsKey, safeTotal);
                changed = true;
            }

            if (PlayerPrefs.HasKey(VersionKey) &&
                PlayerPrefs.HasKey(SnapshotChecksumKey) &&
                !TryValidateActiveSlot(safeTotal, out var integrityError))
            {
                if (TryRecoverActiveSlot(safeTotal, out var recoverySource, out var recoveryError))
                {
                    Debug.LogWarning(
                        $"[TD][P12.5.3] Active save slot {ActiveSaveSlot} recovered from {recoverySource}: {integrityError}");
                    changed = true;
                }
                else
                {
                    Debug.LogError(
                        $"[TD][P12.5.3] Active save slot {ActiveSaveSlot} is corrupt and could not be recovered. " +
                        $"{integrityError} | {recoveryError}");
                }
            }

            if (!PlayerPrefs.HasKey(VersionKey))
            {
                PlayerPrefs.SetInt(VersionKey, SaveVersion);
                PlayerPrefs.SetInt(HighestUnlockedKey, safeSelected);
                TouchActiveSlot();
                changed = true;
            }
            else
            {
                var version = PlayerPrefs.GetInt(VersionKey, SaveVersion);
                if (version != SaveVersion)
                {
                    PlayerPrefs.SetInt(VersionKey, SaveVersion);
                    TouchActiveSlot();
                    changed = true;
                }

                // Preserve existing developer/test routes when P8 is first introduced.
                var highest = GetHighestUnlockedLevel(safeTotal);
                if (safeSelected > highest)
                {
                    PlayerPrefs.SetInt(HighestUnlockedKey, safeSelected);
                    TouchActiveSlot();
                    changed = true;
                }
            }

            if (!PlayerPrefs.HasKey(SnapshotChecksumKey))
            {
                PersistRecoverySnapshot(safeTotal);
                changed = true;
            }

            if (changed)
            {
                PlayerPrefs.Save();
            }
        }

        public static bool SetActiveSaveSlot(int slotId, int totalLevels, out string error)
        {
            error = string.Empty;
            if (slotId < 1 || slotId > MaxSaveSlots)
            {
                error = $"Save slot must be between 1 and {MaxSaveSlots}.";
                return false;
            }

            EnsureSlotMigration(Mathf.Max(1, totalLevels));
            PlayerPrefs.SetInt(ActiveSlotKey, slotId);
            PlayerPrefs.SetInt(TotalLevelsKey, Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels));
            if (!PlayerPrefs.HasKey(VersionKey))
            {
                PlayerPrefs.SetInt(VersionKey, SaveVersion);
                PlayerPrefs.SetInt(HighestUnlockedKey, 1);
                TouchActiveSlot();
            }

            PlayerPrefs.Save();
            return true;
        }

        public static bool TryValidateActiveSlot(int totalLevels, out string error)
        {
            error = string.Empty;
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            if (!PlayerPrefs.HasKey(VersionKey))
            {
                error = "Active save slot is not initialized.";
                return false;
            }

            var snapshot = BuildSnapshotForSlot(ActiveSaveSlot, safeTotal);
            if (!ValidateSnapshot(snapshot, safeTotal, out error))
            {
                return false;
            }

            var expected = PlayerPrefs.GetString(SnapshotChecksumKey, string.Empty);
            if (string.IsNullOrWhiteSpace(expected))
            {
                error = "Active save slot does not contain a local integrity checksum.";
                return false;
            }

            var actual = BuildPortableSaveFingerprint(JsonUtility.ToJson(snapshot));
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Active save checksum mismatch. expected={expected} actual={actual}";
                return false;
            }

            return true;
        }

        public static bool TryCreateRecoverySnapshot(int totalLevels, out string recoveryPath, out string error)
        {
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            PlayerPrefs.SetInt(TotalLevelsKey, safeTotal);
            if (!PersistRecoverySnapshot(safeTotal))
            {
                recoveryPath = GetRecoveryPath(ActiveSaveSlot);
                error = "Active save could not be validated or its recovery snapshot could not be written.";
                return false;
            }

            PlayerPrefs.Save();
            recoveryPath = GetRecoveryPath(ActiveSaveSlot);
            error = string.Empty;
            return true;
        }

        public static bool TryRecoverActiveSlot(
            int totalLevels,
            out string recoverySource,
            out string error)
        {
            recoverySource = string.Empty;
            error = string.Empty;
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            var slotId = ActiveSaveSlot;
            var candidates = new[]
            {
                new KeyValuePair<string, string>("primary_file", ReadRecoveryFile(GetRecoveryPath(slotId))),
                new KeyValuePair<string, string>("previous_file", ReadRecoveryFile(GetRecoveryPreviousPath(slotId))),
                new KeyValuePair<string, string>(
                    "playerprefs_cache",
                    PlayerPrefs.GetString(RecoveryCacheKey, string.Empty))
            };
            var failures = new List<string>();
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate.Value))
                {
                    failures.Add($"{candidate.Key}:missing");
                    continue;
                }

                if (!TryDecodePortableSave(candidate.Value, safeTotal, out var snapshot, out var decodeError))
                {
                    failures.Add($"{candidate.Key}:{decodeError}");
                    continue;
                }

                ImportSnapshot(JsonUtility.ToJson(snapshot), safeTotal);
                if (!TryValidateActiveSlot(safeTotal, out var validationError))
                {
                    failures.Add($"{candidate.Key}:{validationError}");
                    continue;
                }

                recoverySource = candidate.Key;
                return true;
            }

            error = failures.Count == 0
                ? "No recovery snapshot is available."
                : string.Join(" | ", failures);
            return false;
        }

        public static TDCampaignRecoveryAudit DebugAuditRecoveryForTest(int totalLevels)
        {
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            EnsureInitialized(1, safeTotal);
            var originalSnapshot = ExportSnapshot(safeTotal);
            var originalSnapshotData = JsonUtility.FromJson<TDCampaignProgressSnapshot>(originalSnapshot);
            var originalPortable = ExportPortableSave(safeTotal);
            var audit = new TDCampaignRecoveryAudit
            {
                schemaVersion = "p1253-save-recovery-audit-v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                slotId = ActiveSaveSlot,
                totalLevels = safeTotal,
                baselineFingerprint = ReadPortableSaveFingerprint(originalPortable)
            };

            try
            {
                audit.baselineValid = TryValidateActiveSlot(safeTotal, out var baselineError);
                audit.primaryRecoveryValid =
                    TryCreateRecoverySnapshot(safeTotal, out audit.recoveryPath, out var recoveryError) &&
                    TryPreviewPortableSave(
                        ReadRecoveryFile(audit.recoveryPath),
                        safeTotal,
                        out _,
                        out _);

                var tamperedFrontier = originalSnapshotData != null &&
                                       originalSnapshotData.highestUnlockedLevel == 1
                    ? Mathf.Min(safeTotal, 2)
                    : 1;
                PlayerPrefs.SetInt(HighestUnlockedKey, tamperedFrontier);
                PlayerPrefs.Save();
                audit.corruptionDetected = !TryValidateActiveSlot(safeTotal, out _);
                EnsureInitialized(1, safeTotal);
                audit.recoverySucceeded = TryValidateActiveSlot(safeTotal, out var restoreError);
                audit.recoverySource = audit.recoverySucceeded
                    ? "ensure_initialized"
                    : string.Empty;
                var restoredPortable = ExportPortableSave(safeTotal);
                audit.restoredFingerprint = ReadPortableSaveFingerprint(restoredPortable);
                audit.snapshotRestored = string.Equals(
                    originalSnapshot,
                    ExportSnapshot(safeTotal),
                    StringComparison.Ordinal);

                var tampered = originalPortable.Substring(0, originalPortable.Length - 1) +
                               (originalPortable.EndsWith("A", StringComparison.Ordinal) ? "B" : "A");
                audit.tamperedRecoveryRejected =
                    !TryPreviewPortableSave(tampered, safeTotal, out _, out _);
                audit.error = string.Join(
                    " | ",
                    new[] { baselineError, recoveryError, restoreError }
                        .Where(item => !string.IsNullOrWhiteSpace(item)));
                audit.passed = audit.baselineValid &&
                               audit.primaryRecoveryValid &&
                               audit.corruptionDetected &&
                               audit.recoverySucceeded &&
                               audit.snapshotRestored &&
                               audit.tamperedRecoveryRejected;
            }
            finally
            {
                if (!string.Equals(originalSnapshot, ExportSnapshot(safeTotal), StringComparison.Ordinal))
                {
                    ImportSnapshot(originalSnapshot, safeTotal);
                }
            }

            return audit;
        }

        public static TDCampaignSaveSlotSummary[] GetSaveSlotSummaries(int totalLevels)
        {
            EnsureSlotMigration(Mathf.Max(1, totalLevels));
            var result = new TDCampaignSaveSlotSummary[MaxSaveSlots];
            for (var slot = 1; slot <= MaxSaveSlots; slot++)
            {
                var snapshot = BuildSnapshotForSlot(slot, totalLevels);
                var json = JsonUtility.ToJson(snapshot);
                result[slot - 1] = new TDCampaignSaveSlotSummary
                {
                    slotId = slot,
                    initialized = snapshot.initialized,
                    revision = ReadLong(GetSlotKey(slot, "revision")),
                    modifiedUtcTicks = ReadLong(GetSlotKey(slot, "modified_utc_ticks")),
                    fingerprint = BuildPortableSaveFingerprint(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))),
                    progress = BuildSnapshotSummary(snapshot, Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels))
                };
            }

            return result;
        }

        public static string ExportCloudEnvelope(int totalLevels)
        {
            EnsureInitialized(1, totalLevels);
            var envelope = new TDCampaignCloudEnvelope
            {
                schemaVersion = 1,
                saveVersion = SaveVersion,
                slotId = ActiveSaveSlot,
                revision = ReadLong(RevisionKey),
                modifiedUtcTicks = ReadLong(ModifiedUtcKey),
                deviceId = GetOrCreateDeviceId(),
                portableSave = ExportPortableSave(totalLevels)
            };
            var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope)));
            return CloudSavePrefix + BuildPortableSaveFingerprint(payload) + ":" + payload;
        }

        public static bool TryPreviewCloudEnvelope(
            string cloudCode,
            int totalLevels,
            out TDCampaignCloudPreview preview,
            out string error)
        {
            preview = null;
            if (!TryDecodeCloudEnvelope(cloudCode, totalLevels, out var envelope, out var snapshot, out error))
            {
                return false;
            }

            var localRevision = ReadLong(RevisionKey);
            var localModified = ReadLong(ModifiedUtcKey);
            preview = new TDCampaignCloudPreview
            {
                slotId = envelope.slotId,
                revision = envelope.revision,
                modifiedUtcTicks = envelope.modifiedUtcTicks,
                deviceId = envelope.deviceId,
                fingerprint = ReadEnvelopeFingerprint(cloudCode, CloudSavePrefix),
                conflictsWithLocal = envelope.revision != localRevision || envelope.modifiedUtcTicks != localModified,
                claimedRewardIds = NormalizeRewardIds(snapshot.claimedChapterRewards).ToArray(),
                claimedMetaRewardIds = NormalizeRewardIds(snapshot.claimedMetaRewards).ToArray(),
                unlockedProtocolIds = NormalizeRewardIds(snapshot.unlockedProtocols).ToArray(),
                progress = BuildSnapshotSummary(snapshot, Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels))
            };
            return true;
        }

        public static bool TryResolveCloudEnvelope(
            string cloudCode,
            int totalLevels,
            TDCampaignCloudConflictResolution resolution,
            out TDCampaignCloudPreview preview,
            out string error)
        {
            preview = null;
            if (!TryDecodeCloudEnvelope(cloudCode, totalLevels, out var envelope, out var cloudSnapshot, out error))
            {
                return false;
            }

            if (resolution == TDCampaignCloudConflictResolution.KeepLocal)
            {
                return TryPreviewCloudEnvelope(ExportCloudEnvelope(totalLevels), totalLevels, out preview, out error);
            }

            var snapshot = resolution == TDCampaignCloudConflictResolution.Merge
                ? MergeSnapshots(BuildSnapshotForSlot(ActiveSaveSlot, totalLevels), cloudSnapshot, envelope.modifiedUtcTicks >= ReadLong(ModifiedUtcKey), totalLevels)
                : cloudSnapshot;
            ImportSnapshot(JsonUtility.ToJson(snapshot), totalLevels);
            PlayerPrefs.SetString(RevisionKey, Math.Max(ReadLong(RevisionKey), envelope.revision).ToString());
            PlayerPrefs.SetString(ModifiedUtcKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
            return TryPreviewCloudEnvelope(ExportCloudEnvelope(totalLevels), totalLevels, out preview, out error);
        }

        public static int GetHighestUnlockedLevel(int totalLevels)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedKey, 1), 1, Mathf.Max(1, totalLevels));
        }

        public static bool IsLevelUnlocked(int levelIndex, int totalLevels)
        {
            return levelIndex >= 1 &&
                   levelIndex <= Mathf.Max(1, totalLevels) &&
                   levelIndex <= GetHighestUnlockedLevel(totalLevels);
        }

        public static TDCampaignLevelProgress GetLevelProgress(int levelIndex)
        {
            var safeLevel = Mathf.Max(1, levelIndex);
            return new TDCampaignLevelProgress
            {
                levelIndex = safeLevel,
                cleared = PlayerPrefs.GetInt(GetLevelKey(safeLevel, "cleared"), 0) > 0,
                attempts = Mathf.Max(0, PlayerPrefs.GetInt(GetLevelKey(safeLevel, "attempts"), 0)),
                bestStars = Mathf.Clamp(PlayerPrefs.GetInt(GetLevelKey(safeLevel, "stars"), 0), 0, 3),
                bestTacticalScore = Mathf.Clamp(PlayerPrefs.GetInt(GetLevelKey(safeLevel, "score"), 0), 0, 100),
                bestIntegrity = Mathf.Max(0, PlayerPrefs.GetInt(GetLevelKey(safeLevel, "integrity"), 0)),
                contractCompleted = PlayerPrefs.GetInt(GetLevelKey(safeLevel, "contract"), 0) > 0,
                towerLoadout = PlayerPrefs.GetString(GetLevelKey(safeLevel, "loadout"), string.Empty),
                resonanceDoctrine = Mathf.Clamp(
                    PlayerPrefs.GetInt(GetLevelKey(safeLevel, "doctrine"), (int)TDResonanceDoctrine.Adaptive),
                    (int)TDResonanceDoctrine.Adaptive,
                    (int)TDResonanceDoctrine.FractureMark),
                difficultyPreference = Mathf.Clamp(
                    PlayerPrefs.GetInt(GetLevelKey(safeLevel, "difficulty_preference"), (int)TDCampaignDifficultyTier.Standard),
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial),
                highestDifficultyCleared = Mathf.Clamp(
                    PlayerPrefs.GetInt(GetLevelKey(safeLevel, "difficulty_cleared"), (int)TDCampaignDifficultyTier.Standard),
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial)
            };
        }

        public static string[] GetTowerLoadout(int levelIndex)
        {
            var raw = GetLevelProgress(levelIndex).towerLoadout;
            return string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static TDResonanceDoctrine GetResonanceDoctrine(int levelIndex)
        {
            return (TDResonanceDoctrine)GetLevelProgress(levelIndex).resonanceDoctrine;
        }

        public static TDCampaignDifficultyTier GetDifficultyPreference(int levelIndex)
        {
            return (TDCampaignDifficultyTier)GetLevelProgress(levelIndex).difficultyPreference;
        }

        public static void SaveDifficultyPreference(int levelIndex, TDCampaignDifficultyTier difficulty)
        {
            var safeLevel = Mathf.Max(1, levelIndex);
            PlayerPrefs.SetInt(
                GetLevelKey(safeLevel, "difficulty_preference"),
                Mathf.Clamp(
                    (int)difficulty,
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial));
            TouchActiveSlot();
            PlayerPrefs.Save();
        }

        public static string[] GetClaimedChapterRewardIds()
        {
            var raw = PlayerPrefs.GetString(ClaimedRewardsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            var normalized = NormalizeRewardIds(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            return normalized.ToArray();
        }

        public static bool IsChapterRewardClaimed(string rewardId)
        {
            var normalized = NormalizeRewardId(rewardId);
            if (normalized.Length == 0)
            {
                return false;
            }

            var claimed = GetClaimedChapterRewardIds();
            for (var i = 0; i < claimed.Length; i++)
            {
                if (string.Equals(claimed[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ClaimChapterReward(string rewardId)
        {
            var normalized = NormalizeRewardId(rewardId);
            if (normalized.Length == 0)
            {
                return false;
            }

            var claimed = NormalizeRewardIds(GetClaimedChapterRewardIds());
            if (claimed.Contains(normalized) || claimed.Count >= MaxClaimedChapterRewards)
            {
                return false;
            }

            claimed.Add(normalized);
            claimed.Sort(StringComparer.OrdinalIgnoreCase);
            PlayerPrefs.SetString(ClaimedRewardsKey, string.Join(",", claimed));
            TouchActiveSlot();
            PlayerPrefs.Save();
            return true;
        }

        public static string[] GetClaimedMetaRewardIds()
        {
            return ReadNormalizedIds(ClaimedMetaRewardsKey);
        }

        public static string[] GetUnlockedProtocolIds()
        {
            return ReadNormalizedIds(UnlockedProtocolsKey);
        }

        public static bool IsProtocolUnlocked(string protocolId)
        {
            var normalized = NormalizeRewardId(protocolId);
            return normalized.Length > 0 && GetUnlockedProtocolIds().Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ClaimMetaReward(string rewardId, string protocolId)
        {
            var normalizedReward = NormalizeRewardId(rewardId);
            var normalizedProtocol = NormalizeRewardId(protocolId);
            if (normalizedReward.Length == 0 || normalizedProtocol.Length == 0)
            {
                return false;
            }

            var claimed = NormalizeRewardIds(GetClaimedMetaRewardIds());
            if (claimed.Contains(normalizedReward) || claimed.Count >= MaxMetaRecords)
            {
                return false;
            }

            var protocols = NormalizeRewardIds(GetUnlockedProtocolIds());
            if (!protocols.Contains(normalizedProtocol) && protocols.Count < MaxMetaRecords)
            {
                protocols.Add(normalizedProtocol);
                protocols.Sort(StringComparer.OrdinalIgnoreCase);
            }

            claimed.Add(normalizedReward);
            claimed.Sort(StringComparer.OrdinalIgnoreCase);
            PlayerPrefs.SetString(ClaimedMetaRewardsKey, string.Join(",", claimed));
            PlayerPrefs.SetString(UnlockedProtocolsKey, string.Join(",", protocols));
            TouchActiveSlot();
            PlayerPrefs.Save();
            return true;
        }

        public static int GetEnemyObservationFlags(string enemyId)
        {
            return GetObservationFlags(ReadObservationRecords(EnemyObservationsKey), enemyId);
        }

        public static int GetTowerObservationFlags(string towerId)
        {
            return GetObservationFlags(ReadObservationRecords(TowerObservationsKey), towerId);
        }

        public static bool RecordEnemyObservation(string enemyId, int flags)
        {
            return RecordObservation(EnemyObservationsKey, enemyId, flags);
        }

        public static bool RecordTowerObservation(string towerId, int flags)
        {
            return RecordObservation(TowerObservationsKey, towerId, flags);
        }

        public static TDCampaignObservationRecord[] GetEnemyObservations()
        {
            return ReadObservationRecords(EnemyObservationsKey);
        }

        public static TDCampaignObservationRecord[] GetTowerObservations()
        {
            return ReadObservationRecords(TowerObservationsKey);
        }

        public static void ResetCodexObservations()
        {
            PlayerPrefs.DeleteKey(EnemyObservationsKey);
            PlayerPrefs.DeleteKey(TowerObservationsKey);
            TouchActiveSlot();
            PlayerPrefs.Save();
        }

        public static string GetTacticalProtocol(int levelIndex)
        {
            var selections = ReadProtocolSelections(ProtocolSelectionsKey, MaxSnapshotLevels);
            var record = selections.FirstOrDefault(item => item.levelIndex == Mathf.Max(1, levelIndex));
            return record?.protocolId ?? "baseline";
        }

        public static bool SaveTacticalProtocol(int levelIndex, string protocolId)
        {
            var safeLevel = Mathf.Clamp(levelIndex, 1, MaxSnapshotLevels);
            var normalized = NormalizeRewardId(protocolId);
            if (normalized.Length == 0 || (!string.Equals(normalized, "baseline", StringComparison.OrdinalIgnoreCase) && !IsProtocolUnlocked(normalized)))
            {
                return false;
            }

            var selections = ReadProtocolSelections(ProtocolSelectionsKey, MaxSnapshotLevels).ToList();
            selections.RemoveAll(item => item.levelIndex == safeLevel);
            if (!string.Equals(normalized, "baseline", StringComparison.OrdinalIgnoreCase))
            {
                selections.Add(new TDCampaignProtocolSelectionRecord { levelIndex = safeLevel, protocolId = normalized });
                selections.Sort((a, b) => a.levelIndex.CompareTo(b.levelIndex));
            }

            WriteProtocolSelections(ProtocolSelectionsKey, selections);
            TouchActiveSlot();
            PlayerPrefs.Save();
            return true;
        }

        public static void SaveFormation(
            int levelIndex,
            IEnumerable<string> towerIds,
            TDResonanceDoctrine doctrine)
        {
            var safeLevel = Mathf.Max(1, levelIndex);
            var normalized = NormalizeTowerLoadout(towerIds);
            if (normalized.Count == 0)
            {
                return;
            }

            PlayerPrefs.SetString(GetLevelKey(safeLevel, "loadout"), string.Join(",", normalized));
            PlayerPrefs.SetInt(
                GetLevelKey(safeLevel, "doctrine"),
                Mathf.Clamp(
                    (int)doctrine,
                    (int)TDResonanceDoctrine.Adaptive,
                    (int)TDResonanceDoctrine.FractureMark));
            TouchActiveSlot();
            PlayerPrefs.Save();
        }

        public static TDCampaignProgressSummary BuildSummary(int totalLevels)
        {
            var safeTotal = Mathf.Max(1, totalLevels);
            var summary = new TDCampaignProgressSummary
            {
                totalLevels = safeTotal,
                availableStars = safeTotal * 3,
                availableContracts = safeTotal,
                highestUnlockedLevel = GetHighestUnlockedLevel(safeTotal)
            };

            for (var level = 1; level <= safeTotal; level++)
            {
                var progress = GetLevelProgress(level);
                if (progress.cleared)
                {
                    summary.clearedLevels++;
                }

                summary.earnedStars += progress.bestStars;
                if (progress.contractCompleted)
                {
                    summary.completedContracts++;
                }

                if (progress.cleared && progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.Veteran)
                {
                    summary.veteranClears++;
                }

                if (progress.cleared && progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.EmberTrial)
                {
                    summary.emberTrialClears++;
                }
            }

            return summary;
        }

        public static TDCampaignChapterProgressSummary BuildChapterSummary(TDCampaignChapterDefinition chapter)
        {
            if (chapter == null)
            {
                return new TDCampaignChapterProgressSummary();
            }

            var start = Mathf.Max(1, chapter.startLevel);
            var end = Mathf.Max(start, chapter.endLevel);
            var summary = new TDCampaignChapterProgressSummary
            {
                chapterId = chapter.chapterId,
                totalLevels = end - start + 1,
                availableStars = (end - start + 1) * 3,
                availableContracts = end - start + 1,
                rewardClaimed = IsChapterRewardClaimed(chapter.reward?.rewardId)
            };

            for (var level = start; level <= end; level++)
            {
                var progress = GetLevelProgress(level);
                if (progress.cleared)
                {
                    summary.clearedLevels++;
                }

                summary.earnedStars += progress.bestStars;
                if (progress.contractCompleted)
                {
                    summary.completedContracts++;
                }

                if (progress.cleared && progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.Veteran)
                {
                    summary.veteranClears++;
                }

                if (progress.cleared && progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.EmberTrial)
                {
                    summary.emberTrialClears++;
                }
            }

            summary.cleared = summary.clearedLevels == summary.totalLevels;
            summary.mastered = summary.cleared &&
                               summary.earnedStars == summary.availableStars &&
                               summary.completedContracts == summary.availableContracts;
            return summary;
        }

        public static TDCampaignProgressUpdate RecordResult(
            int levelIndex,
            bool victory,
            int earnedStars,
            int tacticalScore,
            int integrity,
            int totalLevels,
            bool contractCompleted = false,
            TDCampaignDifficultyTier difficulty = TDCampaignDifficultyTier.Standard)
        {
            var safeTotal = Mathf.Max(1, totalLevels);
            var safeLevel = Mathf.Clamp(levelIndex, 1, safeTotal);
            if (!PlayerPrefs.HasKey(VersionKey))
            {
                EnsureInitialized(1, safeTotal);
            }

            var previous = GetLevelProgress(safeLevel);
            var previousHighest = GetHighestUnlockedLevel(safeTotal);
            var safeStars = victory ? Mathf.Clamp(earnedStars, 1, 3) : 0;
            var safeScore = Mathf.Clamp(tacticalScore, 0, 100);
            var safeIntegrity = Mathf.Max(0, integrity);
            var safeDifficulty = Mathf.Clamp(
                (int)difficulty,
                (int)TDCampaignDifficultyTier.Standard,
                (int)TDCampaignDifficultyTier.EmberTrial);

            PlayerPrefs.SetInt(GetLevelKey(safeLevel, "attempts"), previous.attempts + 1);
            if (victory)
            {
                PlayerPrefs.SetInt(GetLevelKey(safeLevel, "cleared"), 1);
                PlayerPrefs.SetInt(GetLevelKey(safeLevel, "stars"), Mathf.Max(previous.bestStars, safeStars));
                PlayerPrefs.SetInt(GetLevelKey(safeLevel, "score"), Mathf.Max(previous.bestTacticalScore, safeScore));
                PlayerPrefs.SetInt(GetLevelKey(safeLevel, "integrity"), Mathf.Max(previous.bestIntegrity, safeIntegrity));
                if (contractCompleted)
                {
                    PlayerPrefs.SetInt(GetLevelKey(safeLevel, "contract"), 1);
                }

                PlayerPrefs.SetInt(
                    GetLevelKey(safeLevel, "difficulty_cleared"),
                    Mathf.Max(previous.highestDifficultyCleared, safeDifficulty));

                var unlocked = Mathf.Min(safeTotal, safeLevel + 1);
                if (unlocked > previousHighest)
                {
                    PlayerPrefs.SetInt(HighestUnlockedKey, unlocked);
                }
            }

            TouchActiveSlot();
            PlayerPrefs.Save();
            var current = GetLevelProgress(safeLevel);
            var currentHighest = GetHighestUnlockedLevel(safeTotal);
            return new TDCampaignProgressUpdate
            {
                levelIndex = safeLevel,
                victory = victory,
                firstClear = victory && !previous.cleared,
                newBestStars = victory && current.bestStars > previous.bestStars,
                newBestScore = victory && current.bestTacticalScore > previous.bestTacticalScore,
                firstContractCompletion = victory && contractCompleted && !previous.contractCompleted,
                contractCompleted = current.contractCompleted,
                nextLevelUnlocked = victory && currentHighest > previousHighest,
                earnedStars = safeStars,
                bestStars = current.bestStars,
                bestTacticalScore = current.bestTacticalScore,
                highestUnlockedLevel = currentHighest,
                highestDifficultyCleared = current.highestDifficultyCleared
            };
        }

        public static string ExportSnapshot(int totalLevels)
        {
            return JsonUtility.ToJson(BuildSnapshotForSlot(ActiveSaveSlot, totalLevels));
        }

        public static string ExportPortableSave(int totalLevels)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(ExportSnapshot(totalLevels));
            var payload = Convert.ToBase64String(bytes);
            return PortableSavePrefix + BuildPortableSaveFingerprint(payload) + ":" + payload;
        }

        public static string DebugExportLegacyPortableSaveForTest(int totalLevels)
        {
            var snapshot = BuildSnapshotForSlot(ActiveSaveSlot, totalLevels);
            snapshot.saveVersion = 1;
            var payload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot)));
            return LegacyPortableSavePrefix + BuildPortableSaveFingerprint(payload) + ":" + payload;
        }

        public static bool TryPreviewPortableSave(
            string portableSave,
            int totalLevels,
            out TDCampaignPortableSavePreview preview,
            out string error)
        {
            preview = null;
            if (!TryDecodePortableSave(portableSave, totalLevels, out var snapshot, out error))
            {
                return false;
            }

            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            var claimedRewardIds = NormalizeRewardIds(snapshot.claimedChapterRewards);
            preview = new TDCampaignPortableSavePreview
            {
                saveVersion = snapshot.saveVersion,
                codeLength = portableSave.Trim().Length,
                fingerprint = ReadPortableSaveFingerprint(portableSave.Trim()),
                claimedChapterRewards = claimedRewardIds.Count,
                claimedRewardIds = claimedRewardIds.ToArray(),
                claimedMetaRewardIds = NormalizeRewardIds(snapshot.claimedMetaRewards).ToArray(),
                unlockedProtocolIds = NormalizeRewardIds(snapshot.unlockedProtocols).ToArray(),
                progress = BuildSnapshotSummary(snapshot, safeTotal)
            };
            return true;
        }

        public static bool TryImportPortableSave(
            string portableSave,
            int totalLevels,
            out TDCampaignPortableSavePreview preview,
            out string error)
        {
            if (!TryDecodePortableSave(portableSave, totalLevels, out var snapshot, out error))
            {
                preview = null;
                return false;
            }

            var json = JsonUtility.ToJson(snapshot);
            ImportSnapshot(json, totalLevels);
            return TryPreviewPortableSave(portableSave, totalLevels, out preview, out error);
        }

        public static void ImportSnapshot(string json, int totalLevels)
        {
            var snapshot = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<TDCampaignProgressSnapshot>(json);
            ClearStoredProgress(totalLevels);
            if (snapshot == null || !snapshot.initialized)
            {
                PlayerPrefs.Save();
                return;
            }

            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            PlayerPrefs.SetInt(VersionKey, SaveVersion);
            PlayerPrefs.SetInt(HighestUnlockedKey, Mathf.Clamp(snapshot.highestUnlockedLevel, 1, safeTotal));
            var claimedRewards = NormalizeRewardIds(snapshot.claimedChapterRewards);
            if (claimedRewards.Count > 0)
            {
                PlayerPrefs.SetString(ClaimedRewardsKey, string.Join(",", claimedRewards));
            }
            WriteNormalizedIds(ClaimedMetaRewardsKey, snapshot.claimedMetaRewards);
            WriteNormalizedIds(UnlockedProtocolsKey, snapshot.unlockedProtocols);
            WriteObservationRecords(EnemyObservationsKey, snapshot.enemyObservations);
            WriteObservationRecords(TowerObservationsKey, snapshot.towerObservations);
            WriteProtocolSelections(ProtocolSelectionsKey, snapshot.protocolSelections);
            var levels = snapshot.levels ?? Array.Empty<TDCampaignLevelProgress>();
            for (var i = 0; i < levels.Length && i < safeTotal; i++)
            {
                WriteLevelProgress(levels[i]);
            }

            TouchActiveSlot();
            PlayerPrefs.Save();
        }

        public static void ResetProgress(int totalLevels)
        {
            ClearStoredProgress(totalLevels);
            PlayerPrefs.SetInt(VersionKey, SaveVersion);
            PlayerPrefs.SetInt(HighestUnlockedKey, 1);
            TouchActiveSlot();
            PlayerPrefs.Save();
        }

        private static void WriteLevelProgress(TDCampaignLevelProgress progress)
        {
            if (progress == null || progress.levelIndex <= 0)
            {
                return;
            }

            var level = progress.levelIndex;
            PlayerPrefs.SetInt(GetLevelKey(level, "cleared"), progress.cleared ? 1 : 0);
            PlayerPrefs.SetInt(GetLevelKey(level, "attempts"), Mathf.Max(0, progress.attempts));
            PlayerPrefs.SetInt(GetLevelKey(level, "stars"), Mathf.Clamp(progress.bestStars, 0, 3));
            PlayerPrefs.SetInt(GetLevelKey(level, "score"), Mathf.Clamp(progress.bestTacticalScore, 0, 100));
            PlayerPrefs.SetInt(GetLevelKey(level, "integrity"), Mathf.Max(0, progress.bestIntegrity));
            PlayerPrefs.SetInt(GetLevelKey(level, "contract"), progress.contractCompleted ? 1 : 0);
            var normalizedLoadout = NormalizeTowerLoadout(
                string.IsNullOrWhiteSpace(progress.towerLoadout)
                    ? Array.Empty<string>()
                    : progress.towerLoadout.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (normalizedLoadout.Count > 0)
            {
                PlayerPrefs.SetString(GetLevelKey(level, "loadout"), string.Join(",", normalizedLoadout));
            }

            PlayerPrefs.SetInt(
                GetLevelKey(level, "doctrine"),
                Mathf.Clamp(
                    progress.resonanceDoctrine,
                    (int)TDResonanceDoctrine.Adaptive,
                    (int)TDResonanceDoctrine.FractureMark));
            PlayerPrefs.SetInt(
                GetLevelKey(level, "difficulty_preference"),
                Mathf.Clamp(
                    progress.difficultyPreference,
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial));
            PlayerPrefs.SetInt(
                GetLevelKey(level, "difficulty_cleared"),
                Mathf.Clamp(
                    progress.highestDifficultyCleared,
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial));
        }

        private static void ClearStoredProgress(int totalLevels)
        {
            PlayerPrefs.DeleteKey(VersionKey);
            PlayerPrefs.DeleteKey(HighestUnlockedKey);
            PlayerPrefs.DeleteKey(ClaimedRewardsKey);
            PlayerPrefs.DeleteKey(ClaimedMetaRewardsKey);
            PlayerPrefs.DeleteKey(UnlockedProtocolsKey);
            PlayerPrefs.DeleteKey(EnemyObservationsKey);
            PlayerPrefs.DeleteKey(TowerObservationsKey);
            PlayerPrefs.DeleteKey(ProtocolSelectionsKey);
            PlayerPrefs.DeleteKey(TotalLevelsKey);
            PlayerPrefs.DeleteKey(SnapshotChecksumKey);
            PlayerPrefs.DeleteKey(RecoveryCacheKey);
            var safeTotal = Mathf.Clamp(Mathf.Max(totalLevels, MaxSnapshotLevels), 1, MaxSnapshotLevels);
            for (var level = 1; level <= safeTotal; level++)
            {
                PlayerPrefs.DeleteKey(GetLevelKey(level, "cleared"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "attempts"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "stars"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "score"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "integrity"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "contract"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "loadout"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "doctrine"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "difficulty_preference"));
                PlayerPrefs.DeleteKey(GetLevelKey(level, "difficulty_cleared"));
            }

            DeleteRecoveryFiles(ActiveSaveSlot);
        }

        private static void EnsureSlotMigration(int totalLevels)
        {
            if (PlayerPrefs.GetInt(SlotsMigratedKey, 0) > 0)
            {
                return;
            }

            PlayerPrefs.SetInt(ActiveSlotKey, 1);
            var slotOneVersionKey = GetSlotKey(1, "version");
            var legacyVersionKey = LegacyPrefix + "_version";
            if (!PlayerPrefs.HasKey(slotOneVersionKey) && PlayerPrefs.HasKey(legacyVersionKey))
            {
                var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
                PlayerPrefs.SetInt(slotOneVersionKey, SaveVersion);
                PlayerPrefs.SetInt(
                    GetSlotKey(1, "highest_unlocked"),
                    Mathf.Clamp(PlayerPrefs.GetInt(LegacyPrefix + "_highest_unlocked", 1), 1, safeTotal));
                var rewards = PlayerPrefs.GetString(LegacyPrefix + "_claimed_chapter_rewards", string.Empty);
                if (!string.IsNullOrWhiteSpace(rewards))
                {
                    PlayerPrefs.SetString(GetSlotKey(1, "claimed_chapter_rewards"), rewards);
                }

                for (var level = 1; level <= safeTotal; level++)
                {
                    WriteLevelProgress(ReadLevelProgress(LegacyPrefix, level));
                }

                TouchActiveSlot();
            }

            PlayerPrefs.SetInt(SlotsMigratedKey, 1);
            GetOrCreateDeviceId();
            PlayerPrefs.Save();
        }

        private static TDCampaignProgressSnapshot BuildSnapshotForSlot(int slotId, int totalLevels)
        {
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            var prefix = GetSlotPrefix(slotId);
            var rewardRaw = PlayerPrefs.GetString(prefix + "_claimed_chapter_rewards", string.Empty);
            var snapshot = new TDCampaignProgressSnapshot
            {
                initialized = PlayerPrefs.HasKey(prefix + "_version"),
                saveVersion = SaveVersion,
                highestUnlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt(prefix + "_highest_unlocked", 1), 1, safeTotal),
                claimedChapterRewards = string.IsNullOrWhiteSpace(rewardRaw)
                    ? Array.Empty<string>()
                    : NormalizeRewardIds(rewardRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).ToArray(),
                claimedMetaRewards = ReadNormalizedIds(prefix + "_claimed_meta_rewards"),
                unlockedProtocols = ReadNormalizedIds(prefix + "_unlocked_protocols"),
                enemyObservations = ReadObservationRecords(prefix + "_enemy_observations"),
                towerObservations = ReadObservationRecords(prefix + "_tower_observations"),
                protocolSelections = ReadProtocolSelections(prefix + "_protocol_selections", safeTotal),
                levels = new TDCampaignLevelProgress[safeTotal]
            };
            for (var level = 1; level <= safeTotal; level++)
            {
                snapshot.levels[level - 1] = ReadLevelProgress(prefix, level);
            }

            return snapshot;
        }

        private static TDCampaignLevelProgress ReadLevelProgress(string prefix, int levelIndex)
        {
            var safeLevel = Mathf.Max(1, levelIndex);
            return new TDCampaignLevelProgress
            {
                levelIndex = safeLevel,
                cleared = PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "cleared"), 0) > 0,
                attempts = Mathf.Max(0, PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "attempts"), 0)),
                bestStars = Mathf.Clamp(PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "stars"), 0), 0, 3),
                bestTacticalScore = Mathf.Clamp(PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "score"), 0), 0, 100),
                bestIntegrity = Mathf.Max(0, PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "integrity"), 0)),
                contractCompleted = PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "contract"), 0) > 0,
                towerLoadout = PlayerPrefs.GetString(GetLevelKey(prefix, safeLevel, "loadout"), string.Empty),
                resonanceDoctrine = Mathf.Clamp(
                    PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "doctrine"), (int)TDResonanceDoctrine.Adaptive),
                    (int)TDResonanceDoctrine.Adaptive,
                    (int)TDResonanceDoctrine.FractureMark),
                difficultyPreference = Mathf.Clamp(
                    PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "difficulty_preference"), (int)TDCampaignDifficultyTier.Standard),
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial),
                highestDifficultyCleared = Mathf.Clamp(
                    PlayerPrefs.GetInt(GetLevelKey(prefix, safeLevel, "difficulty_cleared"), (int)TDCampaignDifficultyTier.Standard),
                    (int)TDCampaignDifficultyTier.Standard,
                    (int)TDCampaignDifficultyTier.EmberTrial)
            };
        }

        private static TDCampaignProgressSnapshot MergeSnapshots(
            TDCampaignProgressSnapshot local,
            TDCampaignProgressSnapshot cloud,
            bool cloudIsNewer,
            int totalLevels)
        {
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            var merged = new TDCampaignProgressSnapshot
            {
                initialized = true,
                saveVersion = SaveVersion,
                highestUnlockedLevel = Mathf.Max(local.highestUnlockedLevel, cloud.highestUnlockedLevel),
                claimedChapterRewards = NormalizeRewardIds(
                    (local.claimedChapterRewards ?? Array.Empty<string>())
                        .Concat(cloud.claimedChapterRewards ?? Array.Empty<string>())).ToArray(),
                claimedMetaRewards = NormalizeRewardIds(
                    (local.claimedMetaRewards ?? Array.Empty<string>())
                        .Concat(cloud.claimedMetaRewards ?? Array.Empty<string>())).ToArray(),
                unlockedProtocols = NormalizeRewardIds(
                    (local.unlockedProtocols ?? Array.Empty<string>())
                        .Concat(cloud.unlockedProtocols ?? Array.Empty<string>())).ToArray(),
                enemyObservations = MergeObservationRecords(local.enemyObservations, cloud.enemyObservations),
                towerObservations = MergeObservationRecords(local.towerObservations, cloud.towerObservations),
                protocolSelections = MergeProtocolSelections(
                    cloudIsNewer ? cloud.protocolSelections : local.protocolSelections,
                    cloudIsNewer ? local.protocolSelections : cloud.protocolSelections,
                    safeTotal),
                levels = new TDCampaignLevelProgress[safeTotal]
            };

            for (var i = 0; i < safeTotal; i++)
            {
                var localLevel = local.levels[i];
                var cloudLevel = cloud.levels[i];
                var newer = cloudIsNewer ? cloudLevel : localLevel;
                var older = cloudIsNewer ? localLevel : cloudLevel;
                var cleared = localLevel.cleared || cloudLevel.cleared;
                merged.levels[i] = new TDCampaignLevelProgress
                {
                    levelIndex = i + 1,
                    cleared = cleared,
                    attempts = Mathf.Max(localLevel.attempts, cloudLevel.attempts),
                    bestStars = cleared ? Mathf.Max(1, Mathf.Max(localLevel.bestStars, cloudLevel.bestStars)) : 0,
                    bestTacticalScore = Mathf.Max(localLevel.bestTacticalScore, cloudLevel.bestTacticalScore),
                    bestIntegrity = Mathf.Max(localLevel.bestIntegrity, cloudLevel.bestIntegrity),
                    contractCompleted = localLevel.contractCompleted || cloudLevel.contractCompleted,
                    towerLoadout = !string.IsNullOrWhiteSpace(newer.towerLoadout) ? newer.towerLoadout : older.towerLoadout,
                    resonanceDoctrine = newer.resonanceDoctrine,
                    difficultyPreference = newer.difficultyPreference,
                    highestDifficultyCleared = cleared
                        ? Mathf.Max(localLevel.highestDifficultyCleared, cloudLevel.highestDifficultyCleared)
                        : (int)TDCampaignDifficultyTier.Standard
                };
            }

            return merged;
        }

        private static List<string> NormalizeTowerLoadout(IEnumerable<string> towerIds)
        {
            var result = new List<string>(MaxFormationTowers);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (towerIds == null)
            {
                return result;
            }

            foreach (var rawTowerId in towerIds)
            {
                var towerId = string.IsNullOrWhiteSpace(rawTowerId)
                    ? string.Empty
                    : rawTowerId.Trim().ToLowerInvariant();
                if (towerId.Length == 0 || !TDTower.TryParseTowerId(towerId, out _) || !seen.Add(towerId))
                {
                    continue;
                }

                result.Add(towerId);
                if (result.Count >= MaxFormationTowers)
                {
                    break;
                }
            }

            return result;
        }

        private static List<string> NormalizeRewardIds(IEnumerable<string> rewardIds)
        {
            var result = new List<string>(MaxClaimedChapterRewards);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (rewardIds == null)
            {
                return result;
            }

            foreach (var rawRewardId in rewardIds)
            {
                var rewardId = NormalizeRewardId(rawRewardId);
                if (rewardId.Length == 0 || !seen.Add(rewardId))
                {
                    continue;
                }

                result.Add(rewardId);
                if (result.Count >= MaxClaimedChapterRewards)
                {
                    break;
                }
            }

            return result;
        }

        private static string[] ReadNormalizedIds(string key)
        {
            var raw = PlayerPrefs.GetString(key, string.Empty);
            return string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : NormalizeRewardIds(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }

        private static void WriteNormalizedIds(string key, IEnumerable<string> ids)
        {
            var normalized = NormalizeRewardIds(ids);
            if (normalized.Count == 0)
            {
                PlayerPrefs.DeleteKey(key);
                return;
            }

            normalized.Sort(StringComparer.OrdinalIgnoreCase);
            PlayerPrefs.SetString(key, string.Join(",", normalized));
        }

        private static int GetObservationFlags(IEnumerable<TDCampaignObservationRecord> records, string entryId)
        {
            var normalized = NormalizeRewardId(entryId);
            if (normalized.Length == 0 || records == null)
            {
                return 0;
            }

            var record = records.FirstOrDefault(item => item != null && string.Equals(item.entryId, normalized, StringComparison.OrdinalIgnoreCase));
            return record == null ? 0 : Mathf.Max(0, record.flags);
        }

        private static bool RecordObservation(string key, string entryId, int flags)
        {
            var normalized = NormalizeRewardId(entryId);
            var safeFlags = Mathf.Max(0, flags);
            if (normalized.Length == 0 || safeFlags == 0)
            {
                return false;
            }

            var records = ReadObservationRecords(key).ToList();
            var record = records.FirstOrDefault(item => string.Equals(item.entryId, normalized, StringComparison.OrdinalIgnoreCase));
            if (record == null)
            {
                if (records.Count >= MaxMetaRecords)
                {
                    return false;
                }

                record = new TDCampaignObservationRecord { entryId = normalized };
                records.Add(record);
            }

            var mergedFlags = record.flags | safeFlags;
            if (mergedFlags == record.flags)
            {
                return false;
            }

            record.flags = mergedFlags;
            WriteObservationRecords(key, records);
            TouchActiveSlot();
            PlayerPrefs.Save();
            return true;
        }

        private static TDCampaignObservationRecord[] ReadObservationRecords(string key)
        {
            var raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<TDCampaignObservationRecord>();
            }

            var result = new List<TDCampaignObservationRecord>();
            var tokens = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length && result.Count < MaxMetaRecords; i++)
            {
                var parts = tokens[i].Split('=');
                var id = parts.Length == 2 ? NormalizeRewardId(parts[0]) : string.Empty;
                if (id.Length == 0 || !int.TryParse(parts[1], out var flags) || flags <= 0 || result.Any(item => item.entryId == id))
                {
                    continue;
                }

                result.Add(new TDCampaignObservationRecord { entryId = id, flags = flags });
            }

            result.Sort((a, b) => string.Compare(a.entryId, b.entryId, StringComparison.OrdinalIgnoreCase));
            return result.ToArray();
        }

        private static void WriteObservationRecords(string key, IEnumerable<TDCampaignObservationRecord> records)
        {
            var normalized = MergeObservationRecords(records, Array.Empty<TDCampaignObservationRecord>());
            if (normalized.Length == 0)
            {
                PlayerPrefs.DeleteKey(key);
                return;
            }

            PlayerPrefs.SetString(key, string.Join(",", normalized.Select(item => $"{item.entryId}={item.flags}")));
        }

        private static TDCampaignObservationRecord[] MergeObservationRecords(
            IEnumerable<TDCampaignObservationRecord> first,
            IEnumerable<TDCampaignObservationRecord> second)
        {
            var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in (first ?? Array.Empty<TDCampaignObservationRecord>()).Concat(second ?? Array.Empty<TDCampaignObservationRecord>()))
            {
                var id = NormalizeRewardId(record?.entryId);
                if (id.Length == 0 || record.flags <= 0)
                {
                    continue;
                }

                merged[id] = merged.TryGetValue(id, out var existing) ? existing | record.flags : record.flags;
                if (merged.Count >= MaxMetaRecords)
                {
                    break;
                }
            }

            return merged.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new TDCampaignObservationRecord { entryId = pair.Key, flags = pair.Value })
                .ToArray();
        }

        private static TDCampaignProtocolSelectionRecord[] ReadProtocolSelections(string key, int totalLevels)
        {
            var raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<TDCampaignProtocolSelectionRecord>();
            }

            var records = new List<TDCampaignProtocolSelectionRecord>();
            foreach (var token in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[0], out var level))
                {
                    records.Add(new TDCampaignProtocolSelectionRecord { levelIndex = level, protocolId = parts[1] });
                }
            }

            return NormalizeProtocolSelections(records, totalLevels);
        }

        private static void WriteProtocolSelections(string key, IEnumerable<TDCampaignProtocolSelectionRecord> selections)
        {
            var normalized = NormalizeProtocolSelections(selections, MaxSnapshotLevels);
            if (normalized.Length == 0)
            {
                PlayerPrefs.DeleteKey(key);
                return;
            }

            PlayerPrefs.SetString(key, string.Join(",", normalized.Select(item => $"{item.levelIndex}={item.protocolId}")));
        }

        private static TDCampaignProtocolSelectionRecord[] NormalizeProtocolSelections(
            IEnumerable<TDCampaignProtocolSelectionRecord> selections,
            int totalLevels)
        {
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            var byLevel = new Dictionary<int, string>();
            foreach (var selection in selections ?? Array.Empty<TDCampaignProtocolSelectionRecord>())
            {
                var protocolId = NormalizeRewardId(selection?.protocolId);
                if (selection == null || selection.levelIndex < 1 || selection.levelIndex > safeTotal ||
                    protocolId.Length == 0 || string.Equals(protocolId, "baseline", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byLevel[selection.levelIndex] = protocolId;
            }

            return byLevel.OrderBy(pair => pair.Key)
                .Select(pair => new TDCampaignProtocolSelectionRecord { levelIndex = pair.Key, protocolId = pair.Value })
                .ToArray();
        }

        private static TDCampaignProtocolSelectionRecord[] MergeProtocolSelections(
            IEnumerable<TDCampaignProtocolSelectionRecord> newer,
            IEnumerable<TDCampaignProtocolSelectionRecord> older,
            int totalLevels)
        {
            var merged = NormalizeProtocolSelections(older, totalLevels).ToDictionary(item => item.levelIndex, item => item.protocolId);
            foreach (var selection in NormalizeProtocolSelections(newer, totalLevels))
            {
                merged[selection.levelIndex] = selection.protocolId;
            }

            return merged.OrderBy(pair => pair.Key)
                .Select(pair => new TDCampaignProtocolSelectionRecord { levelIndex = pair.Key, protocolId = pair.Value })
                .ToArray();
        }

        private static string NormalizeRewardId(string rewardId)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                return string.Empty;
            }

            var token = rewardId.Trim().ToLowerInvariant();
            for (var i = 0; i < token.Length; i++)
            {
                var c = token[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    return string.Empty;
                }
            }

            return token;
        }

        private static bool TryDecodePortableSave(
            string portableSave,
            int totalLevels,
            out TDCampaignProgressSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            var normalized = string.IsNullOrWhiteSpace(portableSave) ? string.Empty : portableSave.Trim();
            var prefix = normalized.StartsWith(PortableSavePrefix, StringComparison.Ordinal)
                ? PortableSavePrefix
                : normalized.StartsWith(LegacyPortableSavePrefix, StringComparison.Ordinal)
                    ? LegacyPortableSavePrefix
                    : string.Empty;
            if (prefix.Length == 0)
            {
                error = "Save code prefix is invalid.";
                return false;
            }

            var envelope = normalized.Substring(prefix.Length);
            var checksumSeparator = envelope.IndexOf(':');
            if (checksumSeparator != 8)
            {
                error = "Save code checksum header is invalid.";
                return false;
            }

            var expectedChecksum = envelope.Substring(0, checksumSeparator);
            var payload = envelope.Substring(checksumSeparator + 1);
            if (payload.Length == 0 || payload.Length > 262144)
            {
                error = "Save code payload size is invalid.";
                return false;
            }

            if (!string.Equals(
                    expectedChecksum,
                    BuildPortableSaveFingerprint(payload),
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "Save code checksum does not match the payload.";
                return false;
            }

            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                snapshot = JsonUtility.FromJson<TDCampaignProgressSnapshot>(json);
            }
            catch (Exception)
            {
                error = "Save code payload could not be decoded.";
                return false;
            }

            if (snapshot != null && snapshot.saveVersion == 1)
            {
                snapshot.saveVersion = SaveVersion;
            }

            return ValidateSnapshot(snapshot, totalLevels, out error);
        }

        private static bool TryDecodeCloudEnvelope(
            string cloudCode,
            int totalLevels,
            out TDCampaignCloudEnvelope envelope,
            out TDCampaignProgressSnapshot snapshot,
            out string error)
        {
            envelope = null;
            snapshot = null;
            error = string.Empty;
            var normalized = string.IsNullOrWhiteSpace(cloudCode) ? string.Empty : cloudCode.Trim();
            if (!normalized.StartsWith(CloudSavePrefix, StringComparison.Ordinal))
            {
                error = "Cloud code prefix is invalid.";
                return false;
            }

            var body = normalized.Substring(CloudSavePrefix.Length);
            var separator = body.IndexOf(':');
            if (separator != 8)
            {
                error = "Cloud code checksum header is invalid.";
                return false;
            }

            var expectedChecksum = body.Substring(0, separator);
            var payload = body.Substring(separator + 1);
            if (payload.Length == 0 || payload.Length > 524288 ||
                !string.Equals(expectedChecksum, BuildPortableSaveFingerprint(payload), StringComparison.OrdinalIgnoreCase))
            {
                error = "Cloud code checksum does not match the payload.";
                return false;
            }

            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                envelope = JsonUtility.FromJson<TDCampaignCloudEnvelope>(json);
            }
            catch (Exception)
            {
                error = "Cloud code payload could not be decoded.";
                return false;
            }

            if (envelope == null || envelope.schemaVersion != 1 || envelope.saveVersion < 1 ||
                envelope.saveVersion > SaveVersion || envelope.slotId < 1 || envelope.slotId > MaxSaveSlots ||
                envelope.revision < 0 || envelope.modifiedUtcTicks < 0 || string.IsNullOrWhiteSpace(envelope.deviceId))
            {
                error = "Cloud envelope metadata is invalid.";
                return false;
            }

            if (envelope.slotId != ActiveSaveSlot)
            {
                error = $"Cloud code targets slot {envelope.slotId}; active slot is {ActiveSaveSlot}.";
                return false;
            }

            return TryDecodePortableSave(envelope.portableSave, totalLevels, out snapshot, out error);
        }

        private static bool ValidateSnapshot(TDCampaignProgressSnapshot snapshot, int totalLevels, out string error)
        {
            error = string.Empty;
            var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
            if (snapshot == null || !snapshot.initialized)
            {
                error = "Save code does not contain an initialized campaign profile.";
                return false;
            }

            if (snapshot.saveVersion != SaveVersion)
            {
                error = $"Save version {snapshot.saveVersion} is not supported.";
                return false;
            }

            if (snapshot.highestUnlockedLevel < 1 || snapshot.highestUnlockedLevel > safeTotal)
            {
                error = "Save frontier is outside the campaign range.";
                return false;
            }

            var levels = snapshot.levels;
            if (levels == null || levels.Length != safeTotal)
            {
                error = $"Save code must contain exactly {safeTotal} mission records.";
                return false;
            }

            var seenLevels = new HashSet<int>();
            var clearedLevels = 0;
            var emberTrialRecords = 0;
            for (var i = 0; i < levels.Length; i++)
            {
                var progress = levels[i];
                if (progress == null || progress.levelIndex < 1 || progress.levelIndex > safeTotal ||
                    !seenLevels.Add(progress.levelIndex))
                {
                    error = "Save code contains a missing or duplicate mission record.";
                    return false;
                }

                if (progress.attempts < 0 || progress.bestStars < 0 || progress.bestStars > 3 ||
                    progress.bestTacticalScore < 0 || progress.bestTacticalScore > 100 ||
                    progress.bestIntegrity < 0 ||
                    progress.resonanceDoctrine < (int)TDResonanceDoctrine.Adaptive ||
                    progress.resonanceDoctrine > (int)TDResonanceDoctrine.FractureMark ||
                    progress.difficultyPreference < (int)TDCampaignDifficultyTier.Standard ||
                    progress.difficultyPreference > (int)TDCampaignDifficultyTier.EmberTrial ||
                    progress.highestDifficultyCleared < (int)TDCampaignDifficultyTier.Standard ||
                    progress.highestDifficultyCleared > (int)TDCampaignDifficultyTier.EmberTrial)
                {
                    error = $"Save code contains invalid values for mission {progress.levelIndex}.";
                    return false;
                }

                if (progress.cleared && progress.bestStars == 0)
                {
                    error = $"Cleared mission {progress.levelIndex} has no mastery star.";
                    return false;
                }

                if (!progress.cleared &&
                    progress.highestDifficultyCleared != (int)TDCampaignDifficultyTier.Standard)
                {
                    error = $"Uncleared mission {progress.levelIndex} contains a challenge clear record.";
                    return false;
                }

                if (progress.cleared)
                {
                    clearedLevels++;
                }

                if (progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.EmberTrial)
                {
                    emberTrialRecords++;
                }

                var loadoutTokens = string.IsNullOrWhiteSpace(progress.towerLoadout)
                    ? Array.Empty<string>()
                    : progress.towerLoadout.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (NormalizeTowerLoadout(loadoutTokens).Count != loadoutTokens.Length)
                {
                    error = $"Save code contains an invalid formation for mission {progress.levelIndex}.";
                    return false;
                }
            }

            if (emberTrialRecords > 0 && clearedLevels != safeTotal)
            {
                error = "Ember Trial records require a fully cleared campaign.";
                return false;
            }

            var rawRewards = snapshot.claimedChapterRewards ?? Array.Empty<string>();
            if (rawRewards.Length > MaxClaimedChapterRewards || NormalizeRewardIds(rawRewards).Count != rawRewards.Length)
            {
                error = "Save code contains invalid chapter reward records.";
                return false;
            }

            var rawMetaRewards = snapshot.claimedMetaRewards ?? Array.Empty<string>();
            var rawProtocols = snapshot.unlockedProtocols ?? Array.Empty<string>();
            if (rawMetaRewards.Length > MaxMetaRecords || NormalizeRewardIds(rawMetaRewards).Count != rawMetaRewards.Length ||
                rawProtocols.Length > MaxMetaRecords || NormalizeRewardIds(rawProtocols).Count != rawProtocols.Length)
            {
                error = "Save code contains invalid meta reward or protocol records.";
                return false;
            }

            if (MergeObservationRecords(snapshot.enemyObservations, Array.Empty<TDCampaignObservationRecord>()).Length !=
                    (snapshot.enemyObservations ?? Array.Empty<TDCampaignObservationRecord>()).Length ||
                MergeObservationRecords(snapshot.towerObservations, Array.Empty<TDCampaignObservationRecord>()).Length !=
                    (snapshot.towerObservations ?? Array.Empty<TDCampaignObservationRecord>()).Length)
            {
                error = "Save code contains invalid codex observation records.";
                return false;
            }

            if (NormalizeProtocolSelections(snapshot.protocolSelections, safeTotal).Length !=
                (snapshot.protocolSelections ?? Array.Empty<TDCampaignProtocolSelectionRecord>()).Length)
            {
                error = "Save code contains invalid tactical protocol selections.";
                return false;
            }

            return true;
        }

        private static TDCampaignProgressSummary BuildSnapshotSummary(TDCampaignProgressSnapshot snapshot, int totalLevels)
        {
            var summary = new TDCampaignProgressSummary
            {
                totalLevels = totalLevels,
                availableStars = totalLevels * 3,
                availableContracts = totalLevels,
                highestUnlockedLevel = Mathf.Clamp(snapshot.highestUnlockedLevel, 1, totalLevels)
            };
            var levels = snapshot.levels ?? Array.Empty<TDCampaignLevelProgress>();
            for (var i = 0; i < levels.Length; i++)
            {
                var progress = levels[i];
                if (progress == null || progress.levelIndex < 1 || progress.levelIndex > totalLevels)
                {
                    continue;
                }

                if (progress.cleared)
                {
                    summary.clearedLevels++;
                }

                summary.earnedStars += Mathf.Clamp(progress.bestStars, 0, 3);
                if (progress.contractCompleted)
                {
                    summary.completedContracts++;
                }

                if (progress.cleared && progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.Veteran)
                {
                    summary.veteranClears++;
                }

                if (progress.cleared && progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.EmberTrial)
                {
                    summary.emberTrialClears++;
                }
            }

            return summary;
        }

        private static string BuildPortableSaveFingerprint(string portableSave)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (var i = 0; i < portableSave.Length; i++)
                {
                    hash ^= portableSave[i];
                    hash *= 16777619;
                }

                return hash.ToString("X8");
            }
        }

        private static string ReadPortableSaveFingerprint(string portableSave)
        {
            if (string.IsNullOrWhiteSpace(portableSave))
            {
                return string.Empty;
            }

            var prefix = portableSave.StartsWith(PortableSavePrefix, StringComparison.Ordinal)
                ? PortableSavePrefix
                : portableSave.StartsWith(LegacyPortableSavePrefix, StringComparison.Ordinal)
                    ? LegacyPortableSavePrefix
                    : string.Empty;
            return prefix.Length == 0 ? string.Empty : ReadEnvelopeFingerprint(portableSave, prefix);
        }

        private static string ReadEnvelopeFingerprint(string value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(prefix) ||
                !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var envelope = value.Substring(prefix.Length);
            var separator = envelope.IndexOf(':');
            return separator == 8 ? envelope.Substring(0, separator).ToUpperInvariant() : string.Empty;
        }

        private static void TouchActiveSlot()
        {
            PlayerPrefs.SetString(RevisionKey, (ReadLong(RevisionKey) + 1L).ToString());
            PlayerPrefs.SetString(ModifiedUtcKey, DateTime.UtcNow.Ticks.ToString());
            var totalLevels = Mathf.Clamp(
                PlayerPrefs.GetInt(TotalLevelsKey, 20),
                1,
                MaxSnapshotLevels);
            PlayerPrefs.SetInt(TotalLevelsKey, totalLevels);
            PersistRecoverySnapshot(totalLevels);
        }

        private static bool PersistRecoverySnapshot(int totalLevels)
        {
            if (_recoveryWriteInProgress || !PlayerPrefs.HasKey(VersionKey))
            {
                return false;
            }

            _recoveryWriteInProgress = true;
            try
            {
                var safeTotal = Mathf.Clamp(totalLevels, 1, MaxSnapshotLevels);
                var snapshot = BuildSnapshotForSlot(ActiveSaveSlot, safeTotal);
                if (!ValidateSnapshot(snapshot, safeTotal, out _))
                {
                    return false;
                }

                var json = JsonUtility.ToJson(snapshot);
                var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                var portableSave = PortableSavePrefix + BuildPortableSaveFingerprint(payload) + ":" + payload;
                PlayerPrefs.SetString(SnapshotChecksumKey, BuildPortableSaveFingerprint(json));
                PlayerPrefs.SetString(RecoveryCacheKey, portableSave);
                return WriteRecoveryFileAtomic(GetRecoveryPath(ActiveSaveSlot), portableSave);
            }
            finally
            {
                _recoveryWriteInProgress = false;
            }
        }

        private static bool WriteRecoveryFileAtomic(string path, string contents)
        {
            var tempPath = path + ".tmp";
            var previousPath = Path.ChangeExtension(path, ".previous.save");
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(tempPath, contents);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tempPath, path, previousPath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(path, previousPath, true);
                        File.Copy(tempPath, path, true);
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                        File.Copy(path, previousPath, true);
                        File.Copy(tempPath, path, true);
                        File.Delete(tempPath);
                    }
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TD][P12.5.3] Recovery snapshot write failed: {exception.Message}");
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception)
                {
                    // Best effort cleanup only.
                }

                return false;
            }
        }

        private static string ReadRecoveryFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TD][P12.5.3] Recovery snapshot read failed: {exception.Message}");
                return string.Empty;
            }
        }

        private static string GetRecoveryPath(int slotId)
        {
            return Path.Combine(
                Application.persistentDataPath,
                "CampaignRecovery",
                $"campaign-slot-{Mathf.Clamp(slotId, 1, MaxSaveSlots)}.save");
        }

        private static string GetRecoveryPreviousPath(int slotId)
        {
            return Path.ChangeExtension(GetRecoveryPath(slotId), ".previous.save");
        }

        private static void DeleteRecoveryFiles(int slotId)
        {
            var paths = new[]
            {
                GetRecoveryPath(slotId),
                GetRecoveryPreviousPath(slotId),
                GetRecoveryPath(slotId) + ".tmp"
            };
            for (var i = 0; i < paths.Length; i++)
            {
                try
                {
                    if (File.Exists(paths[i]))
                    {
                        File.Delete(paths[i]);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[TD][P12.5.3] Recovery snapshot cleanup failed: {exception.Message}");
                }
            }
        }

        private static long ReadLong(string key)
        {
            return long.TryParse(PlayerPrefs.GetString(key, "0"), out var value) ? Math.Max(0L, value) : 0L;
        }

        private static string GetOrCreateDeviceId()
        {
            var deviceId = PlayerPrefs.GetString(DeviceIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId;
            }

            deviceId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceIdKey, deviceId);
            return deviceId;
        }

        private static string GetSlotPrefix(int slotId)
        {
            return $"{SlotPrefix}_{Mathf.Clamp(slotId, 1, MaxSaveSlots)}";
        }

        private static string GetSlotKey(string field)
        {
            return GetSlotKey(ActiveSaveSlot, field);
        }

        private static string GetSlotKey(int slotId, string field)
        {
            return $"{GetSlotPrefix(slotId)}_{field}";
        }

        private static string GetLevelKey(int levelIndex, string field)
        {
            return GetLevelKey(GetSlotPrefix(ActiveSaveSlot), levelIndex, field);
        }

        private static string GetLevelKey(string prefix, int levelIndex, string field)
        {
            return $"{prefix}_level_{Mathf.Max(1, levelIndex):00}_{field}";
        }
    }
}
