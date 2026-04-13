using UnityEngine;

namespace BBBNexus
{
    [DisallowMultipleComponent]
    public sealed class QuickHealOverlay : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private BBBCharacterController _player;

        [Header("Layout")]
        [SerializeField] private Vector2 _size = new Vector2(168f, 56f);
        [SerializeField] private Vector2 _screenOffset = new Vector2(20f, 20f);

        [Header("Style")]
        [SerializeField] private Color _panelColor = new Color(0.06f, 0.06f, 0.07f, 0.84f);
        [SerializeField] private Color _borderColor = new Color(0.92f, 0.92f, 0.92f, 0.18f);
        [SerializeField] private Color _textColor = new Color(0.94f, 0.92f, 0.86f, 1f);
        [SerializeField] private Color _countColor = new Color(0.82f, 0.72f, 0.42f, 1f);
        [SerializeField] private Color _disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.92f);

        private GUIStyle _buttonStyle;
        private GUIStyle _countStyle;
        private Texture2D _pixel;

        public void Initialize(BBBCharacterController player)
        {
            _player = player;
        }

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null)
            {
                Destroy(_pixel);
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || _player == null || _player.RuntimeData == null)
            {
                return;
            }

            if (_player.RuntimeData.IsDead)
            {
                return;
            }

            if (_player.InventoryOverlay != null && _player.InventoryOverlay.IsOpen)
            {
                return;
            }

            if (_player.ReadingOverlay != null && _player.ReadingOverlay.IsOpen)
            {
                return;
            }

            if (_player.ExtraActionController == null)
            {
                return;
            }

            EnsureStyles();

            int count = _player.ExtraActionController.GetQuickHealItemCount();
            var rect = new Rect(
                Screen.width - _size.x - _screenOffset.x,
                Screen.height - _size.y - _screenOffset.y,
                _size.x,
                _size.y);

            GUI.color = _panelColor;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = _borderColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), _pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), _pixel);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), _pixel);
            GUI.color = Color.white;

            bool canUse = count > 0;
            var oldColor = GUI.color;
            if (!canUse)
            {
                GUI.color = _disabledColor;
            }

            if (GUI.Button(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 26f), "F 回血", _buttonStyle) && canUse)
            {
                _player.ExtraActionController.TriggerQuickHealFromUi();
            }

            GUI.color = canUse ? _countColor : _disabledColor;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 18f), $"血包 x{count}", _countStyle);
            GUI.color = oldColor;
        }

        private void EnsureStyles()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    richText = false
                };
                _buttonStyle.normal.textColor = _textColor;
                _buttonStyle.hover.textColor = _textColor;
                _buttonStyle.active.textColor = _textColor;
                _buttonStyle.focused.textColor = _textColor;
            }

            if (_countStyle == null)
            {
                _countStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft
                };
                _countStyle.normal.textColor = _countColor;
            }
        }
    }
}
