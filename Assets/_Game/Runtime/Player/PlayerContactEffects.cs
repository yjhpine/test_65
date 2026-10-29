using UnityEngine;

namespace ActionPlatformer.Player
{
    // Short, reusable effects. Target deactivation cannot hide an already accepted hit.
    internal sealed class PlayerContactEffects
    {
        private sealed class Slot
        {
            public SpriteRenderer Flash, Fragments;
            public PlayerHitPresentation Hit;
            public Vector2 Position;
            public double Started;
            public bool Active, Dust, Following;
        }
        private readonly Slot[] slots = new Slot[32];
        private readonly Transform owner;
        private readonly Sprite[] frames;
        private readonly Sprite[] impactFrames, finisherFrames;
        private readonly int sortingLayer, sortingOrder;
        private readonly MaterialPropertyBlock palette = new MaterialPropertyBlock();
        private int next;
        public PlayerContactEffects(Transform owner, SpriteRenderer source, Sprite[] frames, Material material,
            Sprite[] impactFrames = null, Sprite[] finisherFrames = null)
        {
            this.owner = owner; this.frames = frames;
            this.impactFrames = impactFrames; this.finisherFrames = finisherFrames;
            sortingLayer = source.sortingLayerID; sortingOrder = source.sortingOrder + 3;
            for (int i = 0; i < slots.Length; i++) slots[i] = new Slot {
                Flash = Create("Contact flash " + i, source, material), Fragments = Create("Contact fragments " + i, source, material) };
        }
        private SpriteRenderer Create(string name, SpriteRenderer source, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(owner, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.enabled = false;
            renderer.sharedMaterial = material != null ? material : source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + 3;
            return renderer;
        }
        public static Color ColorFor(PlayerAttack kind) => kind == PlayerAttack.Lift || kind == PlayerAttack.Emergence
            ? new Color(.72f, .42f, 1f) : kind == PlayerAttack.Slam || kind == PlayerAttack.Shockwave
            ? new Color(1f, .66f, .18f) : new Color(.42f, 1f, .8f);
        public void Spawn(PlayerHitPresentation hit)
        {
            if ((impactFrames == null || impactFrames.Length == 0) && (frames == null || frames.Length == 0)) return;
            var slot = slots[next]; next = (next + 1) % slots.Length;
            slot.Hit = hit; slot.Position = hit.Point; slot.Started = Time.unscaledTimeAsDouble;
            slot.Active = true; slot.Dust = false; slot.Following = hit.Target != null && hit.Target.gameObject.activeInHierarchy;
            var targetSprite = hit.Target != null ? hit.Target.GetComponentInChildren<SpriteRenderer>(true) : null;
            // Enemy telegraphs render at body order + 2: contact sparks sit between body and warning.
            SetSorting(slot, targetSprite != null ? targetSprite.sortingLayerID : sortingLayer,
                targetSprite != null ? targetSprite.sortingOrder + 1 : sortingOrder);
            SetPalette(slot.Flash, Color.white); SetPalette(slot.Fragments, ColorFor(hit.Kind));
        }
        public void Land(Vector2 point)
        {
            if (frames == null || frames.Length == 0) return;
            var slot = slots[next]; next = (next + 1) % slots.Length;
            slot.Hit = default; slot.Position = point; slot.Started = Time.unscaledTimeAsDouble;
            slot.Active = slot.Dust = true; slot.Following = false;
            SetSorting(slot, sortingLayer, sortingOrder);
            SetPalette(slot.Fragments, new Color(.8f, .65f, .4f)); slot.Flash.enabled = false;
        }
        private void SetPalette(SpriteRenderer renderer, Color color)
        {
            renderer.GetPropertyBlock(palette);
            palette.SetColor("_PrimaryColor", color); palette.SetColor("_AccentColor", color);
            palette.SetFloat("_Highlight", .1f); renderer.SetPropertyBlock(palette);
            palette.SetFloat("_Desaturate", 1f); renderer.SetPropertyBlock(palette);
        }
        private static void SetSorting(Slot slot, int layer, int order)
        {
            slot.Flash.sortingLayerID = slot.Fragments.sortingLayerID = layer;
            slot.Flash.sortingOrder = slot.Fragments.sortingOrder = order;
        }
        public void Tick(double now)
        {
            foreach (var slot in slots)
            {
                if (!slot.Active) continue;
                float age = (float)(now - slot.Started);
                float duration = slot.Dust || slot.Hit.Killed ? .15f : .10f;
                if (age >= duration) { Hide(slot); continue; }
                if (slot.Following)
                {
                    if (slot.Hit.Target != null && slot.Hit.Target.gameObject.activeInHierarchy)
                        slot.Position = slot.Hit.Target.transform.TransformPoint(slot.Hit.LocalPoint);
                    else slot.Following = false;
                }
                float progress = Mathf.Clamp01(age / duration);
                bool imported = !slot.Dust && impactFrames != null && impactFrames.Length > 0;
                var sequence = imported ? (slot.Hit.Killed && finisherFrames != null && finisherFrames.Length > 0
                    ? finisherFrames : impactFrames) : frames;
                int frame = Mathf.Min(sequence.Length - 1, (int)(progress * sequence.Length));
                float size = imported ? (slot.Hit.Killed ? 1.55f : 1.15f) : (slot.Hit.Killed ? .85f : .6f);
                var direction = slot.Dust ? Vector2.up : slot.Hit.Direction;
                Place(slot.Fragments, sequence[frame], slot.Position + direction * (.2f * progress),
                    slot.Dust ? new Vector2(1.4f, .3f) : imported ? Vector2.one * size : new Vector2(size * 1.35f, size * .7f),
                    slot.Dust ? 0 : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                    (1 - progress) * (slot.Dust ? .5f : 1f));
                slot.Flash.enabled = !slot.Dust && age < .05f;
                if (slot.Flash.enabled)
                    Place(slot.Flash, sequence[imported ? Mathf.Min(2, sequence.Length - 1) : 0], slot.Position,
                        Vector2.one * size * .55f, 0, 1 - age / .05f);
            }
        }
        private void Place(SpriteRenderer renderer, Sprite frame, Vector2 point, Vector2 size, float rotation, float alpha)
        {
            renderer.enabled = true; renderer.sprite = frame; renderer.color = new Color(1, 1, 1, alpha);
            renderer.transform.SetPositionAndRotation(new Vector3(point.x, point.y, owner.position.z), Quaternion.Euler(0, 0, rotation));
            Vector3 scale = owner.lossyScale, bounds = frame.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / Mathf.Max(.001f, Mathf.Abs(scale.x) * bounds.x),
                size.y / Mathf.Max(.001f, Mathf.Abs(scale.y) * bounds.y), 1);
        }
        private static void Hide(Slot slot) { slot.Active = false; slot.Flash.enabled = slot.Fragments.enabled = false; slot.Hit = default; }
        public void Reset() { foreach (var slot in slots) Hide(slot); next = 0; }
    }
}
