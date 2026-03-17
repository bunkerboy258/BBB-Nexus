using Characters.Player.Core;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    // 角色数值驱动器 属于纯粹的被动监听层 
    // 它负责盯着黑板里的当前状态 默默计算体力值的扣除与恢复 
    // 这种数值逻辑必须跟状态机解耦  
    public class CharacterStatusDriver
    {
        // 运行时黑板引用 存储当前运动状态与体力残余 
        private readonly PlayerRuntimeData _data;
        // 离线配置注入 拿到体力上限与恢复速率等关键参数 
        private readonly PlayerSO _config;

        // 构造函数接收数据与配置 绝对不要反向依赖控制器 
        // 这样即使换个项目 只要黑板格式对 逻辑就能直接搬走 
        public CharacterStatusDriver(PlayerRuntimeData data, PlayerSO config)
        {
            _data = data;
            _config = config;
        }

        // 核心函数 
        public void Update()
        {
            UpdateStamina();
            // 注： 以后在这里挂载其他状态的更新 饥饿值之类的
        }

        // 处理黑板中的体力更新 
        private void UpdateStamina()
        {
            // 根据意图管线确定的状态 获取当前的体力变化率 
            float drainRate = GetStaminaDrainRateForState(_data.CurrentLocomotionState);

            if (drainRate > 0)
            {
                // 进入消耗阶段 体力值随时间流逝而减少 
                _data.CurrentStamina -= drainRate * Time.deltaTime;

                if (_data.CurrentStamina <= 0f)
                {
                    // 体力枯竭 触发疲劳标记 限制部分高耗能意图 
                    _data.CurrentStamina = 0f;
                    _data.IsStaminaDepleted = true;
                }
            }
            else if (drainRate < 0)
            {
                // 进入恢复阶段 速率取反实现数值自动增长 
                _data.CurrentStamina += (-drainRate) * Time.deltaTime;

                // 恢复到配置注入的阈值以上 才能解除枯竭标记 
                // 别让玩家刚回一点气就能开冲 (不然会抽的很鬼畜） 
                if (_data.CurrentStamina > _config.Core.MaxStamina * _config.Core.StaminaRecoverThreshold)
                {
                    _data.IsStaminaDepleted = false;
                }
            }

            // 强制限制体力范围 保证黑板数据的绝对安全 
            _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0f, _config.Core.MaxStamina);
        }

        // 状态映射函数 将抽象的运动状态转化为具体的物理变化率 
        // 这里把消耗定义为正数 恢复定义为负数 
        private float GetStaminaDrainRateForState(LocomotionState state)
        {
            return state switch
            {
                // 疾跑状态 按照配置注入的速率进行高额消耗 
                LocomotionState.Sprint => _config.Core.StaminaDrainRate,

                // 走路状态  恢复速率会有额外加成系数 
                LocomotionState.Walk => -_config.Core.StaminaRegenRate * _config.Core.WalkStaminaRegenMult,

                // 慢跑与待机 保持配置里的标准恢复节奏 
                LocomotionState.Jog => -_config.Core.StaminaRegenRate,
                LocomotionState.Idle => -_config.Core.StaminaRegenRate,

                // 逻辑死区状态 体力值保持静止不动 
                _ => 0f
            };
        }
    }
}