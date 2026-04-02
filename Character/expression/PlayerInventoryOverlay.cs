using UnityEngine;

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

        private BBBCharacterController _player;
        private InventorySnapshot _snapshot;
        private int _selectedIndex;
        private string _statusMessage = string.Empty;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _accentStyle;
        private GUIStyle _selectedStyle;
        private int _openedFrame = -1;

        public bool IsOpen { get; private set; }

        public void Initialize(BBBCharacterController player)
        {
            _player = player;
        }

        public void Toggle()
        {
            Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Toggle currentIsOpen={IsOpen}", this);
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
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
            Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Open success slotCount={_snapshot?.Slots?.Count ?? 0}", this);
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
            {
                _player.RuntimeData.IsInventoryOpen = false;
            }
            RestoreCursorLock();
            Debug.Log($"[InventoryTrace] frame={Time.frameCount} Overlay.Close success", this);
        }

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
            if (_player == null)
            {
                return null;
            }

            var cameraTransform = _player.PlayerCamera;
            if (cameraTransform != null)
            {
                var manager = cameraTransform.GetComponentInParent<PlayerCameraManager>();
                if (manager != null)
                {
                    return manager;
                }
            }

            return FindObjectOfType<PlayerCameraManager>();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (_player == null || _player.RuntimeData == null || _player.RuntimeData.IsDead)
            {
                Close();
                return;
            }

            if (Time.frameCount == _openedFrame)
            {
                return;
            }

            if (Input.GetKeyDown(_closeKey) || Input.GetKeyDown(_altCloseKey))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveSelection(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveSelection(1);
            }

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
            {
                UseSelectedSlot();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) { AssignSelectedToMainSlot(1); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { AssignSelectedToMainSlot(2); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { AssignSelectedToMainSlot(3); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { AssignSelectedToMainSlot(4); }
            if (Input.GetKeyDown(KeyCode.Alpha5)) { AssignSelectedToMainSlot(5); }

            if (Input.GetKeyDown(KeyCode.U))
            {
                if (PlayerInventoryService.TryUnequipMainHand(_player, out var message))
                {
                    _statusMessage = message;
                    RefreshSnapshot();
                }
                else if (!string.IsNullOrWhiteSpace(message))
                {
                    _statusMessage = message;
                }
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !IsOpen)
            {
                return;
            }

            EnsureStyles();

            var panel = new Rect(
                (Screen.width - _panelWidth) * 0.5f,
                (Screen.height - _panelHeight) * 0.5f,
                _panelWidth,
                _panelHeight);

            GUI.color = _panelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            GUI.color = _borderColor;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - 1f, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.xMax - 1f, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleRect = new Rect(panel.x + _padding.x, panel.y + _padding.y, panel.width - _padding.x * 2f, 28f);
            GUI.Label(titleRect, "Inventory", _titleStyle);

            var equipmentRect = new Rect(panel.x + _padding.x, panel.y + 56f, panel.width - _padding.x * 2f, 90f);
            DrawEquipmentSummary(equipmentRect);

            var listRect = new Rect(panel.x + _padding.x, panel.y + 160f, panel.width - _padding.x * 2f, panel.height - 240f);
            DrawInventoryList(listRect);

            var footerRect = new Rect(panel.x + _padding.x, panel.yMax - 56f, panel.width - _padding.x * 2f, 40f);
            DrawFooter(footerRect);
        }

        private void DrawEquipmentSummary(Rect rect)
        {
            if (_snapshot == null)
            {
                RefreshSnapshot();
            }

            var line1 = $"MainHand: {FormatItemId(_snapshot?.MainHandItemId)}";
            var line2 = $"OffHand:  {FormatItemId(_snapshot?.OffHandItemId)}";
            var hotbar = "MainSlots: ";
            for (var i = 0; i < 5; i++)
            {
                var itemId = _snapshot != null && _snapshot.MainSlotItemIds != null && i < _snapshot.MainSlotItemIds.Length
                    ? _snapshot.MainSlotItemIds[i]
                    : null;
                if (i > 0)
                {
                    hotbar += "   ";
                }

                var label = string.Equals(itemId, EquipmentPackVfs.MainSlotOccupierId, System.StringComparison.Ordinal)
                    ? "[Equipped]"
                    : FormatItemId(itemId);
                hotbar += $"{i + 1}:{label}";
            }

            GUI.Label(new Rect(rect.x, rect.y, rect.width, 24f), line1, _accentStyle);
            GUI.Label(new Rect(rect.x, rect.y + 24f, rect.width, 24f), line2, _textStyle);
            GUI.Label(new Rect(rect.x, rect.y + 52f, rect.width, 24f), hotbar, _textStyle);
        }

        private void DrawInventoryList(Rect rect)
        {
            if (_snapshot == null)
            {
                RefreshSnapshot();
            }

            var slots = _snapshot != null ? _snapshot.Slots : null;
            if (slots == null || slots.Count == 0)
            {
                GUI.Label(rect, "背包为空。", _textStyle);
                return;
            }

            const float lineHeight = 24f;
            for (var i = 0; i < slots.Count; i++)
            {
                var rowRect = new Rect(rect.x, rect.y + i * lineHeight, rect.width, lineHeight);
                if (_selectedIndex == i)
                {
                    GUI.color = _selectedColor;
                    GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                var slot = slots[i];
                var typeLabel = slot.IsConsumable ? "Use" : (slot.IsEquippable ? "Equip" : "Item");
                var text = $"{slot.SlotName,3}  {slot.DisplayName}  x{slot.Data.Count}  [{typeLabel}]";
                GUI.Label(new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width - 12f, rowRect.height), text, _textStyle);
            }
        }

        private void DrawFooter(Rect rect)
        {
            var hint = "[W/S] 选择  [E/Enter] 使用/装备  [1-5] 放入并切换主手槽  [U] 卸下主手  [Tab/Esc] 关闭";
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), hint, _hintStyle);
            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 20f), _statusMessage, _accentStyle);
            }
        }

        private void MoveSelection(int delta)
        {
            RefreshSnapshot();
            var count = _snapshot != null ? _snapshot.Slots.Count : 0;
            if (count <= 0)
            {
                _selectedIndex = 0;
                return;
            }

            _selectedIndex += delta;
            if (_selectedIndex < 0)
            {
                _selectedIndex = count - 1;
            }
            else if (_selectedIndex >= count)
            {
                _selectedIndex = 0;
            }
        }

        private void UseSelectedSlot()
        {
            RefreshSnapshot();
            var slot = GetSelectedSlot();
            if (slot == null)
            {
                _statusMessage = "当前没有可操作的背包槽位。";
                return;
            }

            if (PlayerInventoryService.TryUseSlot(_player, slot.SlotName, out var message))
            {
                _statusMessage = message;
                RefreshSnapshot();
                ClampSelection();
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private void AssignSelectedToMainSlot(int mainSlotIndex)
        {
            RefreshSnapshot();
            var slot = GetSelectedSlot();
            if (slot == null)
            {
                _statusMessage = "当前没有可操作的背包槽位。";
                return;
            }

            if (PlayerInventoryService.TryAssignInventorySlotToMainSlot(_player, slot.SlotName, mainSlotIndex, autoEquip: true, out var message))
            {
                _statusMessage = message;
                RefreshSnapshot();
                ClampSelection();
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        private InventorySlotSnapshot GetSelectedSlot()
        {
            if (_snapshot == null || _snapshot.Slots == null || _snapshot.Slots.Count == 0)
            {
                return null;
            }

            ClampSelection();
            return _snapshot.Slots[_selectedIndex];
        }

        private void RefreshSnapshot()
        {
            if (_player == null)
            {
                _snapshot = null;
                return;
            }

            _snapshot = PlayerInventoryService.BuildSnapshot(_player);
            ClampSelection();
        }

        private void ClampSelection()
        {
            var count = _snapshot != null && _snapshot.Slots != null ? _snapshot.Slots.Count : 0;
            if (count <= 0)
            {
                _selectedIndex = 0;
                return;
            }

            if (_selectedIndex >= count)
            {
                _selectedIndex = count - 1;
            }
            else if (_selectedIndex < 0)
            {
                _selectedIndex = 0;
            }
        }

        private string FormatItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return "-";
            }

            var definition = MetaLib.GetObject<ItemDefinitionSO>(itemId);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            return itemId;
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                _titleStyle.normal.textColor = _titleColor;
            }

            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft
                };
                _textStyle.normal.textColor = _textColor;
            }

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft
                };
                _hintStyle.normal.textColor = _hintColor;
            }

            if (_accentStyle == null)
            {
                _accentStyle = new GUIStyle(_textStyle);
                _accentStyle.normal.textColor = _accentColor;
            }

            if (_selectedStyle == null)
            {
                _selectedStyle = new GUIStyle(_textStyle);
            }
        }
    }
}
