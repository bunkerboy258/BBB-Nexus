using UnityEngine;
using UnityEditor;
using Characters.Player.Data;
using System.Reflection;

public class RootMotionExtractorWindow : EditorWindow
{
    private PlayerSO _targetSO;
    private GameObject _characterPrefab;
    private HumanBodyBones _leftFootBone = HumanBodyBones.LeftFoot;
    private HumanBodyBones _rightFootBone = HumanBodyBones.RightFoot;

    private MotionType _batchMotionType = MotionType.CurveDriven;
    private float _batchTargetDuration = 0f;
    private bool _batchAutoExitTime = true;
    private bool _batchManualExitTime = false;
    private float _batchManualExitTimeValue = 0.5f;

    [MenuItem("Tools/Root Motion Extractor (Ultimate)")]
    public static void ShowWindow()
    {
        GetWindow<RootMotionExtractorWindow>("RM Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Root Motion 智能烘焙器", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        _targetSO = (PlayerSO)EditorGUILayout.ObjectField("配置文件 (PlayerSO)", _targetSO, typeof(PlayerSO), false);
        _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
        EditorGUILayout.Space(5);
        _leftFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("左脚骨骼", _leftFootBone);
        _rightFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("右脚骨骼", _rightFootBone);
        EditorGUILayout.Space(10);

        if (GUILayout.Button("一键智能烘焙 (Bake All)", GUILayout.Height(40)))
        {
            if (_targetSO == null || _characterPrefab == null)
            {
                EditorUtility.DisplayDialog("错误", "缺少 PlayerSO 或 Character Prefab！", "OK");
                return;
            }
            BakeAll();
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("批量设置工具", EditorStyles.boldLabel);

        _batchMotionType = (MotionType)EditorGUILayout.EnumPopup("驱动模式 (Type)", _batchMotionType);
        _batchTargetDuration = EditorGUILayout.FloatField("目标时长 (Target Duration)", _batchTargetDuration);
        _batchAutoExitTime = EditorGUILayout.Toggle("自动计算截断点", _batchAutoExitTime);
        _batchManualExitTime = EditorGUILayout.Toggle("手动指定截断点", _batchManualExitTime);
        if (_batchManualExitTime)
        {
            _batchManualExitTimeValue = EditorGUILayout.FloatField("  手动截断时间 (s)", _batchManualExitTimeValue);
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("应用到所有 MotionClipData", GUILayout.Height(30)))
        {
            if (_targetSO == null)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入 PlayerSO！", "OK");
                return;
            }
            ApplyBatchSettings();
        }
    }

    private void ApplyBatchSettings()
    {
        if (!EditorUtility.DisplayDialog("确认操作",
            $"你确定要将设置应用到 '{_targetSO.name}' 中的所有 MotionClipData 吗？\n\n此操作会修改文件，但不会重新烘焙曲线。", "确定", "取消"))
        {
            return;
        }

        try
        {
            FieldInfo[] fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.Instance);
            int count = 0;
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(MotionClipData))
                {
                    MotionClipData data = (MotionClipData)field.GetValue(_targetSO);
                    if (data != null)
                    {
                        data.Type = _batchMotionType;
                        data.TargetDuration = _batchTargetDuration;
                        data.AutoCalculateExitTime = _batchAutoExitTime;
                        data.ManualExitTime = _batchManualExitTime;
                        data.ManualExitTimeValue = _batchManualExitTimeValue;
                        count++;
                    }
                }
            }
            EditorUtility.SetDirty(_targetSO);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=blue>批量设置成功！共更新了 {count} 个 MotionClipData。</color>");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"批量设置失败: {ex.Message}");
        }
    }

    private void BakeAll()
    {
        GameObject agent = Instantiate(_characterPrefab);
        agent.hideFlags = HideFlags.HideAndDontSave;
        Animator animator = agent.GetComponent<Animator>();

        if (animator == null || !animator.avatar.isHuman)
        {
            Debug.LogError("模型必须带有 Animator 且为 Humanoid Avatar！");
            DestroyImmediate(agent);
            return;
        }

        // 🔥 [修改] 只有当 L 和 R 都不存在时才警告
        if (_targetSO.ReferenceRunLoop_L == null && _targetSO.ReferenceRunLoop_R == null)
        {
            Debug.LogWarning("未配置任何 Reference Run Loop，将跳过智能匹配，使用默认时长。");
        }

        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        try
        {
            FieldInfo[] fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.Instance);
            int total = fields.Length;
            int current = 0;

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(MotionClipData))
                {
                    MotionClipData data = (MotionClipData)field.GetValue(_targetSO);
                    if (data?.Clip?.Clip != null)
                    {
                        EditorUtility.DisplayProgressBar("Baking Root Motion", $"Processing {data.Clip.Clip.name}...", (float)current / total);
                        BakeSingleClip(animator, data);
                    }
                }
                current++;
            }

            EditorUtility.SetDirty(_targetSO);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>模拟烘焙完成！所有数据已更新。</color>");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            DestroyImmediate(agent);
        }
    }

    private void BakeSingleClip(Animator animator, MotionClipData data)
    {
        AnimationClip clip = data.Clip.Clip;
        float frameRate = clip.frameRate > 0 ? clip.frameRate : 30;
        float interval = 1f / frameRate;
        float totalTime = clip.length;
        if (totalTime <= 0.001f) totalTime = 0.001f;
        int frameCount = Mathf.CeilToInt(totalTime * frameRate);

        // --- 阶段 1: 基础数据采集 ---
        AnimationCurve tempRotCurve = new AnimationCurve();
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        float lastRotY = 0f, accRotY = 0f;

        for (int i = 0; i <= frameCount; i++)
        {
            float time = Mathf.Min(i * interval, totalTime);
            clip.SampleAnimation(animator.gameObject, time);
            if (i > 0)
            {
                float currentRotY = animator.transform.eulerAngles.y;
                accRotY += Mathf.DeltaAngle(lastRotY, currentRotY);
                lastRotY = currentRotY;
            }
            tempRotCurve.AddKey(time, accRotY);
        }
        CalculateRotationFinishedTime(data, tempRotCurve);

        // --- 阶段 2: 智能截断点计算 (Pose Matching) ---
        float searchStartTime = data.RotationFinishedTime;
        if (searchStartTime < 0.1f) searchStartTime = 0.1f;
        if (searchStartTime >= totalTime) searchStartTime = totalTime - 0.1f;
        if (searchStartTime < totalTime * 0.2f) searchStartTime = totalTime * 0.2f;

        // 🔥 [修改] 判断是否至少有一个参考动画存在
        bool hasRefL = _targetSO.ReferenceRunLoop_L?.Clip != null;
        bool hasRefR = _targetSO.ReferenceRunLoop_R?.Clip != null;

        if (data.AutoCalculateExitTime && (hasRefL || hasRefR))
        {
            PoseInfo targetL = default;
            PoseInfo targetR = default;

            // 按需采样
            if (hasRefL) targetL = SampleClipPose(animator, _targetSO.ReferenceRunLoop_L.Clip, 0f);
            if (hasRefR) targetR = SampleClipPose(animator, _targetSO.ReferenceRunLoop_R.Clip, 0f);

            float bestTime = totalTime;
            float minCost = float.MaxValue;
            FootPhase bestPhase = FootPhase.LeftFootDown;

            for (int i = 0; i <= frameCount; i++)
            {
                float time = i * interval;
                if (time < searchStartTime) continue;

                PoseInfo currentPose = SampleClipPose(animator, clip, time);

                // 🔥 [修改] 分开判断 L 和 R
                if (hasRefL)
                {
                    float costL = Vector3.Distance(currentPose.LeftLocal, targetL.LeftLocal) + Vector3.Distance(currentPose.RightLocal, targetL.RightLocal);
                    if (costL < minCost) { minCost = costL; bestTime = time; bestPhase = FootPhase.LeftFootDown; }
                }

                if (hasRefR)
                {
                    float costR = Vector3.Distance(currentPose.LeftLocal, targetR.LeftLocal) + Vector3.Distance(currentPose.RightLocal, targetR.RightLocal);
                    if (costR < minCost) { minCost = costR; bestTime = time; bestPhase = FootPhase.RightFootDown; }
                }
            }

            // 防御性检查
            if (bestTime < 0.1f) bestTime = totalTime;

            data.EffectiveExitTime = bestTime;
            data.EndPhase = bestPhase;
            Debug.Log($"[{clip.name}] 最佳截断: {bestTime:F2}s -> {bestPhase} (Cost: {minCost:F3})");
        }
        else
        {
            data.EffectiveExitTime = data.ManualExitTime ? Mathf.Clamp(data.ManualExitTimeValue, 0.1f, totalTime) : totalTime;
            PoseInfo endPose = SampleClipPose(animator, clip, data.EffectiveExitTime);
            data.EndPhase = (endPose.LeftLocal.y < endPose.RightLocal.y) ? FootPhase.LeftFootDown : FootPhase.RightFootDown;
        }

        // --- 阶段 3: 计算播放倍速 ---
        if (data.EffectiveExitTime <= 0.001f) data.EffectiveExitTime = totalTime;
        data.PlaybackSpeed = (data.TargetDuration > 0.01f) ? (data.EffectiveExitTime / data.TargetDuration) : 1f;
        data.Duration = (data.TargetDuration > 0.01f) ? data.TargetDuration : data.EffectiveExitTime;

        // --- 阶段 4: 生成最终曲线 ---
        data.SpeedCurve = new AnimationCurve();
        data.RotationCurve = new AnimationCurve();
        int newFrameCount = Mathf.Max(1, Mathf.CeilToInt(data.EffectiveExitTime * frameRate));
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Vector3 lastPos = Vector3.zero;
        lastRotY = 0f;
        accRotY = 0f;

        for (int i = 0; i <= newFrameCount; i++)
        {
            float originalTime = Mathf.Min(i * interval, data.EffectiveExitTime);
            float scaledTime = originalTime / data.PlaybackSpeed;

            clip.SampleAnimation(animator.gameObject, originalTime);
            Vector3 currentPos = animator.transform.position;
            float currentRotY = animator.transform.eulerAngles.y;

            if (i > 0)
            {
                float dist = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(lastPos.x, 0, lastPos.z));
                float rawSpeed = dist / interval;
                data.SpeedCurve.AddKey(scaledTime, rawSpeed * data.PlaybackSpeed);
                float delta = Mathf.DeltaAngle(lastRotY, currentRotY);
                accRotY += delta;
                data.RotationCurve.AddKey(scaledTime, accRotY);
            }
            else
            {
                data.SpeedCurve.AddKey(0, 0);
                data.RotationCurve.AddKey(0, 0);
            }
            lastPos = currentPos;
            lastRotY = currentRotY;
        }

        // --- 阶段 5: 曲线平滑 ---
        SmoothCurve(data.SpeedCurve, 3);
        SmoothCurve(data.RotationCurve, 5);
    }

    private struct PoseInfo { public Vector3 LeftLocal; public Vector3 RightLocal; }

    private PoseInfo SampleClipPose(Animator anim, AnimationClip clip, float time)
    {
        anim.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clip.SampleAnimation(anim.gameObject, time);
        PoseInfo info = new PoseInfo();
        info.LeftLocal = anim.transform.InverseTransformPoint(anim.GetBoneTransform(_leftFootBone).position);
        info.RightLocal = anim.transform.InverseTransformPoint(anim.GetBoneTransform(_rightFootBone).position);
        return info;
    }

    private void CalculateRotationFinishedTime(MotionClipData data, AnimationCurve rotCurve)
    {
        if (rotCurve.length < 2) return;
        float total = Mathf.Abs(rotCurve.keys[rotCurve.length - 1].value);
        if (total < 15f) { data.RotationFinishedTime = 0f; }
        else
        {
            float threshold = total * 0.95f;
            float finishedTime = data.Duration;
            foreach (var key in rotCurve.keys)
            {
                if (Mathf.Abs(key.value) >= threshold)
                {
                    finishedTime = key.time;
                    break;
                }
            }
            data.RotationFinishedTime = Mathf.Min(finishedTime, data.Duration);
        }
    }

    private void SmoothCurve(AnimationCurve curve, int windowSize)
    {
        if (curve.length < windowSize) return;
        Keyframe[] newKeys = new Keyframe[curve.length];
        int half = windowSize / 2;
        for (int i = 0; i < curve.length; i++)
        {
            float sum = 0;
            int count = 0;
            for (int j = -half; j <= half; j++)
            {
                int idx = i + j;
                if (idx >= 0 && idx < curve.length)
                {
                    sum += curve.keys[idx].value;
                    count++;
                }
            }
            newKeys[i] = new Keyframe(curve.keys[i].time, sum / count, 0, 0);
        }
        curve.keys = newKeys;
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
    }
}
