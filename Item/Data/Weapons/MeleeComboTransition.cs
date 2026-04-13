using Animancer;
#if UNITY_EDITOR
using System;
using Animancer.Editor;
#endif

namespace BBBNexus
{
    [System.Serializable]
    public partial class MeleeComboTransition : ClipTransition
#if UNITY_EDITOR
        , ITransitionGUI
#endif
    {
        [UnityEngine.SerializeField, UnityEngine.HideInInspector]
        private string _StableKey;

        public override object Key
            => string.IsNullOrEmpty(_StableKey) ? this : _StableKey;

#if UNITY_EDITOR
        public static event Action<MeleeComboTransition, TransitionPreviewDetails> PreviewSceneGUIRequested;
        public static event Action<MeleeComboTransition> TimelineBackgroundGUIRequested;
        public static event Action<MeleeComboTransition> TimelineForegroundGUIRequested;

        void ITransitionGUI.OnPreviewSceneGUI(TransitionPreviewDetails details)
            => PreviewSceneGUIRequested?.Invoke(this, details);

        void ITransitionGUI.OnTimelineBackgroundGUI()
            => TimelineBackgroundGUIRequested?.Invoke(this);

        void ITransitionGUI.OnTimelineForegroundGUI()
            => TimelineForegroundGUIRequested?.Invoke(this);
#endif
    }
}


