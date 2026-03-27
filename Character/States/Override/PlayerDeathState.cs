using System;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public sealed class PlayerDeathState : PlayerBaseState
    {
        private float _deathTimeAt;
        private const float RESPAWN_DELAY = 3f;

        public PlayerDeathState(BBBCharacterController player) : base(player) { }

        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            data.SfxQueue.Enqueue(PlayerSfxEvent.Death);

            data.IsDead = true;
            data.Arbitration.IsDead = true;
            data.Arbitration.BlockInput = true;
            data.Arbitration.BlockUpperBody = true;
            data.Arbitration.BlockFacial = true;
            data.Arbitration.BlockIK = true;
            data.Arbitration.BlockInventory = true;

            var clip = config.Core.DeathAnim;
            if (clip != null)
                AnimFacade.PlayFullBodyAction(clip, 0.05f);

            // 记录死亡时间 （用于压测场景）
            _deathTimeAt = Time.time + RESPAWN_DELAY;
        }

        protected override void UpdateStateLogic()
        {
            // 死亡 3 秒后 触发对象池回收(暂时硬编码)
            if (Time.time >= _deathTimeAt)
            {
                TryRespawnViaPool();
            }
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateGravityOnly();
        }

        public override void Exit()
        {
            AnimFacade.StopFullBodyAction();
        }

        /// <summary>
        /// 尝试通过对象池回收该角色实例（用于压力测试） 
        /// 如果不使用池则退避，由外部手动销毁。
        /// </summary>
        private void TryRespawnViaPool()
        {
            if (SimpleObjectPoolSystem.Shared != null)
            {
                SimpleObjectPoolSystem.Shared.Despawn(player.gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(player.gameObject);
            }
        }
    }
}
