using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 生命值仲裁器
    /// </summary>
    public class HealthArbiter
    {
        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _data;

        // 环形缓冲区 (最多同时受击 16 次)
        private DamageRequest[] _damageQueue = new DamageRequest[16];
        private int _head = 0;
        private int _tail = 0;

        public HealthArbiter(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        /// <summary>
        /// 内部调用的入队接口
        /// </summary>
        internal void Enqueue(in DamageRequest request)
        {
            if (_data.IsDead) return; // 防止鞭尸:d

            _damageQueue[_tail] = request;
            _tail = (_tail + 1) % _damageQueue.Length;

            //Debug.Log($"Damage enqueue amount {request.Amount} hp {_data.CurrentHealth}", _player);
        }

        /// <summary>
        /// 计算受击方向角：攻击者方向与角色 forward 的 SignedAngle（度数）。
        /// 无攻击者信息时返回 float.NaN。
        /// </summary>
        private float CalculateHitAngle(in DamageRequest req)
        {
            var attackerTransform = req.ResolveAttackerTransform();
            if (attackerTransform == null) return float.NaN;

            var toAttacker = attackerTransform.position - _player.transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.001f) return float.NaN;
            return Vector3.SignedAngle(_player.transform.forward, toAttacker, Vector3.up);
        }

        /// <summary>
        /// 每帧统一裁决
        /// </summary>
        public void Arbitrate()
        {
            if (_data.IsDead || _head == _tail) return;

            while (_head != _tail)
            {
                ref var req = ref _damageQueue[_head];

                float before = _data.CurrentHealth;
                // 结算伤害
                _data.CurrentHealth -= req.Amount;

                //Debug.Log($"Damage apply amount {req.Amount} hp {before} -> {_data.CurrentHealth}", _player);

                PostSystem.Instance?.Send("OnDamaged", new DamageEvent
                {
                    Target          = _player,
                    Amount          = req.Amount,
                    RemainingHealth = Mathf.Max(_data.CurrentHealth, 0f),
                    HitPoint        = req.HitPoint != Vector3.zero ? req.HitPoint : _player.transform.position + Vector3.up,
                    IsFatal         = _data.CurrentHealth <= 0f,
                });

                // 受击僵直：非致命伤害时施加 HitReaction 状态
                if (_data.CurrentHealth > 0f)
                {
                    var hitReaction = _player.Config?.HitReaction;
                    if (hitReaction != null)
                        _player.StatusEffects.Apply(hitReaction, CalculateHitAngle(in req));
                }

                _head = (_head + 1) % _damageQueue.Length;
            }

            // 死亡判定
            if (_data.CurrentHealth <= 0)
            {
                _data.CurrentHealth = 0;
                _data.IsDead = true;
                _player.StatusEffects?.Clear();

                _data.Arbitration.IsDead = true;
                _data.Arbitration.BlockInput = true;
                _data.Arbitration.BlockUpperBody = true;
                _data.Arbitration.BlockFacial = true;
                _data.Arbitration.BlockIK = true;
                _data.Arbitration.BlockInventory = true;

                //Debug.Log("Death trigger", _player);

                var death = _player.StateRegistry.GetState<PlayerDeathState>();
                _player.StateMachine.ChangeState(death);
            }
        }
    }
}
