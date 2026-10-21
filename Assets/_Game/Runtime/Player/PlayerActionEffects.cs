using UnityEngine;

namespace ActionPlatformer.Player
{
    // Owned and reset by PlayerVisual. Effects observe facts; they never advance gameplay.
    internal sealed class PlayerActionEffects
    {
        private readonly Transform owner;
        private readonly Sprite[] slashFrames, sparkFrames;
        private readonly float slashSize, sparkSize, sparkFps;
        private readonly SpriteRenderer slash;
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[4];
        private readonly Vector2[] sparkPositions = new Vector2[4];
        private readonly float[] sparkAges = new float[4];
        private uint previousGlitchVersion;
        private int nextSpark;

        public PlayerActionEffects(Transform owner, SpriteRenderer source, Sprite[] slashFrames,
            Sprite[] sparkFrames, float slashSize, float sparkSize, float sparkFps, Material effectMaterial)
        {
            this.owner = owner;
            this.slashFrames = slashFrames;
            this.sparkFrames = sparkFrames;
            this.slashSize = Mathf.Max(.01f, slashSize);
            this.sparkSize = Mathf.Max(.01f, sparkSize);
            this.sparkFps = Mathf.Max(1f, sparkFps);
            slash = Create("Basic attack slash", source, effectMaterial);
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Create("Glitch spark " + i, source, effectMaterial);
        }

        public void Tick(IPlayerActionState actions, float deltaTime)
        {
            if (actions == null || actions.ReactionPresentation.Kind != PlayerReaction.None)
            {
                Reset(actions?.GlitchPresentation.Version ?? 0);
                return;
            }
            var attack = actions.AttackPresentation;
            var phase = actions.AttackPhase;
            bool basic = attack.Kind == PlayerAttack.Side || attack.Kind == PlayerAttack.Air;
            slash.enabled = !actions.IsUnderground && basic && HasFrames(slashFrames) &&
                (phase == PlayerAttackPhase.Active || phase == PlayerAttackPhase.Recovery);
            if (slash.enabled)
            {
                // The leading frames wind up the arc. Show its crest with the hit, then its tail in recovery.
                float progress = phase == PlayerAttackPhase.Active
                    ? Mathf.Lerp(.2f, .55f, attack.PhaseProgress) : Mathf.Lerp(.55f, 1f, attack.PhaseProgress);
                slash.sprite = slashFrames[Mathf.Min(slashFrames.Length - 1, (int)(progress * slashFrames.Length))];
                slash.flipX = actions.Facing < 0f;
                Place(slash, actions.AttackBounds.center, slashSize);
            }

            for (int i = 0; i < sparks.Length; i++)
            {
                if (!sparks[i].enabled) continue;
                sparkAges[i] += deltaTime;
                int frame = (int)(sparkAges[i] * sparkFps);
                if (frame >= sparkFrames.Length) { sparks[i].enabled = false; continue; }
                sparks[i].sprite = sparkFrames[frame];
                // Keep both bursts at their recorded world points while the player moves or hides underground.
                Place(sparks[i], sparkPositions[i], sparkSize);
            }
            var glitch = actions.GlitchPresentation;
            if (glitch.Version == previousGlitchVersion) return;
            previousGlitchVersion = glitch.Version;
            if (!HasFrames(sparkFrames)) return;
            SpawnSpark(glitch.From);
            SpawnSpark(glitch.To);
        }

        public void Reset(uint glitchVersion)
        {
            previousGlitchVersion = glitchVersion;
            nextSpark = 0;
            slash.enabled = false;
            for (int i = 0; i < sparks.Length; i++) sparks[i].enabled = false;
        }

        private void SpawnSpark(Vector2 position)
        {
            int index = nextSpark;
            nextSpark = (nextSpark + 1) % sparks.Length;
            sparkPositions[index] = position;
            sparkAges[index] = 0f;
            sparks[index].sprite = sparkFrames[0];
            sparks[index].enabled = true;
            Place(sparks[index], position, sparkSize);
        }

        private SpriteRenderer Create(string name, SpriteRenderer source, Material effectMaterial)
        {
            var child = new GameObject(name);
            child.transform.SetParent(owner, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = effectMaterial != null ? effectMaterial : source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + 3;
            renderer.enabled = false;
            return renderer;
        }

        private void Place(SpriteRenderer renderer, Vector2 position, float size)
        {
            renderer.transform.SetPositionAndRotation(new Vector3(position.x, position.y, owner.position.z), Quaternion.identity);
            var scale = owner.lossyScale;
            var spriteSize = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size / (Mathf.Abs(scale.x) * spriteSize.x),
                size / (Mathf.Abs(scale.y) * spriteSize.y), 1f);
        }

        private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;
    }
}
