using Core.StateMachine;
using Characters.Player.Data;
using Characters.Player.Animation;
using Animancer;
using UnityEngine;
using Characters.Player.Expression;

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
            // 严重bug修复：UpperBodyController 可能在 PlayerController 的 Start 里才被赋值，所以这里不能直接获取，改为后加载。
        }

        public sealed override void LogicUpdate()
        {
            // Lazy fetch to avoid null reference during startup ordering.
            if (controller == null) controller = player.UpperBodyCtrl;

            if (CheckInterrupts()) return;
            UpdateStateLogic();
        }
        
        public override void PhysicsUpdate() { }

        protected virtual bool CheckInterrupts()
        {
            if (controller == null || controller.InterruptProcessor == null) return false;
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