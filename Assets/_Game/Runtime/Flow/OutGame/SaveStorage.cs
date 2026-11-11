using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    public enum SaveReadResult { Missing, Loaded, Failed }

    // Where the out-game records live. Settings and progress are separate entries so either can be reset alone.
    public interface ISaveStorage
    {
        SaveReadResult Read(string name, out string text, out string error);
        bool Write(string name, string text, out string error);
        bool Delete(string name, out string error);
    }

    // Local files under persistentDataPath. A write goes to a temporary file first and then replaces the record,
    // so a crash mid-write leaves the last good record readable.
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string directory;
        public FileSaveStorage(string directory) { this.directory = directory; }
        public string Directory => directory;
        private string PathOf(string name) => Path.Combine(directory, name + ".json");

        public SaveReadResult Read(string name, out string text, out string error)
        {
            text = null; error = null;
            string path = PathOf(name);
            try
            {
                if (!File.Exists(path)) return SaveReadResult.Missing;
                text = File.ReadAllText(path);
                return SaveReadResult.Loaded;
            }
            catch (Exception e) { error = e.Message; return SaveReadResult.Failed; }
        }

        public bool Write(string name, string text, out string error)
        {
            error = null;
            string path = PathOf(name), temporary = path + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                File.WriteAllText(temporary, text);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception) { }
                return false;
            }
        }

        public bool Delete(string name, out string error)
        {
            error = null;
            try { string path = PathOf(name); if (File.Exists(path)) File.Delete(path); return true; }
            catch (Exception e) { error = e.Message; return false; }
        }
    }

    // In-memory records for tests; failures can be switched on to exercise the error paths.
    public sealed class MemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>();
        public bool FailReads { get; set; }
        public bool FailWrites { get; set; }
        public int Writes { get; private set; }
        public IReadOnlyDictionary<string, string> Entries => entries;
        public void Set(string name, string text) => entries[name] = text;

        public SaveReadResult Read(string name, out string text, out string error)
        {
            error = FailReads ? "Simulated read failure." : null;
            text = null;
            if (FailReads) return SaveReadResult.Failed;
            return entries.TryGetValue(name, out text) ? SaveReadResult.Loaded : SaveReadResult.Missing;
        }

        public bool Write(string name, string text, out string error)
        {
            if (FailWrites) { error = "Simulated write failure."; return false; }
            error = null; entries[name] = text; Writes++;
            return true;
        }

        public bool Delete(string name, out string error)
        {
            if (FailWrites) { error = "Simulated delete failure."; return false; }
            error = null; entries.Remove(name);
            return true;
        }
    }

    // The storage the GameManager uses. Tests swap in memory storage before they start a run.
    public static class SaveStorage
    {
        private static ISaveStorage current;
        public static ISaveStorage Current => current ??= new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Save"));
        public static void Use(ISaveStorage storage) => current = storage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;
    }
}
