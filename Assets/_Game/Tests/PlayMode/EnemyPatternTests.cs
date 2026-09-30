#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ActionPlatformer.Units;
using ActionPlatformer.Units.Features;
using ActionPlatformer.Units.Fsm;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionPlatformer.Tests
{
    public sealed class EnemyRepositionProbe : MonoBehaviour, IUnitRepositionState
    { public uint RepositionVersion { get; set; } }

    public sealed class EnemyPatternTests
    {
        readonly List<Object> created = new List<Object>();
        Unit enemy; UnitHealth target; EnemyFsmDefinition settings;
        EnemyCombat2D Combat => enemy.GetComponent<EnemyCombat2D>();
        EnemyPhase Phase => Combat.State.Phase;
        GroundMovement2D Ground => enemy.GetComponent<GroundMovement2D>();
        FlyingMovement2D Flying => enemy.GetComponent<FlyingMovement2D>();
        UnitHealth Health => enemy.GetComponent<UnitHealth>();
        static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();
        static void Set(Object obj, string name, object value)
        {
            var so = new SerializedObject(obj); var p=so.FindProperty(name);
            if(value is Object o)p.objectReferenceValue=o;else if(value is float f)p.floatValue=f;else if(value is int i)p.intValue=i;else if(value is bool b)p.boolValue=b;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject Block(Vector2 position, Vector2 size, bool oneWay=false)
        {
            var go=new GameObject("Enemy test terrain");created.Add(go);go.transform.position=position;
            var collider=go.AddComponent<BoxCollider2D>();collider.size=size;
            if(oneWay){go.AddComponent<PlatformEffector2D>();collider.usedByEffector=true;}
            Physics2D.SyncTransforms();return go;
        }
        IEnumerator Spawn(string name, Vector2 targetOffset)
        {
            Block(new Vector2(0,50),new Vector2(60,1));
            settings=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyFsmDefinition>("Assets/_Game/Data/Fsm/"+name+"Fsm.asset"));created.Add(settings);
            var def=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Game/Data/Units/"+name+"Definition.asset"));created.Add(def);Set(def,"fsm",settings);
            var parent=new GameObject("Enemy test setup");parent.SetActive(false);created.Add(parent);
            float y=name=="FlyingDrone"?54:name=="ShieldSoldier"?51.31f:51.11f;
            var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/"+name+".prefab"),new Vector3(0,y),Quaternion.identity,parent.transform);
            enemy=instance.GetComponent<Unit>();Set(enemy,"definition",def);
            var playerDefinition=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Game/Data/Units/PlayerDefinition.asset"));created.Add(playerDefinition);Set(playerDefinition,"maxHealth",1000);
            var player=new GameObject("Enemy pattern target");player.SetActive(false);created.Add(player);player.layer=2;player.transform.position=(Vector2)instance.transform.position+targetOffset;
            Set(player.AddComponent<Unit>(),"definition",playerDefinition);player.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
            player.AddComponent<BoxCollider2D>().size=new Vector2(.6f,1f);target=player.AddComponent<UnitHealth>();Set(target,"deactivateOnDeath",false);player.AddComponent<EnemyRepositionProbe>();
            player.SetActive(true);parent.SetActive(true);yield return null;yield return Step;yield return null;
            enemy.Fsm.Stop();enemy.Fsm.Start();Physics2D.SyncTransforms();
        }
        void Tick(float dt=.01f)=>enemy.Fsm.Tick(dt);
        void MoveTarget(Vector2 p){target.GetComponent<Rigidbody2D>().position=p;target.transform.position=p;Physics2D.SyncTransforms();}
        [TearDown] public void Cleanup(){for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)Object.DestroyImmediate(created[i]);created.Clear();}

        [UnityTest] public IEnumerator ShieldBlocksFrontOnlyAndBypassDoesNotBlock()
        {
            yield return Spawn("ShieldSoldier",Vector2.left);
            Vector2 p=Combat.Center;uint version=Health.DamageVersion;
            Assert.That(Health.ApplyDamage(10,p+Vector2.left),Is.Zero);Assert.That(Combat.BlockVersion,Is.EqualTo(1));Assert.That(Health.DamageVersion,Is.EqualTo(version));
            Assert.That(Ground.IsForcedMoving,Is.False);
            Assert.That(Health.ApplyDamage(10,p+Vector2.right),Is.EqualTo(10));
            Assert.That(Health.ApplyDamage(10,p+Vector2.left+Vector2.up),Is.EqualTo(10));
            Assert.That(Health.ApplyDamage(10,p+Vector2.left+Vector2.down),Is.EqualTo(10));
            Assert.That(Health.ApplyDamage(10,p+Vector2.left,true),Is.EqualTo(10));
            Assert.That(Health.CurrentHealth,Is.EqualTo(35));
        }
        [UnityTest] public IEnumerator ShieldTurnsSlowlyAndLocksDirectionDuringPreparation()
        {
            yield return Spawn("ShieldSoldier",Vector2.right);
            Tick();Assert.That(Phase,Is.EqualTo(EnemyPhase.Turning));Tick(.54f);Assert.That(Combat.Facing,Is.EqualTo(-1));
            Tick(.02f);Assert.That(Combat.Facing,Is.EqualTo(1));Tick();Assert.That(Phase,Is.EqualTo(EnemyPhase.Preparing));
            MoveTarget(Combat.Center+Vector2.left);Tick(.5f);Health.ApplyDamage(1);Tick(.1f);
            Assert.That(Phase,Is.EqualTo(EnemyPhase.Preparing));Assert.That(Combat.State.Elapsed,Is.EqualTo(.6f).Within(.001));Assert.That(Combat.Facing,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ShieldDashSweepsCrossedPlayerOnceAndStopsAtWall()
        {
            yield return Spawn("ShieldSoldier",Vector2.left*1.5f);
            Tick();Tick(1.21f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Dash));
            Vector2 old=Combat.Center;int hp=target.CurrentHealth;
            enemy.GetComponent<Rigidbody2D>().position+=Vector2.left*2;Physics2D.SyncTransforms();
            Combat.HitDashPath(old);Combat.HitDashPath(old);Assert.That(target.CurrentHealth,Is.EqualTo(hp-14));
            Block(Combat.Center+Vector2.left*.8f,new Vector2(.3f,3));
            yield return Step;yield return null;Tick(.3f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Recovery));
            Assert.That(enemy.transform.position.x,Is.GreaterThan(-2.6f));
        }
        [UnityTest] public IEnumerator ShieldDashStopsBeforeAnUnsupportedLedge()
        {
            yield return Spawn("ShieldSoldier", Vector2.left * 1.5f);
            var floor = created[0] as GameObject;
            floor.transform.position = new Vector2(1f, 50f);
            floor.GetComponent<BoxCollider2D>().size = new Vector2(4f, 1f);
            Physics2D.SyncTransforms(); Tick(); Tick(1.21f);
            Assert.That(Phase, Is.EqualTo(EnemyPhase.Dash));
            yield return new WaitForSeconds(.25f);
            Assert.That(Phase, Is.EqualTo(EnemyPhase.Recovery));
            Assert.That(enemy.transform.position.x, Is.GreaterThan(-.5f));
        }
        [UnityTest] public IEnumerator BeamWidthMissAndTargetLossDuringScanCancelDamage()
        {
            yield return Spawn("SurveillanceTurret", Vector2.left * 3);
            MoveTarget(Combat.Center + new Vector2(-3, .7f));
            Combat.Fire(Vector2.left, false); Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            Tick(); Assert.That(Phase, Is.EqualTo(EnemyPhase.Preparing));
            Block(Combat.Center + new Vector2(-1.5f, .35f), new Vector2(.2f, 3));
            Tick(); Assert.That(Phase, Is.EqualTo(EnemyPhase.Idle));
            Assert.That(Combat.ShotVersion, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ShieldLaunchDoesNotResetPreparationAndCannotDashInAir()
        {
            yield return Spawn("ShieldSoldier",Vector2.left);
            Tick();Tick(.6f);Ground.ApplyForcedMovement(Vector2.up*10,.35f);Health.ApplyDamage(1);
            Tick(.3f);Assert.That(Combat.State.Elapsed,Is.EqualTo(.9f).Within(.001));
            Tick(.31f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Recovery));Assert.That(Ground.IsForcedMoving,Is.True);
        }
        [UnityTest] public IEnumerator SurveillanceResetsOnlyForSuccessfulRepositionAndCancelsLostTarget()
        {
            yield return Spawn("SurveillanceTurret",Vector2.left*3);
            Tick();Tick(.6f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Preparing));
            Tick(.1f);Assert.That(Combat.State.Elapsed,Is.EqualTo(.7f).Within(.001));
            target.GetComponent<EnemyRepositionProbe>().RepositionVersion++;Tick(.1f);Assert.That(Combat.State.Elapsed,Is.Zero);
            target.SetDamageEnabled(false);Tick();Assert.That(Phase,Is.EqualTo(EnemyPhase.Idle));
            Assert.That(enemy.Definition.AllowForcedMovement,Is.False);Assert.That(enemy.GetComponent<IForcedMovementReceiver>(),Is.Null);
            Assert.That(enemy.GetComponent<Rigidbody2D>().bodyType,Is.EqualTo(RigidbodyType2D.Kinematic));
        }
        [UnityTest] public IEnumerator SurveillanceLockedShotFiresAtOriginalAimAndOnlyDamagesOnce()
        {
            yield return Spawn("SurveillanceTurret",Vector2.left*3);
            Tick();Tick(1.01f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Locked));Vector2 aim=Combat.State.Aim;
            MoveTarget(Combat.Center+Vector2.left*3+Vector2.up*2);Health.ApplyDamage(5);Tick(.41f);
            Assert.That(Combat.ShotVersion,Is.EqualTo(1));Assert.That(target.CurrentHealth,Is.EqualTo(1000));Assert.That(Combat.State.Aim,Is.EqualTo(aim));
            MoveTarget(Combat.Center+Vector2.left*3);Tick(.2f);Assert.That(target.CurrentHealth,Is.EqualTo(1000));
            Tick(1.31f);Tick();Tick(1.01f);Tick(.41f);Assert.That(target.CurrentHealth,Is.EqualTo(990));Tick(.3f);Assert.That(target.CurrentHealth,Is.EqualTo(990));
        }
        [UnityTest] public IEnumerator RayGeometryMatchesDamageAndStopsAtTerrainButNotOneWayPlatforms()
        {
            yield return Spawn("SurveillanceTurret",Vector2.left*3);
            var wall=Block(Combat.Center+Vector2.left*1.5f,new Vector2(.2f,3));
            var ray=Combat.CalculateBeam(Vector2.left,false);Assert.That(ray.End.x,Is.EqualTo(-1.4f).Within(.01));
            Combat.Fire(Vector2.left,false);Assert.That(target.CurrentHealth,Is.EqualTo(1000));
            wall.AddComponent<PlatformEffector2D>();wall.GetComponent<BoxCollider2D>().usedByEffector=true;Physics2D.SyncTransforms();
            ray=Combat.CalculateBeam(Vector2.left,false);Assert.That(ray.End.x,Is.EqualTo(-22).Within(.01));
            Combat.Fire(Vector2.left,false);Assert.That(target.CurrentHealth,Is.EqualTo(990));
            Assert.That(Combat.LastShot.Start,Is.EqualTo(ray.Start));Assert.That(Combat.LastShot.End,Is.EqualTo(ray.End));
            target.SetDamageEnabled(false);Combat.Fire(Vector2.left,false);Assert.That(target.CurrentHealth,Is.EqualTo(990));
        }
        [UnityTest] public IEnumerator DroneSelectsVerticalNearbyAndAimedAtDistance()
        {
            yield return Spawn("FlyingDrone",new Vector2(1,-2));Tick();Assert.That(Combat.State.Vertical,Is.True);
            Tick(.81f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Locked));Tick(.41f);Tick(1.21f);
            MoveTarget(Combat.Center+Vector2.left*3);Tick();Assert.That(Combat.State.Vertical,Is.False);
            Tick(.36f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Locked));
        }
        [UnityTest] public IEnumerator DroneLockedDirectionAndAttackClockSurviveKnockback()
        {
            yield return Spawn("FlyingDrone",Vector2.left*3);Tick();Tick(.81f);Vector2 aim=Combat.State.Aim;
            Flying.ApplyForcedMovement(new Vector2(4,10),.35f);Health.ApplyDamage(2);
            yield return Step;yield return Step;Vector2 origin=Combat.Center;
            Assert.That(origin.y,Is.GreaterThan(54));Assert.That(Flying.IsForcedMoving,Is.True);
            MoveTarget(Combat.Center+Vector2.right*3);Tick(.41f);
            Assert.That(Phase,Is.EqualTo(EnemyPhase.Recovery));Assert.That(Combat.State.Aim,Is.EqualTo(aim));
            Assert.That(Vector2.Distance(Combat.LastShot.Start,origin),Is.LessThan(.001));Assert.That(Combat.LastShot.End.x,Is.LessThan(origin.x));
        }
        [UnityTest] public IEnumerator DroneReturnsSmoothlyToCruiseAfterForceEnds()
        {
            yield return Spawn("FlyingDrone",Vector2.left*20);
            Flying.ApplyForcedMovement(new Vector2(8,10),.1f);yield return new WaitForSeconds(.14f);
            Assert.That(Flying.IsForcedMoving,Is.False);Vector2 previous=Flying.Position;
            yield return Step;Assert.That(Vector2.Distance(previous,Flying.Position),Is.LessThan(.1f));
            Assert.That(Mathf.Abs(Flying.Velocity.x),Is.LessThanOrEqualTo(1.6f));Assert.That(Mathf.Abs(Flying.Velocity.y),Is.LessThanOrEqualTo(2f));
        }
        [UnityTest] public IEnumerator VerticalLightningDamagesAboveAndBelowButGroundBlocks()
        {
            yield return Spawn("FlyingDrone",new Vector2(0,-2));Combat.Fire(Vector2.down,true);Assert.That(target.CurrentHealth,Is.EqualTo(990));
            MoveTarget(Combat.Center+Vector2.up*2);Combat.Fire(Vector2.down,true);Assert.That(target.CurrentHealth,Is.EqualTo(980));
            MoveTarget(new Vector2(0,49));Combat.Fire(Vector2.down,true);Assert.That(target.CurrentHealth,Is.EqualTo(980));
        }
        [UnityTest] public IEnumerator DeathCancelsAllPendingAttacksAndDisableClearsVisuals()
        {
            yield return Spawn("SurveillanceTurret",Vector2.left*3);Tick();Tick(1.01f);Health.ApplyDamage(1000);Tick();
            Assert.That(Phase,Is.EqualTo(EnemyPhase.Death));Assert.That(Combat.Target,Is.Null);Tick(.5f);Assert.That(Combat.ShotVersion,Is.Zero);
            yield return null; yield return null;
            var animator=enemy.GetComponentInChildren<Animator>();Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),Is.True);
            Tick(.11f);Assert.That(enemy.gameObject.activeSelf,Is.False);
            foreach(var r in enemy.GetComponentsInChildren<SpriteRenderer>(true))if(r.gameObject.name!="Visual")Assert.That(r.enabled,Is.False);
        }
        [UnityTest] public IEnumerator DisableDuringLockedAttackResetsTimersAndNoDelayedShot()
        {
            yield return Spawn("FlyingDrone",Vector2.left*3);Tick();Tick(.81f);Flying.ApplyForcedMovement(Vector2.up*10,.35f);
            enemy.gameObject.SetActive(false);Assert.That(Flying.IsForcedMoving,Is.False);Assert.That(Combat.Target,Is.Null);
            enemy.gameObject.SetActive(true);Tick();Assert.That(Phase,Is.EqualTo(EnemyPhase.Preparing));Tick(.76f);Assert.That(Phase,Is.EqualTo(EnemyPhase.Preparing));Assert.That(Combat.ShotVersion,Is.Zero);
        }
    }
}
#endif
