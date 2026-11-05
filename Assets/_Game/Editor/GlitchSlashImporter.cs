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

        // Ready and blocked variants of the aiming marker, on the player's 18 px grid, centered on the destination.
        [MenuItem("Game/Effects/Import Generated Glitch Marker")]
        public static void ImportGlitchMarker()
        {
            ImportOnPlayerGrid(("glitch-marker-ready", "Assets/_Game/Art/Effects/PixelFxGlitchMarker/Ready", "glitchMarkerReadyFrames"),
                ("glitch-marker-blocked", "Assets/_Game/Art/Effects/PixelFxGlitchMarker/Blocked", "glitchMarkerBlockedFrames"));
            Debug.Log("Glitch marker VFX imported into PlayerVfx; glitch rules unchanged.");
        }

        // The straight glitch streak between departure and arrival: left-edge pivot, tiled along the path at its own size.
        [MenuItem("Game/Effects/Import Generated Glitch Streak")]
        public static void ImportGlitchStreak()
        {
            const string destination = "Assets/_Game/Art/Effects/PixelFxGlitchStreak";
            // A shorter render must not leave stale frames behind (they would sort into the array).
            if (Directory.Exists(destination))
                foreach (string old in Directory.GetFiles(destination, "*.png")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            ImportOnPlayerGrid(("glitch-streak", destination, "glitchStreakFrames"));
            Debug.Log("Glitch streak imported into PlayerVfx; glitch rules unchanged.");
        }

        // Hit debris (pivot on the contact point) and the kill burst (pivot on the body center), on the player's grid.
        [MenuItem("Game/Effects/Import Generated Contact Bursts")]
        public static void ImportContactBursts()
        {
            ImportOnPlayerGrid(("glitch-hit-debris", "Assets/_Game/Art/Effects/PixelFxContactBursts/Debris", "hitDebrisFrames"),
                ("glitch-shatter", "Assets/_Game/Art/Effects/PixelFxContactBursts/Shatter", "killShatterFrames"));
            Debug.Log("Contact burst VFX imported into PlayerVfx; combat settings unchanged.");
        }

        [Serializable] private sealed class LoopManifest { public float fps; }

        // The shared enemy danger zone: a looping red glitch tile on the player's 18 px grid, put into Data/Presentation/AttackZone.
        // Only the frames and their fps are replaced; the opacity ramp and sorting already in the profile are kept.
        [MenuItem("Game/Effects/Import Generated Attack Zone")]
        public static void ImportAttackZone()
        {
            const string id = "custom/attack-zone", destination = "Assets/_Game/Art/Effects/PixelFxAttackZone";
            const string profilePath = "Assets/_Game/Data/Presentation/AttackZone.asset", missing = "Generate attack-zone with Tools/PixelFx first.";
            string manifest = $"Library/PixelFxPipeline/output/{id}/manifest.json";
            if (!File.Exists(manifest)) throw new InvalidOperationException(missing);
            string text = File.ReadAllText(manifest);
            var pivot = JsonUtility.FromJson<Manifest>(text).unityPivot;
            float fps = JsonUtility.FromJson<LoopManifest>(text).fps;
            // A shorter loop must not leave stale frames behind (they would sort into the array).
            if (Directory.Exists(destination))
                foreach (string old in Directory.GetFiles(destination, "*.png")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            var frames = ImportFrames(id, destination, 18, new Vector2(pivot[0], pivot[1]), missing);
            // A 1 px border, drawn Tiled: the rim stays one art pixel wide and the glitch noise keeps its pixel size.
            for (int i = 0; i < frames.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(frames[i]);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.spriteBorder = new Vector4(1, 1, 1, 1);
                importer.SaveAndReimport();
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var profile = AssetDatabase.LoadAssetAtPath<AttackZoneProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<AttackZoneProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            var data = new SerializedObject(profile);
            Assign(data.FindProperty("frames"), frames);
            if (fps > 0f) data.FindProperty("fps").floatValue = fps;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"Attack zone imported into Data/Presentation/AttackZone ({frames.Length} frames, {fps} fps); enemy attacks unchanged.");
        }

        private static void ImportOnPlayerGrid(params (string name, string destination, string field)[] effects)
        {
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>(ProfilePath));
            foreach (var (name, destination, field) in effects)
            {
                string id = "custom/" + name, missing = $"Generate {name} with Tools/PixelFx first.";
                string manifest = $"Library/PixelFxPipeline/output/{id}/manifest.json";
                if (!File.Exists(manifest)) throw new InvalidOperationException(missing);
                var pivot = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifest)).unityPivot;
                Assign(data.FindProperty(field), ImportFrames(id, destination, 18, new Vector2(pivot[0], pivot[1]), missing));
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
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
