using Animancer;
using UnityEngine;

namespace BBBNexus
{
    // 上半身基础状态抽象类 
    // 与PlayerBaseState地位相同 但管理上半身子状态机的独立流程 
    // 注意 UpperBodyController 在 PlayerController 的 Start 里才初始化 所以这里采用延迟加载
    public abstract class UpperBodyBaseState : BaseState
    {
        protected BBBCharacterController player;
        protected PlayerRuntimeData data;
        protected UpperBodyController controller;

        public UpperBodyBaseState(BBBCharacterController player)
        {
            this.player = player;
            this.data = player.RuntimeData;
        }

        // 统一的LogicUpdate流程 延迟加载controller避免启动顺序问题 
        public sealed override void LogicUpdate()
        {
            // 延迟获取controller 避免在启动顺序不确定时出现空引用
            if (controller == null) controller = player.UpperBodyCtrl;

            if (CheckInterrupts()) return;
            UpdateStateLogic();
        }
        
        public override void PhysicsUpdate() { }

        // 上半身的拦截器检测 负责检查是否能进入特定的上半身状态 
        protected virtual bool CheckInterrupts()
        {
            if (controller == null || controller.InterruptProcessor == null) return false;
            return controller.InterruptProcessor.TryProcessInterrupts(this);
        }

        // 状态自身的正常逻辑 
        protected abstract void UpdateStateLogic();

        // 播放上半身动画的通用方法 
        // 默认 Layer = 1 上半身层 使用 NextStatePlayOptions 或默认选项
        // 这里的层级设置确保上半身动画只影响特定骨骼 与下半身互不干扰
        protected void ChooseOptionsAndPlay(ClipTransition clip)
        {
            if (player.AnimFacade == null)
            {
                Debug.LogError($"[{nameof(UpperBodyBaseState)}] AnimFacade is not initialized!");
                return;
            }

            // 优先级 NextStatePlayOptions 默认值
            var options = data.NextStatePlayOptions ?? AnimPlayOptions.Default;
            options.Layer = 1;
            
            player.AnimFacade.PlayTransition(clip, options);
            data.NextStatePlayOptions = null;
        }
    }
}