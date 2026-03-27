using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 攻击与交互意图处理器：将输入快照写入黑板。
    /// WantsToAction  → 左键主要攻击，由武器 Behaviour 消费
    /// WantsToInteract → E 键情境交互，由 ActionController 消费
    /// </summary>
    public sealed class ActionIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public ActionIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        public void Update(in ProcessedInputData input)
        {
            if (input.PrimaryAttackPressed)
                _data.WantsToAction = true;

            if (input.InteractPressed)
                _data.WantsToInteract = true;
        }
    }
}
