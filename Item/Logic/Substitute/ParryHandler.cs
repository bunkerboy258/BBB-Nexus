using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 替身格挡处理器（玩家专属组件，挂在 BBBCharacterController 同 GameObject）
    ///
    /// 由 BBBCharacterController.RequestDamage 在检测到闭眼状态时调用。
    /// 职责：
    ///   1. 烘焙玩家当前帧姿势（BakeMesh 快照）
    ///   2. 在玩家位置实例化替身，注入快照与武器追踪目标
    ///   3. 对攻击者施加"被格挡"异常状态
    /// </summary>
    public class ParryHandler : MonoBehaviour
    {
        [Header("替身配置")]

        [Tooltip("玩家主体 SkinnedMeshRenderer，用于 BakeMesh 快照")]
        public SkinnedMeshRenderer CharacterSMR;

        [Tooltip("替身预制件（需要挂有 SubstituteFollower 组件）")]
        public GameObject SubstitutePrefab;

        [Tooltip("施加给攻击者的异常状态（被格挡/踉跄）")]
        public StatusEffectSO ParriedEffect;

        [Tooltip("施加给攻击者的完美弹反状态（击倒/knockdown）")]
        public StatusEffectSO PerfectParriedEffect;

        [Header("Debug")]
        public bool DebugLog = false;

        // ── 触发入口 ────────────────────────────────────────────────────

        /// <summary>
        /// 由 BBBCharacterController.RequestDamage 在闭眼拦截时调用（普通弹反）。
        /// </summary>
        public void TriggerParry(in DamageRequest req) => ExecuteParry(in req, ParriedEffect, "parry");

        /// <summary>
        /// 由 BBBCharacterController.RequestDamage 在完美弹反窗口内调用（击倒）。
        /// </summary>
        public void TriggerPerfectParry(in DamageRequest req) => ExecuteParry(in req, PerfectParriedEffect, "perfect parry");

        // ── 内部实现 ────────────────────────────────────────────────────

        private void ExecuteParry(in DamageRequest req, StatusEffectSO effect, string label)
        {
            var attackerController = req.ResolveAttackerController();

            Debug.Log(
                $"[ParryTrace] trigger {label} attacker={req.Attacker?.name ?? "null"} " +
                $"attackerCtrl={attackerController?.name ?? "null"} weapon={req.WeaponTransform?.name ?? "null"} " +
                $"hasSubstitute={SubstitutePrefab != null} hasEffect={effect != null}",
                this);

            // ── Step 1: 烘焙当前姿势快照 ──
            Mesh bakedMesh = null;
            if (CharacterSMR != null)
            {
                bakedMesh = new Mesh();
                CharacterSMR.BakeMesh(bakedMesh);
            }
            else
            {
                Debug.LogWarning("[ParryHandler] CharacterSMR 未赋值，替身将无法显示姿势。", this);
            }

            // ── Step 2: 实例化替身 ──
            if (SubstitutePrefab != null)
            {
                var instance = Instantiate(SubstitutePrefab, transform.position, transform.rotation);
                var follower = instance.GetComponent<SubstituteFollower>();

                if (follower != null)
                {
                    follower.Init(bakedMesh, req.WeaponTransform, attackerController);
                }
                else
                {
                    Debug.LogWarning("[ParryHandler] SubstitutePrefab 上找不到 SubstituteFollower，已销毁实例。", SubstitutePrefab);
                    if (bakedMesh != null) Destroy(bakedMesh);
                    Destroy(instance);
                    return;
                }
            }
            else
            {
                // 没有替身预制件时，释放已分配的 Mesh
                if (bakedMesh != null) Destroy(bakedMesh);
                Debug.LogWarning($"[ParryHandler] SubstitutePrefab 未赋值，{label} 无视觉表现。", this);
            }

            // ── Step 3: 对攻击者施加状态 ──
            if (attackerController != null && effect != null)
            {
                attackerController.StatusEffects.Apply(effect);
                Debug.Log($"[ParryTrace] applied status '{effect.DisplayName}' ({label}) to '{attackerController.name}'.", attackerController);

                if (DebugLog)
                    Debug.Log($"[ParryHandler] 对 '{attackerController.name}' 施加了：{effect.DisplayName}（{label}）。", attackerController);
            }
            else if (effect != null)
            {
                Debug.LogWarning(
                    $"[ParryTrace] unable to resolve attacker controller, {label} status was not applied. " +
                    $"Attacker={req.Attacker?.name} Weapon={req.WeaponTransform?.name}",
                    this);
            }

            Debug.Log($"[ParryTrace] {label} handler completed for attacker={req.Attacker?.name ?? "null"}.", this);
        }
    }
}
