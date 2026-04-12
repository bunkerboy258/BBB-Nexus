using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 额外动作控制器 — 处理扩展行为输入
    /// 架构模式：纯 C# 类，由 BBBCharacterController 在 Awake 实例化，Update 调用
    ///
    /// 设计说明：
    /// - ExtraAction1~4 为通用槽位，语义由外部 IExtraActionService 实现定义
    /// - ExtraAction1~4 意图通过 IExtraActionService.PushIntents() 推送给外部服务
    /// - Reload / UseItem 为 BBBNexus 内部行为，直接处理
    ///
    /// 与 Expression 系统的区别：
    /// - Expression：专用于面部表情/动画（按键 6789）
    /// - ExtraAction：专用于游戏逻辑/特殊交互（独立绑定，语义外部定义）
    /// </summary>
    public class ExtraActionController
    {
        private const string DefaultQuickHealItemId = "BloodVial";

        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _runtimeData;

        // 状态追踪（边沿检测）
        private bool _lastReload;
        private bool _lastUseItem;
        private bool _lastToggleInventory;

        public ExtraActionController(BBBCharacterController player, PlayerRuntimeData runtimeData)
        {
            _player = player;
            _runtimeData = runtimeData;
        }

        public void Update()
        {
            ProcessReload();
            ProcessUseItem();
            ProcessToggleInventory();

            ExtraActionServiceRegistry.Current?.PushIntents(new ExtraActionIntents(
                _runtimeData.WantsExtraAction1,
                _runtimeData.WantsExtraAction2,
                _runtimeData.WantsExtraAction3,
                _runtimeData.WantsExtraAction4
            ));
        }

        public void TriggerQuickHealFromUi()
        {
            TryUseQuickHeal();
        }

        public int GetQuickHealItemCount()
        {
            var healItem = ResolveQuickHealItem();
            return healItem == null || _player?.InventoryService == null ? 0 : _player.InventoryService.GetCount(healItem);
        }

        private void ProcessReload()
        {
            bool currentIntent = _runtimeData.WantsReload;
            if (currentIntent && !_lastReload)
                TryManualReload();
            _lastReload = currentIntent;
        }

        private void ProcessUseItem()
        {
            bool currentIntent = _runtimeData.WantsUseItem;
            if (currentIntent && !_lastUseItem)
                TryUseQuickHeal();
            _lastUseItem = currentIntent;
        }

        private void ProcessToggleInventory()
        {
            bool currentIntent = _runtimeData.WantsToggleInventory;
            if (currentIntent && !_lastToggleInventory)
                _player?.InventoryOverlay?.Toggle();
            _lastToggleInventory = currentIntent;
        }

        private void TryManualReload()
        {
            if (_player?.EquipmentDriver?.CurrentItemDirector is not IManualReloadable reloadable)
            {
                return;
            }

            if (!reloadable.CanManualReload)
            {
                return;
            }

            int targetCount = _runtimeData != null ? _runtimeData.RequestedReloadTargetCount : -1;
            if (targetCount > 0 && reloadable is IAiReloadable aiReloadable)
            {
                aiReloadable.RequestManualReload(targetCount);
                return;
            }

            reloadable.RequestManualReload();
        }

        private void TryUseQuickHeal()
        {
            if (_player == null || _runtimeData == null || _runtimeData.IsDead)
            {
                return;
            }

            var healItem = ResolveQuickHealItem();
            if (healItem == null)
            {
                Debug.LogWarning($"[ExtraActionController] 未找到快捷治疗物 '{DefaultQuickHealItemId}'。", _player);
                return;
            }

            if (!healItem.AllowUseAtFullHealth && _runtimeData.CurrentHealth >= _runtimeData.MaxHealth - 0.01f)
            {
                ShowMessage(healItem.FullHealthMessageTitle, healItem.FullHealthMessageBody);
                return;
            }

            if (_player.InventoryService == null || !_player.InventoryService.TryRemove(healItem, 1))
            {
                ShowMessage(healItem.EmptyMessageTitle, healItem.EmptyMessageBody);
                return;
            }

            if (!_player.TryHeal(healItem.HealAmount))
            {
                _player.InventoryService.TryAdd(healItem, 1);
            }
        }

        private HealingItemSO ResolveQuickHealItem()
        {
            if (_player != null && _player.QuickHealItem != null)
            {
                return _player.QuickHealItem;
            }

            var item = MetaLib.GetObject<HealingItemSO>(DefaultQuickHealItemId);
            if (item != null)
            {
                return item;
            }

            var resources = Resources.LoadAll<HealingItemSO>(string.Empty);
            for (var i = 0; i < resources.Length; i++)
            {
                if (resources[i] != null && resources[i].ItemID == DefaultQuickHealItemId)
                {
                    return resources[i];
                }
            }

            return null;
        }

        private void ShowMessage(string title, string body)
        {
            if (_player?.ReadingOverlay == null)
            {
                return;
            }

            _player.ReadingOverlay.Show(title, body);
        }
    }
}
