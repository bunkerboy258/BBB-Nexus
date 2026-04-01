using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BBBNexus.Editor
{
    [CustomEditor(typeof(FistsSO))]
    public class FistsSOEditor : UnityEditor.Editor
    {
        private const float WindowBarHeight = 14f;
        private static readonly Color DamageFillColor = new(0.85f, 0.18f, 0.18f, 0.85f);
        private static readonly Color DamageFillDisabledColor = new(0.35f, 0.18f, 0.18f, 0.4f);
        private static readonly Color AlignmentFillColor = new(0.18f, 0.55f, 0.92f, 0.85f);
        private static readonly Color AlignmentFillDisabledColor = new(0.18f, 0.28f, 0.35f, 0.4f);

        private SerializedProperty _enterStanceAnim;
        private SerializedProperty _exitStanceAnim;
        private SerializedProperty _exitUsesLock;
        private SerializedProperty _comboSequence;
        private SerializedProperty _comboAttackHands;
        private SerializedProperty _comboDamageWindows;
        private SerializedProperty _comboAlignmentWindows;
        private SerializedProperty _comboWindowStart;
        private SerializedProperty _comboLateBuffer;
        private SerializedProperty _comboPriority;
        private SerializedProperty _attackGeometryId;

        private void OnEnable()
        {
            _enterStanceAnim = serializedObject.FindProperty("EnterStanceAnim");
            _exitStanceAnim = serializedObject.FindProperty("ExitStanceAnim");
            _exitUsesLock = serializedObject.FindProperty("ExitUsesLock");
            _comboSequence = serializedObject.FindProperty("ComboSequence");
            _comboAttackHands = serializedObject.FindProperty("ComboAttackHands");
            _comboDamageWindows = serializedObject.FindProperty("ComboDamageWindows");
            _comboAlignmentWindows = serializedObject.FindProperty("ComboAlignmentWindows");
            _comboWindowStart = serializedObject.FindProperty("ComboWindowStart");
            _comboLateBuffer = serializedObject.FindProperty("ComboLateBuffer");
            _comboPriority = serializedObject.FindProperty("ComboPriority");
            _attackGeometryId = serializedObject.FindProperty("AttackGeometryId");
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void HandleUndoRedo()
        {
            FistsComboTransition.ClearOverlayCache();
            serializedObject.UpdateIfRequiredOrScript();
            Repaint();
            InternalEditorUtility.RepaintAllViews();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "EnterStanceAnim",
                "ExitStanceAnim",
                "ExitUsesLock",
                "ComboSequence",
                "ComboAttackHands",
                "ComboDamageWindows",
                "ComboAlignmentWindows",
                "ComboWindowStart",
                "ComboLateBuffer",
                "ComboPriority",
                "AttackGeometryId");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Combo Setup", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enterStanceAnim);
            EditorGUILayout.PropertyField(_exitStanceAnim);
            EditorGUILayout.PropertyField(_exitUsesLock);
            EditorGUILayout.PropertyField(_comboWindowStart);
            EditorGUILayout.PropertyField(_comboLateBuffer);
            EditorGUILayout.PropertyField(_comboPriority);
            EditorGUILayout.PropertyField(_attackGeometryId);
            DrawAttackGeometryActions();

            EditorGUILayout.Space();
            DrawComboArrays();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawComboArrays()
        {
            if (_comboSequence == null || _comboAttackHands == null || _comboDamageWindows == null || _comboAlignmentWindows == null)
                return;

            int targetSize = Mathf.Max(0, EditorGUILayout.IntField("Combo Count", _comboSequence.arraySize));
            if (targetSize != _comboSequence.arraySize)
            {
                _comboSequence.arraySize = targetSize;
                SyncParallelArray(_comboAttackHands, targetSize, InitializeHandSlot);
                SyncParallelArray(_comboDamageWindows, targetSize, InitializeDamageWindowSlot);
                SyncParallelArray(_comboAlignmentWindows, targetSize, InitializeAlignmentWindowSlot);
            }
            else
            {
                SyncParallelArray(_comboAttackHands, targetSize, InitializeHandSlot);
                SyncParallelArray(_comboDamageWindows, targetSize, InitializeDamageWindowSlot);
                SyncParallelArray(_comboAlignmentWindows, targetSize, InitializeAlignmentWindowSlot);
            }

            for (int i = 0; i < _comboSequence.arraySize; i++)
            {
                SerializedProperty sequence = _comboSequence.GetArrayElementAtIndex(i);
                SerializedProperty hand = _comboAttackHands.GetArrayElementAtIndex(i);
                SerializedProperty window = _comboDamageWindows.GetArrayElementAtIndex(i);
                SerializedProperty alignmentWindow = _comboAlignmentWindows.GetArrayElementAtIndex(i);
                EnsureStableKey(sequence);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Combo {i + 1}", EditorStyles.boldLabel);
                FistsComboTransition.SetOverlay(
                    FistsComboTransition.GetPropertyKey(sequence),
                    window.FindPropertyRelative("Enabled").boolValue,
                    window.FindPropertyRelative("StartNormalized").floatValue,
                    window.FindPropertyRelative("EndNormalized").floatValue);
                FistsComboTransition.SetAlignmentOverlay(
                    FistsComboTransition.GetPropertyKey(sequence),
                    alignmentWindow.FindPropertyRelative("Enabled").boolValue,
                    alignmentWindow.FindPropertyRelative("StartNormalized").floatValue,
                    alignmentWindow.FindPropertyRelative("EndNormalized").floatValue);
                EditorGUILayout.PropertyField(sequence, new GUIContent("Transition"), true);
                EditorGUILayout.PropertyField(hand, new GUIContent("Attack Hand"));
                DrawDamageWindowBar(window);
                DrawWindowBar(
                    alignmentWindow,
                    "PreAttack Alignment Window",
                    AlignmentFillColor,
                    AlignmentFillDisabledColor);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }
        }

        private static void SyncParallelArray(SerializedProperty array, int targetSize, System.Action<SerializedProperty, int> initializer)
        {
            int oldSize = array.arraySize;
            if (oldSize == targetSize)
                return;

            array.arraySize = targetSize;
            for (int i = oldSize; i < targetSize; i++)
            {
                initializer?.Invoke(array.GetArrayElementAtIndex(i), i);
            }
        }

        private static void InitializeHandSlot(SerializedProperty element, int index)
        {
            element.enumValueIndex = index % 2 == 0 ? (int)FistsAttackHand.MainHand : (int)FistsAttackHand.OffHand;
        }

        private static void InitializeDamageWindowSlot(SerializedProperty element, int _)
        {
            element.FindPropertyRelative("Enabled").boolValue = false;
            element.FindPropertyRelative("StartNormalized").floatValue = 0.15f;
            element.FindPropertyRelative("EndNormalized").floatValue = 0.45f;
        }

        private static void InitializeAlignmentWindowSlot(SerializedProperty element, int _)
        {
            element.FindPropertyRelative("Enabled").boolValue = false;
            element.FindPropertyRelative("StartNormalized").floatValue = 0f;
            element.FindPropertyRelative("EndNormalized").floatValue = 0.25f;
        }

        private static void EnsureStableKey(SerializedProperty sequence)
        {
            SerializedProperty key = sequence.FindPropertyRelative("_StableKey");
            if (key != null && string.IsNullOrEmpty(key.stringValue))
            {
                key.stringValue = GUID.Generate().ToString();
            }
        }

        private static void DrawDamageWindowBar(SerializedProperty window)
        {
            DrawWindowBar(window, "Damage Window Sidecar", DamageFillColor, DamageFillDisabledColor);
        }

        private static void DrawWindowBar(
            SerializedProperty window,
            string label,
            Color enabledColor,
            Color disabledColor)
        {
            SerializedProperty enabled = window.FindPropertyRelative("Enabled");
            SerializedProperty start = window.FindPropertyRelative("StartNormalized");
            SerializedProperty end = window.FindPropertyRelative("EndNormalized");

            Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect barRect = Rect.zero;

            enabled.boolValue = EditorGUI.ToggleLeft(headerRect, label, enabled.boolValue);

            if (!enabled.boolValue)
                return;

            barRect = EditorGUILayout.GetControlRect(false, WindowBarHeight + 4f);
            barRect = new Rect(barRect.x, barRect.y + 2f, barRect.width, WindowBarHeight);

            float startValue = Mathf.Clamp01(start.floatValue);
            float endValue = Mathf.Clamp01(end.floatValue);
            if (endValue < startValue)
            {
                (startValue, endValue) = (endValue, startValue);
            }

            EditorGUI.DrawRect(barRect, new Color(0.13f, 0.13f, 0.13f));

            float fillStart = Mathf.Lerp(barRect.x + 1f, barRect.xMax - 1f, startValue);
            float fillEnd = Mathf.Lerp(barRect.x + 1f, barRect.xMax - 1f, endValue);
            Rect fillRect = new Rect(fillStart, barRect.y + 1f, Mathf.Max(2f, fillEnd - fillStart), barRect.height - 2f);
            Color fillColor = enabled.boolValue
                ? enabledColor
                : disabledColor;
            EditorGUI.DrawRect(fillRect, fillColor);

            EditorGUI.MinMaxSlider(barRect, GUIContent.none, ref startValue, ref endValue, 0f, 1f);

            start.floatValue = startValue;
            end.floatValue = endValue;

            Rect leftLabelRect = new Rect(barRect.x + 4f, barRect.y + 1f, 52f, barRect.height - 2f);
            Rect rightLabelRect = new Rect(barRect.xMax - 56f, barRect.y + 1f, 52f, barRect.height - 2f);
            GUIStyle mini = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
            GUIStyle miniRight = new GUIStyle(mini) { alignment = TextAnchor.MiddleRight };
            GUI.Label(leftLabelRect, $"{Mathf.RoundToInt(startValue * 100f)}%", mini);
            GUI.Label(rightLabelRect, $"{Mathf.RoundToInt(endValue * 100f)}%", miniRight);
        }

        private void DrawAttackGeometryActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("写入几何模板 JSON"))
                {
                    WriteAttackGeometryTemplateJson();
                }

                if (GUILayout.Button("清空几何缓存"))
                {
                    AttackClipGeometryLibrary.ClearCache();
                    Repaint();
                    InternalEditorUtility.RepaintAllViews();
                }
            }
        }

        private void WriteAttackGeometryTemplateJson()
        {
            if (target is not FistsSO fists)
            {
                return;
            }

            string geometryId = fists.GetAttackGeometryId();
            var definition = BuildTemplateDefinition(fists);
            AttackClipGeometryLibrary.WriteDefinitionAndRegister(geometryId, definition, $"{fists.name} Attack Sweep");
            Debug.Log($"[FistsSOEditor] Wrote attack geometry template: {AttackClipGeometryLibrary.ToAssetPath(geometryId)}");
        }

        private static AttackClipGeometryDefinition BuildTemplateDefinition(FistsSO fists)
        {
            return AttackClipGeometryTemplateFactory.CreateForFists(fists);
        }
    }
}
