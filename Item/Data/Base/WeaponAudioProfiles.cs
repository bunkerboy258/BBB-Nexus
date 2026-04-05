using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 从 AudioClip[] 中随机选一个播放的工具方法。
    /// </summary>
    public static class WeaponAudioUtil
    {
        // 归一化增益表：从 Resources/AudioNormalization 延迟加载
        private static AudioNormalizationProfile _normProfile;
        private static bool _normProfileLoaded;

        private static AudioNormalizationProfile NormProfile
        {
            get
            {
                if (!_normProfileLoaded)
                {
                    _normProfile       = Resources.Load<AudioNormalizationProfile>("AudioNormalization");
                    _normProfileLoaded = true;
                }
                return _normProfile;
            }
        }

        private static float GetGain(AudioClip clip)
            => NormProfile != null ? NormProfile.GetGain(clip) : 1f;

        public static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        public static AudioClip PickCombo(ComboSegmentAudio[] combos, int index)
        {
            if (combos == null || index < 0 || index >= combos.Length) return null;
            return Pick(combos[index].Clips);
        }

        public static void PlayAt(AudioClip[] clips, Vector3 position)
        {
            var clip = Pick(clips);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, GetGain(clip));
        }

        public static void PlayAt(AudioClip clip, Vector3 position)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, GetGain(clip));
        }

        public static void PlayComboAt(ComboSegmentAudio[] combos, int index, Vector3 position)
        {
            var clip = PickCombo(combos, index);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, GetGain(clip));
        }
    }

    /// <summary>
    /// 近战音效配置：起手式、多段攻击（每段独立）、命中、收招。
    /// </summary>
    [Serializable]
    public struct MeleeAudioProfile
    {
        [Tooltip("起手式音效（随机选一个）")]
        public AudioClip[] EnterStanceSounds;

        [Tooltip("每段攻击的音效。外层索引对应 combo 段号，每段内随机选一个播放。")]
        public ComboSegmentAudio[] ComboSwingSounds;

        [Tooltip("命中音效（随机选一个）")]
        public AudioClip[] HitSounds;

        [Tooltip("收招音效（随机选一个）")]
        public AudioClip[] ExitStanceSounds;
    }

    /// <summary>
    /// 单段连招的音效集合。
    /// </summary>
    [Serializable]
    public struct ComboSegmentAudio
    {
        [Tooltip("该段攻击可用的音效（随机选一个播放）")]
        public AudioClip[] Clips;
    }

    /// <summary>
    /// 格挡/弹反音效配置：普通格挡、完美弹反各自的命中瞬间音效。
    /// 替身的生命周期音效（生成/贴附/消散）配置在 SubstituteFollower 组件上。
    /// </summary>
    [Serializable]
    public struct ParryAudioProfile
    {
        [Tooltip("普通闭眼格挡命中瞬间（随机选一个）")]
        public AudioClip[] ParrySounds;

        [Tooltip("完美弹反命中瞬间（随机选一个）")]
        public AudioClip[] PerfectParrySounds;
    }

    /// <summary>
    /// 远程/枪械音效配置：进入瞄准、退出瞄准、换弹、射击。
    /// </summary>
    [Serializable]
    public struct RangedAudioProfile
    {
        [Tooltip("进入瞄准音效（随机选一个）")]
        public AudioClip[] EnterAimSounds;

        [Tooltip("退出瞄准音效（随机选一个）")]
        public AudioClip[] ExitAimSounds;

        [Tooltip("换弹音效（随机选一个）")]
        public AudioClip[] ReloadSounds;

        [Tooltip("射击音效（随机选一个）")]
        public AudioClip[] ShootSounds;

        [Tooltip("弹丸命中音效（随机选一个）")]
        public AudioClip[] ProjectileHitSounds;
    }
}
