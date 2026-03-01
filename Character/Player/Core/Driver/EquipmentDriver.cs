using Items.Core;
using Items.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    public class EquipmentDriver
    {
        private readonly PlayerController _player;

        public EquippableItemSO CurrentItemData { get; private set; }
        public ItemInstance CurrentItemInstance { get; private set; }
        public IHoldableItem CurrentItemDirector { get; private set; }

        private GameObject _currentWeaponInstance;

        public EquipmentDriver(PlayerController player)
        {
            _player = player;
        }

        public void EquipItem(ItemInstance itemInstance)
        {
            UnequipCurrentItem();

            CurrentItemInstance = itemInstance;
            CurrentItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;

            if (CurrentItemData == null)
            {
                // 空手
                _player?.NotifyEquipmentChanged();
                return;
            }

            if (CurrentItemData.Prefab != null && _player != null && _player.WeaponContainer != null)
            {
                _currentWeaponInstance = Object.Instantiate(CurrentItemData.Prefab, _player.WeaponContainer);

                // 应用生成偏移（局部空间）
                _currentWeaponInstance.transform.localPosition += CurrentItemData.HoldPositionOffset;
                _currentWeaponInstance.transform.localRotation = _currentWeaponInstance.transform.localRotation * CurrentItemData.HoldRotationOffset;

                CurrentItemDirector = _currentWeaponInstance.GetComponent<IHoldableItem>();
                CurrentItemDirector?.Initialize(CurrentItemInstance);
            }

            _player?.NotifyEquipmentChanged();
        }

        public void UnequipCurrentItem()
        {
            if (_currentWeaponInstance != null)
            {
                Object.Destroy(_currentWeaponInstance);
                _currentWeaponInstance = null;
            }

            CurrentItemData = null;
            CurrentItemInstance = null;
            CurrentItemDirector = null;
        }
    }
}