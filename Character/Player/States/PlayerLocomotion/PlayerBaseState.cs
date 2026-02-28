using Core.StateMachine;
using Characters.Player.Data;
using Animancer;
using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家基础状态抽象类
    /// - 封装通用引用与工具方法
    /// - 统一 LogicUpdate 执行顺序：先强制转移，再状态自身逻辑
    /// </summary>
    public abstract class PlayerBaseState : BaseState
    {
        protected PlayerController player;
        protected PlayerRuntimeData data;
        protected PlayerSO config;
        protected IAnimationFacade AnimFacade;

        protected PlayerBaseState(PlayerController player)
        {
            this.player = player;
            this.data = player.RuntimeData;
            this.config = player.Config;
            this.AnimFacade = player.AnimFacade;
        }


        /// <summary>
        /// 统一封闭 LogicUpdate：
        /// 1) CheckInterrupts：全局强制转移（高优先级）
        /// 2) UpdateStateLogic：状态自身逻辑
        /// </summary>
        public sealed override void LogicUpdate()
        {
            if (CheckInterrupts()) return;
            UpdateStateLogic();
        }

        /// <summary>
        /// 全局强制转移检测：重构后通过遍历拦截器实现解耦
        /// </summary>
        protected virtual bool CheckInterrupts()
        {
            return player.InterruptProcessor.TryProcessInterrupts(this);
        }


        /// <summary>
        /// 状态自身的正常逻辑。
        /// 之前写在 LogicUpdate 里的内容应迁移到这里。
        /// </summary>
        protected abstract void UpdateStateLogic();

        /// <summary> 选择播放配置的状态机公共api
        /// <summary>
        /// 选择动画播放选项并播放。
        /// 优先使用 NextStatePlayOptions（临时覆写），否则使用默认选项。
        /// </summary>
        protected void ChooseOptionsAndPlay(ClipTransition clip)
        {
            if (AnimFacade == null)
            {
                Debug.LogError($"[{nameof(PlayerBaseState)}] AnimFacade is not initialized!");
                return;
            }

            // 优先级：NextStatePlayOptions >  默认值
            var options = data.NextStatePlayOptions ??  AnimPlayOptions.Default;
            options.Layer = 0;
            
            AnimFacade.PlayTransition(clip, options);
            data.NextStatePlayOptions = null;
        }
    }
}