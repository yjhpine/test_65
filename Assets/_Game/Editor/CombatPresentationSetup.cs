using System.Linq;
using ActionPlatformer.Player;
using ActionPlatformer.Units.Features;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ActionPlatformer.Editor
{
    // Explicit, repeatable asset wiring. Never changes a scene or runs automatically on import.
    public static class CombatPresentationSetup
    {
        [MenuItem("Game/Prototype/Configure Combat Presentation %#g")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode before configuring presentation."); return; }
            const string folder = "Assets/_Game/Animations/Player/ArtCharacter/";
            var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "SideAttack.anim");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(original).Single();
            var originalKeys = AnimationUtility.GetObjectReferenceCurve(original, binding);
            var activeKeys = originalKeys.Where(k => k.time >= .35f && k.time < .6f).ToArray();
            AnimationClip[] variants = new AnimationClip[2];
            for (int variant = 0; variant < 2; variant++)
            {
                string path = folder + (variant == 0 ? "SideAttackA.anim" : "SideAttackB.anim");
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                EditorUtility.CopySerialized(original, clip); clip.name = variant == 0 ? "SideAttackA" : "SideAttackB";
                var keys = originalKeys.Select(k => new ObjectReferenceKeyframe { time = k.time, value = k.value }).ToArray();
                int index = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i].time < .35f) keys[i].value = activeKeys[variant == 0 ? 0 : activeKeys.Length - 1].value;
                    else if (keys[i].time < .6f)
                    { keys[i].value = activeKeys[variant == 0 ? index : activeKeys.Length - 1 - index].value; index++; }
                }
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys); variants[variant] = clip;
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Game/Animations/Player/Player.controller");
            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state.name.StartsWith("Attack") || child.state.name.StartsWith("AirAttack"))
                    child.state.motion = variants[child.state.name.EndsWith("2") ? 1 : 0];
            EditorUtility.SetDirty(controller);
            const string materialPath = "Assets/_Game/Art/Effects/UnitReaction.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("ActionPlatformer/Unit Reaction");
                if (shader == null) throw new System.InvalidOperationException("Unit Reaction shader was not imported.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
            }
            foreach (string name in new[] { "Player", "Enemies/PatrolEnemy", "Enemies/ShieldSoldier", "Enemies/FlyingDrone", "Enemies/SurveillanceTurret" })
            {
                string path = "Assets/_Game/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var visual = root.transform.Find("Visual");
                    var renderer = visual.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
                    if (name != "Player" && visual.GetComponent<UnitDamageFlash>() == null) visual.gameObject.AddComponent<UnitDamageFlash>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerCombatTuning>("Assets/_Game/Data/PlayerCombatTuning.asset");
            var data = new SerializedObject(tuning);
            Set(data, "shakeAmplitude", 1.5f); Set(data, "heavyShakeAmplitude", 2); Set(data, "slamShakeAmplitude", 3);
            Set(data, "shakeDuration", .06f); Set(data, "damageShakeAmplitude", 4); Set(data, "damageShakeDuration", .12f);
            Set(data, "damageZoom", 0); Set(data, "damageFlashOpacity", 0);
            Set(data, "damageVignetteOpacity", .12f); Set(data, "damageScreenDuration", .16f);
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Combat presentation configured: A/B clips, reaction material, hit flashes, reference-pixel camera. Gameplay/time settings preserved.");
        }
        private static void Set(SerializedObject data, string field, float value) => data.FindProperty(field).floatValue = value;
    }
}
