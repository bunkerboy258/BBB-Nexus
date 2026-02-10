using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;

namespace Characters.Player.Core
{
    /// <summary>
    /// 耐力控制器
    /// 职责：管理玩家耐力的消耗/恢复逻辑，根据耐力状态和输入意图判定是否允许奔跑，驱动奔跑相关状态标记更新。
    /// </summary>
    public class StaminaController
    {
        private PlayerInputReader _input;       // 输入读取器：获取冲刺（奔跑）输入状态
        private PlayerRuntimeData _data;        // 运行时数据：读写耐力值、奔跑状态标记
        private PlayerSO _config;               // 配置文件：读取耐力消耗/恢复速率、阈值等参数
        private bool _isStaminaDepleted = false;// 耐力耗尽标记：用于限制耐力耗尽后立即恢复奔跑

        /// <summary>
        /// 构造函数：注入玩家核心依赖（输入、运行时数据、配置）
        /// </summary>
        /// <param name="player">玩家核心控制器</param>
        public StaminaController(PlayerController player)
        {
            _input = player.InputReader;
            _data = player.RuntimeData;
            _config = player.Config;
        }

        /// <summary>
        /// 每帧更新：判定奔跑意图，执行耐力消耗/恢复逻辑，更新奔跑状态标记
        /// </summary>
        public void Update()
        {
            // 判定是否有有效移动输入（避免浮点精度问题，sqrMagnitude > 0.01f 替代 magnitude > 0）
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            // 1. 初始判定：玩家是否“想要奔跑”（按下冲刺键 + 有移动输入）
            bool wantsToRun = _input.IsSprinting && isMoving;

            // 2. 耐力耗尽限制：耐力耗尽后需恢复至最大值的20%才能重新奔跑
            if (_isStaminaDepleted)
            {
                // 耐力恢复至20%阈值以上 → 解除耗尽限制
                if (_data.CurrentStamina > _config.MaxStamina * 0.2f)
                    _isStaminaDepleted = false;
                else
                    wantsToRun = false; // 强制禁止奔跑（即使按下冲刺键也无效）
            }

            // 3. 执行奔跑/恢复逻辑
            if (wantsToRun && _data.CurrentStamina > 0)
            {
                // 标记为“正在奔跑”“想要奔跑”
                _data.IsRunning = true;
                _data.WantToRun = true;
                // 按消耗速率扣除耐力（乘以Time.deltaTime保证帧率无关）
                _data.CurrentStamina -= _config.StaminaDrainRate * Time.deltaTime;

                // 检测耐力是否在本帧耗尽
                if (_data.CurrentStamina <= 0)
                {
                    // 重置耐力值为0，标记耐力耗尽，取消奔跑状态
                    _data.CurrentStamina = 0;
                    _isStaminaDepleted = true;
                    _data.IsRunning = false;
                    _data.WantToRun = false;
                }
            }
            else
            {
                // 非奔跑状态：取消“正在奔跑”标记
                _data.IsRunning = false;
                // 保留奔跑意图：即使静止，只要按下冲刺键且耐力未耗尽，仍标记“想要奔跑”
                _data.WantToRun = _input.IsSprinting && !_isStaminaDepleted;
                // 按恢复速率恢复耐力（乘以Time.deltaTime保证帧率无关）
                _data.CurrentStamina += _config.StaminaRegenRate * Time.deltaTime;
            }

            // 限制耐力值范围：确保耐力不会低于0或超过最大值
            _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0, _config.MaxStamina);
        }
    }
}