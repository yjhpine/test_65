using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ActionPlatformer.Presentation;

namespace ActionPlatformer.Editor
{
    // Explicit authoring commands only. Never change combat tuning or scene instances.
    public static class GlitchSlashImporter
    {
        private const string ProfilePath = "Assets/_Game/Data/Presentation/PlayerVfx.asset";

        [Serializable] private sealed class Manifest { public float[] unityPivot; }

        [MenuItem("Game/Effects/Import Generated Glitch Combo")]
        public static void Import()
        {
            const string root = "Assets/_Game/Art/Effects/PixelFxGlitchCombo";
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>(ProfilePath));
            for (int strike = 1; strike <= 3; strike++)
                Assign(data.FindProperty($"comboSlash{strike}Frames"), ImportFrames($"custom/glitch-slash-{strike}",
                    $"{root}/Strike{strike}", 96, new Vector2(.4f, .5f), "Generate glitch-slash-1/2/3 with Tools/PixelFx first."));
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Glitch combo VFX imported: three separate strike frame arrays; combat settings unchanged.");
        }

        // Authored on the player art's 18 px grid; the manifest pivot is the Visual center.
        [MenuItem("Game/Effects/Import Generated Finisher Aura")]
        public static void ImportFinisherAura()
        {
            const string id = "custom/finisher-aura";
            string manifest = $"Library/PixelFxPipeline/output/{id}/manifest.json";
            if (!File.Exists(manifest)) throw new InvalidOperationException("Generate finisher-aura with Tools/PixelFx first.");
            var pivot = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifest)).unityPivot;
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>(ProfilePath));
            Assign(data.FindProperty("finisherAuraFrames"), ImportFrames(id, "Assets/_Game/Art/Effects/PixelFxFinisherAura",
                18, new Vector2(pivot[0], pivot[1]), "Generate finisher-aura with Tools/PixelFx first."));
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Finisher aura VFX imported into PlayerVfx; combat settings unchanged.");
        }

        // Ready and blocked variants of the aiming marker, on the player's 18 px grid, centered on the destination.
        [MenuItem("Game/Effects/Import Generated Glitch Marker")]
        public static void ImportGlitchMarker()
        {
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>(ProfilePath));
            foreach (var (variant, field) in new[] { ("ready", "glitchMarkerReadyFrames"), ("blocked", "glitchMarkerBlockedFrames") })
            {
                string id = "custom/glitch-marker-" + variant;
                string manifest = $"Library/PixelFxPipeline/output/{id}/manifest.json";
                if (!File.Exists(manifest)) throw new InvalidOperationException("Generate glitch-marker-ready/blocked with Tools/PixelFx first.");
                var pivot = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifest)).unityPivot;
                Assign(data.FindProperty(field), ImportFrames(id, "Assets/_Game/Art/Effects/PixelFxGlitchMarker/" +
                    char.ToUpperInvariant(variant[0]) + variant.Substring(1), 18, new Vector2(pivot[0], pivot[1]),
                    "Generate glitch-marker-ready/blocked with Tools/PixelFx first."));
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Glitch marker VFX imported into PlayerVfx; glitch rules unchanged.");
        }

        private static Sprite[] ImportFrames(string id, string destination, float pixelsPerUnit, Vector2 pivot, string missing)
        {
            string source = $"Library/PixelFxPipeline/output/{id}";
            if (!Directory.Exists(source + "/frames")) throw new InvalidOperationException(missing);
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source + "/frames", "*.png"))
                File.Copy(file, destination + "/" + Path.GetFileName(file), true);
            foreach (string file in new[] { "manifest.json", "LICENSE", "NOTICE.md" })
                File.Copy(source + "/" + file, destination + "/" + file, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var files = Directory.GetFiles(destination, "*.png").OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var sprites = new Sprite[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sprites;
        }

        private static void Assign(SerializedProperty array, Sprite[] sprites)
        {
            array.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }
}
