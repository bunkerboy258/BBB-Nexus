using UnityEngine;

namespace BBBNexus
{
    // 装备驱动器 负责生成模型注入数据驱动逻辑
    public class EquipmentDriver
    {
        private readonly BBBCharacterController _player;
        // 当前物品配置
        public EquippableItemSO CurrentItemData { get; private set; }
        // 当前物品实例
        public ItemInstance CurrentItemInstance { get; private set; }
        // 当前物品逻辑接口
        public IHoldableItem CurrentItemDirector { get; private set; }
        // 当前模型实例
        private GameObject _currentWeaponInstance;

        public EquipmentDriver(BBBCharacterController player)
        {
            _player = player;
        }

        // 装配物品生成模型注入数据驱动逻辑
        public void EquipItem(ItemInstance itemInstance)
        {
            UnequipCurrentItem();
            CurrentItemInstance = itemInstance;
            CurrentItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;
            if (CurrentItemData == null)
            {
                Debug.Log("驱动器判定当前为空手状态 正在重置表现层");
                _player?.NotifyEquipmentChanged();
                return;
            }

            if (CurrentItemData.Prefab != null && _player != null && _player.WeaponContainer != null)
            {
                var prefab = CurrentItemData.Prefab;

                if (SimpleObjectPoolSystem.Shared != null)
                {
                    _currentWeaponInstance = SimpleObjectPoolSystem.Shared.Spawn(prefab);
                    _currentWeaponInstance.transform.SetParent(_player.WeaponContainer, false);
                    _currentWeaponInstance.transform.localPosition = Vector3.zero;
                    _currentWeaponInstance.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    _currentWeaponInstance = Object.Instantiate(prefab, _player.WeaponContainer);
                }

                // 复用对象必须重置 localScale，避免上一次使用遗留缩放导致异常（甚至把角色一起缩没）
                _currentWeaponInstance.transform.localScale = Vector3.one;

                _currentWeaponInstance.transform.localPosition = CurrentItemData.HoldPositionOffset;
                _currentWeaponInstance.transform.localRotation = CurrentItemData.HoldRotationOffset;
                if (_currentWeaponInstance.transform.localScale != Vector3.one) Debug.LogWarning("检测到预制件缩放异常 建议检查prefab配置");
                CurrentItemDirector = _currentWeaponInstance.GetComponent<IHoldableItem>();
                CurrentItemDirector?.Initialize(CurrentItemInstance);
                if (CurrentItemDirector == null)
                {
                    Debug.LogWarning("生成的模型缺少控制接口 状态机将无法驱动该武器");
                }
            }
            else
            {
                Debug.LogWarning("装配失败 检查预制件引用或容器挂点是否丢失");
            }
            _player?.NotifyEquipmentChanged();
        }

        // 卸载当前物品销毁模型清理逻辑
        public void UnequipCurrentItem()
        {
            if (_currentWeaponInstance != null)
            {
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    SimpleObjectPoolSystem.Shared.Despawn(_currentWeaponInstance);
                }
                else
                {
                    Object.Destroy(_currentWeaponInstance);
                }
                _currentWeaponInstance = null;
            }
            
            // 清理黑板上的所有 IK 引用与意图，避免切装备时 IK 继续追踪失活的目标
            // 这是统一的清理点，保证即使武器的OnForceUnequip有漏洞也能兜底
            ClearAllIKReferencesFromRuntimeData();
            
            _player.NotifyEquipmentChanged();
            CurrentItemData = null;
            CurrentItemInstance = null;
            CurrentItemDirector = null;
        }

        // 清理运行时黑板上的所有 IK 引用与意图
        // 这个方法由两部分组成：
        // 1. 主清理逻辑：清理所有IK相关的黑板数据
        // 2. 防御性检查：防止武器清理不彻底导致的悬空引用
        // 
        // 为什么需要两层清理？
        // 对象池失活时，Despawn() 在 ClearAllIKReferencesFromRuntimeData() 之前执行
        // 武器的 OnForceUnequip() 也可能有漏洞或异常
        // IKController 的 SanitizeAimReference() 是最后的兜底，但在 Update 中才触发
        // 
        // 以及 unity疑似重载了"==" 即使对象失活了 ik系统可能还会继续持有引用
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