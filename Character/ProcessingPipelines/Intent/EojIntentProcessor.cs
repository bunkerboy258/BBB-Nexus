namespace BBBNexus
{
    // 表情意图处理器 
    public class EojIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public EojIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        public void Update(in ProcessedInputData input)
        {
            if (input.Expression1Pressed)
            {
                _data.WantsExpression1 = true;
            }
            if (input.Expression2Pressed)
            {
                _data.WantsExpression2 = true;
            }
            if (input.Expression3Pressed)
            {
                _data.WantsExpression3 = true;
            }
            if (input.Expression4Pressed)
            {
                _data.WantsExpression4 = true;
            }
        }
    }
}