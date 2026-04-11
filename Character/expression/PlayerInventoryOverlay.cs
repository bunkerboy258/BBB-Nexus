using UnityEngine;
using System.Collections.Generic;

namespace BBBNexus
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventoryOverlay : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] private KeyCode _closeKey = KeyCode.Tab;
        [SerializeField] private KeyCode _altCloseKey = KeyCode.Escape;

        [Header("Layout")]
        [SerializeField] private float _panelWidth = 760f;
        [SerializeField] private float _panelHeight = 520f;
        [SerializeField] private Vector2 _padding = new Vector2(18f, 16f);

        [Header("Style")]
        [SerializeField] private Color _panelColor = new Color(0.05f, 0.05f, 0.06f, 0.92f);
        [SerializeField] private Color _borderColor = new Color(0.88f, 0.88f, 0.88f, 0.16f);
        [SerializeField] private Color _titleColor = new Color(0.96f, 0.92f, 0.76f, 1f);
        [SerializeField] private Color _textColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.22f, 0.24f, 0.29f, 0.96f);
        [SerializeField] private Color _hintColor = new Color(0.66f, 0.66f, 0.66f, 1f);
        [SerializeField] private Color _accentColor = new Color(0.78f, 0.70f, 0.42f, 1f);

        // ──────────────── 快照数据结构 ────────────────

        private sealed class ItemSlot
        {
            public string ItemId;
            public int Count;
            public ItemDefinitionSO Definition;
            public bool IsEquippable => Definition is EquippableItemSO;
            public bool IsConsumable => Definition is HealingItemSO;
            public string DisplayName => Definition != null && !string.IsNullOrWhiteSpace(Definition.DisplayName)
                ? Definition.DisplayName : ItemId ?? string.Empty;
        }

        private sealed class OverlaySnapshot
        {
            public List<ItemSlot> Items = new();
            public string MainHandItemId;
            public string OffHandItemId;
            public string[] MainSlotItemIds = new string[5];
            public int OccupiedMainSlotIndex = -1; // 1-based，-1 表示无
        }

        // ──────────────── 状态 ────────────────

        private BBBCharacterController _player;
        private OverlaySnapshot _snapshot;
        private int _selectedIndex;
        private string _statusMessage = string.Empty;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _accentStyle;
        private int _openedFrame = -1;

        public bool IsOpen { get; private set; }

        // ──────────────── 生命周期 ────────────────

        public void Initialize(BBBCharacterController player)
        {
            _player = player;
        }

        public void Toggle()
        {
            Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Toggle currentIsOpen={IsOpen}", this);
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (IsOpen || _player == null || _player.RuntimeData == null)
            {
                Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Open aborted isOpen={IsOpen} playerValid={_player != null} runtimeValid={_player?.RuntimeData != null}", this);
                return;
            }

            IsOpen = true;
            _player.RuntimeData.IsInventoryOpen = true;
            _openedFrame = Time.frameCount;
            SetCursorUnlocked();
            RefreshSnapshot();
            Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Open success itemCount={_snapshot?.Items?.Count ?? 0}", this);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Close skipped because already closed.", this);
                return;
            }

            IsOpen = false;
            _openedFrame = -1;
            if (_player != null && _player.RuntimeData != null)
                _player.RuntimeData.IsInventoryOpen = false;
            RestoreCursorLock();
            Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Close success", this);
        }

        // ──────────────── Update ────────────────

        private void Update()
        {
            if (!IsOpen) return;

            if (_player == null || _player.RuntimeData == null || _player.RuntimeData.IsDead)
            {
                Close();
                return;
            }

            if (Time.frameCount == _openedFrame) return;

            if (Input.GetKeyDown(_closeKey) || Input.GetKeyDown(_altCloseKey))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                MoveSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                MoveSelection(1);

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                UseSelectedSlot();

            if (Input.GetKeyDown(KeyCode.Alpha1)) AssignSelectedToMainSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) AssignSelectedToMainSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) AssignSelectedToMainSlot(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) AssignSelectedToMainSlot(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) AssignSelectedToMainSlot(5);

            if (Input.GetKeyDown(KeyCode.U))
                TryUnequipMainHand();
        }

        // ──────────────── 操作 ────────────────

        private void UseSelectedSlot()
        {
            RefreshSnapshot();
            var slot = GetSelectedSlot();
            if (slot == null) { _statusMessage = "当前没有可操作的背包槽位。"; return; }

            if (slot.IsConsumable && slot.Definition is HealingItemSO healing)
            {
                TryUseHealingItem(slot, healing);
                return;
            }

            if (slot.IsEquippable)
            {
                var targetSlot = ResolvePreferredMainSlot();
                AssignSelectedToMainSlot(targetSlot);
                return;
            }

            _statusMessage = "该物品当前没有可执行的直接使用逻辑。";
        }

        private void TryUseHealingItem(ItemSlot slot, HealingItemSO healing)
        {
            var inv = _player.InventoryService;
            if (inv == null) { _statusMessage = "库存服务未初始化。"; return; }

            if (!healing.AllowUseAtFullHealth && _player.RuntimeData.CurrentHealth >= _player.RuntimeData.MaxHealth - 0.01f)
            {
                _statusMessage = string.IsNullOrWhiteSpace(healing.FullHealthMessageBody) ? "当前生命值已满。" : healing.FullHealthMessageBody;
                return;
            }

            if (!inv.TryRemove(healing, 1))
            {
                _statusMessage = string.IsNullOrWhiteSpace(healing.EmptyMessageBody) ? "该治疗物品已不足。" : healing.EmptyMessageBody;
                return;
            }

            if (!_player.TryHeal(healing.HealAmount))
            {
                inv.TryAdd(healing, 1);
                _statusMessage = "治疗失败，物品已回退。";
                return;
            }

            _statusMessage = $"使用了 {slot.DisplayName}。";
            RefreshSnapshot();
            ClampSelection();
        }

        private void AssignSelectedToMainSlot(int mainSlotIndex)
        {
            if (mainSlotIndex < 1 || mainSlotIndex > 5) return;

            RefreshSnapshot();
            var slot = GetSelectedSlot();
            if (slot == null) { _statusMessage = "当前没有可操作的背包槽位。"; return; }
            if (!slot.IsEquippable) { _statusMessage = "只有可装备物品才能放进快捷主手槽。"; return; }

            var inv = _player.InventoryService;
            var equip = _player.EquipmentService;
            if (inv == null || equip == null) { _statusMessage = "服务未初始化。"; return; }

            // 如果目标槽已有旧装备，先放回背包
            var oldItemId = equip.GetEquippedSO($"config:weapon{mainSlotIndex}");
            if (!string.IsNullOrWhiteSpace(oldItemId))
            {
                var oldSO = MetaLib.GetObject<ItemDefinitionSO>(oldItemId);
                if (oldSO != null) inv.TryAdd(oldSO, 1);
            }

            // 从背包移除新装备
            if (!inv.TryRemove(slot.Definition, 1))
            {
                _statusMessage = "移除背包物品失败。";
                return;
            }

            // 写入配置槽
            equip.TrySetEquipSO($"config:weapon{mainSlotIndex}", slot.ItemId);

            // 直接触发装备切换（从配置槽复制到实例槽并实例化）
            var itemId = equip.GetConfigSlotItemId(mainSlotIndex - 1);
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                equip.TryEquipFromConfig(mainSlotIndex - 1);
                // 触发实际装备实例化
                var itemSO = MetaLib.GetObject<EquippableItemSO>(itemId);
                if (itemSO != null)
                {
                    var instance = new ItemInstance(itemSO, null, 1);
                    _player.EquipmentDriver.EquipItemToSlot(instance, EquipmentSlot.MainHand);
                    _player.RuntimeData.CurrentItem = instance;
                }
                _statusMessage = $"已放入主手槽 {mainSlotIndex} 并切换装备。";
            }
            else
            {
                _statusMessage = $"写入配置槽成功，但获取装备信息失败。";
            }
            RefreshSnapshot();
            ClampSelection();
        }

        private void TryUnequipMainHand()
        {
            if (_snapshot == null || _snapshot.OccupiedMainSlotIndex < 1) { _statusMessage = "当前没有可卸下的主手装备。"; return; }
            if (_player.RuntimeData != null && _player.RuntimeData.Arbitration.BlockInventory) { _statusMessage = "当前状态不允许切换装备。"; return; }

            // 直接卸下主手装备
            var equip = _player.EquipmentService;
            if (equip != null && equip.TryUnequipMainHand())
            {
                _player.EquipmentDriver.UnequipMainhand();
                _player.RuntimeData.CurrentItem = null;
                _statusMessage = "主手装备已卸下。";
            }
            else
            {
                _statusMessage = "卸下装备失败。";
            }
            RefreshSnapshot();
        }

        // ──────────────── 快照构建 ────────────────

        private void RefreshSnapshot()
        {
            if (_player == null) { _snapshot = null; return; }
            _snapshot = BuildSnapshot();
            ClampSelection();
        }

        private OverlaySnapshot BuildSnapshot()
        {
            var snap = new OverlaySnapshot();

            var inv = _player.InventoryService;
            if (inv != null)
            {
                var allItems = inv.GetAllItems();
                foreach (var pair in allItems)
                {
                    var so = MetaLib.GetObject<ItemDefinitionSO>(pair.Key);
                    if (so == null) continue;
                    snap.Items.Add(new ItemSlot { ItemId = pair.Key, Count = pair.Value, Definition = so });
                }
                snap.Items.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
            }

            var equip = _player.EquipmentService;
            if (equip != null)
            {
                snap.MainHandItemId = equip.GetEquippedSO("instance:mainhand");
                snap.OffHandItemId = equip.GetEquippedSO("instance:offhand");
                for (int i = 1; i <= 5; i++)
                    snap.MainSlotItemIds[i - 1] = equip.GetEquippedSO($"config:weapon{i}");

                if (!string.IsNullOrWhiteSpace(snap.MainHandItemId))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if (snap.MainSlotItemIds[i] == snap.MainHandItemId)
                        {
                            snap.OccupiedMainSlotIndex = i + 1;
                            break;
                        }
                    }
                }
            }

            return snap;
        }

        // ──────────────── 辅助 ────────────────

        private int ResolvePreferredMainSlot()
        {
            if (_snapshot == null) return 1;
            if (_snapshot.OccupiedMainSlotIndex >= 1) return _snapshot.OccupiedMainSlotIndex;
            for (int i = 0; i < 5; i++)
                if (string.IsNullOrWhiteSpace(_snapshot.MainSlotItemIds[i])) return i + 1;
            return 1;
        }

        private ItemSlot GetSelectedSlot()
        {
            if (_snapshot == null || _snapshot.Items == null || _snapshot.Items.Count == 0) return null;
            ClampSelection();
            return _snapshot.Items[_selectedIndex];
        }

        private void MoveSelection(int delta)
        {
            RefreshSnapshot();
            var count = _snapshot?.Items?.Count ?? 0;
            if (count <= 0) { _selectedIndex = 0; return; }
            _selectedIndex = (_selectedIndex + delta + count) % count;
        }

        private void ClampSelection()
        {
            var count = _snapshot?.Items?.Count ?? 0;
            if (count <= 0) { _selectedIndex = 0; return; }
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, count - 1);
        }

        private string FormatItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return "-";
            var so = MetaLib.GetObject<ItemDefinitionSO>(itemId);
            return so != null && !string.IsNullOrWhiteSpace(so.DisplayName) ? so.DisplayName : itemId;
        }

        // ──────────────── GUI ────────────────

        private void OnGUI()
        {
            if (!Application.isPlaying || !IsOpen) return;

            EnsureStyles();

            var panel = new Rect(
                (Screen.width - _panelWidth) * 0.5f,
                (Screen.height - _panelHeight) * 0.5f,
                _panelWidth, _panelHeight);

            GUI.color = _panelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            GUI.color = _borderColor;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - 1f, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.xMax - 1f, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + _padding.x, panel.y + _padding.y, panel.width - _padding.x * 2f, 28f), "Inventory", _titleStyle);
            DrawEquipmentSummary(new Rect(panel.x + _padding.x, panel.y + 56f, panel.width - _padding.x * 2f, 90f));
            DrawInventoryList(new Rect(panel.x + _padding.x, panel.y + 160f, panel.width - _padding.x * 2f, panel.height - 240f));
            DrawFooter(new Rect(panel.x + _padding.x, panel.yMax - 56f, panel.width - _padding.x * 2f, 40f));
        }

        private void DrawEquipmentSummary(Rect rect)
        {
            if (_snapshot == null) RefreshSnapshot();

            var line1 = $"MainHand: {FormatItemId(_snapshot?.MainHandItemId)}";
            var line2 = $"OffHand:  {FormatItemId(_snapshot?.OffHandItemId)}";
            var hotbar = "MainSlots: ";
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) hotbar += "   ";
                var itemId = _snapshot?.MainSlotItemIds?[i];
                var isEquipped = (i + 1) == _snapshot?.OccupiedMainSlotIndex;
                var label = isEquipped ? "[Equipped]" : FormatItemId(itemId);
                hotbar += $"{i + 1}:{label}";
            }

            GUI.Label(new Rect(rect.x, rect.y, rect.width, 24f), line1, _accentStyle);
            GUI.Label(new Rect(rect.x, rect.y + 24f, rect.width, 24f), line2, _textStyle);
            GUI.Label(new Rect(rect.x, rect.y + 52f, rect.width, 24f), hotbar, _textStyle);
        }

        private void DrawInventoryList(Rect rect)
        {
            if (_snapshot == null) RefreshSnapshot();

            var items = _snapshot?.Items;
            if (items == null || items.Count == 0)
            {
                GUI.Label(rect, "背包为空。", _textStyle);
                return;
            }

            const float lineHeight = 24f;
            for (int i = 0; i < items.Count; i++)
            {
                var rowRect = new Rect(rect.x, rect.y + i * lineHeight, rect.width, lineHeight);
                if (_selectedIndex == i)
                {
                    GUI.color = _selectedColor;
                    GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                var item = items[i];
                var typeLabel = item.IsConsumable ? "Use" : (item.IsEquippable ? "Equip" : "Item");
                var text = $"{item.DisplayName}  x{item.Count}  [{typeLabel}]";
                GUI.Label(new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width - 12f, rowRect.height), text, _textStyle);
            }
        }

        private void DrawFooter(Rect rect)
        {
            var hint = "[W/S] 选择  [E/Enter] 使用/装备  [1-5] 放入并切换主手槽  [U] 卸下主手  [Tab/Esc] 关闭";
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), hint, _hintStyle);
            if (!string.IsNullOrWhiteSpace(_statusMessage))
                GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 20f), _statusMessage, _accentStyle);
        }

        // ──────────────── 光标 ────────────────

        private void SetCursorUnlocked()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreCursorLock()
        {
            var cameraManager = ResolveCameraManager();
            if (cameraManager != null)
            {
                if (cameraManager.HideCursorOnPlay)
                {
                    Cursor.visible = false;
                    Cursor.lockState = cameraManager.CursorLock;
                }
                else
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                return;
            }
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private PlayerCameraManager ResolveCameraManager()
        {
            if (_player == null) return null;
            var cameraTransform = _player.PlayerCamera;
            if (cameraTransform != null)
            {
                var manager = cameraTransform.GetComponentInParent<PlayerCameraManager>();
                if (manager != null) return manager;
            }
            return FindObjectOfType<PlayerCameraManager>();
        }

        // ──────────────── 样式 ────────────────

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                _titleStyle.normal.textColor = _titleColor;
            }
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
                _textStyle.normal.textColor = _textColor;
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
                _hintStyle.normal.textColor = _hintColor;
            }
            if (_accentStyle == null)
            {
                _accentStyle = new GUIStyle(_textStyle);
                _accentStyle.normal.textColor = _accentColor;
            }
        }
    }
}
