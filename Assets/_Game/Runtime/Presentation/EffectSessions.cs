using System.Collections.Generic;
using UnityEngine;
namespace ActionPlatformer.Presentation
{
    internal sealed class EffectSessions<T> where T : class
    {
        internal sealed class Slot { public Transform Owner; public int Scene; public uint Generation; public T Value; }
        internal readonly List<Slot> Slots = new List<Slot>();
        private readonly int manager;
        public EffectSessions(int manager) { this.manager=manager; }
        public EffectOwnerHandle Add(Transform owner, T value)
        {
            int i=Slots.FindIndex(s=>s.Value==null);
            if(i<0) { i=Slots.Count; Slots.Add(new Slot()); }
            var s=Slots[i]; s.Generation++; if(s.Generation==0)s.Generation++;
            s.Owner=owner; s.Scene=owner.gameObject.scene.handle; s.Value=value;
            return new EffectOwnerHandle(manager,i,s.Generation);
        }
        public Slot Get(EffectOwnerHandle h) => h.Manager==manager && h.Generation!=0 && h.Index>=0 && h.Index<Slots.Count && Slots[h.Index].Generation==h.Generation && Slots[h.Index].Value!=null ? Slots[h.Index] : null;
        public void Clear(Slot s) { s.Owner=null; s.Value=null; }
    }
}
