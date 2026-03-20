namespace BBBNexus
{
    /// <summary>
    /// Audio 控制器：消费黑板上的音频事件意图队列 并委托给 AudioDriver 播放 
    /// </summary>
    public sealed class AudioController
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;

        public AudioController(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        public void Update()
        {
            if (_player == null || _data == null) return;
            if (_player.AudioDriver == null) { _data.SfxQueue.Clear(); return; }

            int count = _data.SfxQueue.Count;
            for (int i = 0; i < count; i++)
            {
                var evt = _data.SfxQueue.Get(i);
                _player.AudioDriver.Play(evt);
            }

            _data.SfxQueue.Clear();
        }
    }
}
