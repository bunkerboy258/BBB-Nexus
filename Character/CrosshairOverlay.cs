using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 屏幕中心准星绘制。
    /// 挂在场景任意 GameObject 上，拖入 CharacterController 引用即可。
    /// 仅在 IsTacticalStance（持枪瞄准姿态）时显示。
    /// </summary>
    public class CrosshairOverlay : MonoBehaviour
    {
        [Header("--- 目标角色 ---")]
        [Tooltip("持枪角色控制器，用于读取瞄准状态；留空则始终显示")]
        [SerializeField] private BBBCharacterController _character;

        [Header("--- 样式 ---")]
        [Tooltip("准星线段长度（像素）")]
        [SerializeField] private float _lineLength = 10f;
        [Tooltip("中心间隙（像素）")]
        [SerializeField] private float _gap = 4f;
        [Tooltip("线段粗细（像素）")]
        [SerializeField] private float _thickness = 2f;
        [Tooltip("是否显示中心点")]
        [SerializeField] private bool _showDot = true;
        [Tooltip("中心点半径（像素）")]
        [SerializeField] private float _dotRadius = 1.5f;
        [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.9f);

        private Texture2D _tex;

        private void Awake()
        {
            _tex = new Texture2D(1, 1);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }

        private void OnDestroy()
        {
            if (_tex != null)
                Destroy(_tex);
        }

        private void OnGUI()
        {
            if (_character != null)
            {
                if (!_character.RuntimeData.IsTacticalStance)
                    return;
                if (_character.RuntimeData.MainhandItem?.GetSODataAs<RangedWeaponSO>() == null)
                    return;
            }

            GUI.color = _color;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float half = _thickness * 0.5f;

            // 上
            DrawRect(cx - half, cy - _gap - _lineLength, _thickness, _lineLength);
            // 下
            DrawRect(cx - half, cy + _gap, _thickness, _lineLength);
            // 左
            DrawRect(cx - _gap - _lineLength, cy - half, _lineLength, _thickness);
            // 右
            DrawRect(cx + _gap, cy - half, _lineLength, _thickness);

            if (_showDot)
                DrawRect(cx - _dotRadius, cy - _dotRadius, _dotRadius * 2f, _dotRadius * 2f);

            GUI.color = Color.white;
        }

        private void DrawRect(float x, float y, float w, float h)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), _tex);
        }
    }
}
