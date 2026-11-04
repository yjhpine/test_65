using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using System.Collections.Generic;
using UnityEngine;

namespace ActionPlatformer.Units.Bosses
{
    // One boss's state graph and fight memory. Created per Unit by BossFsmDefinition; shared assets stay read-only.
    public sealed class BossBrain : IBossActionState, IUnitDamageGuard
    {
        private readonly BossFsmDefinition settings;
        private readonly BossContext context;
        private readonly Dictionary<BossPatternDefinition, BossPatternRun> runs = new Dictionary<BossPatternDefinition, BossPatternRun>();
        private readonly Node dormant, approach, telegraph, execute, recover, stagger, phaseShift, death;
        private BossPatternRun activeRun;
        private BossPatternDefinition pattern;
        private BossState current;
        private bool recognized;
        private float lostTime;
        // Set by Go() so Exit can tell a scheduled transition from a Stop; the version guards against a Stop+Start inside one Tick.
        private Node scheduledFrom;
        private uint scheduledVersion;
        private uint seenInterrupt;
        private bool hasStaggered;
        private float lastStaggerAt;
        private int phasePending;
        private float elapsed;
        private float progressDuration;
        private uint stateVersion;
        private string selectionTrace;

        public BossBrain(Unit owner, BossFsmDefinition settings)
        {
            this.settings = settings;
            var health = owner.GetComponent<UnitHealth>();
            var combat = owner.GetComponent<BossCombat2D>();
            var mover = new GroundBossMover(owner.GetComponent<GroundMovement2D>());
            var board = new BossBlackboard(settings.UseFixedSeed ? settings.Seed : (int?)null);
            context = new BossContext(owner, health, combat, mover, board, settings) { Facing = combat.InitialFacing };
            dormant = new Node(this, BossState.Dormant); approach = new Node(this, BossState.Approach);
            telegraph = new Node(this, BossState.Telegraph); execute = new Node(this, BossState.Execute);
            recover = new Node(this, BossState.Recover); death = new Node(this, BossState.Death);
            stagger = new Node(this, BossState.Staggered); phaseShift = new Node(this, BossState.PhaseShift);
            current = BossState.Dormant;
            board.BossFacing = context.Facing;
            seenInterrupt = health.InterruptVersion;
            context.RunProvider = RunFor;
            combat.Bind(this, this);
        }

        public IFsmState InitialState => dormant;
        public BossContext Context => context;
        public BossBlackboard Board => context.Board;

        public BossState State => current;
        public uint StateVersion => stateVersion;
        public BossPatternDefinition Pattern => pattern;
        public float StateElapsed => elapsed;
        public float Progress => current == BossState.Telegraph || current == BossState.Execute ? (activeRun?.Progress ?? 0f) :
            progressDuration > 0f ? Mathf.Clamp01(elapsed / progressDuration) : 0f;
        public float Facing => context.Facing;
        public int Phase => Board.Phase;
        public BossTelegraph Telegraph => (current == BossState.Telegraph || current == BossState.Execute) && activeRun != null ? activeRun.Telegraph : default;
        public string LastSelectionTrace => selectionTrace;
        public BossFsmDefinition Settings => settings;

        public bool BlocksDamage(Vector2 source) =>
            (current == BossState.Telegraph || current == BossState.Execute) && activeRun != null && activeRun.BlocksDamage(source);

        // Everything every state does first, in priority order. Returns a state to force a transition.
        private Node PreTick(float dt)
        {
            Board.Advance(dt);
            BossSenses.Refresh(Board, context, dt);
            recognized = BossSenses.IsRecognized(Board, settings);
            // Counts continuously across every state, so a long Telegraph/Recover still adds up to losing the player.
            lostTime = recognized ? 0f : lostTime + dt;
            // An idle boss forgets a target it cannot recognize, so the next tick searches again.
            if (!recognized && current == BossState.Dormant) Board.Target = null;
            Board.BossFacing = context.Facing;
            if (!context.Health.IsAlive) return death;
            phasePending = Mathf.Max(phasePending, settings.PhaseFor(Board.HealthRatio));
            uint version = context.Health.InterruptVersion;
            bool interrupted = version != seenInterrupt;
            seenInterrupt = version;
            // A hit that cannot stagger is dropped, never applied later.
            if (interrupted && CanStagger() && (!hasStaggered || Board.Clock - lastStaggerAt >= settings.StaggerImmunity)) return stagger;
            if ((current == BossState.Dormant || current == BossState.Approach) && phasePending > Phase) return phaseShift;
            return null;
        }

        private bool CanStagger()
        {
            switch (current)
            {
                case BossState.Dormant: case BossState.Approach: case BossState.Recover: return true;
                case BossState.Telegraph: return pattern != null && pattern.InterruptibleDuringTelegraph;
                case BossState.Execute: return pattern != null && pattern.InterruptibleDuringExecute;
                default: return false;
            }
        }

        // Where a finished Recover or Staggered goes: a pending phase change comes first.
        private Node AfterAction => phasePending > Phase ? phaseShift : approach;

        private BossPatternRun RunFor(BossPatternDefinition p)
        {
            if (!runs.TryGetValue(p, out var run)) runs[p] = run = p.CreateRun(context);
            return run;
        }

        private void Select()
        {
            var selection = new BossSelection(Board, context);
            pattern = settings.Selector.Select(selection);
            selectionTrace = selection.TraceText;
        }

        // Leaving through anything but a scheduled transition (disable, Fsm.Stop) means the fight is over.
        private void StopCleanup()
        {
            activeRun?.Cancel();
            activeRun = null;
            foreach (var run in runs.Values) run.OnBossStopped();
            pattern = null;
            context.Mover.Stop();
            context.Health.SetDamageEnabled(true);
            Board.ResetMemory();
            Board.Target = null;
            lostTime = 0f;
            Board.Phase = 0;
            phasePending = 0; hasStaggered = false; lastStaggerAt = 0f;
            seenInterrupt = context.Health.InterruptVersion;
            scheduledFrom = null;
            current = BossState.Dormant; elapsed = 0f; progressDuration = 0f;
            // Like other enemies, a restarted boss goes back to its original facing.
            context.Facing = context.Combat.InitialFacing;
            Board.BossFacing = context.Facing;
        }

        private sealed class Node : IFsmState
        {
            private readonly BossBrain b;
            private readonly BossState kind;
            private float selectTimer;
            private uint tickVersion;

            public Node(BossBrain brain, BossState state) { b = brain; kind = state; }

            private BossContext C => b.context;
            private BossBlackboard Board => b.context.Board;

            public void Enter()
            {
                b.scheduledFrom = null;
                b.current = kind; b.elapsed = 0f; b.progressDuration = 0f; b.stateVersion++;
                selectTimer = 0f;
                switch (kind)
                {
                    case BossState.Dormant: C.Mover.Stop(); b.pattern = null; break;
                    case BossState.Approach: b.pattern = null; break;
                    case BossState.Telegraph:
                        C.Mover.Stop();
                        if (Mathf.Abs(Board.DeltaX) > .01f) C.Facing = Mathf.Sign(Board.DeltaX);
                        Board.BossFacing = C.Facing;
                        Board.RecordStart(b.pattern);
                        b.activeRun = b.RunFor(b.pattern);
                        b.activeRun.BeginTelegraph();
                        break;
                    case BossState.Execute: b.activeRun.BeginExecute(); break;
                    case BossState.Recover:
                        b.activeRun.End(); b.activeRun = null;
                        b.progressDuration = b.pattern.Recovery;
                        C.Mover.Stop();
                        break;
                    case BossState.Staggered:
                        b.activeRun?.Cancel(); b.activeRun = null; b.pattern = null;
                        C.Mover.Stop();
                        b.progressDuration = b.settings.StaggerDuration;
                        b.hasStaggered = true;
                        break;
                    case BossState.PhaseShift:
                        Board.Phase = b.phasePending;
                        b.progressDuration = b.settings.PhaseShiftDuration;
                        b.pattern = null;
                        C.Mover.Stop();
                        if (b.settings.InvulnerableDuringPhaseShift) C.Health.SetDamageEnabled(false);
                        break;
                    case BossState.Death:
                        b.progressDuration = b.settings.DeathDuration;
                        b.activeRun?.Cancel(); b.activeRun = null;
                        foreach (var run in b.runs.Values) run.OnBossDied();
                        C.Mover.Stop();
                        break;
                }
            }

            public IFsmState Tick(float dt)
            {
                if (kind == BossState.Death)
                {
                    b.elapsed += dt;
                    if (b.elapsed >= b.settings.DeathDuration) C.Owner.gameObject.SetActive(false);
                    return null;
                }
                tickVersion = b.stateVersion;
                var forced = b.PreTick(dt);
                if (forced != null) return Go(forced);
                b.elapsed += dt;
                switch (kind)
                {
                    case BossState.Dormant:
                        C.Mover.Stop();
                        return b.recognized ? Go(b.approach) : null;
                    case BossState.Approach: return TickApproach(dt);
                    case BossState.Telegraph: return b.activeRun.TickTelegraph(dt) ? Go(b.execute) : null;
                    case BossState.Execute: return b.activeRun.TickExecute(dt) ? Go(b.recover) : null;
                    case BossState.Recover: return b.elapsed >= b.pattern.Recovery ? Go(b.AfterAction) : null;
                    case BossState.Staggered:
                        return b.elapsed >= b.settings.StaggerDuration && !C.Mover.IsForcedMoving && C.Mover.IsGrounded ? Go(b.AfterAction) : null;
                    case BossState.PhaseShift: return b.elapsed >= b.settings.PhaseShiftDuration ? Go(b.approach) : null;
                }
                return null;
            }

            private IFsmState TickApproach(float dt)
            {
                if (b.lostTime >= b.settings.LoseTargetTime) return Go(b.dormant);
                // Without a recognized target there is nothing to select against or walk toward.
                if (!b.recognized || !Board.HasTarget) { C.Mover.Stop(); return null; }
                selectTimer -= dt;
                if (selectTimer <= 0f)
                {
                    selectTimer = b.settings.DecisionInterval;
                    b.Select();
                    if (b.pattern != null) return Go(b.telegraph);
                }
                float dx = Board.DeltaX;
                if (Mathf.Abs(dx) > b.settings.PreferredDistance)
                {
                    float side = Mathf.Sign(dx);
                    // DeltaX is measured from the collider center, MoveTo takes the body's x: offset from the body position.
                    C.Mover.MoveTo(C.Mover.Position.x + dx - side * b.settings.PreferredDistance, b.settings.ApproachSpeed);
                    C.Facing = side;
                    Board.BossFacing = side;
                }
                else C.Mover.Stop();
                return null;
            }

            private IFsmState Go(Node next) { b.scheduledFrom = this; b.scheduledVersion = tickVersion; return next; }

            public void Exit()
            {
                if (kind == BossState.PhaseShift) C.Health.SetDamageEnabled(true);
                // Immunity runs from the end of a stagger.
                if (kind == BossState.Staggered) b.lastStaggerAt = Board.Clock;
                if (b.scheduledFrom == this && b.scheduledVersion == b.stateVersion) { b.scheduledFrom = null; return; }
                b.StopCleanup();
            }
        }
    }
}
