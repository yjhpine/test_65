using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    // Saved story progress (planning doc 7.1): the main stage, its last rest-area checkpoint, unlocks and clears.
    // Combat state (health, enemies, shots, positions) is never saved; a resume starts at the rest area's arrival point.
    [Serializable]
    public sealed class ProgressData
    {
        public const int CurrentVersion = 1;
        public const int NoCheckpoint = -1;

        public int version = CurrentVersion;
        public string mainStage = "";
        // Index of the cleared encounter whose following rest area is the checkpoint; NoCheckpoint is the stage start.
        public int mainCheckpoint = NoCheckpoint;
        public List<string> unlocked = new List<string>();
        public List<string> cleared = new List<string>();
        public bool allCleared;

        public bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && unlocked.Contains(id);
        public bool IsCleared(string id) => !string.IsNullOrEmpty(id) && cleared.Contains(id);

        public ProgressData Clone()
        {
            var copy = (ProgressData)MemberwiseClone();
            copy.unlocked = new List<string>(unlocked);
            copy.cleared = new List<string>(cleared);
            return copy;
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        // Null when the text is unreadable or does not describe this catalog; such a record is reported, never deleted.
        public static ProgressData FromJson(string json, StageCatalog catalog)
        {
            ProgressData data;
            try { data = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<ProgressData>(json); }
            catch (ArgumentException) { return null; }
            if (data == null || data.unlocked == null || data.cleared == null || catalog == null) return null;
            if (catalog.IndexOfId(data.mainStage) < 0) return null;
            data.unlocked.RemoveAll(id => catalog.IndexOfId(id) < 0);
            data.cleared.RemoveAll(id => catalog.IndexOfId(id) < 0);
            if (!data.IsUnlocked(data.mainStage)) return null;
            if (data.mainCheckpoint < NoCheckpoint) data.mainCheckpoint = NoCheckpoint;
            data.version = CurrentVersion;
            return data;
        }
    }

    // When each save point changes the record (planning doc 7.2). Every method returns true when the record changed.
    public static class ProgressRules
    {
        public static ProgressData NewGame(StageCatalog catalog)
        {
            var first = catalog[0].Id;
            var data = new ProgressData { mainStage = first };
            data.unlocked.Add(first);
            return data;
        }

        // Entering an unlocked, uncleared stage makes it the main stage at the entry point.
        // Replaying a cleared stage changes nothing, so the continue point never moves back.
        public static bool OnStageEntered(ProgressData data, StageCatalog catalog, string id, int checkpoint)
        {
            if (data == null || catalog.IndexOfId(id) < 0 || data.IsCleared(id) || !data.IsUnlocked(id)) return false;
            if (data.mainStage == id && data.mainCheckpoint == checkpoint) return false;
            data.mainStage = id;
            data.mainCheckpoint = checkpoint;
            return true;
        }

        public static bool OnRestReached(ProgressData data, string id, int encounterIndex)
        {
            if (data == null || data.mainStage != id || data.IsCleared(id) || data.mainCheckpoint == encounterIndex) return false;
            data.mainCheckpoint = encounterIndex;
            return true;
        }

        // Clearing records completion and the next unlock in one change; the last stage marks the whole game cleared.
        public static bool OnStageCleared(ProgressData data, StageCatalog catalog, string id)
        {
            int index = catalog.IndexOfId(id);
            if (data == null || index < 0 || data.IsCleared(id)) return false;
            data.cleared.Add(id);
            if (!data.IsUnlocked(id)) data.unlocked.Add(id);
            data.mainCheckpoint = ProgressData.NoCheckpoint;
            var next = catalog.Next(id);
            if (next == null)
            {
                data.allCleared = true;
                data.mainStage = id;
                return true;
            }
            if (!data.IsUnlocked(next.Id)) data.unlocked.Add(next.Id);
            // The main point moves on only when this stage was the main one; an earlier replay never pulls it back.
            if (data.mainStage == id || data.IsCleared(data.mainStage)) data.mainStage = next.Id;
            return true;
        }

        public const string NoRecordReason = "이어할 진행 기록이 없습니다";
        public const string AllClearedReason = "모든 스테이지를 클리어했습니다";
        public const string UnreadableReason = "진행 기록을 읽을 수 없습니다";

        public static bool CanContinue(ProgressData data, SaveReadResult loaded, out string reason)
        {
            reason = null;
            if (loaded == SaveReadResult.Failed) reason = UnreadableReason;
            else if (data == null) reason = NoRecordReason;
            else if (data.allCleared) reason = AllClearedReason;
            else if (!data.IsUnlocked(data.mainStage) || data.IsCleared(data.mainStage)) reason = NoRecordReason;
            return reason == null;
        }
    }
}
