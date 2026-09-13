using System;
using System.Collections.Generic;
using ActionPlatformer.Units.Fsm;
using NUnit.Framework;

namespace ActionPlatformer.Tests
{
    public sealed class FsmRuntimeTests
    {
        private sealed class State : IFsmState
        {
            public Action OnEnter;
            public Action OnExit;
            public Func<float, IFsmState> OnTick;
            public int Enters, Ticks, Exits;
            public void Enter() { Enters++; OnEnter?.Invoke(); }
            public IFsmState Tick(float deltaTime) { Ticks++; return OnTick?.Invoke(deltaTime); }
            public void Exit() { Exits++; OnExit?.Invoke(); }
        }

        [Test] public void StartAndStopAreIdempotentAndStoppedFsmDoesNotTick()
        {
            var state = new State();
            var fsm = new FsmRuntime(state);
            fsm.Tick(0.02f);
            Assert.That(state.Ticks, Is.Zero);
            fsm.Start(); fsm.Start();
            Assert.That(state.Enters, Is.EqualTo(1));
            fsm.Stop(); fsm.Stop(); fsm.Tick(0.02f);
            Assert.That(state.Exits, Is.EqualTo(1));
            Assert.That(state.Ticks, Is.Zero);
            Assert.That(fsm.CurrentState, Is.Null);
            Assert.That(fsm.IsRunning, Is.False);
        }

        [Test] public void TransitionExitsBeforeEnteringAndTicksNextStateOnFollowingFrame()
        {
            var calls = new List<string>();
            var second = new State
            {
                OnEnter = () => calls.Add("second enter"),
                OnTick = dt => { calls.Add("second tick"); return null; }
            };
            var first = new State
            {
                OnTick = dt => { calls.Add("first tick"); Assert.That(dt, Is.EqualTo(0.25f)); return second; },
                OnExit = () => calls.Add("first exit")
            };
            var fsm = new FsmRuntime(first);
            fsm.Start(); fsm.Tick(0.25f);
            CollectionAssert.AreEqual(new[] { "first tick", "first exit", "second enter" }, calls);
            Assert.That(fsm.CurrentState, Is.SameAs(second));
            fsm.Tick(0.25f);
            Assert.That(second.Ticks, Is.EqualTo(1));
        }

        [Test] public void NullAndSelfTransitionsKeepTheCurrentState()
        {
            var state = new State();
            var fsm = new FsmRuntime(state);
            fsm.Start(); fsm.Tick(0.02f);
            state.OnTick = dt => state;
            fsm.Tick(0.02f);
            Assert.That(fsm.CurrentState, Is.SameAs(state));
            Assert.That(state.Enters, Is.EqualTo(1));
            Assert.That(state.Exits, Is.Zero);
        }

        [Test] public void RestartReturnsToInitialStateAndLetsEnterResetItsTimer()
        {
            float elapsed = 0f;
            var second = new State();
            var first = new State { OnEnter = () => elapsed = 0f, OnTick = dt => { elapsed += dt; return second; } };
            var fsm = new FsmRuntime(first);
            fsm.Start(); fsm.Tick(0.5f); fsm.Stop(); fsm.Start();
            Assert.That(fsm.CurrentState, Is.SameAs(first));
            Assert.That(first.Enters, Is.EqualTo(2));
            Assert.That(elapsed, Is.Zero);
            Assert.That(second.Exits, Is.EqualTo(1));
        }

        [Test] public void StopDuringTickDiscardsTheReturnedTransition()
        {
            var first = new State();
            var next = new State();
            var fsm = new FsmRuntime(first);
            first.OnTick = dt => { fsm.Stop(); return next; };
            fsm.Start(); fsm.Tick(0.1f);
            Assert.That(fsm.IsRunning, Is.False);
            Assert.That(first.Exits, Is.EqualTo(1));
            Assert.That(next.Enters, Is.Zero);
        }

        [Test] public void StopDuringExitDoesNotExitTwiceOrEnterNextState()
        {
            var next = new State();
            var first = new State { OnTick = dt => next };
            var fsm = new FsmRuntime(first);
            first.OnExit = () => fsm.Stop();
            fsm.Start(); fsm.Tick(0.1f);
            Assert.That(first.Exits, Is.EqualTo(1));
            Assert.That(next.Enters, Is.Zero);
            Assert.That(fsm.IsRunning, Is.False);
        }

        [Test] public void CallbackFailureStopsTheFsmInsteadOfRepeatingEveryFrame()
        {
            var state = new State { OnTick = dt => throw new InvalidOperationException("state failure") };
            var fsm = new FsmRuntime(state);
            fsm.Start();
            Assert.Throws<InvalidOperationException>(() => fsm.Tick(0.02f));
            Assert.That(fsm.IsRunning, Is.False);
            Assert.That(state.Exits, Is.EqualTo(1));
            Assert.DoesNotThrow(() => fsm.Tick(0.02f));
        }

        [Test] public void FailedTransitionExitLeavesNoRunningState()
        {
            var next = new State();
            var state = new State { OnTick = dt => next, OnExit = () => throw new InvalidOperationException() };
            var fsm = new FsmRuntime(state);
            fsm.Start();
            Assert.Throws<InvalidOperationException>(() => fsm.Tick(0.02f));
            Assert.That(fsm.IsRunning, Is.False);
            Assert.That(fsm.CurrentState, Is.Null);
            Assert.That(next.Enters, Is.Zero);
        }

        [Test] public void InvalidConstructionAndDeltaTimeAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new FsmRuntime(null));
            var fsm = new FsmRuntime(new State());
            Assert.Throws<ArgumentOutOfRangeException>(() => fsm.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => fsm.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => fsm.Tick(float.PositiveInfinity));
        }
    }
}
