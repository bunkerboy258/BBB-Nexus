// RootMotionExtractorWindow
//
// Root Motion Extractor (Ultimate) - 根运动烘焙器（终极版）
//
// This Unity Editor tool bakes root motion and animation curves from character animation clips into PlayerSO configuration assets.
// 本工具用于将角色动画片段的根运动与动画曲线烘焙到PlayerSO配置资源中。
//
// Principle 原理:
// - Samples each MotionClipData animation on a humanoid prefab frame by frame.
//   逐帧采样PlayerSO中每个MotionClipData动画，基于Humanoid模型。
// - Extracts root position, rotation, and foot phase, generating speed/rotation curves and direction data.
//   提取根节点位置、旋转、脚相位，生成速度/旋转曲线与方向数据。
// - [UPGRADED] Uses robust Quaternion-based math for yaw extraction to avoid gimbal lock and jitter.
//   [已升级] 使用基于四元数的稳定算法提取偏航角，避免万向节死锁与抖动。
// - [UPGRADED] Supports variable sample rates (Clip Native, 60 FPS, 120 FPS).
//   [已升级] 支持可变采样率（片段自带、60帧、120帧）。
//
// Workflow 工作流程:
// 1. Select a PlayerSO asset and a humanoid character prefab.
//    选择PlayerSO资源和Humanoid角色Prefab。
// 2. Choose a sample rate for baking precision.
//    选择一个用于烘焙精度的采样率。
// 3. Click "Bake All MotionClipData" to start baking.
//    点击“烘焙全部 MotionClipData”开始烘焙。
//
// This tool is intended for advanced users familiar with Unity animation and root motion systems.
// 本工具适用于熟悉Unity动画与根运动系统的高级用户。

using UnityEngine;
using UnityEditor;
using Characters.Player.Data;
using System.Reflection;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System;

public class RootMotionExtractorWindow : EditorWindow
{
    // --- Asset & Prefab Selection ---
    private PlayerSO _targetSO;
    private GameObject _characterPrefab;

    // --- Baking Configuration ---
    public enum SampleRateMode { FromClip, Fps60, Fps120 }
    private SampleRateMode _sampleRateMode = SampleRateMode.FromClip;
    private HumanBodyBones _leftFootBone = HumanBodyBones.LeftFoot;
    private HumanBodyBones _rightFootBone = HumanBodyBones.RightFoot;

    // --- Batch Settings ---
    private MotionType _batchMotionType = MotionType.CurveDriven;
    private float _batchTargetDuration = 0f;

    // --- Local Direction Filter ---
    private float _localDirFilterAngleDeg = 12f;
    private float _localDirMinDistance = 0.02f;

    // --- Dashboard & Logging ---
    private bool _verboseLogging = true;
    private int _logEveryNFrames = 15;
    private int _maxDashboardEvents = 30;

    // --- Baking State ---
    private bool _isBaking;
    private int _bakeIndex;
    private int _bakeTotal;
    private float _bakeProgress01;
    private string _currentClipName;
    private string _currentStage;
    private string _currentDetail;
    private FootPhase _currentEndPhase;
    private float _currentRotationFinishedTime;
    private int _currentSpeedKeys;
    private int _currentRotKeys;
    private long _currentClipMs;

    // --- UI & Internals ---
    private readonly List<DashboardEvent> _events = new List<DashboardEvent>();
    private Vector2 _eventScroll;
    private readonly Stopwatch _swAll = new Stopwatch();
    private readonly Stopwatch _swClip = new Stopwatch();

    private struct DashboardEvent
    {
        public double Time;
        public string Msg;
        public Color Color;
    }

    private struct PoseInfo { public Vector3 LeftLocal; public Vector3 RightLocal; }

    [MenuItem("Tools/BBB-Nexus/Root Motion Extractor (Ultimate)")]
    public static void ShowWindow()
    {
        GetWindow<RootMotionExtractorWindow>("RM Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Root Motion 烘焙器 (v2.0 - 四元数内核)", EditorStyles.boldLabel);

        // --- Main Configuration ---
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("核心配置", EditorStyles.boldLabel);
            _targetSO = (PlayerSO)EditorGUILayout.ObjectField("配置文件 (PlayerSO)", _targetSO, typeof(PlayerSO), false);
            _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
            _sampleRateMode = (SampleRateMode)EditorGUILayout.EnumPopup("烘焙采样率", _sampleRateMode);
            EditorGUILayout.HelpBox("更高的采样率可以捕捉更精细的运动细节，但会增加烘焙时间和数据量。", MessageType.Info);
        }

        // --- Logging & Dashboard ---
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("日志与仪表盘", EditorStyles.boldLabel);
            _verboseLogging = EditorGUILayout.ToggleLeft("详细日志（控制台 + 仪表盘）", _verboseLogging);
            // ... (rest of the logging UI is unchanged)
            if (GUILayout.Button("清空仪表盘事件"))
            {
                _events.Clear();
                Repaint();
            }
        }

        // --- Advanced Settings ---
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("高级设置", EditorStyles.boldLabel);
            _leftFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("左脚骨骼", _leftFootBone);
            _rightFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("右脚骨骼", _rightFootBone);
            _localDirFilterAngleDeg = Mathf.Clamp(EditorGUILayout.FloatField("方向过滤角度阈值(°)", _localDirFilterAngleDeg), 0f, 90f);
            _localDirMinDistance = Mathf.Max(0f, EditorGUILayout.FloatField("方向最小位移阈值(m)", _localDirMinDistance));
        }

        EditorGUILayout.Space(10);

        // --- Bake Button ---
        using (new EditorGUI.DisabledScope(_isBaking))
        {
            if (GUILayout.Button("烘焙全部 MotionClipData", GUILayout.Height(40)))
            {
                if (_targetSO == null || _characterPrefab == null)
                {
                    EditorUtility.DisplayDialog("错误", "缺少 PlayerSO 或 Character Prefab！", "OK");
                    return;
                }
                BakeAll();
            }
        }

        // --- Batch Settings ---
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("批量设置（精简）Batch Settings", EditorStyles.boldLabel);
        _batchMotionType = (MotionType)EditorGUILayout.EnumPopup("驱动模式 (Type)", _batchMotionType);
        _batchTargetDuration = EditorGUILayout.FloatField("目标时长 (Target Duration)", _batchTargetDuration);
        if (GUILayout.Button("应用到所有 MotionClipData", GUILayout.Height(30)))
        {
            if (_targetSO == null)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入 PlayerSO！", "OK");
                return;
            }
            ApplyBatchSettings();
        }

        DrawDashboard();
    }

    // --- Main Baking Logic (Entry Point) ---
    private void BakeAll()
    {
        _isBaking = true;
        _bakeIndex = 0;
        _bakeProgress01 = 0f;
        _currentClipName = string.Empty;
        _currentStage = "Init";
        _currentDetail = string.Empty;
        _swAll.Restart();

        AddEvent($"开始烘焙：{_targetSO.name}", new Color(0.4f, 1f, 0.6f));
        LogVerbose($"=== BakeAll START => {_targetSO.name} ===", "#44ff88");

        GameObject agent = Instantiate(_characterPrefab);
        agent.hideFlags = HideFlags.HideAndDontSave;
        Animator animator = agent.GetComponent<Animator>();

        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            UnityEngine.Debug.LogError("模型必须带有 Animator 且为 Humanoid Avatar！");
            DestroyImmediate(agent);
            _isBaking = false;
            return;
        }

        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        try
        {
            var allClips = new List<MotionClipData>();
            ScanMotionClipDataRecursive(_targetSO, data => {
                if (data != null && data.Clip != null && data.Clip.Clip != null)
                    allClips.Add(data);
            });

            _bakeTotal = allClips.Count;
            for (int current = 0; current < allClips.Count; current++)
            {
                var data = allClips[current];
                _bakeIndex = current;
                _currentClipName = data.Clip.Clip.name;
                _bakeProgress01 = (float)current / Mathf.Max(1, _bakeTotal);
                _currentStage = "Bake";
                _currentDetail = "Preparing...";

                LogVerbose($"--> Clip START: {_currentClipName}", "#ffd54a");
                AddEvent($"开始：{_currentClipName}", new Color(1f, 0.92f, 0.35f));

                EditorUtility.DisplayProgressBar("Baking Root Motion", $"Processing {data.Clip.Clip.name}...", _bakeProgress01);

                _swClip.Restart();
                BakeSingleClip(animator, data); // The core logic is here
                _swClip.Stop();

                _currentClipMs = _swClip.ElapsedMilliseconds;
                LogVerbose($"<-- Clip DONE: {_currentClipName} ({_currentClipMs}ms)", "#66ff88");
                AddEvent($"完成：{_currentClipName}  ({_currentClipMs}ms)", new Color(0.5f, 1f, 0.65f));

                Repaint();
            }

            EditorUtility.SetDirty(_targetSO);
            AssetDatabase.SaveAssets();
            _swAll.Stop();

            AddEvent($"全部完成：{_swAll.ElapsedMilliseconds}ms", new Color(0.35f, 1f, 0.9f));
            LogVerbose($"=== BakeAll DONE => {_swAll.ElapsedMilliseconds}ms ===", "#44ffee");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            DestroyImmediate(agent);
            _isBaking = false;
            _currentStage = "Idle";
            _bakeProgress01 = 1f;
            Repaint();
        }
    }

    // --- [UPGRADED] Core Clip Baking Method ---
    private void BakeSingleClip(Animator animator, MotionClipData data)
    {
        AnimationClip clip = data.Clip.Clip;

        // 1. Determine Sample Rate based on UI selection
        float frameRate;
        switch (_sampleRateMode)
        {
            case SampleRateMode.Fps60:
                frameRate = 60f;
                break;
            case SampleRateMode.Fps120:
                frameRate = 120f;
                break;
            case SampleRateMode.FromClip:
            default:
                frameRate = clip.frameRate > 0 ? clip.frameRate : 30;
                break;
        }

        float interval = 1f / frameRate;
        float totalTime = Mathf.Max(clip.length, 0.001f);
        int frameCount = Mathf.CeilToInt(totalTime * frameRate);

        _currentStage = "RotateScan";
        _currentDetail = $"frames={frameCount}, fps={frameRate}";
        Repaint();

        // 2. First Pass: Collect rotation curve for RotationFinishedTime using Quaternion math
        AnimationCurve tempRotCurve = new AnimationCurve();
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Quaternion lastRot = Quaternion.identity;
        float accRotY = 0f;

        for (int i = 0; i <= frameCount; i++)
        {
            float time = Mathf.Min(i * interval, totalTime);
            clip.SampleAnimation(animator.gameObject, time);
            Quaternion currentRot = animator.transform.rotation;

            if (i > 0)
            {
                // Calculate yaw delta using the robust Quaternion method
                Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRot);
                Vector3 rotatedForward = deltaRot * Vector3.forward;
                rotatedForward.y = 0;
                float deltaYaw = Vector3.SignedAngle(Vector3.forward, rotatedForward.normalized, Vector3.up);

                accRotY += deltaYaw;
            }

            tempRotCurve.AddKey(time, accRotY);
            lastRot = currentRot;
        }

        data.RotationFinishedTime = CalculateRotationFinishedTime(tempRotCurve, totalTime);
        _currentRotationFinishedTime = data.RotationFinishedTime;

        // 3. Sample last pose for foot phase
        _currentStage = "EndPhase";
        Repaint();
        PoseInfo endPose = SampleClipPose(animator, clip, totalTime);
        data.EndPhase = (endPose.LeftLocal.y < endPose.RightLocal.y) ? FootPhase.LeftFootDown : FootPhase.RightFootDown;
        _currentEndPhase = data.EndPhase;

        // 4. Calculate playback speed
        _currentStage = "Speed";
        Repaint();
        data.PlaybackSpeed = (data.TargetDuration > 0.01f) ? (totalTime / data.TargetDuration) : 1f;

        // 5. Second Pass: Bake final speed and rotation curves
        _currentStage = "Curves";
        Repaint();
        data.SpeedCurve = new AnimationCurve();
        data.RotationCurve = new AnimationCurve();
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Vector3 lastPos = Vector3.zero;
        lastRot = Quaternion.identity; // Reset for second pass
        accRotY = 0f;

        Vector3 startPos = Vector3.zero;
        Quaternion startRot = Quaternion.identity;

        for (int i = 0; i <= frameCount; i++)
        {
            float originalTime = Mathf.Min(i * interval, totalTime);
            float scaledTime = originalTime / data.PlaybackSpeed;

            clip.SampleAnimation(animator.gameObject, originalTime);

            Vector3 currentPos = animator.transform.position;
            Quaternion currentRot = animator.transform.rotation;

            if (i == 0)
            {
                startPos = currentPos;
                startRot = currentRot;
                lastPos = currentPos;
                lastRot = currentRot;

                data.SpeedCurve.AddKey(0, 0);
                data.RotationCurve.AddKey(0, 0);
                continue;
            }

            // --- Speed Calculation ---
            float dist = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(lastPos.x, 0, lastPos.z));
            float rawSpeed = dist / interval;
            data.SpeedCurve.AddKey(scaledTime, rawSpeed * data.PlaybackSpeed);

            // --- [UPGRADED] Rotation Calculation ---
            Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRot);
            Vector3 rotatedForward = deltaRot * Vector3.forward;
            rotatedForward.y = 0;
            float deltaYaw = Vector3.SignedAngle(Vector3.forward, rotatedForward.normalized, Vector3.up);
            accRotY += deltaYaw;
            data.RotationCurve.AddKey(scaledTime, accRotY);

            lastPos = currentPos;
            lastRot = currentRot;
        }

        // 6. Infer TargetLocalDirection
        if (data.AllowBakeTargetLocalDirection)
        {
            Vector3 endPos = animator.transform.position;
            Vector3 startForwardVec = startRot * Vector3.forward;
            startForwardVec.y = 0;
            float startRootYaw = Vector3.SignedAngle(Vector3.forward, startForwardVec.normalized, Vector3.up);
            BakeTargetLocalDirection(data, startPos, endPos, startRootYaw);
        }
        else
        {
            data.TargetLocalDirection = Vector3.zero;
        }

        _currentSpeedKeys = data.SpeedCurve.length;
        _currentRotKeys = data.RotationCurve.length;

        // 7. Smooth curves (optional but recommended)
        _currentStage = "Smooth";
        Repaint();
        // SmoothCurve(data.SpeedCurve, 3);
        // SmoothCurve(data.RotationCurve, 5);

        LogVerbose($"{clip.name} DONE => EndPhase={data.EndPhase}, RotFinish={data.RotationFinishedTime:0.00}s, PlaybackSpeed={data.PlaybackSpeed:0.000}", "#66ff88");
    }

    // --- Helper & Utility Functions (Unchanged) ---
    private void DrawDashboard()
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("仪表盘 Dashboard", EditorStyles.boldLabel);
            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r, _bakeProgress01, _isBaking ? $"{_bakeIndex + 1}/{Mathf.Max(1, _bakeTotal)}  {_currentClipName}" : "Idle");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("阶段 Stage", _currentStage ?? string.Empty);
                EditorGUILayout.TextField("细节 Detail", _currentDetail ?? string.Empty);
            }
            var phaseLabel = _currentEndPhase == FootPhase.LeftFootDown ? "L" : "R";
            var phaseColor = _currentEndPhase == FootPhase.LeftFootDown ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.35f, 0.6f, 1f);
            var phaseRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(phaseRect, new Color(0, 0, 0, 0.15f));
            var leftRect = phaseRect;
            leftRect.width = 60;
            EditorGUI.DrawRect(leftRect, phaseColor);
            GUI.Label(leftRect, $"End {phaseLabel}", EditorStyles.whiteLabel);
            var rightRect = phaseRect;
            rightRect.x += 64;
            rightRect.width -= 64;
            GUI.Label(rightRect, $"RotFinish={_currentRotationFinishedTime:F2}s | SpeedKeys={_currentSpeedKeys} | RotKeys={_currentRotKeys} | Clip={_currentClipMs}ms", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            GUILayout.Label("事件流 Event Log", EditorStyles.boldLabel);
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MinHeight(140));
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                var style = new GUIStyle(EditorStyles.label) { richText = true, normal = { textColor = e.Color } };
                GUILayout.Label($"[{e.Time:0.00}s] {e.Msg}", style);
            }
            EditorGUILayout.EndScrollView();
        }
    }
    private void AddEvent(string msg, Color color)
    {
        _events.Insert(0, new DashboardEvent { Time = EditorApplication.timeSinceStartup, Msg = msg, Color = color });
        if (_events.Count > _maxDashboardEvents)
            _events.RemoveRange(_maxDashboardEvents, _events.Count - _maxDashboardEvents);
        Repaint();
    }
    private void LogVerbose(string msg, string colorTag)
    {
        if (!_verboseLogging) return;
        UnityEngine.Debug.Log($"<color={colorTag}>[RM Baker]</color> {msg}");
    }
    private void ScanMotionClipDataRecursive(object target, Action<MotionClipData> onFound)
    {
        if (target == null || !(target is ScriptableObject)) return;
        var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var value = field.GetValue(target);
            if (value == null) continue;
            if (value is MotionClipData mcd) onFound(mcd);
            else if (value is ScriptableObject so) ScanMotionClipDataRecursive(so, onFound);
            else if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    if (item is MotionClipData itemMcd) onFound(itemMcd);
                    else if (item is ScriptableObject itemSo) ScanMotionClipDataRecursive(itemSo, onFound);
                }
            }
        }
    }
    private void ApplyBatchSettings()
    {
        if (!EditorUtility.DisplayDialog("确认操作", $"你确定要将设置应用到 '{_targetSO.name}' 中的所有 MotionClipData 吗？", "确定 OK", "取消 Cancel")) return;
        try
        {
            int count = 0;
            ScanMotionClipDataRecursive(_targetSO, data => {
                if (data != null) { data.Type = _batchMotionType; data.TargetDuration = _batchTargetDuration; count++; }
            });
            EditorUtility.SetDirty(_targetSO);
            AssetDatabase.SaveAssets();
            AddEvent($"批量设置完成：{count} 个", new Color(0.3f, 0.7f, 1f));
        }
        catch (System.Exception ex)
        {
            AddEvent($"批量设置失败：{ex.Message}", new Color(1f, 0.35f, 0.35f));
            UnityEngine.Debug.LogError($"批量设置失败: {ex.Message}");
        }
    }
    private void BakeTargetLocalDirection(MotionClipData data, Vector3 startPos, Vector3 endPos, float startRootYaw)
    {
        Vector3 delta = endPos - startPos;
        delta.y = 0f;
        if (delta.magnitude < _localDirMinDistance) { data.TargetLocalDirection = Vector3.zero; return; }
        Quaternion startYawRot = Quaternion.Euler(0f, startRootYaw, 0f);
        Vector3 localDir = Quaternion.Inverse(startYawRot) * delta.normalized;
        localDir.y = 0f;
        localDir = localDir.sqrMagnitude > 0.0001f ? localDir.normalized : Vector3.zero;
        if (Vector3.Angle(Vector3.forward, localDir) <= _localDirFilterAngleDeg) { data.TargetLocalDirection = Vector3.zero; return; }
        data.TargetLocalDirection = localDir;
    }
    private PoseInfo SampleClipPose(Animator anim, AnimationClip clip, float time)
    {
        anim.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clip.SampleAnimation(anim.gameObject, time);
        var leftT = anim.GetBoneTransform(_leftFootBone);
        var rightT = anim.GetBoneTransform(_rightFootBone);
        return new PoseInfo
        {
            LeftLocal = leftT != null ? anim.transform.InverseTransformPoint(leftT.position) : Vector3.zero,
            RightLocal = rightT != null ? anim.transform.InverseTransformPoint(rightT.position) : Vector3.zero
        };
    }
    private static float CalculateRotationFinishedTime(AnimationCurve rotCurve, float totalTime)
    {
        if (rotCurve == null || rotCurve.length < 2) return 0f;
        float total = Mathf.Abs(rotCurve.keys[rotCurve.length - 1].value);
        if (total < 15f) return 0f;
        float threshold = total * 0.95f;
        foreach (var key in rotCurve.keys)
        {
            if (Mathf.Abs(key.value) >= threshold) return Mathf.Clamp(key.time, 0f, totalTime);
        }
        return totalTime;
    }
    private void SmoothCurve(AnimationCurve curve, int windowSize)
    {
        if (curve == null || curve.length < windowSize) return;
        Keyframe[] newKeys = new Keyframe[curve.length];
        int half = windowSize / 2;
        for (int i = 0; i < curve.length; i++)
        {
            float sum = 0; int count = 0;
            for (int j = -half; j <= half; j++)
            {
                int idx = i + j;
                if (idx >= 0 && idx < curve.length) { sum += curve.keys[idx].value; count++; }
            }
            newKeys[i] = new Keyframe(curve.keys[i].time, sum / count, 0, 0);
        }
        curve.keys = newKeys;
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
    }
}
