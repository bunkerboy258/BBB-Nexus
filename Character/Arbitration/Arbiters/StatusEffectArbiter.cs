using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 异常状态仲裁器
    ///
    /// 管理 BBBCharacterController 上当前激活的异常状态（StatusEffectSO）：
    /// - 每帧将状态的阻断标志写入 ArbitrationFlags
    /// - 状态到期后自动清除标志、淡出动画
    /// - 高优先级状态覆盖低优先级；同优先级不覆盖（除非 CanBeRefreshed）
    /// </summary>
    public class StatusEffectArbiter
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;

        private StatusEffectSO _current;
        private float _remainingTime;

        public StatusEffectSO Current => _current;
        public bool IsActive => _current != null && (_current.Duration <= 0f || _remainingTime > 0f);

        public StatusEffectArbiter(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        // ─────────────────────────────────────────────────────────
        // 施加状态
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 施加一个异常状态。
        /// 优先级规则：新效果 Priority > 当前才覆盖；同优先级且 CanBeRefreshed 则刷新计时。
        /// </summary>
        public void Apply(StatusEffectSO effect)
        {
            if (effect == null) return;
            if (_data.IsDead) return;

            // 已有更高优先级状态，忽略
            if (_current != null && effect.Priority < _current.Priority) return;

            // 同优先级：仅刷新计时（如果允许）
            if (_current == effect)
            {
                if (effect.CanBeRefreshed)
                    _remainingTime = effect.Duration;
                return;
            }

            // 覆盖或首次施加
            _current = effect;
            _remainingTime = effect.Duration;

            // 播放状态动画
            if (effect.Clip != null && _player.AnimFacade != null)
                _player.AnimFacade.PlayTransition(effect.Clip, effect.PlayOptions);
        }

        /// <summary>
        /// 立即清除当前状态（外部强制结束用）
        /// </summary>
        public void Clear()
        {
            _current = null;
            _remainingTime = 0f;
        }

        // ─────────────────────────────────────────────────────────
        // 每帧仲裁（由 ArbiterPipeline 调用）
        // ─────────────────────────────────────────────────────────

        public void Arbitrate()
        {
            if (_current == null) return;

            // 永久状态（Duration=0）不计时
            if (_current.Duration > 0f)
            {
                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0f)
                {
                    _current = null;
                    return;
                }
            }

            // 将阻断标志写入本帧 ArbitrationFlags（每帧叠加，LateUpdate 末尾 ResetIntent 会还原非持久标志）
            _current.ApplyBlockFlagsTo(ref _data.Arbitration);
        }
    }
}
