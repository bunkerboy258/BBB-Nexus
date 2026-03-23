namespace BBBNexus
{
    /// <summary>
    /// 表情意图处理器 - 监听按键输入并将表情请求写入黑板
    /// 支持四个快捷表情按键（6789）
    /// </summary>
    public class EojIntentProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _input;

        public EojIntentProcessor(PlayerRuntimeData data, InputPipeline inputPipeline)
        {
            _data = data;
            _input = inputPipeline;
        }

        /// <summary>
        /// 更新表情意图 - 根据输入按键设置对应的表情事件
        /// </summary>
        public void Update(in ProcessedInputData input)
        {
            // 检测快捷表情按键 6789
            // 优先级：最后一个按下的表情会覆盖前面的
            if (input.Expression1Pressed)
            {
                _data.FacialEventRequest = PlayerFacialEvent.QuickExpression1;
                _data.WantsExpression1 = true;
                _input?.ConsumeExpression1Pressed();
            }

            if (input.Expression2Pressed)
            {
                _data.FacialEventRequest = PlayerFacialEvent.QuickExpression2;
                _data.WantsExpression2 = true;
                _input?.ConsumeExpression2Pressed();
            }

            if (input.Expression3Pressed)
            {
                _data.FacialEventRequest = PlayerFacialEvent.QuickExpression3;
                _data.WantsExpression3 = true;
                _input?.ConsumeExpression3Pressed();
            }

            if (input.Expression4Pressed)
            {
                _data.FacialEventRequest = PlayerFacialEvent.QuickExpression4;
                _data.WantsExpression4 = true;
                _input?.ConsumeExpression4Pressed();
            }
        }
    }
}