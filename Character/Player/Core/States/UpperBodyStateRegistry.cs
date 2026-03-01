using Characters.Player.Data;
using Characters.Player.States;
using Characters.Player.States.UpperBody;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Characters.Player.Core
{
    public class UpperBodyStateRegistry
    {
        private readonly Dictionary<Type, UpperBodyBaseState> _states = new Dictionary<Type, UpperBodyBaseState>();
        public UpperBodyBaseState InitialState { get; private set; }

        public void InitializeFromBrain(PlayerBrainSO brain, PlayerController player)
        {
            if (brain == null || brain.UpperBodyStates == null || brain.UpperBodyStates.Count == 0) return;

            for (int i = 0; i < brain.UpperBodyStates.Count; i++)
            {
                var stateTypeEnum = brain.UpperBodyStates[i];

                // 【核心映射】：和全身状态机一模一样的清爽 Switch！
                UpperBodyBaseState newState = stateTypeEnum switch
                {
                    UpperBodyStateType.Idle => new UpperBodyIdleState(player),
                    UpperBodyStateType.Equip => new UpperBodyEquipState(player),
                    UpperBodyStateType.Unequip => new UpperBodyUnequipState(player),
                    UpperBodyStateType.Aim => new UpperBodyAimState(player),
                    UpperBodyStateType.Attack => new UpperBodyAttackState(player),
                    UpperBodyStateType.Unavailable => new UpperBodyUnavailableState(player),
                    _ => null
                };

                if (newState != null)
                {
                    Type type = newState.GetType();
                    if (!_states.ContainsKey(type)) _states.Add(type, newState);
                    if (InitialState == null) InitialState = newState;
                }
            }
        }

        public T GetState<T>() where T : UpperBodyBaseState
        {
            if (_states.TryGetValue(typeof(T), out var state)) return state as T;
            return null;
        }
    }
}