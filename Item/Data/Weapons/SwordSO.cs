using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New SwordSO", menuName = "BBBNexus/Items/Weapons/Sword")]
    public class SwordSO : MeleeWeaponSO
    {
        [Header("--- 剑的攻击配置 (Sword Attack Configurations) ---")]
        [Tooltip("攻击的完整接管请求配置")]
        public ActionRequest AttackRequest;

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
