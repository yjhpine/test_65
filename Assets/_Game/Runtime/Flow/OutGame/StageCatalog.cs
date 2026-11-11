using System;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    // The ordered list of playable stages and the title scene. Stage identity in saves is the id, never the scene index.
    [CreateAssetMenu(menuName = "Action Platformer/Flow/Stage Catalog", fileName = "StageCatalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private string scene;
            public Entry(string id, string displayName, string scene) { this.id = id; this.displayName = displayName; this.scene = scene; }
            public string Id => id;
            public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
            public string Scene => scene;
        }

        [SerializeField] private string titleScene = "Title";
        [SerializeField] private Entry[] stages = Array.Empty<Entry>();

        public string TitleScene => titleScene;
        public int Count => stages?.Length ?? 0;
        public Entry this[int index] => stages[index];
        // A single stage hides the stage list (planning doc 02: a one-stage build may skip it).
        public bool HasStageSelect => Count >= 2;

        public static StageCatalog Create(string titleScene, params Entry[] stages)
        {
            var catalog = CreateInstance<StageCatalog>();
            catalog.titleScene = titleScene;
            catalog.stages = stages ?? Array.Empty<Entry>();
            return catalog;
        }

        public int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Count; i++) if (stages[i] != null && stages[i].Id == id) return i;
            return -1;
        }

        public int IndexOfScene(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return -1;
            for (int i = 0; i < Count; i++) if (stages[i] != null && stages[i].Scene == scene) return i;
            return -1;
        }

        public Entry Find(string id) { int i = IndexOfId(id); return i < 0 ? null : stages[i]; }
        public bool IsLast(string id) { int i = IndexOfId(id); return i >= 0 && i == Count - 1; }
        public Entry Next(string id) { int i = IndexOfId(id); return i >= 0 && i + 1 < Count ? stages[i + 1] : null; }

        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(titleScene)) error = "Stage catalog needs a title scene.";
            else if (Count == 0) error = "Stage catalog needs at least one stage.";
            else
                for (int i = 0; i < Count && error == null; i++)
                {
                    var entry = stages[i];
                    if (entry == null || string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.Scene))
                        error = $"Stage catalog entry {i} needs an id and a scene.";
                    else if (IndexOfId(entry.Id) != i) error = $"Stage id '{entry.Id}' is used twice.";
                }
            return error == null;
        }
    }
}
