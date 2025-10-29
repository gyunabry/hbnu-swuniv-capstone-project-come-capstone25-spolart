using UnityEngine;

namespace Game.Foundation.Items
{
    // 공격 시 전달되는 컨텍스트
    public struct AttackContent
    {
        public GameObject Attacker;
        public GameObject Target;
        public float BaseDamage;
        public bool IsCritical;
        public Vector2 HitPoint;
        public Vector2 HitNormal;
    }

    public struct MineContext
    {
        public GameObject Miner;
        public GameObject MineableObject;
        public float BaseMiningDamage;
        public Vector2 HitPoint;
    }
}
