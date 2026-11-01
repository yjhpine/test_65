using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ActionPlatformer.Presentation;

namespace ActionPlatformer.Editor
{
    // Explicit authoring command only. Never changes combat tuning or scene instances.
    public static class GlitchSlashImporter
    {
        [MenuItem("Game/Effects/Import Generated Glitch Combo")]
        public static void Import()
        {
            const string root = "Assets/_Game/Art/Effects/PixelFxGlitchCombo";
            Directory.CreateDirectory(root);
            for (int strike = 1; strike <= 3; strike++)
            {
                string source = $"Library/PixelFxPipeline/output/custom/glitch-slash-{strike}";
                string destination = $"{root}/Strike{strike}";
                if (!Directory.Exists(source + "/frames")) throw new InvalidOperationException("Generate glitch-slash-1/2/3 with Tools/PixelFx first.");
                Directory.CreateDirectory(destination);
                foreach (string file in Directory.GetFiles(source + "/frames", "*.png"))
                    File.Copy(file, destination + "/" + Path.GetFileName(file), true);
                foreach (string file in new[] { "manifest.json", "LICENSE", "NOTICE.md" })
                    File.Copy(source + "/" + file, destination + "/" + file, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var profile = AssetDatabase.LoadAssetAtPath<PlayerVfxProfile>("Assets/_Game/Data/Presentation/PlayerVfx.asset");
            var data = new SerializedObject(profile);
            for (int strike = 1; strike <= 3; strike++)
            {
                var files = Directory.GetFiles($"{root}/Strike{strike}", "*.png").OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var array = data.FindProperty($"comboSlash{strike}Frames");
                array.arraySize = files.Length;
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i].Replace('\\', '/');
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 96;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(.4f, .5f);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                    array.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Glitch combo VFX imported: three separate strike frame arrays; combat settings unchanged.");
        }
    }
}
