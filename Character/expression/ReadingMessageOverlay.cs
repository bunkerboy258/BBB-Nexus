using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 基于 OnGUI 的阅读面板。
    /// 当前用于告示牌/留言/碑文等简单文本展示。
    /// </summary>
    public sealed class ReadingMessageOverlay : MonoBehaviour
    {
        private const bool DebugTrace = true;

        [Header("Close Keys")]
        [SerializeField] private KeyCode _closeKey = KeyCode.E;
        [SerializeField] private KeyCode _altCloseKey = KeyCode.Escape;

        [Header("Layout")]
        [SerializeField] private float _panelWidth = 560f;
        [SerializeField] private float _panelMinHeight = 96f;
        [SerializeField] private float _panelMaxHeight = 180f;
        [SerializeField] private float _titleHeight = 24f;
        [SerializeField] private float _footerHeight = 22f;
        [SerializeField] private Vector2 _padding = new Vector2(18f, 14f);
        [SerializeField] private float _sanityBottomMargin = 96f;
        [SerializeField] private float _sanityBarHeight = 14f;
        [SerializeField] private float _sanityLabelOffset = 22f;
        [SerializeField] private float _gapBelowCrosshair = 56f;
        [SerializeField] private float _gapAboveSanity = 18f;

        [Header("Style")]
        [SerializeField] private Color _panelColor = new Color(0.04f, 0.04f, 0.04f, 0.82f);
        [SerializeField] private Color _borderColor = new Color(0.88f, 0.88f, 0.88f, 0.16f);
        [SerializeField] private Color _titleColor = new Color(0.96f, 0.90f, 0.72f, 1f);
        [SerializeField] private Color _bodyColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        [SerializeField] private Color _hintColor = new Color(0.64f, 0.64f, 0.64f, 1f);

        public bool IsOpen { get; private set; }
        public string CurrentTitle { get; private set; } = string.Empty;
        public string CurrentBody { get; private set; } = string.Empty;

        private int _openedFrame = -1;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;

        private void Update()
        {
            if (!IsOpen)
                return;

            if (Time.frameCount == _openedFrame)
                return;

            if (Input.GetKeyDown(_closeKey) || Input.GetKeyDown(_altCloseKey))
            {
                if (DebugTrace)
                    Debug.Log($"[ReadingOverlay] Hide by key frame={Time.frameCount} title={CurrentTitle}", this);
                Hide();
            }
        }

        public void Show(string title, string body)
        {
            CurrentTitle = string.IsNullOrWhiteSpace(title) ? "告示" : title;
            CurrentBody = string.IsNullOrWhiteSpace(body) ? "没有内容。" : body;
            IsOpen = true;
            _openedFrame = Time.frameCount;

            if (DebugTrace)
                Debug.Log($"[ReadingOverlay] Show frame={_openedFrame} title={CurrentTitle}", this);
        }

        public void Hide()
        {
            IsOpen = false;
            CurrentTitle = string.Empty;
            CurrentBody = string.Empty;
            _openedFrame = -1;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !IsOpen)
                return;

            EnsureStyles();

            float width = Mathf.Min(_panelWidth, Screen.width - 80f);
            float bodyWidth = width - _padding.x * 2f;
            float bodyHeight = _bodyStyle.CalcHeight(new GUIContent(CurrentBody), bodyWidth);
            float height = Mathf.Clamp(
                _padding.y * 2f + _titleHeight + _footerHeight + bodyHeight,
                _panelMinHeight,
                Mathf.Min(_panelMaxHeight, Screen.height - 120f));

            float crosshairBottom = Screen.height * 0.5f + _gapBelowCrosshair;
            float sanityBarY = Screen.height - _sanityBottomMargin - _sanityBarHeight;
            float sanityTop = sanityBarY - _sanityLabelOffset - _gapAboveSanity;
            float availableTop = crosshairBottom;
            float availableBottom = sanityTop;
            float idealY = availableTop + Mathf.Max(0f, (availableBottom - availableTop - height) * 0.5f);

            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                idealY,
                width,
                height);

            GUI.color = _panelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            GUI.color = _borderColor;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - 1f, panel.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.xMax - 1f, panel.y, 1f, panel.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = panel.x + _padding.x;
            float y = panel.y + _padding.y;
            float innerWidth = panel.width - _padding.x * 2f;

            GUI.Label(new Rect(x, y, innerWidth, _titleHeight), CurrentTitle, _titleStyle);

            float bodyY = y + _titleHeight + 10f;
            float footerY = panel.yMax - _padding.y - _footerHeight;
            float bodyAreaHeight = Mathf.Max(40f, footerY - bodyY - 10f);
            GUI.Label(new Rect(x, bodyY, innerWidth, bodyAreaHeight), CurrentBody, _bodyStyle);

            GUI.Label(new Rect(x, footerY, innerWidth, _footerHeight), "[E] / [Esc] 关闭", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false
                };
                _titleStyle.normal.textColor = _titleColor;
            }

            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    richText = true
                };
                _bodyStyle.normal.textColor = _bodyColor;
            }

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleRight,
                    wordWrap = false
                };
                _hintStyle.normal.textColor = _hintColor;
            }
        }
    }
}
