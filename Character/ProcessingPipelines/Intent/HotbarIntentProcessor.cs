namespace BBBNexus
{
    // 数字键装备意图处理器。
    // 这里只负责把 1~5 键转换为主手装备切换意图，不处理 OffHand 或更细粒度槽位。
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
                _data.WantToEquipSlotIndex = 0;
            }
            else if (input.Number2Pressed)
            {
                _data.WantToEquipSlotIndex = 1;
            }
            else if (input.Number3Pressed)
            {
                _data.WantToEquipSlotIndex = 2;
            }
            else if (input.Number4Pressed)
            {
                _data.WantToEquipSlotIndex = 3;
            }
            else if (input.Number5Pressed)
            {
                _data.WantToEquipSlotIndex = 4;
            }
        }
    }
}
