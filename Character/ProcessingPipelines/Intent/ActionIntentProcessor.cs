using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 攻击与交互意图处理器：将输入快照写入黑板。
    /// WantsToPrimaryAction  → 左键主要攻击，由武器 Behaviour 消费
    /// WantsToInteract → E 键情境交互，由 ActionController 消费
    /// </summary>
    public sealed class ActionIntentProcessor
    {
        private const bool ActionIntentTrace = true;
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;

        public ActionIntentProcessor(PlayerRuntimeData data, InputPipeline input)
        {
            _data = data;
            _input = input;
        }

        public void Update(in ProcessedInputData input)
        {
            if (_data.Arbitration.BlockAction)
            {
                if (input.PrimaryAttackPressed)
                    _input?.ConsumePrimaryAttackPressed();

                if (input.InteractPressed)
                    _input?.ConsumeInteractPressed();

                return;
            }

            if (input.PrimaryAttackPressed)
            {
                _data.WantsToPrimaryAction = true;
                if (ActionIntentTrace)
                {
                    Debug.Log(
                        $"[ActionIntentTrace] frame={Time.frameCount} primaryPressed={input.PrimaryAttackPressed} " +
                        $"primaryHeld={input.PrimaryAttackHeld} sprintHeld={input.SprintHeld} jumpHeld={input.JumpHeld}");
                }
            }

            if (input.InteractPressed)
                _data.WantsToInteract = true;
        }
    }
}
