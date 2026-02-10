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
                    // 4. 绑定约束 (ParentConstraint)
                    ParentConstraint constraint = go.AddComponent<ParentConstraint>();
                    ConstraintSource source = new ConstraintSource();
                    source.sourceTransform = _player.RightHandBone;
                    source.weight = 1f;
                    constraint.AddSource(source);

                    // 应用配置的偏移
                    constraint.SetTranslationOffset(0, instance.SpawnPosOffset);
                    constraint.SetRotationOffset(0, instance.SpawnRotOffset);
                    constraint.constraintActive = true;

                    // 5. 初始化组件
                    instance.Initialize();

                    // 6. 记录实例
                    _data.CurrentEquipment.Instance = instance;
                    // 7.嘗試獲取邏輯組件 并且根據類型初始化邏輯組件
                    var logiccomponent= go.GetComponent<DeviceController>();
                    if(logiccomponent!=null)
                    {
                        if(desiredDef is RangedWeaponSO rangerdweaponDef)
                        {
                            logiccomponent.Initialize(rangerdweaponDef,_player.gameObject);
                        }

                        _data.CurrentEquipment.DeviceLogic= logiccomponent;
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
            _data.CurrentEquipment.DeviceLogic= null;

            _player.NotifyEquipmentChanged();
        }
    }
}

