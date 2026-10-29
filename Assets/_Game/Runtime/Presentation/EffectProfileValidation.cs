using UnityEngine;
namespace ActionPlatformer.Presentation
{
    internal static class EffectProfileValidation
    {
        public static bool Frames(Sprite[] frames) { if(frames==null)return true; foreach(var f in frames)if(f==null)return false; return true; }
        public static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value>0;
        public static bool Valid(PlayerVfxProfile p) => Frames(p.BasicAttackEffectFrames)&&Frames(p.GlitchEffectFrames)&&Frames(p.JumpDustFrames)&&Frames(p.GroundDustFrames)&&Frames(p.HitImpactFrames)&&Frames(p.FinisherImpactFrames)&&Positive(p.BasicAttackEffectSize)&&Positive(p.GlitchEffectSize)&&Positive(p.GlitchEffectFps)&&Positive(p.GlitchTrailSpacing)&&Positive(p.GlitchTrailInterval);
        public static bool Valid(EnemyVfxProfile p)
        {
            bool empty=(p.StartFrames==null||p.StartFrames.Length==0)&&(p.BodyFrames==null||p.BodyFrames.Length==0)&&(p.EndFrames==null||p.EndFrames.Length==0)&&p.BeamMaterial==null;
            return empty || (p.HasLaser && Frames(p.StartFrames)&&Frames(p.BodyFrames)&&Frames(p.EndFrames));
        }
    }
}
