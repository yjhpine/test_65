using UnityEngine;

namespace ActionPlatformer.Units.Features
{
    // ARCBIT's modular sprites only display the beam already resolved by EnemyCombat2D.
    internal sealed class EnemyBeamEffects
    {
        private readonly Sprite[] startFrames, bodyFrames, endFrames;
        private readonly Material beamMaterial;
        private readonly Color telegraphColor;
        private readonly Transform transform;
        public EnemyBeamEffects(Transform root, ActionPlatformer.Presentation.EnemyVfxProfile profile, SpriteRenderer source)
        {
            transform=root; startFrames=profile.StartFrames; bodyFrames=profile.BodyFrames; endFrames=profile.EndFrames;
            beamMaterial=profile.BeamMaterial; telegraphColor=profile.TelegraphColor; Initialize(source);
        }
        private readonly SpriteRenderer[] starts = new SpriteRenderer[2];
        private readonly SpriteRenderer[] bodies = new SpriteRenderer[2];
        private readonly SpriteRenderer[] ends = new SpriteRenderer[2];
        private bool initialized;
        public Color TelegraphColor => telegraphColor;
        public bool IsConfigured => startFrames != null && startFrames.Length == 21 &&
            bodyFrames != null && bodyFrames.Length == 13 && endFrames != null && endFrames.Length == 13 && beamMaterial != null;

        public void Initialize(SpriteRenderer source)
        {
            if (initialized || !IsConfigured) return;
            for (int i = 0; i < 2; i++)
            {
                starts[i] = Create("Laser start " + i, source);
                bodies[i] = Create("Laser body " + i, source);
                ends[i] = Create("Laser end " + i, source);
                bodies[i].drawMode = SpriteDrawMode.Tiled;
                bodies[i].tileMode = SpriteTileMode.Continuous;
            }
            initialized = true;
        }

        public void ShowCharge(EnemyBeam beam, Vector2 emitter, bool vertical, float progress)
        {
            if (!initialized) return;
            int frame = Mathf.Min(7, Mathf.FloorToInt(Mathf.Clamp01(progress) * 8));
            if (vertical)
            {
                Charge(0, emitter, Vector2.up, beam.Width, frame);
                Charge(1, emitter, Vector2.down, beam.Width, frame);
            }
            else Charge(0, beam.Start, beam.End - beam.Start, beam.Width, frame);
        }

        public bool ShowShot(EnemyBeam beam, Vector2 emitter, bool vertical, float progress)
        {
            if (!initialized) return false;
            // Retimed to the existing BeamDisplayDuration; these sprites never delay or repeat damage.
            // Body/end: on 0..2, loop 3..8, off 9..12. Start has eight additional charge frames.
            float p = Mathf.Clamp01(progress);
            int frame = p < .25f ? Mathf.Min(2, (int)(p / .25f * 3)) : p < .7f ?
                3 + Mathf.Min(5, (int)((p - .25f) / .45f * 6)) : 9 + Mathf.Min(3, (int)((p - .7f) / .3f * 4));
            if (vertical)
            {
                // The drone fires both ways from its own center, with a cap at each clipped endpoint.
                Vector2 delta = beam.End - beam.Start;
                float along = delta.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(emitter - beam.Start, delta) / delta.sqrMagnitude);
                Vector2 origin = beam.Start + delta * along;
                Segment(0, new EnemyBeam(origin, beam.Start, beam.Width), frame);
                Segment(1, new EnemyBeam(origin, beam.End, beam.Width), frame);
            }
            else Segment(0, beam, frame);
            return true;
        }

        private void Charge(int index, Vector2 origin, Vector2 direction, float width, int frame)
        {
            var sprite = startFrames[frame];
            starts[index].sprite = sprite;
            Place(starts[index], origin, direction, width * sprite.pixelsPerUnit / 8f);
        }

        private void Segment(int index, EnemyBeam beam, int frame)
        {
            Vector2 delta = beam.End - beam.Start;
            float length = delta.magnitude;
            if (length <= .0001f) return;
            Vector2 direction = delta / length;
            var start = startFrames[frame + 8]; var end = endFrames[frame]; var body = bodyFrames[frame];
            // The authored beam core is eight pixels high; the surrounding sparks are only a visual fringe.
            float scale = beam.Width * body.pixelsPerUnit / 8f;
            float lead = (start.rect.width - start.pivot.x) / start.pixelsPerUnit;
            float tail = end.pivot.x / end.pixelsPerUnit;
            // Very short wall-clipped shots shrink both caps together instead of overlapping past each other.
            scale = Mathf.Min(scale, length / (lead + tail));
            starts[index].sprite = start; ends[index].sprite = end;
            Place(starts[index], beam.Start, direction, scale);
            Place(ends[index], beam.End, direction, scale);
            float gap = Mathf.Max(0, length - (lead + tail) * scale);
            if (gap <= .0001f) return;
            bodies[index].sprite = body;
            bodies[index].size = new Vector2(gap / scale, body.rect.height / body.pixelsPerUnit);
            Place(bodies[index], beam.Start + direction * lead * scale, direction, scale);
        }

        private void Place(SpriteRenderer renderer, Vector2 position, Vector2 direction, float scale)
        {
            var parentScale = transform.lossyScale;
            renderer.transform.SetPositionAndRotation(new Vector3(position.x, position.y, transform.position.z),
                Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            renderer.transform.localScale = new Vector3(scale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
            renderer.enabled = true;
        }

        private SpriteRenderer Create(string label, SpriteRenderer source)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = beamMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + 2;
            renderer.enabled = false;
            return renderer;
        }

        public void Hide()
        {
            if (!initialized) return;
            for (int i = 0; i < 2; i++) starts[i].enabled = bodies[i].enabled = ends[i].enabled = false;
        }

    }
}
