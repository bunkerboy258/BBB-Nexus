using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 额外动作控制器 — 处理扩展行为输入（如闭眼、特殊交互等）
    /// 架构模式：纯 C# 类，由 BBBCharacterController 在 Awake 实例化，Update 调用
    ///
    /// 设计说明：
    /// - 使用独立的 ExtraAction1-4 输入槽位（与 Expression 系统完全分离）
    /// - ExtraAction1：闭眼交互（Toggle 状态）
    /// - ExtraAction2-4：预留未来扩展（如特殊技能、情境动作等）
    ///
    /// 与 Expression 系统的区别：
    /// - Expression：专用于面部表情/动画（按键 6789）
    /// - ExtraAction：专用于游戏逻辑/特殊交互（独立绑定）
    ///
    /// 扩展方式：
    /// - 新增额外动作时，添加新的 ProcessExtraActionX 方法
    /// - 在 WantsExtraActionX 中存储意图
    /// - 在 ExtraActionController 中添加具体实现
    /// </summary>
    public class ExtraActionController
    {
        private const string DefaultQuickHealItemId = "BloodVial";

        private readonly BBBCharacterController _player;
        private readonly PlayerRuntimeData _runtimeData;
        private readonly EyesClosedSystemManager _eyesClosedManager;

        // 状态追踪（边沿检测）
        private bool _lastToggleEyes;
        private bool _lastReload;
        private bool _lastUseItem;
        private bool _lastOpenInventory;
        private bool _lastExtraAction4;

        public ExtraActionController(BBBCharacterController player, PlayerRuntimeData runtimeData, EyesClosedSystemManager eyesClosedManager)
        {
            _player = player;
            _runtimeData = runtimeData;
            _eyesClosedManager = eyesClosedManager;
        }

        public void Update()
        {
            ProcessToggleEyes();
            ProcessReload();
            ProcessUseItem();
            ProcessOpenInventory();
            ProcessExtraAction4();
        }

        private void ProcessToggleEyes()
        {
            bool currentIntent = _runtimeData.WantsToggleEyes;
            if (currentIntent && !_lastToggleEyes)
                _eyesClosedManager?.ForceSetEyesClosed(!_eyesClosedManager.IsEyesClosed);
            _lastToggleEyes = currentIntent;
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

        private void ProcessOpenInventory()
        {
            bool currentIntent = _runtimeData.WantsOpenInventory;
            if (currentIntent && !_lastOpenInventory)
            {
                Debug.Log($"[InventoryTrace] frame={Time.frameCount} ProcessOpenInventory edge overlayPresent={_player?.InventoryOverlay != null} isOpen={_player?.InventoryOverlay?.IsOpen ?? false}", _player);
                _player?.InventoryOverlay?.Toggle();
            }
            _lastOpenInventory = currentIntent;
        }

        private void ProcessExtraAction4()
        {
            bool currentIntent = _runtimeData.WantsExtraAction4;
            if (currentIntent && !_lastExtraAction4)
                Debug.Log("[ExtraActionController] ExtraAction4 触发（预留）");
            _lastExtraAction4 = currentIntent;
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

            if (!ItemPackVfs.TryConsumeItem(healItem.ItemID, 1, _player))
            {
                ShowMessage(healItem.EmptyMessageTitle, healItem.EmptyMessageBody);
                return;
            }

            if (!_player.TryHeal(healItem.HealAmount))
            {
                ItemPackVfs.TryAddItem(healItem.ItemID, 1, _player);
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
