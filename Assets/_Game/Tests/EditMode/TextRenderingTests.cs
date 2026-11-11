using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ActionPlatformer.Tests
{
    // Every game text is drawn from a signed-distance-field font, so it stays sharp when the UI scales up or the
    // camera zooms in: UI Toolkit text uses a TextCore SDF asset, world text uses TextMeshPro.
    public sealed class TextRenderingTests
    {
        private const string Folder = "Assets/_Game/UI/Cinematics/Fonts/";

        [Test] public void BothFontAssetsAreDynamicSdfWithTheGameText()
        {
            var ui = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(Folder + "NotoSansKR-Regular-SDF.asset");
            var world = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "NotoSansKR-Regular-TMP-SDF.asset");
            Assert.That(ui, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(ui.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SDFAA));
            Assert.That(world.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SDFAA));
            Assert.That(ui.atlasPopulationMode, Is.EqualTo(UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic),
                "New lines of text still find their glyphs.");
            Assert.That(world.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(ui.atlasTextures.All(t => t != null && AssetDatabase.Contains(t)), Is.True, "The atlas is saved with the asset.");
            Assert.That(world.atlasTextures.All(t => t != null && AssetDatabase.Contains(t)), Is.True);
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
            Assert.That(sign.font, Is.SameAs(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "NotoSansKR-Regular-TMP-SDF.asset")));
            Assert.That(sign.text, Does.Contain("휴식 구역"));
            Assert.That(sign.GetComponent<MeshRenderer>().sortingOrder, Is.LessThan(50), "The player (50) stays above the sign.");
        }

        [Test] public void StyleSheetsUseTheSdfFontAsset()
        {
            string theme = File.ReadAllText("Assets/_Game/UI/StageTheme.tss");
            string cutscene = File.ReadAllText("Assets/_Game/UI/Cinematics/Cutscene.uss");
            Assert.That(theme, Does.Contain("-unity-font-definition: url(\"Cinematics/Fonts/NotoSansKR-Regular-SDF.asset\")"));
            Assert.That(cutscene, Does.Contain("-unity-font-definition: url(\"Fonts/NotoSansKR-Regular-SDF.asset\")"));
            Assert.That(cutscene, Does.Not.Contain("-unity-font:"), "A plain font file draws a bitmap atlas.");
        }
    }
}
