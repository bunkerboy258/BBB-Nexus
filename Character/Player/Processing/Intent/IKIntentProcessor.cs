using UnityEngine;
using Characters.Player.Data;
using Items.Data; // 引用物品数据

namespace Characters.Player.Processing
{
    /// <summary>
    /// IK 意图处理器。
    /// 职责：
    /// 根据当前装备的物品类型、握持方式以及玩家的状态（如是否在翻越、是否在瞄准），
    /// 动态计算出 IK 系统应该做什么（开启/关闭，目标在哪）。
    /// </summary>
    public class IKIntentProcessor
    {
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        public IKIntentProcessor(PlayerController player)
        {
            _data = player.RuntimeData;
            _config = player.Config;
        }

        public void Update()
        {
            // --- 1. 获取上下文 ---
            var equip = _data.CurrentEquipment;
            bool hasItem = equip.HasItem;
            bool isVaulting = _data.IsVaulting;
            bool isAiming = _data.IsAiming;

            // --- 2. 默认重置意图 ---
            _data.WantsLeftHandIK = false;
            _data.WantsRightHandIK = false;
            _data.WantsLookAtIK = false;
            _data.LeftHandGoal = null;
            _data.RightHandGoal = null;

            // --- 3. 优先级判断逻辑 ---

            // A. 如果正在翻越，强制禁用所有 IK (最高优先级)
            if (isVaulting)
            {
                return;
            }

            // B. 如果手里有东西
            if (hasItem)
            {
                // 设置目标点 (始终有效，哪怕权重为0)
                _data.LeftHandGoal = equip.Instance.LeftHandGrip;
                _data.RightHandGoal = equip.Instance.RightHandGrip;

                // 简单规定：只要是双手握持物品，或者正在瞄准，就开左手 IK
                bool shouldActiveLeftHand = isAiming || (equip.Definition is EquippableItemSO equipDef&& equipDef.HoldType==ItemHoldType.TwoHanded);

                if (shouldActiveLeftHand)
                {
                    _data.WantsLeftHandIK = true;
                }

                // 右手 IK 通常由动画控制 (RightHandGoal 设为 null 或者 Weight 设为 0)
                // 除非是特殊重武器。这里我们暂且只控制左手。
            }

            // C. 身体注视 (Look At)
            // 规则：只要在瞄准，就开启注视
            if (isAiming)
            {
                _data.WantsLookAtIK = true;
                _data.LookAtPosition = _data.TargetAimPoint;
            }
            else
            {
                _data.WantsLookAtIK=false;
            }
        }
    }
}
