using Animancer;
using UnityEngine;

namespace Characters.Player.Data
{
    [CreateAssetMenu(fileName = "AimingModule", menuName = "Player/Modules/Aiming Module")]
    public class AimingModuleSO : ScriptableObject
    {
        [Header("AIMING SYSTEM - Ãé×¼ÏµÍ³")]
        public float AimSensitivity = 1f;
        public float AimWalkSpeed = 1.5f;
        public float AimJogSpeed = 2.5f;
        public float AimSprintSpeed = 5.0f;
        public float AimRotationSmoothTime = 0.05f;

        [Header("Animation Blending")]
        public float AimXAnimBlendSmoothTime = 0.2f;
        public float AimYAnimBlendSmoothTime = 0.2f;

        [Header("Animation Assets")]
        public MixerTransition2D AimLocomotionMixer;
    }
}
