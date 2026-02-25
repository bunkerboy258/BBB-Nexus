using UnityEngine;
using Animancer;
using Core.StateMachine;
using Characters.Player.Data;
using Characters.Player.States.UpperBody;

namespace Characters.Player.Layers
{
    public class UpperBodyController
    {
        private PlayerController _player;
        private StateMachine _stateMachine;

        // 状态实例
        public UpperBodyIdleState IdleState { get; private set; }
        public UpperBodyEquipState EquipState { get; private set; }
        public UpperBodyUnequipState UnequipState { get; private set; }
        public UpperBodyAimState AimState { get; private set; }
        public UpperBodyUnavailableState UnavailableState { get; private set; }
        

        public UpperBodyController(PlayerController player)
        {
            _player = player;

            // Layer Setup
            var layer = _player.Animancer.Layers[1];
            layer.Mask = _player.Config.Core.UpperBodyMask;
            layer.Weight = 1f;
            layer.ApplyAnimatorIK = true;

            // Set layer to additive mode so played clips are applied additively to base animation
            // 将上半身层设置为 Additive 模式，使其动画以叠加方式应用于下层基础动作
            //layer.IsAdditive = true;

            // State Machine Setup
            _stateMachine = new StateMachine();
            IdleState = new UpperBodyIdleState(player, this);
            EquipState = new UpperBodyEquipState(player, this);
            UnequipState = new UpperBodyUnequipState(player, this);
            AimState = new UpperBodyAimState(player, this);
            UnavailableState = new UpperBodyUnavailableState(player, this);

            _stateMachine.Initialize(IdleState);
        }

        public void Update()
        {
            _stateMachine.CurrentState.LogicUpdate();
        }

        public void ChangeState(BaseState newState) => _stateMachine.ChangeState(newState);

        // 提供给 State 使用的 Layer 访问器
        public AnimancerLayer Layer => _player.Animancer.Layers[1];
    }
}
