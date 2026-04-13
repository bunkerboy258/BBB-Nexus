using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 基于 OnGUI 的轻量交互提示。
    /// 读取 PlayerInteractionSensor 的缓存目标，在屏幕中心附近显示提示文本。
    /// </summary>
    public sealed class InteractionPromptOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerInteractionSensor _sensor;
        [SerializeField] private bool _showKeyPrefix = true;
        [SerializeField] private string _keyText = "E";
        [SerializeField] private float _sanityBottomMargin = 96f;
        [SerializeField] private float _sanityBarHeight = 14f;
        [SerializeField] private float _sanityLabelOffset = 22f;
        [SerializeField] private float _promptGapAboveSanity = 18f;
        [SerializeField] private Vector2 _panelPadding = new Vector2(14f, 8f);
        [SerializeField] private Color _textColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.58f);
        [SerializeField] private Color _panelBorderColor = new Color(1f, 1f, 1f, 0.12f);

        private GUIStyle _style;
        private GUIStyle _shadowStyle;

        private void Awake()
        {
            if (_sensor == null)
                _sensor = GetComponent<PlayerInteractionSensor>() ?? GetComponentInParent<PlayerInteractionSensor>();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || _sensor == null || !_sensor.HasInteractable)
                return;

            string prompt = _sensor.CurrentPromptText;
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            if (_showKeyPrefix)
                prompt = $"[{_keyText}] {prompt}";

            GUIStyle style = GetStyle(false);
            GUIStyle shadow = GetStyle(true);
            Vector2 size = style.CalcSize(new GUIContent(prompt));
            float panelWidth = size.x + _panelPadding.x * 2f;
            float panelHeight = size.y + _panelPadding.y * 2f;

            float sanityBarY = Screen.height - _sanityBottomMargin - _sanityBarHeight;
            float sanityLabelY = sanityBarY - _sanityLabelOffset;
            float y = sanityLabelY - panelHeight - _promptGapAboveSanity;
            float x = Screen.width * 0.5f - panelWidth * 0.5f;

            DrawPanel(new Rect(x, y, panelWidth, panelHeight));

            float textX = x + _panelPadding.x;
            float textY = y + _panelPadding.y;
            GUI.Label(new Rect(textX + 1f, textY + 1f, size.x, size.y), prompt, shadow);
            GUI.Label(new Rect(textX, textY, size.x, size.y), prompt, style);
        }

        private GUIStyle GetStyle(bool shadow)
        {
            GUIStyle cache = shadow ? _shadowStyle : _style;
            if (cache != null)
                return cache;

            cache = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            cache.normal.textColor = shadow ? _shadowColor : _textColor;
            if (shadow) _shadowStyle = cache;
            else _style = cache;
            return cache;
        }

        private void DrawPanel(Rect rect)
        {
            GUI.color = _panelColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = _panelBorderColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}
