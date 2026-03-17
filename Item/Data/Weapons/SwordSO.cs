using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New SwordSO", menuName = "BBBNexus/Items/Weapons/Sword")]
    public class SwordSO : MeleeWeaponSO
    {
        [Header("--- 剑的攻击动画 (Sword Attack Animations) ---")]
        [Tooltip("第一段攻击动画")]
        public ClipTransition AttackAnim1;
        public AnimPlayOptions AttackAnimOptions1 = AnimPlayOptions.Default;

        [Tooltip("第二段攻击动画")]
        public ClipTransition AttackAnim2;
        public AnimPlayOptions AttackAnimOptions2 = AnimPlayOptions.Default;

        [Tooltip("第三段攻击动画")]
        public ClipTransition AttackAnim3;
        public AnimPlayOptions AttackAnimOptions3 = AnimPlayOptions.Default;

        [Header("--- 攻击音效 (Attack Sounds) ---")]
        [Tooltip("挥动时的音效")]
        public AudioClip SwingSound;

        [Tooltip("击中时的音效")]
        public AudioClip HitSound;

        [Header("--- 攻击伤害 (Damage) ---")]
        [Tooltip("攻击伤害值")]
        public float AttackDamage = 10f;
    }
}
