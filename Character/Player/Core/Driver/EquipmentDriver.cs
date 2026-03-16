using Items.Core;
using Items.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    // 装备驱动器 
    // 职责是根据物品实例 在挂点上生成对应的模型
    // 完成了从配置注入到依赖注入的完整闭环
    public class EquipmentDriver
    {
        // 宿主控制器引用 用于访问挂点和分发全局通知
        private readonly PlayerController _player;

        // 运行时缓存的离线配置数据 方便状态机快速检索参数
        public EquippableItemSO CurrentItemData { get; private set; }
        // 当前持有的物品实例 它是黑板中数据的唯一源头
        public ItemInstance CurrentItemInstance { get; private set; }
        // 当前持有的物品逻辑接口 用于跨系统驱动表现层行为
        public IHoldableItem CurrentItemDirector { get; private set; }

        // 运行时生成的模型实例引用 
        private GameObject _currentWeaponInstance;

        public EquipmentDriver(PlayerController player)
        {
            _player = player;
        }

        // 核心装配逻辑 执行从数据到实体的转化
        public void EquipItem(ItemInstance itemInstance)
        {
            // 第一步必须强制卸载旧物品 否则会导致双持模型或逻辑重叠 Bug
            UnequipCurrentItem();

            // 缓存传入的物品实例 准备进行配置注入
            CurrentItemInstance = itemInstance;
            CurrentItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;

            if (CurrentItemData == null)
            {
                // 如果没有有效配置则判定为空手状态 同步 UI 状态
                Debug.Log("驱动器判定当前为空手状态 正在重置表现层");
                _player?.NotifyEquipmentChanged();
                return;
            }

            // 执行模型实例化流程 将模型至预设的武器容器挂点
            if (CurrentItemData.Prefab != null && _player != null && _player.WeaponContainer != null)
            {
                _currentWeaponInstance = Object.Instantiate(CurrentItemData.Prefab, _player.WeaponContainer);

                // 根据离线配置修正模型相对位置 
                // 这里不能用+= !!! 因为prefab类型在生成的时候是默认带创建时的世界坐标的 直接覆盖才能保证正确的偏移
                _currentWeaponInstance.transform.localPosition = CurrentItemData.HoldPositionOffset;
                _currentWeaponInstance.transform.localRotation = CurrentItemData.HoldRotationOffset;

                // 如果预制件本身的缩放不为1 也会导致各种奇怪的问题
                if (_currentWeaponInstance.transform.localScale != Vector3.one) Debug.LogWarning("检测到预制件缩放异常 建议检查离线配置");

                // 提取控制接口 
                CurrentItemDirector = _currentWeaponInstance.GetComponent<IHoldableItem>();

                // 执行核心注入 将物品实例强行塞进生成的模型脚本中
                // 这一步让模型肉体获得了黑板中的实例数据(也就是 如果它被修改过属性 这里重新注入进去)
                CurrentItemDirector?.Initialize(CurrentItemInstance);

                if (CurrentItemDirector == null)
                {
                    Debug.LogWarning("生成的模型缺少控制接口 状态机将无法驱动该武器");
                }
                else
                {
                    // 驱动初始持握逻辑 触发初始化表现
                    //Debug.Log("装备装配成功 正在启动拔枪流程");
                    CurrentItemDirector.OnEquipEnter(_player);
                }
            }
            else
            {
                Debug.LogWarning("装配失败 检查预制件引用或容器挂点是否丢失");
            }

            // 必须通知宿主装备已变更 保证 UI 注册表同步刷新
            _player?.NotifyEquipmentChanged();
        }

        // 强制卸载流程 保证内存与逻辑的干净回收
        public void UnequipCurrentItem()
        {
            // 销毁肉体前必须让逻辑接口执行清理 否则 IK 或粒子音效会残留报错
            if (CurrentItemDirector != null)
            {
                Debug.Log("执行强制下线逻辑 正在清理 IK 调度与协程任务");
                CurrentItemDirector.OnForceUnequip();
            }

            // 执行物理销毁 
            if (_currentWeaponInstance != null)
            {
                Object.Destroy(_currentWeaponInstance);
                _currentWeaponInstance = null;
            }

            // 通知变更 (不然下一次按同一快捷键将无法触发意图管线的激活)
            _player?.NotifyEquipmentChanged();

            // 彻底清空缓存 
            CurrentItemData = null;
            CurrentItemInstance = null;
            CurrentItemDirector = null;
        }
    }
}