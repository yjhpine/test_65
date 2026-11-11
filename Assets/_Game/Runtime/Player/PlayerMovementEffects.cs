using UnityEngine;

namespace ActionPlatformer.Player
{
    // Observes presentation state only; never changes movement, collisions or jump timing.
    internal sealed class PlayerMovementEffects
    {
        private sealed class Puff
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public Vector2 Point;
            public double Started;
            public float Duration, Width, Alpha;
        }
        private readonly Puff[] pool = new Puff[8];
        private readonly Transform owner;
        private readonly Sprite[] jump, ground;
        private int next;
        public PlayerMovementEffects(Transform owner, SpriteRenderer source, Sprite[] jump, Sprite[] ground, Material material)
        {
            this.owner = owner; this.jump = jump; this.ground = ground;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_PrimaryColor", new Color(.75f, .85f, .85f));
            properties.SetColor("_AccentColor", new Color(.75f, .85f, .85f));
            properties.SetFloat("_Highlight", 0);
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Movement dust " + i); go.transform.SetParent(owner, false);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.enabled = false;
                renderer.sharedMaterial = material != null ? material : source.sharedMaterial;
                renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder - 1;
                renderer.SetPropertyBlock(properties);
                pool[i] = new Puff { Renderer = renderer };
            }
        }
        private void Spawn(Sprite[] frames, Vector2 point, float width, float duration, float alpha, bool flip = false)
        {
            if (frames == null || frames.Length == 0) return;
            var puff = pool[next]; next = (next + 1) % pool.Length;
            puff.Frames = frames; puff.Point = point; puff.Width = width;
            puff.Duration = duration; puff.Alpha = alpha; puff.Started = Time.unscaledTimeAsDouble;
            puff.Renderer.flipX = flip;
        }
        public void Land(Vector2 point, bool slam = false)
        {
            Spawn(ground, point, slam ? 2.8f : 1.7f, slam ? .15f : .28f, .7f);
        }

        public void Play(ActionPlatformer.Presentation.MovementDustKind kind,Vector2 point,bool flip)
        {
            switch(kind) {
                case ActionPlatformer.Presentation.MovementDustKind.Jump: Spawn(jump,point,1.35f,.32f,.7f,flip); break;
                case ActionPlatformer.Presentation.MovementDustKind.Brake: Spawn(jump,point,1.15f,.28f,.55f,flip); break;
                case ActionPlatformer.Presentation.MovementDustKind.Trail: Spawn(ground,point,.8f,.24f,.4f); break;
                default: Land(point,kind==ActionPlatformer.Presentation.MovementDustKind.SlamLanding); break;
            }
        }
        public void Render(double now)
        {
            foreach (var puff in pool)
            {
                if (puff.Frames == null) continue;
                float progress = (float)((now - puff.Started) / puff.Duration);
                if (progress >= 1) { puff.Renderer.enabled = false; puff.Frames = null; continue; }
                var frame = puff.Frames[Mathf.Clamp((int)(progress * puff.Frames.Length), 0, puff.Frames.Length - 1)];
                var renderer = puff.Renderer;
                renderer.enabled = true; renderer.sprite = frame;
                renderer.color = new Color(1, 1, 1, puff.Alpha * (1 - progress * .65f));
                renderer.transform.SetPositionAndRotation(new Vector3(puff.Point.x, puff.Point.y, owner.position.z), Quaternion.identity);
                float size = puff.Width / Mathf.Max(.001f, frame.bounds.size.x);
                Vector3 scale = owner.lossyScale;
                renderer.transform.localScale = new Vector3(size / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    size / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
            }
        }
        public void Reset()
        {
            foreach (var puff in pool) { puff.Frames = null; puff.Renderer.enabled = false; }
            next = 0;
        }
    }
}
