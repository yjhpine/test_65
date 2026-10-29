using UnityEngine;
namespace ActionPlatformer.Presentation
{
    public static class EffectsRuntime
    {
        private static GameObject root;
        private static bool quitting;
        public static VfxManager Vfx { get { Ensure(); return root != null ? root.GetComponent<VfxManager>() : null; } }
        public static SfxManager Sfx { get { Ensure(); return root != null ? root.GetComponent<SfxManager>() : null; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { root=null; quitting=false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => Ensure();
        private static void Ensure()
        {
            if (root != null || quitting || !Application.isPlaying) return;
            var existing=Object.FindFirstObjectByType<VfxManager>(FindObjectsInactive.Include);
            if (existing != null) { root=existing.gameObject; return; }
            root=new GameObject("Global presentation");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<VfxManager>(); root.AddComponent<SfxManager>();
        }
        internal static void Quitting() { quitting=true; }
    }
}
