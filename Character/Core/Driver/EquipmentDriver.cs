using UnityEngine;
using System.Collections.Generic;

namespace BBBNexus
{
    // 装备驱动器 负责生成模型注入数据驱动逻辑
    public class EquipmentDriver
    {
        private readonly BBBCharacterController _player;
        // 主手物品配置
        public EquippableItemSO MainhandItemData { get; private set; }
        // 副手物品配置
        public EquippableItemSO OffhandItemData { get; private set; }
        // 主手物品实例
        public ItemInstance MainhandItemInstance { get; private set; }
        // 副手物品实例
        public ItemInstance OffhandItemInstance { get; private set; }
        // 主手物品逻辑接口
        public IHoldableItem MainhandItemDirector { get; private set; }
        // 副手物品逻辑接口
        public IHoldableItem OffhandItemDirector { get; private set; }
        // 主手模型实例
        private GameObject _currentMainhandWeaponInstance;
        // 副手模型实例
        private GameObject _currentOffhandWeaponInstance;

        // 向后兼容属性
        public EquippableItemSO CurrentItemData => MainhandItemData;
        public ItemInstance CurrentItemInstance => MainhandItemInstance;
        public IHoldableItem CurrentItemDirector => MainhandItemDirector;

        // 所有装备的缓存列表（避免每帧分配）
        private readonly List<IHoldableItem> _allEquippedItemsCache = new List<IHoldableItem>(2);

        /// <summary>
        /// 获取所有装备的武器接口（只读）
        /// </summary>
        public IReadOnlyList<IHoldableItem> AllEquippedItems
        {
            get
            {
                _allEquippedItemsCache.Clear();
                if (MainhandItemDirector != null) _allEquippedItemsCache.Add(MainhandItemDirector);
                if (OffhandItemDirector != null) _allEquippedItemsCache.Add(OffhandItemDirector);
                return _allEquippedItemsCache;
            }
        }

        public EquipmentDriver(BBBCharacterController player)
        {
            _player = player;
        }

        /// <summary>
        /// 装备物品到指定槽位
        /// </summary>
        public void EquipItemToSlot(ItemInstance itemInstance, EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.MainHand)
            {
                EquipMainhand(itemInstance);
                _player.RuntimeData.MainhandItem = itemInstance;
                // 设置武器的 CurrentEquipSlot
                if (MainhandItemDirector != null)
                {
                    MainhandItemDirector.CurrentEquipSlot = slot;
                }
            }
            else if (slot == EquipmentSlot.OffHand)
            {
                EquipOffhand(itemInstance);
                _player.RuntimeData.OffhandItem = itemInstance;
                // 设置武器的 CurrentEquipSlot
                if (OffhandItemDirector != null)
                {
                    OffhandItemDirector.CurrentEquipSlot = slot;
                }
            }
            else
            {
                Debug.LogWarning($"[EquipmentDriver] 不支持的装备槽位：{slot}");
            }
        }

        /// <summary>
        /// 装备主手武器（向后兼容）
        /// </summary>
        public void EquipItem(ItemInstance itemInstance)
        {
            EquipMainhand(itemInstance);
        }

        /// <summary>
        /// 装备主手武器
        /// </summary>
        private void EquipMainhand(ItemInstance itemInstance)
        {
            UnequipMainhand();
            MainhandItemInstance = itemInstance;
            MainhandItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;
            if (MainhandItemData == null)
            {
                Debug.Log("驱动器判定当前主手为空手状态");
                _player?.NotifyEquipmentChanged();
                return;
            }

            if (MainhandItemData.Prefab != null && _player != null && _player.MainhandWeaponContainer != null)
            {
                var prefab = MainhandItemData.Prefab;

                if (SimpleObjectPoolSystem.Shared != null)
                {
                    _currentMainhandWeaponInstance = SimpleObjectPoolSystem.Shared.Spawn(prefab);
                    _currentMainhandWeaponInstance.transform.SetParent(_player.MainhandWeaponContainer, false);
                    _currentMainhandWeaponInstance.transform.localPosition = Vector3.zero;
                    _currentMainhandWeaponInstance.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    _currentMainhandWeaponInstance = Object.Instantiate(prefab, _player.MainhandWeaponContainer);
                }

                _currentMainhandWeaponInstance.transform.localScale = Vector3.one;
                _currentMainhandWeaponInstance.transform.localPosition = MainhandItemData.HoldPositionOffset;
                _currentMainhandWeaponInstance.transform.localRotation = MainhandItemData.HoldRotationOffset;

                MainhandItemDirector = _currentMainhandWeaponInstance.GetComponent<IHoldableItem>();
                if (MainhandItemDirector != null)
                {
                    MainhandItemDirector.Initialize(MainhandItemInstance);
                    MainhandItemDirector.CurrentEquipSlot = EquipmentSlot.MainHand;
                    MainhandItemDirector.OnEquipEnter(_player);
                }
                else
                {
                    Debug.LogWarning("生成的模型缺少控制接口 状态机将无法驱动该武器");
                }
            }
            else
            {
                Debug.LogWarning("主手装配失败 检查预制件引用或容器挂点是否丢失");
            }
            _player?.NotifyEquipmentChanged();
        }

        /// <summary>
        /// 装备副手武器
        /// </summary>
        private void EquipOffhand(ItemInstance itemInstance)
        {
            UnequipOffhand();
            OffhandItemInstance = itemInstance;
            OffhandItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;
            if (OffhandItemData == null)
            {
                Debug.Log("驱动器判定当前副手为空手状态");
                _player?.NotifyEquipmentChanged();
                return;
            }

            if (OffhandItemData.Prefab != null && _player != null && _player.OffhandWeaponContainer != null)
            {
                var prefab = OffhandItemData.Prefab;

                if (SimpleObjectPoolSystem.Shared != null)
                {
                    _currentOffhandWeaponInstance = SimpleObjectPoolSystem.Shared.Spawn(prefab);
                    _currentOffhandWeaponInstance.transform.SetParent(_player.OffhandWeaponContainer, false);
                    _currentOffhandWeaponInstance.transform.localPosition = Vector3.zero;
                    _currentOffhandWeaponInstance.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    _currentOffhandWeaponInstance = Object.Instantiate(prefab, _player.OffhandWeaponContainer);
                }

                _currentOffhandWeaponInstance.transform.localScale = Vector3.one;
                _currentOffhandWeaponInstance.transform.localPosition = OffhandItemData.HoldPositionOffset;
                _currentOffhandWeaponInstance.transform.localRotation = OffhandItemData.HoldRotationOffset;

                OffhandItemDirector = _currentOffhandWeaponInstance.GetComponent<IHoldableItem>();
                if (OffhandItemDirector != null)
                {
                    OffhandItemDirector.Initialize(OffhandItemInstance);
                    OffhandItemDirector.CurrentEquipSlot = EquipmentSlot.OffHand;
                    OffhandItemDirector.OnEquipEnter(_player);
                }
                else
                {
                    Debug.LogWarning("生成的模型缺少控制接口 状态机将无法驱动该武器");
                }
            }
            else
            {
                Debug.LogWarning("副手装配失败 检查预制件引用或容器挂点是否丢失");
            }
            _player?.NotifyEquipmentChanged();
        }

        // 卸载当前物品销毁模型清理逻辑
        public void UnequipCurrentItem()
        {
            UnequipMainhand();
            UnequipOffhand();
        }

        /// <summary>
        /// 卸载主手武器
        /// </summary>
        public void UnequipMainhand()
        {
            if (_currentMainhandWeaponInstance != null)
            {
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    SimpleObjectPoolSystem.Shared.Despawn(_currentMainhandWeaponInstance);
                }
                else
                {
                    Object.Destroy(_currentMainhandWeaponInstance);
                }
                _currentMainhandWeaponInstance = null;
            }

            ClearAllIKReferencesFromRuntimeData();
            _player.NotifyEquipmentChanged();
            MainhandItemData = null;
            MainhandItemInstance = null;
            MainhandItemDirector = null;
            _player.RuntimeData.MainhandItem = null;
        }

        /// <summary>
        /// 卸载副手武器
        /// </summary>
        public void UnequipOffhand()
        {
            if (_currentOffhandWeaponInstance != null)
            {
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    SimpleObjectPoolSystem.Shared.Despawn(_currentOffhandWeaponInstance);
                }
                else
                {
                    Object.Destroy(_currentOffhandWeaponInstance);
                }
                _currentOffhandWeaponInstance = null;
            }

            _player.NotifyEquipmentChanged();
            OffhandItemData = null;
            OffhandItemInstance = null;
            OffhandItemDirector = null;
            _player.RuntimeData.OffhandItem = null;
        }

        // 清理运行时黑板上的所有 IK 引用与意图
        // 这个方法由两部分组成：
        // 1. 主清理逻辑：清理所有 IK 相关的黑板数据
        // 2. 防御性检查：防止武器清理不彻底导致的悬空引用
        //
        // 为什么需要两层清理？
        // 对象池失活时，Despawn() 在 ClearAllIKReferencesFromRuntimeData() 之前执行
        // 武器的 OnForceUnequip() 也可能有漏洞或异常
        // IKController 的 SanitizeAimReference() 是最后的兜底，但在 Update 中才触发
        //
        // 以及 unity 疑似重载了"==" 即使对象失活了 ik 系统可能还会继续持有引用
        //
        // 职责明确：EquipmentDriver 在物品销毁时负责彻底清理黑板
        private void ClearAllIKReferencesFromRuntimeData()
        {
            if (_player?.RuntimeData == null) return;

            _player.RuntimeData.WantsLeftHandIK = false;
            _player.RuntimeData.LeftHandGoal = null;

            _player.RuntimeData.WantsRightHandIK = false;
            _player.RuntimeData.RightHandGoal = null;

            _player.RuntimeData.WantsLookAtIK = false;
            _player.RuntimeData.CurrentAimReference = null;

            _player.RuntimeData.LookAtPosition = Vector3.zero;
        }
    }
}
