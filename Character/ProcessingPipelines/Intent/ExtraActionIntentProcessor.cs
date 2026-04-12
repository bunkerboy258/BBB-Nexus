namespace BBBNexus
{
    /// <summary>
    /// 额外动作意图处理器 - 监听 ExtraAction 输入并将意图写入黑板
    /// 支持四个额外动作槽位（与 Expression 系统完全独立）
    /// 
    /// 用途说明：
    /// - ExtraAction1~4：通用槽位，语义由外部 IExtraActionService 实现定义
    /// 
    /// 与 Expression 系统的区别：
    /// - Expression：专用于面部表情/动画（按键 6789）
    /// - ExtraAction：专用于游戏逻辑/特殊交互（独立绑定）
    /// </summary>
    public class ExtraActionIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;

        public ExtraActionIntentProcessor(PlayerRuntimeData data, InputPipeline inputPipeline)
        {
            _data = data;
            _input = inputPipeline;
        }

        /// <summary>
        /// 更新额外动作意图 - 根据输入按键设置对应的意图标志
        /// </summary>
        public void Update(in ProcessedInputData input)
        {
            if (input.ExtraAction1Pressed)
            {
                _data.WantsExtraAction1 = true;
            }

            if (input.ReloadPressed)
            {
                _data.WantsReload = true;
                _input?.ConsumeReloadPressed();
            }

            if (input.UseItemPressed)
            {
                _data.WantsUseItem = true;
                _input?.ConsumeUseItemPressed();
            }

            if (input.InventoryPressed)
            {
                _data.WantsToggleInventory = true;
                _input?.ConsumeInventoryPressed();
            }

            if (input.ExtraAction2Pressed)
            {
                _data.WantsExtraAction2 = true;
            }

            if (input.ExtraAction3Pressed)
            {
                _data.WantsExtraAction3 = true;
            }

            if (input.ExtraAction4Pressed)
            {
                _data.WantsExtraAction4 = true;
            }
        }
    }
}
