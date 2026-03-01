using System;
using System.Collections.Generic;
using Characters.Player.States;
using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    /// <summary>
    /// 上半身状态注册表：根据 BrainSO 的枚举配置，映射并创建对应的状态实例。
    /// </summary>
    public class UpperBodyStateRegistry
    {
        private readonly Dictionary<Type, UpperBodyBaseState> _states = new Dictionary<Type, UpperBodyBaseState>();
        public UpperBodyBaseState InitialState { get; private set; }

        public void InitializeFromBrain(PlayerBrainSO brain, PlayerController player)
        {
            if (brain == null || brain.UpperBodyStates == null || brain.UpperBodyStates.Count == 0)
            {
                Debug.LogWarning("[UpperBodyStateRegistry] PlayerBrainSO 中未配置上半身状态！");
                return;
            }

            for (int i = 0; i < brain.UpperBodyStates.Count; i++)
            {
                var stateTypeEnum = brain.UpperBodyStates[i];

                // 【核心重构】：仅保留三个极简状态的映射
                UpperBodyBaseState newState = stateTypeEnum switch
                {
                    UpperBodyStateType.EmptyHands => new UpperBodyEmptyState(player),
                    UpperBodyStateType.HoldItem => new UpperBodyHoldItemState(player),
                    UpperBodyStateType.Unavailable => new UpperBodyUnavailableState(player),
                    _ => null
                };

                if (newState != null)
                {
                    Type type = newState.GetType();
                    if (!_states.ContainsKey(type))
                    {
                        _states.Add(type, newState);
                    }

                    // 设置默认启动状态
                    if (InitialState == null)
                    {
                        InitialState = newState;
                    }
                }
            }
        }

        public T GetState<T>() where T : UpperBodyBaseState
        {
            if (_states.TryGetValue(typeof(T), out var state))
                return state as T;

            return null;
        }
    }
}