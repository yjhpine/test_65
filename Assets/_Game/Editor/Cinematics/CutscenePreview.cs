using ActionPlatformer.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;

namespace ActionPlatformer.Editor
{
    // Preview only owns camera presentation, never gameplay input/time or completion events.
    [InitializeOnLoad]
    public static class CutscenePreview
    {
        private static CutsceneRunner preview;
        static CutscenePreview()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.playModeStateChanged += _ => End();
            EditorSceneManager.sceneSaving += (_, __) => { if (preview != null) { End(); AnimationMode.StopAnimationMode(); } };
        }
        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { End(); return; }
            var director = TimelineEditor.inspectedDirector;
            var runner = director != null ? director.GetComponent<CutsceneRunner>() : null;
            bool active = AnimationMode.InAnimationMode() && runner != null &&
                runner.Coordinator != null && runner.Coordinator.CameraRig != null && runner.Coordinator.CameraRig.IsConfigured;
            if (preview != null && (!active || runner != preview)) End();
            if (!active) return;
            if (preview == null)
            {
                preview = runner;
                preview.BindOutputs();
                // Restore these flags as part of Timeline's own preview transaction, before another
                // Editor update or Play Mode transition can observe the temporary camera owner.
                var rig = preview.Coordinator.CameraRig;
                PreserveEnabled(rig.Output.transform.parent.GetComponent<ActionPlatformer.Flow.StageCameraFollow>());
                PreserveEnabled(rig.Brain);
                preview.Coordinator.CameraRig.Begin();
            }
            preview.EvaluatePresentation(true);
            preview.Coordinator.CameraRig.Tick(0);
            preview.UpdatePresentationPosition();
        }
        private static void PreserveEnabled(Behaviour component)
        {
            AnimationMode.AddPropertyModification(
                EditorCurveBinding.FloatCurve(string.Empty, component.GetType(), "m_Enabled"),
                new PropertyModification { target = component, propertyPath = "m_Enabled", value = component.enabled ? "1" : "0" },
                true);
        }
        public static void End()
        {
            if (preview == null) return;
            var previous = preview; preview = null;
            previous.ClearPreview();
            if (previous.Coordinator != null) previous.Coordinator.CameraRig?.End();
        }
    }
}
