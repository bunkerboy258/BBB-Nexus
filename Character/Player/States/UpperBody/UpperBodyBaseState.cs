using Core.StateMachine;
using Characters.Player.Data;
using Characters.Player.Animation;
using Animancer;
using UnityEngine;

namespace Characters.Player.States
{
    public abstract class UpperBodyBaseState : BaseState
    {
        protected PlayerController player;
        protected PlayerRuntimeData data;
        protected UpperBodyController controller;

        public UpperBodyBaseState(PlayerController player)
        {
            this.player = player;
            this.data = player.RuntimeData;
            // controller 不需要在这里赋值，它会在 LogicUpdate 之前通过 player._upperBodyController 拿到最新引用
        }

        public sealed override void LogicUpdate()
        {
            // 确保在 LogicUpdate 时 controller 已经通过 player 准备好
            if (controller == null) controller = player.UpperBodyCtrl;
            
            if (CheckInterrupts()) return;
            UpdateStateLogic();
        }
        
        public override void PhysicsUpdate() { }

        protected virtual bool CheckInterrupts()
        {
            return controller.InterruptProcessor.TryProcessInterrupts(this);
        }

        protected abstract void UpdateStateLogic();

        /// <summary>
        /// 播放上半身动画的通用方法。
        /// 默认 Layer = 1（上半身层），使用 NextStatePlayOptions 或默认选项。
        /// </summary>
        protected void ChooseOptionsAndPlay(ClipTransition clip)
        {
            if (player.AnimFacade == null)
            {
                Debug.LogError($"[{nameof(UpperBodyBaseState)}] AnimFacade is not initialized!");
                return;
            }

            // 优先级：NextStatePlayOptions > 默认值
            var options = data.NextStatePlayOptions ?? AnimPlayOptions.Default;
            options.Layer = 1; // 上半身层
            
            player.AnimFacade.PlayTransition(clip, options);
            data.NextStatePlayOptions = null;
        }
    }
}