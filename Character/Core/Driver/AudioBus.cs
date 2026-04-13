using UnityEngine;
using UnityEngine.Audio;

namespace BBBNexus
{
    /// <summary>
    /// 全局音频总线。所有音效播放的统一入口。
    /// 内部维护 AudioSource 池，按 MixerGroup 路由。
    /// </summary>
    public sealed class AudioBus : SingletonMono<AudioBus>
    {
        [Header("Mixer Groups（在 Unity Editor 中赋值）")]
        [Tooltip("SFX 混音组（角色动作、武器等）")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Tooltip("BGM 混音组")]
        [SerializeField] private AudioMixerGroup _bgmGroup;

        [Tooltip("UI 混音组")]
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Header("Source Pool")]
        [Tooltip("预分配的 3D 音效 AudioSource 数量")]
        [SerializeField] private int _poolSize = 16;

        private AudioSource[] _pool;
        private int _nextIndex;

        // 一个常驻的 2D source 用于 UI / 非空间化音效
        private AudioSource _2dSource;

        protected override void Awake()
        {
            base.Awake();
            InitPool();
        }

        private void InitPool()
        {
            _pool = new AudioSource[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var child = new GameObject($"SfxSource_{i}");
                child.transform.SetParent(transform);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f; // 3D
                if (_sfxGroup != null) src.outputAudioMixerGroup = _sfxGroup;
                _pool[i] = src;
            }

            var uiChild = new GameObject("2DSource");
            uiChild.transform.SetParent(transform);
            _2dSource = uiChild.AddComponent<AudioSource>();
            _2dSource.playOnAwake = false;
            _2dSource.spatialBlend = 0f; // 2D
            if (_uiGroup != null) _2dSource.outputAudioMixerGroup = _uiGroup;
        }

        /// <summary>
        /// 在指定世界位置播放一个 3D 音效（走 SFX 混音组）。
        /// </summary>
        public void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            var src = GetNextSource();
            src.transform.position = position;
            if (_sfxGroup != null) src.outputAudioMixerGroup = _sfxGroup;
            src.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// 从 clip 数组随机选一个播放。
        /// </summary>
        public void PlaySfxRandom(AudioClip[] clips, Vector3 position, float volume = 1f)
        {
            var clip = WeaponAudioUtil.Pick(clips);
            PlaySfx(clip, position, volume);
        }

        /// <summary>
        /// 播放 2D 音效（UI 等非空间化音效）。
        /// </summary>
        public void PlayUI(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _2dSource == null) return;
            if (_uiGroup != null) _2dSource.outputAudioMixerGroup = _uiGroup;
            _2dSource.PlayOneShot(clip, volume);
        }

        private AudioSource GetNextSource()
        {
            var src = _pool[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _pool.Length;
            return src;
        }
    }
}
