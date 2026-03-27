using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public class PlayerStateRegistry
    {
        private readonly Dictionary<Type, PlayerBaseState> _states = new Dictionary<Type, PlayerBaseState>();

        // 记录启动状态（brain名单里的第一个）
        public PlayerBaseState InitialState { get; private set; }

        /// <summary>
        /// 核心工厂方法：根据枚举名单 实例化状态并注入依赖
        /// </summary>
        public void InitializeFromBrain(PlayerBrainSO brain, BBBCharacterController player)
        {
            if (brain == null || brain.AvailableStates == null || brain.AvailableStates.Count == 0)
            {
                Debug.LogError("[BBBNexus] 存在PlayerBrainSO 未配置或没有添加任何状态！");
                return;
            }

            for (int i = 0; i < brain.AvailableStates.Count; i++)
            {
                var stateTypeEnum = brain.AvailableStates[i];

                // 直接调用带参构造
                PlayerBaseState newState = stateTypeEnum switch
                {
                    PlayerStateType.Idle => new PlayerIdleState(player),
                    PlayerStateType.MoveStartState => new PlayerMoveStartState(player),
                    PlayerStateType.MoveLoopState => new PlayerMoveLoopState(player),
                    PlayerStateType.StopState => new PlayerStopState(player),
                    PlayerStateType.Jump => new PlayerJumpState(player),
                    PlayerStateType.DoubleJump => new PlayerDoubleJumpState(player),
                    PlayerStateType.Fall => new PlayerFallState(player),
                    PlayerStateType.Land => new PlayerLandState(player),
                    PlayerStateType.Dodge => new PlayerDodgeState(player),
                    PlayerStateType.Roll => new PlayerRollState(player),
                    PlayerStateType.Vault => new PlayerVaultState(player),
                    PlayerStateType.AimIdle => new PlayerAimIdleState(player),
                    PlayerStateType.AimMove => new PlayerAimMoveState(player),
                    PlayerStateType.Override=> new OverrideState(player),
                    PlayerStateType.Death => new PlayerDeathState(player),
                    _ => null
                };

                if (newState != null)
                {
                    Type type = newState.GetType();
                    if (!_states.ContainsKey(type))
                    {
                        _states.Add(type, newState);
                    }

                    // 凭什么启动状态必须是 Idle？如果我做个一出场就在天上掉落的角色呢？
                    // 默认列表里的第一个状态作为启动状态 (如果不这么做 就要硬编码一个状态 我有洁癖哈哈)
                    if (InitialState == null)
                    {
                        InitialState = newState;
                    }
                }
            }
            //Debug.Log($"[BBBNexus] 成功装载了 {_states.Count} 个状态！");
        }

        /// <summary>
        /// 供外部获取具体状态实例的 API
        /// </summary>
        public T GetState<T>() where T : PlayerBaseState
        {
            if (_states.TryGetValue(typeof(T), out var state))
                return state as T;

            Debug.LogError($"未找到状态 {typeof(T).Name}！请检查 PlayerBrainSO 是否添加了对应的枚举！");
            return null;
        }
    }
}