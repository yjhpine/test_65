#if UNITY_EDITOR
using ActionPlatformer.Units;
using ActionPlatformer.Units.Fsm;
using ActionPlatformer.Units.Features;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ActionPlatformer.Tests
{
    public sealed class EnemyPatternDefinitionTests
    {
        [TestCase("ShieldSoldier",75,1.6f,true)]
        [TestCase("SurveillanceTurret",90,1.2f,false)]
        [TestCase("FlyingDrone",60,.8f,true)]
        public void PrefabsHaveValidDefinitionsAndAnimationBindings(string name,int hp,float height,bool movable)
        {
            var go=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/"+name+".prefab");
            Assert.That(go,Is.Not.Null);Assert.That(go.transform.localScale,Is.EqualTo(Vector3.one));Assert.That(go.layer,Is.EqualTo(2));
            var definition=go.GetComponent<Unit>().Definition;
            Assert.That(definition.TryValidate(out var error),Is.True,error);Assert.That(definition.MaxHealth,Is.EqualTo(hp));Assert.That(definition.AllowForcedMovement,Is.EqualTo(movable));Assert.That(definition.AllowGlitchTarget,Is.True);
            Assert.That(((EnemyFsmDefinition)definition.Fsm).TryValidate(out error),Is.True,error);
            Assert.That(go.GetComponent<BoxCollider2D>().size,Is.EqualTo(new Vector2(1,height)));
            Assert.That(go.GetComponent<EnemyCombat2D>(),Is.Not.Null);Assert.That(go.GetComponentInChildren<EnemyVisual2D>(),Is.Not.Null);
            var animator=go.GetComponentInChildren<Animator>();Assert.That(animator.applyRootMotion,Is.False);Assert.That(animator.runtimeAnimatorController.animationClips.Length,Is.EqualTo(4));
            foreach(var clip in animator.runtimeAnimatorController.animationClips)Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip)[0].propertyName,Is.EqualTo("m_Sprite"));
        }
        [Test] public void InvalidTimingAndNonFiniteSettingsAreRejected()
        {
            var s=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyFsmDefinition>("Assets/_Game/Data/Fsm/FlyingDroneFsm.asset"));
            try
            {
                var data=new SerializedObject(s);data.FindProperty("lockDuration").floatValue=2;data.ApplyModifiedPropertiesWithoutUndo();Assert.That(s.TryValidate(out _),Is.False);
                data.FindProperty("lockDuration").floatValue=.4f;data.FindProperty("dashSpeed").floatValue=float.NaN;data.ApplyModifiedPropertiesWithoutUndo();Assert.That(s.TryValidate(out _),Is.False);
            }
            finally{Object.DestroyImmediate(s);}
        }
    }
}
#endif
