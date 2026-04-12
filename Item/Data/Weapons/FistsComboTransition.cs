using Animancer;

namespace BBBNexus
{
    [System.Serializable]
    public partial class FistsComboTransition : ClipTransition
    {
        [UnityEngine.SerializeField, UnityEngine.HideInInspector]
        private string _StableKey;

        public override object Key
            => string.IsNullOrEmpty(_StableKey) ? this : _StableKey;
    }
}

// Editor-only timeline overlay (ITransitionGUI) 已拆分到 FistsComboTransition.Editor.cs
// 由于 partial class 不能跨程序集，该实现需要在 BBBNexus.Editor 内以
// CustomPropertyDrawer 形式重新实现后才能恢复喵~

#if UNITY_EDITOR_DISABLED
namespace BBBNexus
{
    using System;
    using System.Collections.Generic;
    using Animancer.Editor;
    using Animancer.Editor.Previews;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public partial class FistsComboTransition : ITransitionGUI
    {
        private enum OverlayKind
        {
            Damage = 0,
            Alignment = 1,
            ExtraDamage = 2,
        }

        private struct OverlayData
        {
            public OverlayKind Kind;
            public bool Enabled;
            public float StartNormalized;
            public float EndNormalized;
        }

        private readonly struct EffectiveWindowData
        {
            public readonly float DominantStart;
            public readonly float DominantEnd;
            public readonly float EffectiveStart;
            public readonly float EffectiveEnd;

            public EffectiveWindowData(float dominantStart, float dominantEnd, float effectiveStart, float effectiveEnd)
            {
                DominantStart = dominantStart;
                DominantEnd = dominantEnd;
                EffectiveStart = effectiveStart;
                EffectiveEnd = effectiveEnd;
            }
        }

        private static readonly Dictionary<string, List<OverlayData>> OverlayByPropertyKey = new Dictionary<string, List<OverlayData>>();
        private static readonly Color DamageEnabledColor = new Color(0.9f, 0.2f, 0.2f, 0.38f);
        private static readonly Color DamageDisabledColor = new Color(0.45f, 0.2f, 0.2f, 0.18f);
        private static readonly Color ExtraDamageEnabledColor = new Color(0.95f, 0.5f, 0.15f, 0.38f);
        private static readonly Color ExtraDamageDisabledColor = new Color(0.45f, 0.28f, 0.12f, 0.18f);
        private static readonly Color AlignmentEnabledColor = new Color(0.95f, 0.28f, 0.72f, 0.34f);
        private static readonly Color AlignmentDisabledColor = new Color(0.42f, 0.18f, 0.34f, 0.16f);
        private static readonly Color DamagePreviewBannerColor = new Color(0.82f, 0.16f, 0.16f, 0.82f);
        private static readonly Color ExtraDamagePreviewBannerColor = new Color(0.92f, 0.45f, 0.12f, 0.82f);
        private static readonly Color AlignmentPreviewBannerColor = new Color(0.95f, 0.22f, 0.66f, 0.84f);
        private static GUIStyle _PreviewBannerStyle;

        public static void SetOverlay(string propertyKey, bool enabled, float startNormalized, float endNormalized)
        {
            SetOverlay(propertyKey, OverlayKind.Damage, enabled, startNormalized, endNormalized);
        }

        public static void SetAlignmentOverlay(string propertyKey, bool enabled, float startNormalized, float endNormalized)
        {
            SetOverlay(propertyKey, OverlayKind.Alignment, enabled, startNormalized, endNormalized);
        }

        /// <summary>
        /// 设置额外伤害窗口 overlay。会移除所有旧的 ExtraDamage 条目后重新添加。
        /// </summary>
        public static void SetExtraDamageOverlays(string propertyKey, bool parentEnabled, DamageSubWindow[] extraWindows)
        {
            if (!OverlayByPropertyKey.TryGetValue(propertyKey, out var overlays))
            {
                overlays = new List<OverlayData>(4);
                OverlayByPropertyKey[propertyKey] = overlays;
            }

            // 移除所有旧的 ExtraDamage
            overlays.RemoveAll(o => o.Kind == OverlayKind.ExtraDamage);

            if (extraWindows == null || !parentEnabled) return;

            for (int i = 0; i < extraWindows.Length; i++)
            {
                overlays.Add(new OverlayData
                {
                    Kind = OverlayKind.ExtraDamage,
                    Enabled = true,
                    StartNormalized = extraWindows[i].StartNormalized,
                    EndNormalized = extraWindows[i].EndNormalized,
                });
            }
        }

        private static void SetOverlay(string propertyKey, OverlayKind kind, bool enabled, float startNormalized, float endNormalized)
        {
            if (!OverlayByPropertyKey.TryGetValue(propertyKey, out var overlays))
            {
                overlays = new List<OverlayData>(2);
                OverlayByPropertyKey[propertyKey] = overlays;
            }

            bool replaced = false;
            for (int i = 0; i < overlays.Count; i++)
            {
                if (overlays[i].Kind != kind)
                    continue;

                overlays[i] = new OverlayData
                {
                    Kind = kind,
                    Enabled = enabled,
                    StartNormalized = startNormalized,
                    EndNormalized = endNormalized,
                };
                replaced = true;
                break;
            }

            if (!replaced)
            {
                overlays.Add(new OverlayData
                {
                    Kind = kind,
                    Enabled = enabled,
                    StartNormalized = startNormalized,
                    EndNormalized = endNormalized,
                });
            }
        }

        public static string GetPropertyKey(SerializedProperty property)
        {
            Object target = property.serializedObject.targetObject;
            int id = target != null ? target.GetInstanceID() : 0;
            return id + ":" + property.propertyPath;
        }

        public static void ClearOverlayCache()
        {
            OverlayByPropertyKey.Clear();
        }

        public void OnPreviewSceneGUI(TransitionPreviewDetails details)
        {
            SerializedProperty property = TransitionDrawer.Context.Property;
            if (property == null)
                return;

            if (!OverlayByPropertyKey.TryGetValue(GetPropertyKey(property), out List<OverlayData> overlays) || overlays.Count == 0)
                return;

            float previewNormalizedTime = TransitionPreviewWindow.PreviewNormalizedTime;
            bool hasBanner = false;
            OverlayKind bannerKind = OverlayKind.Damage;
            for (int i = 0; i < overlays.Count; i++)
            {
                OverlayData overlay = overlays[i];
                if (!overlay.Enabled)
                    continue;

                if (!TryGetEffectiveWindow(property, overlay, out _, out _, out EffectiveWindowData window))
                    continue;

                float previewTime = previewNormalizedTime * GetClip(property).length;
                float min = Mathf.Min(window.EffectiveStart, window.EffectiveEnd);
                float max = Mathf.Max(window.EffectiveStart, window.EffectiveEnd);
                if (previewTime < min || previewTime > max)
                    continue;

                hasBanner = true;
                bannerKind = overlay.Kind;
                break;
            }

            if (!hasBanner)
                return;

            Handles.BeginGUI();
            try
            {
                _PreviewBannerStyle ??= new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(10, 10, 4, 4),
                };

                Rect area = new Rect(14f, 14f, 120f, 24f);
                EditorGUI.DrawRect(area, GetPreviewBannerColor(bannerKind));
                GUI.Label(area, GetPreviewBannerLabel(bannerKind), _PreviewBannerStyle);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        public void OnTimelineBackgroundGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            SerializedProperty property = TransitionDrawer.Context.Property;
            if (property == null)
                return;

            if (!OverlayByPropertyKey.TryGetValue(GetPropertyKey(property), out List<OverlayData> overlays) || overlays.Count == 0)
                return;

            TimelineGUI timeline = TimelineGUI.Current;
            if (timeline == null)
                return;

            float overlayHeight = Mathf.Max(4f, (timeline.Area.height - timeline.TickHeight) / 2f);
            for (int i = 0; i < overlays.Count; i++)
            {
                OverlayData overlay = overlays[i];
                if (!TryGetEffectiveWindow(property, overlay, out _, out _, out EffectiveWindowData window))
                    continue;

                DrawOverlayRect(timeline, window.DominantStart, window.DominantEnd, overlayHeight, GetOverlayDomainColor(overlay.Kind, overlay.Enabled));
                DrawOverlayRect(timeline, window.EffectiveStart, window.EffectiveEnd, overlayHeight, GetOverlayColor(overlay.Kind, overlay.Enabled));
            }
        }

        public void OnTimelineForegroundGUI()
        {
        }

        private static void DrawOverlayRect(TimelineGUI timeline, float startTime, float endTime, float overlayHeight, Color color)
        {
            float xMin = Mathf.Clamp(timeline.SecondsToPixels(startTime), timeline.Area.xMin, timeline.Area.xMax);
            float xMax = Mathf.Clamp(timeline.SecondsToPixels(endTime), timeline.Area.xMin, timeline.Area.xMax);
            Rect area = new Rect(
                Mathf.Min(xMin, xMax),
                0f,
                Mathf.Max(2f, Mathf.Abs(xMax - xMin)),
                overlayHeight);

            EditorGUI.DrawRect(area, color);
        }

        private static AnimationClip GetClip(SerializedProperty property)
        {
            SerializedProperty clipProperty = property.FindPropertyRelative(ClipTransition.ClipFieldName);
            return clipProperty?.objectReferenceValue as AnimationClip;
        }

        private static float GetSpeed(SerializedProperty property)
        {
            SerializedProperty speedProperty = property.FindPropertyRelative("_Speed");
            float speed = speedProperty != null ? speedProperty.floatValue : 1f;
            return float.IsNaN(speed) ? 1f : speed;
        }

        private static float GetNormalizedStartTime(SerializedProperty property)
        {
            SerializedProperty startProperty = property.FindPropertyRelative("_NormalizedStartTime");
            return startProperty != null ? startProperty.floatValue : float.NaN;
        }

        private static float GetRealNormalizedEndTime(SerializedProperty property, float speed)
        {
            SerializedProperty eventsProperty = property.FindPropertyRelative("_Events");
            SerializedProperty normalizedTimes = eventsProperty?.FindPropertyRelative("_NormalizedTimes");
            if (normalizedTimes == null || !normalizedTimes.isArray || normalizedTimes.arraySize == 0)
                return AnimancerEvent.Sequence.GetDefaultNormalizedEndTime(speed);

            SerializedProperty endProperty = normalizedTimes.GetArrayElementAtIndex(normalizedTimes.arraySize - 1);
            float endTime = endProperty.floatValue;
            return float.IsNaN(endTime) ? AnimancerEvent.Sequence.GetDefaultNormalizedEndTime(speed) : endTime;
        }

        private static bool TryGetEffectiveWindow(
            SerializedProperty property,
            OverlayData overlay,
            out AnimationClip clip,
            out float duration,
            out EffectiveWindowData window)
        {
            window = default;
            if (!TryGetDominantWindow(property, out clip, out duration, out float dominantStart, out float dominantEnd))
                return false;

            float start = Mathf.Clamp01(overlay.StartNormalized);
            float end = Mathf.Clamp01(overlay.EndNormalized);
            if (end < start)
                (start, end) = (end, start);

            float effectiveStart = Mathf.LerpUnclamped(dominantStart, dominantEnd, start);
            float effectiveEnd = Mathf.LerpUnclamped(dominantStart, dominantEnd, end);
            window = new EffectiveWindowData(dominantStart, dominantEnd, effectiveStart, effectiveEnd);
            return true;
        }

        private static bool TryGetDominantWindow(
            SerializedProperty property,
            out AnimationClip clip,
            out float duration,
            out float dominantStart,
            out float dominantEnd)
        {
            clip = GetClip(property);
            duration = clip != null && clip.length > 0f ? clip.length : 0f;
            dominantStart = 0f;
            dominantEnd = 0f;
            if (clip == null || duration <= 0f)
                return false;

            float speed = GetSpeed(property);
            float normalizedStartTime = GetNormalizedStartTime(property);
            float normalizedEndTime = GetRealNormalizedEndTime(property, speed);
            float transitionStart = TimelineGUI.GetStartTime(normalizedStartTime, speed, duration);
            float transitionEnd = normalizedEndTime * duration;

            dominantStart = transitionStart;
            dominantEnd = transitionEnd;
            return true;
        }

        private static Color GetOverlayColor(OverlayKind kind, bool enabled)
        {
            return kind switch
            {
                OverlayKind.Alignment => enabled ? AlignmentEnabledColor : AlignmentDisabledColor,
                OverlayKind.ExtraDamage => enabled ? ExtraDamageEnabledColor : ExtraDamageDisabledColor,
                _ => enabled ? DamageEnabledColor : DamageDisabledColor,
            };
        }

        private static Color GetOverlayDomainColor(OverlayKind kind, bool enabled)
        {
            Color color = GetOverlayColor(kind, enabled);
            color.a *= 0.28f;
            return color;
        }

        private static Color GetPreviewBannerColor(OverlayKind kind)
        {
            return kind switch
            {
                OverlayKind.Alignment => AlignmentPreviewBannerColor,
                OverlayKind.ExtraDamage => ExtraDamagePreviewBannerColor,
                _ => DamagePreviewBannerColor,
            };
        }

        private static string GetPreviewBannerLabel(OverlayKind kind)
        {
            return kind switch
            {
                OverlayKind.Alignment => "ALIGN",
                _ => "DAMAGE",
            };
        }
    }
}
#endif
