using ActionPlatformer.Units;
using ActionPlatformer.Units.Fsm;

namespace ActionPlatformer.Tests
{
    public sealed class TestFsmDefinition : FsmDefinition
    {
        protected override IFsmState CreateInitialState(Unit owner) => new ProbeState(owner);

        public sealed class ProbeState : IFsmState
        {
            public readonly Unit Owner;
            public int Enters, Exits;
            public float Elapsed;
            public IFsmState Next;

            public ProbeState(Unit owner) { Owner = owner; }
            public void Enter() { Enters++; Elapsed = 0f; Next = null; }
            public IFsmState Tick(float deltaTime) { Elapsed += deltaTime; return Next; }
            public void Exit() { Exits++; }
        }
    }
}
