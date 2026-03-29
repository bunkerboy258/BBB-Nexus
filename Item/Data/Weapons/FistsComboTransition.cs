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

#if UNITY_EDITOR
namespace BBBNexus
{
    using System.Collections.Generic;
    using Animancer.Editor;
    using Animancer.Editor.Previews;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public partial class FistsComboTransition : ITransitionGUI
    {
        private struct OverlayData
        {
            public bool Enabled;
            public float StartNormalized;
            public float EndNormalized;
        }

        private static readonly Dictionary<string, OverlayData> OverlayByPropertyKey = new Dictionary<string, OverlayData>();
        private static readonly Color EnabledColor = new Color(0.9f, 0.2f, 0.2f, 0.38f);
        private static readonly Color DisabledColor = new Color(0.45f, 0.2f, 0.2f, 0.18f);
        private static readonly Color PreviewBannerColor = new Color(0.82f, 0.16f, 0.16f, 0.82f);
        private static GUIStyle _PreviewBannerStyle;

        public static void SetOverlay(string propertyKey, bool enabled, float startNormalized, float endNormalized)
        {
            OverlayByPropertyKey[propertyKey] = new OverlayData
            {
                Enabled = enabled,
                StartNormalized = startNormalized,
                EndNormalized = endNormalized,
            };
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

            if (!OverlayByPropertyKey.TryGetValue(GetPropertyKey(property), out OverlayData overlay) || !overlay.Enabled)
                return;

            if (!TryGetEffectiveWindow(property, overlay, out _, out _, out float effectiveStartNormalized, out float effectiveEndNormalized))
                return;

            float previewNormalizedTime = TransitionPreviewWindow.PreviewNormalizedTime;
            float min = Mathf.Min(effectiveStartNormalized, effectiveEndNormalized);
            float max = Mathf.Max(effectiveStartNormalized, effectiveEndNormalized);
            if (previewNormalizedTime < min || previewNormalizedTime > max)
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
                EditorGUI.DrawRect(area, PreviewBannerColor);
                GUI.Label(area, "DAMAGE", _PreviewBannerStyle);
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

            if (!OverlayByPropertyKey.TryGetValue(GetPropertyKey(property), out OverlayData overlay))
                return;

            TimelineGUI timeline = TimelineGUI.Current;
            if (timeline == null)
                return;

            if (!TryGetEffectiveWindow(property, overlay, out _, out _, out float effectiveStartNormalized, out float effectiveEndNormalized))
                return;

            float duration = GetClip(property).length;
            float effectiveStart = effectiveStartNormalized * duration;
            float effectiveEnd = effectiveEndNormalized * duration;

            float xMin = Mathf.Clamp(timeline.SecondsToPixels(effectiveStart), timeline.Area.xMin, timeline.Area.xMax);
            float xMax = Mathf.Clamp(timeline.SecondsToPixels(effectiveEnd), timeline.Area.xMin, timeline.Area.xMax);
            float overlayHeight = Mathf.Max(4f, (timeline.Area.height - timeline.TickHeight) / 2f);
            Rect area = new Rect(
                Mathf.Min(xMin, xMax),
                0f,
                Mathf.Max(2f, Mathf.Abs(xMax - xMin)),
                overlayHeight);

            EditorGUI.DrawRect(area, overlay.Enabled ? EnabledColor : DisabledColor);
        }

        public void OnTimelineForegroundGUI()
        {
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

        private static float GetFadeDuration(SerializedProperty property)
        {
            SerializedProperty fadeProperty = property.FindPropertyRelative("_FadeDuration");
            return fadeProperty != null ? fadeProperty.floatValue : AnimancerGraph.DefaultFadeDuration;
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
            out float effectiveStartNormalized,
            out float effectiveEndNormalized)
        {
            clip = GetClip(property);
            duration = clip != null && clip.length > 0f ? clip.length : 0f;
            effectiveStartNormalized = 0f;
            effectiveEndNormalized = 0f;
            if (clip == null || duration <= 0f)
                return false;

            float start = Mathf.Clamp01(overlay.StartNormalized);
            float end = Mathf.Clamp01(overlay.EndNormalized);
            if (end < start)
                (start, end) = (end, start);

            float speed = GetSpeed(property);
            float normalizedStartTime = GetNormalizedStartTime(property);
            float fadeDuration = GetFadeDuration(property);
            float normalizedEndTime = GetRealNormalizedEndTime(property, speed);

            float transitionStart = TimelineGUI.GetStartTime(normalizedStartTime, speed, duration);
            float fadeInEnd = transitionStart + fadeDuration * speed;
            float transitionEnd = normalizedEndTime * duration;

            float dominantStart = fadeInEnd;
            float dominantEnd = transitionEnd;
            float effectiveStart = Mathf.LerpUnclamped(dominantStart, dominantEnd, start);
            float effectiveEnd = Mathf.LerpUnclamped(dominantStart, dominantEnd, end);

            effectiveStartNormalized = duration > 0f ? effectiveStart / duration : 0f;
            effectiveEndNormalized = duration > 0f ? effectiveEnd / duration : 0f;
            return true;
        }
    }
}
#endif
