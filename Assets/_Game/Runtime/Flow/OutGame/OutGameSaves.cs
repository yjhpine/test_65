using UnityEngine;

namespace ActionPlatformer.Flow
{
    // Loads and saves the settings and progress records and keeps the in-run retry checkpoint.
    // A failed read never deletes or overwrites a record; a failed write keeps the run marked unsaved.
    public sealed class OutGameSaves
    {
        public const string SettingsName = "settings";
        public const string ProgressName = "progress";

        private readonly StageCatalog catalog;
        private ISaveStorage storage;

        public OutGameSaves(StageCatalog catalog, ISaveStorage storage)
        {
            this.catalog = catalog;
            this.storage = storage;
        }

        public StageCatalog Catalog => catalog;
        public ISaveStorage Storage => storage;
        public SettingsData Settings { get; private set; } = new SettingsData();
        // Fields replaced by defaults on the last load; -1 means the whole record was unreadable.
        public int SettingsFixedFields { get; private set; }
        public ProgressData Progress { get; private set; }
        public SaveReadResult ProgressLoad { get; private set; } = SaveReadResult.Missing;
        // Progress the last write could not store. It stays in memory and is reported until a later save works.
        public bool ProgressUnsaved { get; private set; }
        public bool SettingsUnsaved { get; private set; }
        public string LastError { get; private set; }

        // The rest area the current run retries from. It is never written to disk by itself.
        public string RetryStage { get; private set; }
        public int RetryCheckpoint { get; private set; } = ProgressData.NoCheckpoint;

        public bool HasProgressRecord => Progress != null && ProgressLoad != SaveReadResult.Failed;

        public void UseStorage(ISaveStorage value) => storage = value;

        public void Load()
        {
            LoadSettings();
            LoadProgress();
            ClearRetry();
            ProgressUnsaved = SettingsUnsaved = false;
        }

        public void LoadSettings()
        {
            var result = storage.Read(SettingsName, out var text, out var error);
            if (result == SaveReadResult.Loaded)
            {
                Settings = SettingsData.FromJson(text, out int fixedFields);
                SettingsFixedFields = fixedFields;
                if (fixedFields != 0) Debug.LogWarning($"Settings record had {(fixedFields < 0 ? "no readable" : fixedFields.ToString())} invalid field(s); defaults are used for them.");
            }
            else
            {
                Settings = new SettingsData();
                SettingsFixedFields = 0;
                if (result == SaveReadResult.Failed) Debug.LogWarning("Settings could not be read; defaults are used: " + error);
            }
        }

        public void LoadProgress()
        {
            var result = storage.Read(ProgressName, out var text, out var error);
            Progress = null;
            ProgressLoad = result;
            if (result == SaveReadResult.Loaded)
            {
                Progress = ProgressData.FromJson(text, catalog);
                if (Progress == null) { ProgressLoad = SaveReadResult.Failed; LastError = "progress record is not readable"; }
            }
            else if (result == SaveReadResult.Failed) LastError = error;
        }

        public bool SaveSettings()
        {
            bool ok = storage.Write(SettingsName, Settings.ToJson(), out var error);
            SettingsUnsaved = !ok;
            if (!ok) LastError = error;
            return ok;
        }

        public bool SaveProgress()
        {
            if (Progress == null) return true;
            bool ok = storage.Write(ProgressName, Progress.ToJson(), out var error);
            ProgressUnsaved = !ok;
            if (ok) ProgressLoad = SaveReadResult.Loaded;
            else LastError = error;
            return ok;
        }

        // The new record replaces the old one only after the first stage was entered (planning doc 4.2).
        public void StartNewProgress()
        {
            Progress = ProgressRules.NewGame(catalog);
            ProgressLoad = SaveReadResult.Loaded;
        }

        public bool DeleteProgress()
        {
            if (!storage.Delete(ProgressName, out var error)) { LastError = error; return false; }
            Progress = null;
            ProgressLoad = SaveReadResult.Missing;
            ProgressUnsaved = false;
            ClearRetry();
            return true;
        }

        public void SetRetry(string stage, int checkpoint) { RetryStage = stage; RetryCheckpoint = checkpoint; }
        public void ClearRetry() { RetryStage = null; RetryCheckpoint = ProgressData.NoCheckpoint; }
        public int RetryCheckpointFor(string stage) => stage != null && stage == RetryStage ? RetryCheckpoint : ProgressData.NoCheckpoint;

        public bool CanContinue(out string reason) => ProgressRules.CanContinue(Progress, ProgressLoad, out reason);
        // Any existing record, readable or not, is "existing progress" for the new-game overwrite question.
        public bool HasAnyProgress => Progress != null || ProgressLoad == SaveReadResult.Failed;
    }
}
