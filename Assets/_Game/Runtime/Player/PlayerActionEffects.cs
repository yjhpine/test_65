using UnityEngine;

namespace ActionPlatformer.Player
{
    // Owned and reset by PlayerVisual. Effects observe facts; they never advance gameplay.
    internal sealed class PlayerActionEffects
    {
        private readonly Transform owner;
        private readonly ActionPlatformer.Presentation.PlayerVfxProfile profile;
        private const int MaxGlitchSparks = 16;
        private readonly Sprite[] slashFrames, sparkFrames;
        private readonly float slashSize, sparkSize, sparkFps, sparkSpacing, sparkInterval;
        private readonly SpriteRenderer slash;
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[MaxGlitchSparks];
        // The straight streak between the endpoints (PixelFX tile, left-edge pivot, +x = travel). Without its frames
        // the older row of small sparks fills the path instead.
        private readonly Sprite[] streakFrames;
        private readonly float streakFps, streakDrawTime, streakRetract;
        private readonly SpriteRenderer streak;
        private bool Streaking => HasFrames(streakFrames);
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
            float slashSize, float sparkSize, float sparkFps, float sparkSpacing, float sparkInterval, Material effectMaterial,
            ActionPlatformer.Presentation.PlayerVfxProfile profile = null)
        {
            this.owner = owner;
            this.profile = profile;
            this.slashFrames = slashFrames;
            this.sparkFrames = sparkFrames;
            this.slashSize = Mathf.Max(.01f, slashSize);
            this.sparkSize = Mathf.Max(.01f, sparkSize);
            this.sparkFps = Mathf.Max(1, sparkFps);
            this.sparkSpacing = Mathf.Max(.1f, sparkSpacing);
            this.sparkInterval = Mathf.Max(.001f, sparkInterval);
            slash = Create("Basic attack slash", source, effectMaterial);
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Create("Glitch spark " + i, source, effectMaterial);
            streakFrames = profile?.GlitchStreakFrames;
            streakFps = Mathf.Max(1f, profile != null ? profile.GlitchStreakFps : 30f);
            streakDrawTime = Mathf.Max(0f, profile != null ? profile.GlitchStreakDrawTime : 0f);
            streakRetract = Mathf.Clamp01(profile != null ? profile.GlitchStreakRetractStart : .6f);
            streak = Create("Glitch streak", source, effectMaterial);
            // Tiled at art-pixel size along the path, under the endpoint bursts.
            streak.drawMode = SpriteDrawMode.Tiled; streak.tileMode = SpriteTileMode.Continuous;
            streak.sortingOrder = source.sortingOrder + 2;
            // Authored teal/red pixels carry their own palette: neutralize the shared material's tint on this renderer only.
            var block = new MaterialPropertyBlock();
            streak.GetPropertyBlock(block); block.SetColor("_PrimaryColor", Color.white); block.SetColor("_AccentColor", Color.white);
            streak.SetPropertyBlock(block);
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
            var comboFrames = attack.Kind == PlayerAttack.Side || attack.Kind == PlayerAttack.Air
                ? profile?.ComboSlashFrames(attack.Strike) : null;
            bool authoredCombo = HasFrames(comboFrames);
            var frames = authoredCombo ? comboFrames : slashFrames;
            slash.enabled = !actions.IsUnderground && HasFrames(frames) &&
                (down ? phase == PlayerAttackPhase.Descending : phase != PlayerAttackPhase.Ready);
            if (slash.enabled)
            {
                // The leading frames wind up the arc. Show its crest with the hit, then its tail in recovery.
                float progress = down ? .4f : phase == PlayerAttackPhase.Windup ? .2f : phase == PlayerAttackPhase.Active
                    ? Mathf.Lerp(.2f, .55f, attack.PhaseProgress) : Mathf.Lerp(.55f, 1f, attack.PhaseProgress);
                slash.sprite = frames[Mathf.Min(frames.Length - 1, (int)(progress * frames.Length))];
                slash.flipX = actions.Facing < 0f;
                slash.flipY = !authoredCombo && !up && !down && variant == 1;
                // Both slash sources draw their arc beside the pivot (combo art pivots on the character), so the
                // player's center keeps the arc in front of, above or below the body without rising as it grows.
                Place(slash, owner.position, slashSize);
                if (authoredCombo)
                    slash.transform.localScale = Vector3.Scale(slash.transform.localScale,
                        new Vector3(1, slash.sprite.bounds.size.y / slash.sprite.bounds.size.x, 1));
                slash.transform.rotation = Quaternion.Euler(0, 0, up ? 90 * actions.Facing : down ? -90 * actions.Facing : 0);
                if (up || down) slash.transform.localScale = Vector3.Scale(slash.transform.localScale, new Vector3(.8f, .45f, 1));
                // A strong lift or slam keeps its kind's look, larger and brighter; side strikes use their own third arc.
                bool strongSpecial = attack.Finisher && (up || down);
                if (strongSpecial) slash.transform.localScale *= 1.3f;
                // Authored cyan/red pixels already carry their palette. Keep the same material,
                // but neutralize its tint only on this renderer (never on shared glitch sparks).
                Color color = authoredCombo ? Color.white : PlayerContactEffects.ColorFor(attack.Kind);
                if (strongSpecial) color = Color.Lerp(color, Color.white, .35f);
                slash.GetPropertyBlock(palette); palette.SetColor("_PrimaryColor", color); palette.SetColor("_AccentColor", color);
                slash.SetPropertyBlock(palette);
            }

            if (glitchStartedFrame != Time.frameCount) glitchAge += Mathf.Max(0, deltaTime);
            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                if (i >= sparkCount) { spark.enabled = false; continue; }
                // The departure bursts at once. With the streak, the arrival bursts as the line reaches it; without it,
                // the arrival is immediate too and smaller sparks pop along the path in order.
                bool endpoint = i == 0 || i == sparkCount - 1;
                float delay = !endpoint ? i * sparkInterval : Streaking && i > 0 ? streakDrawTime : 0f;
                float age = glitchAge - delay;
                int frame = Mathf.FloorToInt(age * sparkFps);
                spark.enabled = frame >= 0 && frame < sparkFrames.Length;
                if (!spark.enabled) continue;
                spark.sprite = sparkFrames[frame];
                float along = sparkCount == 1 ? 0 : (float)i / (sparkCount - 1);
                Place(spark, Vector2.Lerp(glitchFrom, glitchTo, along), sparkSize * (endpoint ? 1f : .5f));
            }
            RenderStreak();
        }

        // Shoots from the departure to the arrival in the draw time (fast start), plays its frames, and once
        // RetractStart of them has passed pulls its tail in from the departure side until it is gone.
        private void RenderStreak()
        {
            float distance = Vector2.Distance(glitchFrom, glitchTo);
            float life = Streaking ? streakFrames.Length / streakFps : 0f;
            streak.enabled = Streaking && glitchStartedFrame >= 0 && distance > .001f && glitchAge < life;
            if (!streak.enabled) return;
            streak.sprite = streakFrames[Mathf.Min(streakFrames.Length - 1, Mathf.FloorToInt(glitchAge * streakFps))];
            float head = streakDrawTime <= 0f ? 1f : Mathf.Clamp01(glitchAge / streakDrawTime);
            head = 1f - (1f - head) * (1f - head);
            float retractFrom = life * streakRetract;
            float tail = life <= retractFrom ? 0f : Mathf.Clamp01((glitchAge - retractFrom) / (life - retractFrom));
            float length = (head - tail) * distance;
            if (length <= .001f) { streak.enabled = false; return; }
            Vector2 direction = (glitchTo - glitchFrom) / distance, start = glitchFrom + direction * (tail * distance);
            var t = streak.transform;
            t.SetPositionAndRotation(new Vector3(start.x, start.y, owner.position.z),
                Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            // Cancel the owner's scale so the tile keeps its art-pixel size and the streak is exactly the path long.
            var scale = owner.lossyScale;
            t.localScale = new Vector3(Inverse(scale.x), Inverse(scale.y), 1f);
            streak.size = new Vector2(length, streak.sprite.bounds.size.y);
        }

        private static float Inverse(float scale) => Mathf.Abs(scale) < 1e-4f ? 1f : 1f / Mathf.Abs(scale);

        public void PlayGlitch(PlayerGlitchPresentation glitch)
        {
            glitchFrom=glitch.From; glitchTo=glitch.To; glitchAge=0; glitchStartedFrame=Time.frameCount;
            float distance=Vector2.Distance(glitchFrom,glitchTo);
            // With the streak only the two endpoint bursts remain; the line itself fills the path.
            sparkCount=!HasFrames(sparkFrames)?0:distance<.001f?1:Streaking?2:Mathf.Clamp(Mathf.CeilToInt(distance/sparkSpacing)+1,2,sparks.Length);
        }

        public void Reset(uint glitchVersion)
        {
            glitchStartedFrame = -1;
            slash.enabled = false;
            foreach (var spark in sparks) spark.enabled = false;
            streak.enabled = false;
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
