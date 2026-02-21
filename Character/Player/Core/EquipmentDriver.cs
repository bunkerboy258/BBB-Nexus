using Characters.Player.Data;
using Items.Data;
using Items.Logic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;

namespace Characters.Player.Core
{
    /// <summary>
    /// [执行层] 负责物品模型的实例化与销毁。
    /// </summary>
    public class EquipmentDriver
    {
        private PlayerController _player;
        private PlayerRuntimeData _data;

        public EquipmentDriver(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        /// <summary>
        /// [Equip] 根据意图 (Desired) 实例化新武器。
        /// </summary>
        public void SyncModelToDesired()
        {
            var desiredDef = _data.DesiredItemDefinition;
            var currentDef = _data.CurrentEquipment.Definition;

            if (desiredDef == currentDef) return;

            // 1. 先彻底清理旧的
            // (虽然 UnequipState 可能已经清理过了，但为了保险再清理一次)
            UnloadCurrentModel();

            // 2. 检查新物品是否为可装备类型
            // (防止把苹果实例化出来)
            if (desiredDef is EquippableItemSO equipDef && equipDef.Prefab != null)
            {
                // 3. 实例化
                GameObject go = Object.Instantiate(equipDef.Prefab, _player.WeaponContainer, false);
                InteractableItem instance = go.GetComponent<InteractableItem>();

                if (instance != null)
                {
                    // 关键：保证约束偏移是基于 HandBone，而不是 Prefab 自带 Transform。
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;

                    // 4. 绑定约束 (ParentConstraint)
                    ParentConstraint constraint = go.AddComponent<ParentConstraint>();

                    // 先初始化，再设置 source/offset，最后激活。
                    // 有些情况下如果不 lock，会被约束系统认为 offset 尚未校准。
                    constraint.constraintActive = false;
                    constraint.locked = false;

                    ConstraintSource source = new ConstraintSource
                    {
                        sourceTransform = _player.RightHandBone,
                        weight = 1f
                    };

                    constraint.AddSource(source);

                    // 应用配置的偏移
                    constraint.SetTranslationOffset(0, instance.SpawnPosOffset);
                    constraint.SetRotationOffset(0, instance.SpawnRotOffset);

                    // 激活并锁定（使 offset 固化并开始驱动 transform）
                    constraint.constraintActive = true;
                    constraint.locked = true;

                    // 强制刷新一次 offset（避免某些情况下 inspector 显示/运行时不应用）
                    constraint.translationAtRest = instance.SpawnPosOffset;
                    constraint.rotationAtRest = instance.SpawnRotOffset;
                    constraint.translationOffsets = constraint.translationOffsets; // 触发内部更新（无分配）
                    constraint.rotationOffsets = constraint.rotationOffsets;

                    if (instance.SpawnPosOffset == Vector3.zero && instance.SpawnRotOffset == Vector3.zero)
                    {
                        Debug.LogWarning($"[EquipmentDriver] {equipDef.name} SpawnPosOffset/SpawnRotOffset are both zero. If this weapon should have an offset, check InteractableItem values on the Prefab root.");
                    }

                    // 5. 初始化组件
                    instance.Initialize();

                    // 6. 记录实例
                    _data.CurrentEquipment.Instance = instance;

                    // 7. 尝试获取逻辑组件 并根据类型初始化
                    var logiccomponent = go.GetComponent<DeviceController>();
                    if (logiccomponent != null)
                    {
                        if (desiredDef is RangedWeaponSO rangerdweaponDef)
                        {
                            logiccomponent.Initialize(rangerdweaponDef, _player.gameObject);
                        }

                        _data.CurrentEquipment.DeviceLogic = logiccomponent;
                    }
                }
                else
                {
                    // 如果 Prefab 没挂脚本，没法用，销毁
                    Object.Destroy(go);
                    Debug.LogWarning($"装备失败：{desiredDef.Name} 的 Prefab 缺少 InteractableItem 组件！");
                }
            }

            // 7. 更新现状记录
            _data.CurrentEquipment.Definition = desiredDef;

            // 8. 通知系统
            _player.NotifyEquipmentChanged();
        }

        /// <summary>
        /// [Unequip] 强制卸载当前模型，不装备新物体。
        /// </summary>
        public void UnloadCurrentModel()
        {
            if (_data.CurrentEquipment.HasItem)
            {
                Object.Destroy(_data.CurrentEquipment.Instance.gameObject);
            }

            // 清空 Current，保留 Desired (状态机会根据 Desired 决定下一步)
            _data.CurrentEquipment.Definition = null;
            _data.CurrentEquipment.Instance = null;
            _data.CurrentEquipment.DeviceLogic = null;

            _player.NotifyEquipmentChanged();
        }
    }
}

