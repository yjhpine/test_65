using UnityEngine;
using UnityEngine.Rendering;

namespace ActionPlatformer.Player
{
    // A camera-local presentation surface, owned and cleaned up by PlayerImpactFeedback.
    // No Canvas, input interception, render-pipeline asset edits or per-hit instantiation.
    public sealed class PlayerDamageScreen
    {
        private readonly Material template;
        private GameObject surface;
        private Mesh mesh;
        private Material material;
        private MeshRenderer renderer;
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int StreakId = Shader.PropertyToID("_Streak");
        public PlayerDamageScreen(Material template) { this.template = template; }

        public void Draw(Camera camera, float flash, float vignette, float streak)
        {
            if (camera == null || template == null || Mathf.Max(flash, vignette, streak) <= .001f) { Hide(); return; }
            if (mesh == null)
            {
                mesh = new Mesh { name = "Player damage screen quad", hideFlags = HideFlags.HideAndDontSave };
                mesh.vertices = new[] { new Vector3(-1,-1), new Vector3(-1,1), new Vector3(1,1), new Vector3(1,-1) };
                mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds();
            }
            if (material == null) material = new Material(template) { name = "Player damage screen (instance)", hideFlags = HideFlags.HideAndDontSave };
            if (surface == null)
            {
                surface = new GameObject("Player Damage Screen") { hideFlags = HideFlags.DontSave };
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.sortingOrder = 32760;
            }
            // Use a layer already visible to the explicitly assigned camera.
            for (int layer = 0; layer < 32; layer++)
                if ((camera.cullingMask & (1 << layer)) != 0) { surface.layer = layer; break; }
            surface.transform.SetParent(camera.transform, false);
            float distance = camera.nearClipPlane + .05f;
            float halfHeight = camera.orthographic ? camera.orthographicSize : Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * distance;
            surface.transform.localPosition = new Vector3(0,0,distance);
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(halfHeight * camera.aspect * 1.002f, halfHeight * 1.002f, 1);
            material.SetFloat(FlashId, flash); material.SetFloat(VignetteId, vignette); material.SetFloat(StreakId, streak);
            renderer.enabled = true;
        }
        public void Hide() { if (renderer != null) renderer.enabled = false; }
        public void Dispose()
        {
            Hide();
            if (surface != null) Object.Destroy(surface);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
            surface = null; mesh = null; material = null; renderer = null;
        }
    }
}
