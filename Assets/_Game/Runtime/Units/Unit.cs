using ActionPlatformer.Units.Fsm;
using UnityEngine;

namespace ActionPlatformer.Units
{
    [DisallowMultipleComponent]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private UnitDefinition definition;
        private bool initialized;
        private bool started;

        public UnitDefinition Definition => definition;
        public UnitKind Kind => definition == null ? UnitKind.Unspecified : definition.Kind;
        public FsmRuntime Fsm { get; private set; }

        private void Awake()
        {
            if (definition == null)
            {
                Debug.LogError("Unit definition is missing.", this);
                enabled = false;
                return;
            }
            if (!definition.TryValidate(out string error))
            {
                Debug.LogError("Invalid unit definition: " + error, this);
                enabled = false;
                return;
            }
            OnUnitAwake();
            initialized = true;
        }

        private void Start()
        {
            started = true;
            StartFsm();
        }

        private void OnEnable()
        {
            if (!initialized) { enabled = false; return; }
            if (started) StartFsm();
        }

        private void StartFsm()
        {
            if (!initialized || !isActiveAndEnabled) return;
            if (Fsm == null && definition.Fsm != null)
                Fsm = definition.Fsm.CreateRuntime(this);
            Fsm?.Start();
        }

        private void Update()
        {
            Fsm?.Tick(Time.deltaTime);
            if (isActiveAndEnabled) OnUnitUpdate();
        }

        private void OnDisable()
        {
            try { Fsm?.Stop(); }
            finally { OnUnitDisabled(); }
        }

        // Override these hooks instead of hiding the Unit lifecycle messages.
        protected virtual void OnUnitAwake() { }
        protected virtual void OnUnitUpdate() { }
        protected virtual void OnUnitDisabled() { }
    }
}
