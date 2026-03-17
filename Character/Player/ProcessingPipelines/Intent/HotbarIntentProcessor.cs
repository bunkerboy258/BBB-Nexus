namespace BBBNexus
{
    // 快捷栏意图处理器 将数字按键转为装备切换意图写入黑板
    public class HotbarIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public HotbarIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        public void Update(in ProcessedInputData input)
        {
            if (input.Number1Pressed)
            {
                _data.WantsToEquipHotbarIndex = 0;
            }
            else if (input.Number2Pressed)
            {
                _data.WantsToEquipHotbarIndex = 1;
            }
            else if (input.Number3Pressed)
            {
                _data.WantsToEquipHotbarIndex = 2;
            }
            else if (input.Number4Pressed)
            {
                _data.WantsToEquipHotbarIndex = 3;
            }
            else if (input.Number5Pressed)
            {
                _data.WantsToEquipHotbarIndex = 4;
            }
        }
    }
}