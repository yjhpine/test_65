using UnityEngine;

namespace ActionPlatformer.Units.Fsm
{
    public abstract class FsmDefinition : ScriptableObject
    {
        public FsmRuntime CreateRuntime(Unit owner)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            return new FsmRuntime(CreateInitialState(owner));
        }

        // Create a fresh state graph per call. Shared assets hold configuration, never runtime state.
        protected abstract IFsmState CreateInitialState(Unit owner);
    }
}
