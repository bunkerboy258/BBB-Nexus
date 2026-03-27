using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 根据audioso的事件映射表执行音频播放行为
    /// </summary>
    public sealed class AudioDriver
    {
        private readonly Transform _emitter;
        private readonly AudioSource _source;
        private readonly AudioSO _audio;

        public AudioDriver(Transform emitter, AudioSource source, AudioSO audio)
        {
            _emitter = emitter;
            _source = source;
            _audio = audio;
        }

        public void Play(PlayerSfxEvent evt)
        {
            if (_audio == null || _source == null) return;
            if (!_audio.TryPickClip(evt, out var clip) || clip == null) return;

            _source.PlayOneShot(clip);
        }
    }
}
