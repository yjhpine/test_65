using System;

namespace ActionPlatformer.Units.Fsm
{
    public sealed class FsmRuntime
    {
        private readonly IFsmState initialState;
        private int generation;
        private bool ticking;

        public IFsmState CurrentState { get; private set; }
        public bool IsRunning { get; private set; }

        public FsmRuntime(IFsmState initialState)
        {
            this.initialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
        }

        public void Start()
        {
            if (IsRunning) return;
            generation++;
            IsRunning = true;
            CurrentState = initialState;
            try { initialState.Enter(); }
            catch { Stop(); throw; }
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsRunning) return;
            if (ticking) throw new InvalidOperationException("An FSM cannot tick recursively.");

            ticking = true;
            try
            {
                int activeGeneration = generation;
                IFsmState next = CurrentState.Tick(deltaTime);
                if (activeGeneration != generation || !IsRunning || next == null || ReferenceEquals(next, CurrentState))
                    return;

                // Clear first: disabling the unit from Exit must not exit the same state twice.
                IFsmState previous = CurrentState;
                CurrentState = null;
                previous.Exit();
                if (activeGeneration != generation || !IsRunning) return;
                CurrentState = next;
                next.Enter();
            }
            catch { Stop(); throw; }
            finally { ticking = false; }
        }

        public void Stop()
        {
            if (!IsRunning) return;
            generation++;
            IsRunning = false;
            IFsmState previous = CurrentState;
            CurrentState = null;
            previous?.Exit();
        }
    }
}
