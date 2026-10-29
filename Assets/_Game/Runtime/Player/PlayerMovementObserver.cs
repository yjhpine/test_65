using UnityEngine;
using ActionPlatformer.Presentation;
namespace ActionPlatformer.Player
{
    // Local observation owns emission decisions; the global manager only plays the requested dust.
    internal sealed class PlayerMovementObserver
    {
        private readonly System.Action<MovementDustKind,Vector2,bool> emit;
        private bool observed,wasGrounded,wasSuppressed;
        private Vector2 previousPoint,previousVelocity;
        private uint previousGlitch;
        private double nextTrail;
        public PlayerMovementObserver(System.Action<MovementDustKind,Vector2,bool> emit) { this.emit=emit; }
        public void Tick(Vector2 feet,Vector2 velocity,bool grounded,bool suppressed,uint glitchVersion,bool slamLanding,double now)
        {
            bool continuous=observed && !suppressed && !wasSuppressed && previousGlitch==glitchVersion && (feet-previousPoint).sqrMagnitude<4f && Time.deltaTime>0;
            if(continuous) {
                if(wasGrounded && !grounded && velocity.y>1)emit(MovementDustKind.Jump,previousPoint,velocity.x<0);
                else if(!wasGrounded && grounded && previousVelocity.y< -1 && !slamLanding)emit(MovementDustKind.Landing,feet,false);
                else if(grounded && wasGrounded && Mathf.Abs(previousVelocity.x)>4 && (Mathf.Abs(velocity.x)<1 || previousVelocity.x*velocity.x<0))emit(MovementDustKind.Brake,feet,previousVelocity.x<0);
                else if(grounded && Mathf.Abs(velocity.x)>5 && now>=nextTrail) { emit(MovementDustKind.Trail,feet-Vector2.right*Mathf.Sign(velocity.x)*.22f,false); nextTrail=now+.18; }
            }
            observed=true;wasGrounded=grounded;wasSuppressed=suppressed;previousPoint=feet;previousVelocity=velocity;previousGlitch=glitchVersion;
        }
        public void Reset() { observed=false; nextTrail=0; }
    }
}
