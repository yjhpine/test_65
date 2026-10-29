using UnityEngine;

namespace ActionPlatformer.Player
{
    // Owned and reset by PlayerVisual. Effects observe facts; they never advance gameplay.
    internal sealed class PlayerActionEffects
    {
        private readonly Transform owner;
        private const int MaxGlitchSparks = 16;
        private readonly Sprite[] slashFrames, sparkFrames;
        private readonly float slashSize, sparkSize, sparkFps, sparkSpacing, sparkInterval;
        private readonly SpriteRenderer slash;
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[MaxGlitchSparks];
        private Vector2 glitchFrom, glitchTo;
        private float glitchAge;
        private int sparkCount;
        private int glitchStartedFrame = -1;
        private readonly SpriteRenderer[] transferSparks = new SpriteRenderer[8];
        private readonly Vector2[] transferPositions = new Vector2[8];
        private readonly double[] transferStarted = new double[8];
        private int nextTransferSpark;
        private readonly MaterialPropertyBlock palette = new MaterialPropertyBlock();

        public PlayerActionEffects(Transform owner, SpriteRenderer source, Sprite[] slashFrames, Sprite[] sparkFrames,
            float slashSize, float sparkSize, float sparkFps, float sparkSpacing, float sparkInterval, Material effectMaterial)
        {
            this.owner = owner;
            this.slashFrames = slashFrames;
            this.sparkFrames = sparkFrames;
            this.slashSize = Mathf.Max(.01f, slashSize);
            this.sparkSize = Mathf.Max(.01f, sparkSize);
            this.sparkFps = Mathf.Max(1, sparkFps);
            this.sparkSpacing = Mathf.Max(.1f, sparkSpacing);
            this.sparkInterval = Mathf.Max(.001f, sparkInterval);
            slash = Create("Basic attack slash", source, effectMaterial);
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Create("Glitch spark " + i, source, effectMaterial);
            for (int i = 0; i < transferSparks.Length; i++)
            {
                transferSparks[i] = Create("Transfer spark " + i, source, effectMaterial);
                transferStarted[i] = double.NegativeInfinity;
            }
        }

        public void Burst(Vector2 point)
        {
            transferPositions[nextTransferSpark] = point;
            transferStarted[nextTransferSpark] = Time.unscaledTimeAsDouble;
            nextTransferSpark = (nextTransferSpark + 1) % transferSparks.Length;
        }

        public void Tick(IPlayerActionState actions, float deltaTime, int variant = 0, bool reactionBlocksAttack = false)
        {
            if (actions == null || reactionBlocksAttack || actions.ReactionPresentation.Kind == PlayerReaction.Die ||
                (actions.ReactionPresentation.Kind == PlayerReaction.Hit && actions.AttackPhase == PlayerAttackPhase.Ready))
            {
                Reset(actions?.GlitchPresentation.Version ?? 0);
                return;
            }
            var attack = actions.AttackPresentation;
            for (int i = 0; i < transferSparks.Length; i++)
            {
                var burst = transferSparks[i];
                double age = Time.unscaledTimeAsDouble - transferStarted[i];
                bool visible = HasFrames(sparkFrames) && age >= 0 && age < sparkFrames.Length / sparkFps;
                burst.enabled = visible;
                if (!visible) continue;
                burst.sprite = sparkFrames[(int)(age * sparkFps)];
                Place(burst, transferPositions[i], sparkSize);
            }
            var phase = actions.AttackPhase;
            bool down = attack.Kind == PlayerAttack.Slam;
            bool up = attack.Kind == PlayerAttack.Lift || attack.Kind == PlayerAttack.Emergence;
            slash.enabled = !actions.IsUnderground && HasFrames(slashFrames) &&
                (down ? phase == PlayerAttackPhase.Descending : phase != PlayerAttackPhase.Ready);
            if (slash.enabled)
            {
                // The leading frames wind up the arc. Show its crest with the hit, then its tail in recovery.
                float progress = down ? .4f : phase == PlayerAttackPhase.Windup ? .2f : phase == PlayerAttackPhase.Active
                    ? Mathf.Lerp(.2f, .55f, attack.PhaseProgress) : Mathf.Lerp(.55f, 1f, attack.PhaseProgress);
                slash.sprite = slashFrames[Mathf.Min(slashFrames.Length - 1, (int)(progress * slashFrames.Length))];
                slash.flipX = actions.Facing < 0f;
                slash.flipY = !up && !down && variant == 1;
                Vector2 offset = up ? new Vector2(.15f * actions.Facing, .55f) : down ?
                    new Vector2(.3f * actions.Facing, -.35f) : new Vector2(.65f * actions.Facing, .3f);
                Place(slash, owner.TransformPoint(offset), slashSize);
                slash.transform.rotation = Quaternion.Euler(0, 0, up ? 90 * actions.Facing : down ? -90 * actions.Facing : 0);
                if (up || down) slash.transform.localScale = Vector3.Scale(slash.transform.localScale, new Vector3(.8f, .45f, 1));
                Color color = PlayerContactEffects.ColorFor(attack.Kind);
                slash.GetPropertyBlock(palette); palette.SetColor("_PrimaryColor", color); palette.SetColor("_AccentColor", color);
                slash.SetPropertyBlock(palette);
            }

            if (glitchStartedFrame != Time.frameCount) glitchAge += Mathf.Max(0, deltaTime);
            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                if (i >= sparkCount) { spark.enabled = false; continue; }
                // Keep the original departure/arrival bursts immediate. Smaller sparks pop along the path in order.
                bool endpoint = i == 0 || i == sparkCount - 1;
                float age = glitchAge - (endpoint ? 0 : i * sparkInterval);
                int frame = Mathf.FloorToInt(age * sparkFps);
                spark.enabled = frame >= 0 && frame < sparkFrames.Length;
                if (!spark.enabled) continue;
                spark.sprite = sparkFrames[frame];
                float along = sparkCount == 1 ? 0 : (float)i / (sparkCount - 1);
                Place(spark, Vector2.Lerp(glitchFrom, glitchTo, along), sparkSize * (endpoint ? 1f : .5f));
            }
        }

        public void PlayGlitch(PlayerGlitchPresentation glitch)
        {
            glitchFrom=glitch.From; glitchTo=glitch.To; glitchAge=0; glitchStartedFrame=Time.frameCount;
            float distance=Vector2.Distance(glitchFrom,glitchTo);
            sparkCount=!HasFrames(sparkFrames)?0:distance<.001f?1:Mathf.Clamp(Mathf.CeilToInt(distance/sparkSpacing)+1,2,sparks.Length);
        }

        public void Reset(uint glitchVersion)
        {
            glitchStartedFrame = -1;
            slash.enabled = false;
            foreach (var spark in sparks) spark.enabled = false;
            glitchAge = 0;
            sparkCount = 0;
            for (int i = 0; i < transferSparks.Length; i++)
            { transferSparks[i].enabled = false; transferStarted[i] = double.NegativeInfinity; }
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
