using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ActionPlatformer.Tests
{
    // Since 2026-11-12 every game text uses the Galmuri11 pixel font (OFL), with Noto Sans KR as the fallback for glyphs
    // Galmuri lacks. UI Toolkit text draws from a 12px point-filtered bitmap asset at whole multiples of 12 screen pixels
    // (PixelText), so each font pixel is a crisp square; world text uses a TextMeshPro SDF asset of the same font.
    public sealed class TextRenderingTests
    {
        private const string Folder = "Assets/_Game/UI/Fonts/Galmuri/";
        private const string NotoFolder = "Assets/_Game/UI/Cinematics/Fonts/";

        [Test] public void FontAssetsAreDynamicWithTheGameText()
        {
            var ui = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(Folder + "Galmuri11-Bitmap.asset");
            var world = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "Galmuri11-TMP-SDF.asset");
            Assert.That(ui, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(ui.atlasRenderMode, Is.EqualTo(GlyphRenderMode.RASTER), "One atlas texel per font pixel, no anti-aliasing.");
            Assert.That(ui.faceInfo.pointSize, Is.EqualTo(12));
            Assert.That(ui.atlasTextures.All(t => t.filterMode == FilterMode.Point), Is.True, "Whole-number zoom keeps square pixels.");
            Assert.That(world.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SDFAA));
            Assert.That(ui.atlasPopulationMode, Is.EqualTo(UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic),
                "New lines of text still find their glyphs.");
            Assert.That(world.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(ui.atlasTextures.All(t => t != null && AssetDatabase.Contains(t)), Is.True, "The atlas is saved with the asset.");
            Assert.That(world.atlasTextures.All(t => t != null && AssetDatabase.Contains(t)), Is.True);
            var bold = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(Folder + "Galmuri11-Bold-Bitmap.asset");
            Assert.That(ui.fontWeightTable[7].regularTypeface, Is.SameAs(bold), "Bold text uses the real Galmuri11 Bold cut.");
            Assert.That(ui.fallbackFontAssetTable, Does.Contain(AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(NotoFolder + "NotoSansKR-Regular-SDF.asset")));
            Assert.That(world.fallbackFontAssetTable, Does.Contain(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoFolder + "NotoSansKR-Regular-TMP-SDF.asset")));
            foreach (string text in new[] { "치지직", "새 게임 이어하기 설정 조작 안내 게임 종료", "휴식 구역 준비되면 오른쪽으로 이동 →",
                "STAGE CLEAR COMBO 0123456789" })
            {
                foreach (char c in text.Where(c => c != ' '))
                {
                    Assert.That(ui.characterLookupTable.ContainsKey(c), Is.True, "UI font is missing '" + c + "'.");
                    Assert.That(world.characterLookupTable.ContainsKey(c), Is.True, "World font is missing '" + c + "'.");
                }
            }
        }

        [Test] public void PixelTextDrawsWholeMultiplesOfTheNativeSize()
        {
            // 1920x1080 with the 40% smaller UI: 0.9 screen pixels per panel unit.
            Assert.That(ActionPlatformer.Flow.PixelText.Snap(22f, .9f) * .9f, Is.EqualTo(24f).Within(1e-3f));
            Assert.That(ActionPlatformer.Flow.PixelText.Snap(16f, .9f) * .9f, Is.EqualTo(12f).Within(1e-3f));
            Assert.That(ActionPlatformer.Flow.PixelText.Snap(112f, .9f) * .9f, Is.EqualTo(96f).Within(1e-3f));
            Assert.That(ActionPlatformer.Flow.PixelText.Snap(4f, .9f) * .9f, Is.EqualTo(12f).Within(1e-3f), "Never below the native size.");
            // 2560x1440: 1.2 screen pixels per panel unit.
            Assert.That(ActionPlatformer.Flow.PixelText.Snap(22f, 1.2f) * 1.2f, Is.EqualTo(24f).Within(1e-3f));
            Assert.That(ActionPlatformer.Flow.PixelText.Snap(40f, 1.2f) * 1.2f, Is.EqualTo(48f).Within(1e-3f));
        }

        [Test] public void NoLegacyTextMeshIsLeftInPrefabsOrScenes()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { "Assets/_Game" })
                .Select(AssetDatabase.GUIDToAssetPath).ToList();
            Assert.That(paths, Is.Not.Empty);
            foreach (string path in paths)
                Assert.That(File.ReadAllText(path).Contains("\nTextMesh:"), Is.False, path + " still uses a legacy TextMesh.");
        }

        [Test] public void RestAreaSignIsTextMeshProBelowThePlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/RestArea.prefab");
            var sign = prefab.GetComponentInChildren<TextMeshPro>(true);
            Assert.That(sign, Is.Not.Null);
            Assert.That(sign.font, Is.SameAs(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "Galmuri11-TMP-SDF.asset")));
            Assert.That(sign.text, Does.Contain("휴식 구역"));
            Assert.That(sign.GetComponent<MeshRenderer>().sortingOrder, Is.LessThan(50), "The player (50) stays above the sign.");
        }

        [Test] public void StyleSheetsUseTheSdfFontAsset()
        {
            string theme = File.ReadAllText("Assets/_Game/UI/StageTheme.tss");
            string cutscene = File.ReadAllText("Assets/_Game/UI/Cinematics/Cutscene.uss");
            Assert.That(theme, Does.Contain("-unity-font-definition: url(\"Fonts/Galmuri/Galmuri11-Bitmap.asset\")"));
            Assert.That(cutscene, Does.Contain("-unity-font-definition: url(\"../Fonts/Galmuri/Galmuri11-Bitmap.asset\")"));
            Assert.That(cutscene, Does.Not.Contain("-unity-font:"), "A plain font file draws a bitmap atlas.");
        }
    }
}
