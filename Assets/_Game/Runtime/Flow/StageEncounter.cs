using System;
using ActionPlatformer.Units.Features;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    [Serializable]
    public sealed class StageEncounter
    {
        [SerializeField] private GameObject enemyRoot;
        [SerializeField] private UnitHealth[] enemies;
        [SerializeField] private float startX, endX;
        [SerializeField] private StageGate exitGate;
        [SerializeField] private StageRestArea followingRest;
        public GameObject EnemyRoot => enemyRoot;
        public UnitHealth[] Enemies => enemies;
        public float StartX => startX;
        public float EndX => endX;
        public StageGate ExitGate => exitGate;
        public StageRestArea FollowingRest => followingRest;
    }
}
