namespace ActionPlatformer.Units.Fsm
{
    public interface IFsmState
    {
        void Enter();
        // Return null or this to remain in the state; return another state to transition.
        IFsmState Tick(float deltaTime);
        void Exit();
    }
}
