using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BBBNexus.Editor
{
    [CustomEditor(typeof(WeaponSO))]
    public class WeaponSOEditor : UnityEditor.Editor
    {
        private const float WindowBarHeight = 14f;
        private const string MeleeFoldoutKey = "BBBNexus.WeaponSOEditor.MeleeFoldout";
        private const string RangedFoldoutKey = "BBBNexus.WeaponSOEditor.RangedFoldout";
        private const string BakeUseRootTransformForSweepKey = "BBBNexus.WeaponSOEditor.BakeUseRootTransformForSweep";
        private static readonly Color DamageFillColor = new(0.85f, 0.18f, 0.18f, 0.85f);
        private static readonly Color DamageFillDisabledColor = new(0.35f, 0.18f, 0.18f, 0.4f);
        private static readonly Color AlignmentFillColor = new(0.18f, 0.55f, 0.92f, 0.85f);
        private static readonly Color AlignmentFillDisabledColor = new(0.18f, 0.28f, 0.35f, 0.4f);

        private SerializedProperty _enterStanceAnim;
        private SerializedProperty _exitStanceAnim;
        private SerializedProperty _exitUsesLock;
        private SerializedProperty _equipEndTime;
        private SerializedProperty _enableIK;
        private SerializedProperty _comboSequence;
        private SerializedProperty _comboAttackHands;
        private SerializedProperty _comboDamageWindows;
        private SerializedProperty _comboAlignmentWindows;
        private SerializedProperty _comboWindowStart;
        private SerializedProperty _comboLateBuffer;
        private SerializedProperty _comboPriority;
        private SerializedProperty _attackGeometryId;
        private SerializedProperty _sprintCameraPreset;

        private bool _meleeFoldout;
        private bool _rangedFoldout;

        private SerializedProperty _bakingCharacterPrefab;
        private SerializedProperty _bakingWeaponPrefab;
        private bool _bakeUseRootTransformForSweep;

        private void OnEnable()
        {
            _meleeFoldout = SessionState.GetBool(MeleeFoldoutKey, true);
            _rangedFoldout = SessionState.GetBool(RangedFoldoutKey, true);
            _bakeUseRootTransformForSweep = SessionState.GetBool(BakeUseRootTransformForSweepKey, true);

            _enterStanceAnim = serializedObject.FindProperty("EnterStanceAnim");
            _exitStanceAnim = serializedObject.FindProperty("ExitStanceAnim");
            _exitUsesLock = serializedObject.FindProperty("ExitUsesLock");
            _equipEndTime = serializedObject.FindProperty("EquipEndTime");
            _enableIK = serializedObject.FindProperty("EnableIK");
            _comboSequence = serializedObject.FindProperty("ComboSequence");
            _comboAttackHands = serializedObject.FindProperty("ComboAttackHands");
            _comboDamageWindows = serializedObject.FindProperty("ComboDamageWindows");
            _comboAlignmentWindows = serializedObject.FindProperty("ComboAlignmentWindows");
            _comboWindowStart = serializedObject.FindProperty("ComboWindowStart");
            _comboLateBuffer = serializedObject.FindProperty("ComboLateBuffer");
            _comboPriority = serializedObject.FindProperty("ComboPriority");
            _attackGeometryId = serializedObject.FindProperty("AttackGeometryId");
            _sprintCameraPreset = serializedObject.FindProperty("SprintCameraPreset");
            _bakingCharacterPrefab = serializedObject.FindProperty("BakingCharacterPrefab");
            _bakingWeaponPrefab = serializedObject.FindProperty("BakingWeaponPrefab");
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
                "EquipEndTime",
                "EnableIK",
                "ComboSequence",
                "ComboAttackHands",
                "ComboDamageWindows",
                "ComboAlignmentWindows",
                "ComboWindowStart",
                "ComboLateBuffer",
                "ComboPriority",
                "AttackGeometryId",
                "SprintCameraPreset",
                "EnablesAimState",
                "UseAimCorrection",
                "AimAnim",
                "AimAnimPlayOptions",
                "MaxAmmo",
                "FireRate",
                "MagazineSize",
                "ReloadTime",
                "TacticalReloadTime",
                "ReloadAnim",
                "ReloadAnimOptions",
                "AmmoItem",
                "ProjectilePrefab",
                "ProjectileSpeed",
                "IsFullAuto",
                "ShootInterval",
                "HitScanRange",
                "DamageAmount",
                "HeadDamageMultiplier",
                "TorsoDamageMultiplier",
                "ArmDamageMultiplier",
                "LegDamageMultiplier",
                "TracerDuration",
                "MuzzleVFXPrefab",
                "RecoilPitchAngle",
                "RecoilYawAngle",
                "RecoilPitchRandomRange",
                "RecoilYawRandomRange",
                "AimingCameraPreset");

            DrawMeleeFoldout();
            DrawRangedFoldout();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMeleeFoldout()
        {
            _meleeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_meleeFoldout, "近战武器专属");
            SessionState.SetBool(MeleeFoldoutKey, _meleeFoldout);
            if (_meleeFoldout)
            {
                EditorGUILayout.LabelField("近战通用", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(_equipEndTime);
                EditorGUILayout.PropertyField(_enableIK);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("近战连招", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(_enterStanceAnim);
                EditorGUILayout.PropertyField(_exitStanceAnim);
                EditorGUILayout.PropertyField(_exitUsesLock);
                EditorGUILayout.PropertyField(_comboWindowStart);
                EditorGUILayout.PropertyField(_comboLateBuffer);
                EditorGUILayout.PropertyField(_comboPriority);
                EditorGUILayout.PropertyField(_attackGeometryId);
                EditorGUILayout.PropertyField(_sprintCameraPreset);
                DrawAttackGeometryActions();

                EditorGUILayout.Space();
                DrawComboArrays();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRangedFoldout()
        {
            _rangedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_rangedFoldout, "远程武器专属");
            SessionState.SetBool(RangedFoldoutKey, _rangedFoldout);
            if (_rangedFoldout)
            {
                EditorGUILayout.LabelField("远程通用", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("EnablesAimState"), new GUIContent("Enables Aim State"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("UseAimCorrection"), new GUIContent("Use Aim Correction"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AimAnim"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AimAnimPlayOptions"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("MaxAmmo"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("FireRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("MagazineSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ReloadTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TacticalReloadTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ReloadAnim"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ReloadAnimOptions"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AmmoItem"));

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("远程专属", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ProjectilePrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ProjectileSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("IsFullAuto"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ShootInterval"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("HitScanRange"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("DamageAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("HeadDamageMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TorsoDamageMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ArmDamageMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("LegDamageMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TracerDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("MuzzleVFXPrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RecoilPitchAngle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RecoilYawAngle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RecoilPitchRandomRange"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RecoilYawRandomRange"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AimingCameraPreset"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawComboArrays()
        {
            if (_comboSequence == null || _comboAttackHands == null || _comboDamageWindows == null || _comboAlignmentWindows == null)
                return;

            int targetSize = Mathf.Max(0, EditorGUILayout.IntField("Combo Count", _comboSequence.arraySize));
            if (targetSize != _comboSequence.arraySize)
            {
                int oldSize = _comboSequence.arraySize;
                _comboSequence.arraySize = targetSize;
                for (int i = oldSize; i < targetSize; i++)
                    InitializeComboSlot(_comboSequence.GetArrayElementAtIndex(i));
                SyncParallelArray(_comboAttackHands, targetSize, InitializeHandSlot);
                SyncParallelArray(_comboDamageWindows, targetSize, InitializeDamageWindowSlot);
                SyncParallelArray(_comboAlignmentWindows, targetSize, InitializeAlignmentWindowSlot);
                // 新元素在 ApplyModifiedProperties 之前 C# 侧尚未实体化，
                // Animancer 会通过反射拿到 null，导致 Context.Transition.GetType() 崩溃。
                // 提前提交并跳过本帧渲染，下一帧元素已就绪再画。
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                return;
            }
            else
            {
                SyncParallelArray(_comboAttackHands, targetSize, InitializeHandSlot);
                SyncParallelArray(_comboDamageWindows, targetSize, InitializeDamageWindowSlot);
                SyncParallelArray(_comboAlignmentWindows, targetSize, InitializeAlignmentWindowSlot);
            }

            EnsureUniqueStableKeys(_comboSequence);

            for (int i = 0; i < _comboSequence.arraySize; i++)
            {
                SerializedProperty sequence = _comboSequence.GetArrayElementAtIndex(i);
                SerializedProperty hand = _comboAttackHands.GetArrayElementAtIndex(i);
                SerializedProperty window = _comboDamageWindows.GetArrayElementAtIndex(i);
                SerializedProperty alignmentWindow = _comboAlignmentWindows.GetArrayElementAtIndex(i);
                EnsureStableKey(sequence);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                int comboAction = DrawComboEntryHeader(i);
                if (comboAction != 0)
                {
                    EditorGUILayout.EndVertical();
                    HandleComboAction(comboAction, i);
                    break;
                }

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
                FistsComboTransition.SetExtraDamageOverlays(
                    FistsComboTransition.GetPropertyKey(sequence),
                    window.FindPropertyRelative("Enabled").boolValue,
                    GetExtraWindowsArray(window));
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

        private int DrawComboEntryHeader(int index)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Combo {index + 1}", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUILayout.Button("\u2191", GUILayout.Width(28f)))
                {
                    EditorGUILayout.EndHorizontal();
                    return -1;
                }
            }

            using (new EditorGUI.DisabledScope(index >= _comboSequence.arraySize - 1))
            {
                if (GUILayout.Button("\u2193", GUILayout.Width(28f)))
                {
                    EditorGUILayout.EndHorizontal();
                    return 1;
                }
            }

            if (GUILayout.Button("Remove", GUILayout.Width(72f)))
            {
                EditorGUILayout.EndHorizontal();
                return 2;
            }

            EditorGUILayout.EndHorizontal();
            return 0;
        }

        private void HandleComboAction(int action, int index)
        {
            switch (action)
            {
                case -1:
                    MoveComboSlot(index, index - 1);
                    break;
                case 1:
                    MoveComboSlot(index, index + 1);
                    break;
                case 2:
                    RemoveComboSlot(index);
                    break;
            }
        }

        private void MoveComboSlot(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= _comboSequence.arraySize || toIndex >= _comboSequence.arraySize)
                return;

            MoveParallelElement(_comboSequence, fromIndex, toIndex);
            MoveParallelElement(_comboAttackHands, fromIndex, toIndex);
            MoveParallelElement(_comboDamageWindows, fromIndex, toIndex);
            MoveParallelElement(_comboAlignmentWindows, fromIndex, toIndex);
        }

        private void RemoveComboSlot(int index)
        {
            DeleteParallelElement(_comboSequence, index);
            DeleteParallelElement(_comboAttackHands, index);
            DeleteParallelElement(_comboDamageWindows, index);
            DeleteParallelElement(_comboAlignmentWindows, index);
        }

        private static void MoveParallelElement(SerializedProperty array, int fromIndex, int toIndex)
        {
            if (array == null || !array.isArray)
                return;

            array.MoveArrayElement(fromIndex, toIndex);
        }

        private static void DeleteParallelElement(SerializedProperty array, int index)
        {
            if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
                return;

            int sizeBefore = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == sizeBefore)
                array.DeleteArrayElementAtIndex(index);
        }

        private static void SyncParallelArray(SerializedProperty array, int targetSize, System.Action<SerializedProperty, int> initializer)
        {
            int oldSize = array.arraySize;
            if (oldSize == targetSize) return;
            array.arraySize = targetSize;
            for (int i = oldSize; i < targetSize; i++)
                initializer?.Invoke(array.GetArrayElementAtIndex(i), i);
        }

        private static void InitializeComboSlot(SerializedProperty element)
        {
            SerializedProperty speed = element.FindPropertyRelative("_Speed");
            if (speed != null)
                speed.floatValue = 1f;
        }

        private static void InitializeHandSlot(SerializedProperty element, int index)
        {
            element.enumValueIndex = index % 2 == 0 ? (int)FistsAttackHand.MainHand : (int)FistsAttackHand.OffHand;
        }

        private static void InitializeDamageWindowSlot(SerializedProperty element, int _)
        {
            element.FindPropertyRelative("Enabled").boolValue = false;
            element.FindPropertyRelative("DamageMultiplier").floatValue = 1f;
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
                key.stringValue = GUID.Generate().ToString();
        }

        private static void EnsureUniqueStableKeys(SerializedProperty comboSequence)
        {
            if (comboSequence == null || !comboSequence.isArray)
                return;

            var used = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < comboSequence.arraySize; i++)
            {
                SerializedProperty sequence = comboSequence.GetArrayElementAtIndex(i);
                SerializedProperty key = sequence.FindPropertyRelative("_StableKey");
                if (key == null)
                    continue;

                if (string.IsNullOrWhiteSpace(key.stringValue) || !used.Add(key.stringValue))
                {
                    string next;
                    do
                    {
                        next = GUID.Generate().ToString();
                    } while (!used.Add(next));

                    key.stringValue = next;
                }
            }
        }

        private static DamageSubWindow[] GetExtraWindowsArray(SerializedProperty window)
        {
            SerializedProperty extraWindowsProp = window.FindPropertyRelative("ExtraWindows");
            if (extraWindowsProp == null || !extraWindowsProp.isArray)
                return null;

            var result = new DamageSubWindow[extraWindowsProp.arraySize];
            for (int i = 0; i < extraWindowsProp.arraySize; i++)
            {
                SerializedProperty sub = extraWindowsProp.GetArrayElementAtIndex(i);
                result[i] = new DamageSubWindow
                {
                    StartNormalized = sub.FindPropertyRelative("StartNormalized").floatValue,
                    EndNormalized = sub.FindPropertyRelative("EndNormalized").floatValue,
                };
            }
            return result;
        }

        private static readonly Color ExtraDamageFillColor = new(0.92f, 0.45f, 0.18f, 0.85f);
        private static readonly Color ExtraDamageFillDisabledColor = new(0.4f, 0.25f, 0.12f, 0.4f);

        private static void DrawDamageWindowBar(SerializedProperty window)
        {
            DrawWindowBar(window, "Damage Window (Primary)", DamageFillColor, DamageFillDisabledColor);

            SerializedProperty enabled = window.FindPropertyRelative("Enabled");
            if (!enabled.boolValue) return;

            SerializedProperty multiplier = window.FindPropertyRelative("DamageMultiplier");
            if (multiplier != null)
            {
                float value = multiplier.floatValue;
                multiplier.floatValue = EditorGUILayout.FloatField("Damage Multiplier", value > 0f ? value : 1f);
                if (multiplier.floatValue <= 0f)
                    multiplier.floatValue = 1f;
            }

            SerializedProperty extraWindows = window.FindPropertyRelative("ExtraWindows");
            if (extraWindows == null) return;

            EditorGUI.indentLevel++;
            int extraCount = EditorGUILayout.IntField("Extra Damage Windows", extraWindows.arraySize);
            if (extraCount != extraWindows.arraySize)
                extraWindows.arraySize = Mathf.Max(0, extraCount);

            for (int i = 0; i < extraWindows.arraySize; i++)
            {
                SerializedProperty sub = extraWindows.GetArrayElementAtIndex(i);
                DrawSubWindowBar(sub, $"Extra Window {i + 1}", ExtraDamageFillColor, ExtraDamageFillDisabledColor);
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawSubWindowBar(
            SerializedProperty subWindow,
            string label,
            Color fillColor,
            Color disabledColor)
        {
            SerializedProperty start = subWindow.FindPropertyRelative("StartNormalized");
            SerializedProperty end = subWindow.FindPropertyRelative("EndNormalized");

            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);

            Rect barRect = EditorGUILayout.GetControlRect(false, WindowBarHeight + 4f);
            barRect = new Rect(barRect.x, barRect.y + 2f, barRect.width, WindowBarHeight);

            float startValue = Mathf.Clamp01(start.floatValue);
            float endValue = Mathf.Clamp01(end.floatValue);
            if (endValue < startValue)
                (startValue, endValue) = (endValue, startValue);

            EditorGUI.DrawRect(barRect, new Color(0.13f, 0.13f, 0.13f));
            float fillStart = Mathf.Lerp(barRect.x + 1f, barRect.xMax - 1f, startValue);
            float fillEnd = Mathf.Lerp(barRect.x + 1f, barRect.xMax - 1f, endValue);
            Rect fillRect = new Rect(fillStart, barRect.y + 1f, Mathf.Max(2f, fillEnd - fillStart), barRect.height - 2f);
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
            enabled.boolValue = EditorGUI.ToggleLeft(headerRect, label, enabled.boolValue);
            if (!enabled.boolValue) return;

            Rect barRect = EditorGUILayout.GetControlRect(false, WindowBarHeight + 4f);
            barRect = new Rect(barRect.x, barRect.y + 2f, barRect.width, WindowBarHeight);

            float startValue = Mathf.Clamp01(start.floatValue);
            float endValue = Mathf.Clamp01(end.floatValue);
            if (endValue < startValue)
                (startValue, endValue) = (endValue, startValue);

            EditorGUI.DrawRect(barRect, new Color(0.13f, 0.13f, 0.13f));
            float fillStart = Mathf.Lerp(barRect.x + 1f, barRect.xMax - 1f, startValue);
            float fillEnd = Mathf.Lerp(barRect.x + 1f, barRect.xMax - 1f, endValue);
            Rect fillRect = new Rect(fillStart, barRect.y + 1f, Mathf.Max(2f, fillEnd - fillStart), barRect.height - 2f);
            EditorGUI.DrawRect(fillRect, enabled.boolValue ? enabledColor : disabledColor);
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
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Attack Geometry Baking", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_bakingCharacterPrefab, new GUIContent("角色 Prefab（烘焙用）"));
            EditorGUILayout.PropertyField(_bakingWeaponPrefab, new GUIContent("武器 Prefab（烘焙用）"));
            _bakeUseRootTransformForSweep = EditorGUILayout.ToggleLeft(
                "要不要使用 Root 变换渲染武器扫掠",
                _bakeUseRootTransformForSweep);
            SessionState.SetBool(BakeUseRootTransformForSweepKey, _bakeUseRootTransformForSweep);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_bakingCharacterPrefab.objectReferenceValue == null))
                {
                    if (GUILayout.Button("从 Collider + Clip 烘焙"))
                        BakeFromClipSampling();
                }

#pragma warning disable CS0618 // Obsolete
                if (GUILayout.Button("写入旧模板 (Fallback)"))
                    WriteAttackGeometryTemplateJsonLegacy();
#pragma warning restore CS0618

                if (GUILayout.Button("清空几何缓存"))
                {
                    AttackClipGeometryLibrary.ClearCache();
                    Repaint();
                    InternalEditorUtility.RepaintAllViews();
                }
            }
        }

        private void BakeFromClipSampling()
        {
            if (target is not WeaponSO weapon) return;
            GameObject bakingCharacterPrefab = _bakingCharacterPrefab.objectReferenceValue as GameObject;
            GameObject bakingWeaponPrefab = _bakingWeaponPrefab.objectReferenceValue as GameObject;
            if (bakingCharacterPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "请先指定角色 Prefab（烘焙用）", "OK");
                return;
            }

            if (weapon.ComboSequence == null || weapon.ComboSequence.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "ComboSequence 为空，无法烘焙", "OK");
                return;
            }

            // 提取 AnimationClip 数组
            var clips = new UnityEngine.AnimationClip[weapon.ComboSequence.Length];
            for (int i = 0; i < weapon.ComboSequence.Length; i++)
            {
                clips[i] = weapon.ComboSequence[i]?.Clip;
            }

            string geometryId = weapon.GetAttackGeometryId();
            string displayName = string.IsNullOrWhiteSpace(weapon.DisplayName) ? weapon.name : weapon.DisplayName;
            GameObject bakeWeaponPrefab = bakingWeaponPrefab != null ? bakingWeaponPrefab : weapon.Prefab;

            var definition = AttackClipGeometryTemplateFactory.CreateFromClipSampling(
                bakingCharacterPrefab,
                bakeWeaponPrefab,
                weapon.ItemID,
                displayName,
                clips,
                weapon.ComboDamageWindows,
                mountSlot: weapon.EquipSlot,
                holdPositionOffset: weapon.HoldPositionOffset,
                holdRotationOffset: weapon.HoldRotationOffset,
                applyHoldOffset: true,
                useRootTransformForSweep: _bakeUseRootTransformForSweep);

            if (definition == null)
            {
                EditorUtility.DisplayDialog("Error", "烘焙失败，请检查 Console 日志", "OK");
                return;
            }

            AttackClipGeometryLibrary.WriteDefinitionAndRegister(geometryId, definition, $"{weapon.name} Attack Sweep");
            Debug.Log($"[WeaponSOEditor] Baked attack geometry from Collider + Clip: {AttackClipGeometryLibrary.ToAssetPath(geometryId)}");
            InternalEditorUtility.RepaintAllViews();
        }

#pragma warning disable CS0618
        private void WriteAttackGeometryTemplateJsonLegacy()
        {
            if (target is not WeaponSO weapon) return;

            string geometryId = weapon.GetAttackGeometryId();
            var definition = AttackClipGeometryTemplateFactory.CreateForWeapon(weapon);
            AttackClipGeometryLibrary.WriteDefinitionAndRegister(geometryId, definition, $"{weapon.name} Attack Sweep");
            Debug.Log($"[WeaponSOEditor] Wrote legacy attack geometry template: {AttackClipGeometryLibrary.ToAssetPath(geometryId)}");
        }
#pragma warning restore CS0618
    }
}
