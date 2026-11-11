namespace ActionPlatformer.Presentation
{
    public readonly struct EffectOwnerHandle
    {
        internal readonly int Manager, Index;
        internal readonly uint Generation;
        internal EffectOwnerHandle(int manager, int index, uint generation) { Manager=manager; Index=index; Generation=generation; }
        public bool IsAssigned => Generation != 0;
    }
}
