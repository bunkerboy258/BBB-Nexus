using UnityEngine;
using UnityEditor;
using Characters.Player.Data;
using System.Reflection;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;

public class RootMotionExtractorWindow : EditorWindow
{
    private PlayerSO _targetSO;
    private GameObject _characterPrefab;
    private HumanBodyBones _leftFootBone = HumanBodyBones.LeftFoot;
    private HumanBodyBones _rightFootBone = HumanBodyBones.RightFoot;

    private MotionType _batchMotionType = MotionType.CurveDriven;
    private float _batchTargetDuration = 0f;

    // [新增] 目标局部方向过滤：与 forward 偏差不大则视为 0（避免“微小的方向偏移”导致横向驱动）
    private float _localDirFilterAngleDeg = 12f;
    private float _localDirMinDistance = 0.02f;

    // =========================
    // 详细日志与仪表盘（Dashboard + Logs）
    // =========================
    private bool _verboseLogging = true;
    private int _logEveryNFrames = 15;
    private int _maxDashboardEvents = 30;

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

    [MenuItem("Tools/BBB-Nexus/Root Motion Extractor (Ultimate)")]
    public static void ShowWindow()
    {
        GetWindow<RootMotionExtractorWindow>("RM Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Root Motion 烘焙器（精简：无截断/无PoseMatching）", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("日志与仪表盘", EditorStyles.boldLabel);
            _verboseLogging = EditorGUILayout.ToggleLeft("详细日志（控制台 + 仪表盘）", _verboseLogging);
            using (new EditorGUI.DisabledScope(!_verboseLogging))
            {
                _logEveryNFrames = Mathf.Clamp(EditorGUILayout.IntField("采样频率（每N帧记录）", _logEveryNFrames), 1, 999);
                _maxDashboardEvents = Mathf.Clamp(EditorGUILayout.IntField("仪表盘事件条数", _maxDashboardEvents), 10, 300);
            }

            if (GUILayout.Button("清空仪表盘事件"))
            {
                _events.Clear();
                Repaint();
            }
        }

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("目标局部方向烘焙", EditorStyles.boldLabel);
            _localDirFilterAngleDeg = Mathf.Clamp(EditorGUILayout.FloatField("过滤角度阈值(°)", _localDirFilterAngleDeg), 0f, 90f);
            _localDirMinDistance = Mathf.Max(0f, EditorGUILayout.FloatField("最小位移阈值(m)", _localDirMinDistance));
            EditorGUILayout.HelpBox("根据整段动画水平位移计算 TargetLocalDirection。若与 forward 夹角小于阈值或位移过小，则视为 0。", MessageType.Info);
        }

        EditorGUILayout.Space(5);
        _targetSO = (PlayerSO)EditorGUILayout.ObjectField("配置文件 (PlayerSO)", _targetSO, typeof(PlayerSO), false);
        _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
        EditorGUILayout.Space(5);
        _leftFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("左脚骨骼", _leftFootBone);
        _rightFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("右脚骨骼", _rightFootBone);
        EditorGUILayout.Space(10);

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

        DrawDashboard();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("批量设置（精简）", EditorStyles.boldLabel);

        _batchMotionType = (MotionType)EditorGUILayout.EnumPopup("驱动模式 (Type)", _batchMotionType);
        _batchTargetDuration = EditorGUILayout.FloatField("目标时长 (Target Duration)", _batchTargetDuration);

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

    private void DrawDashboard()
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("仪表盘", EditorStyles.boldLabel);

            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r, _bakeProgress01, _isBaking
                ? $"{_bakeIndex + 1}/{Mathf.Max(1, _bakeTotal)}  {_currentClipName}"
                : "Idle");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("阶段", _currentStage ?? string.Empty);
                EditorGUILayout.TextField("细节", _currentDetail ?? string.Empty);
            }

            // EndPhase 大字标签
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
            GUI.Label(rightRect,
                $"RotFinish={_currentRotationFinishedTime:F2}s | SpeedKeys={_currentSpeedKeys} | RotKeys={_currentRotKeys} | Clip={_currentClipMs}ms",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(6);
            GUILayout.Label("事件流", EditorStyles.boldLabel);
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MinHeight(140));
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                var style = new GUIStyle(EditorStyles.label);
                style.richText = true;
                var old = GUI.color;
                GUI.color = e.Color;
                GUILayout.Label($"[{e.Time:0.00}s] {e.Msg}", style);
                GUI.color = old;
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void AddEvent(string msg, Color color)
    {
        _events.Insert(0, new DashboardEvent
        {
            Time = EditorApplication.timeSinceStartup,
            Msg = msg,
            Color = color
        });

        if (_events.Count > _maxDashboardEvents)
            _events.RemoveRange(_maxDashboardEvents, _events.Count - _maxDashboardEvents);

        Repaint();
    }

    private void LogVerbose(string msg, string colorTag)
    {
        if (!_verboseLogging) return;
        UnityEngine.Debug.Log($"<color={colorTag}>[RM Baker]</color> {msg}");
    }

    private void ApplyBatchSettings()
    {
        if (!EditorUtility.DisplayDialog(
                "确认操作",
                $"你确定要将设置应用到 '{_targetSO.name}' 中的所有 MotionClipData 吗？\n\n此操作会修改文件，但不会重新烘焙曲线。",
                "确定",
                "取消"))
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
                        count++;
                    }
                }
            }

            EditorUtility.SetDirty(_targetSO);
            AssetDatabase.SaveAssets();

            AddEvent($"批量设置完成：{count} 个 MotionClipData", new Color(0.3f, 0.7f, 1f));
            LogVerbose($"Batch applied => {count} MotionClipData", "#4aa3ff");
        }
        catch (System.Exception ex)
        {
            AddEvent($"批量设置失败：{ex.Message}", new Color(1f, 0.35f, 0.35f));
            UnityEngine.Debug.LogError($"批量设置失败: {ex.Message}");
        }
    }

    private void BakeAll()
    {
        _isBaking = true;
        _bakeIndex = 0;
        _bakeProgress01 = 0f;
        _currentClipName = string.Empty;
        _currentStage = "Init";
        _currentDetail = string.Empty;

        _swAll.Reset();
        _swAll.Start();

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
            FieldInfo[] fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.Instance);
            _bakeTotal = fields.Length;

            int current = 0;
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(MotionClipData))
                {
                    MotionClipData data = (MotionClipData)field.GetValue(_targetSO);
                    if (data?.Clip?.Clip != null)
                    {
                        _bakeIndex = current;
                        _currentClipName = data.Clip.Clip.name;
                        _bakeProgress01 = (float)current / Mathf.Max(1, _bakeTotal);

                        _currentStage = "Bake";
                        _currentDetail = "Preparing...";

                        if (_verboseLogging)
                        {
                            AddEvent($"开始：{_currentClipName}", new Color(1f, 0.92f, 0.35f));
                            LogVerbose($"--> Clip START: {_currentClipName}", "#ffd54a");
                        }

                        EditorUtility.DisplayProgressBar(
                            "Baking Root Motion",
                            $"Processing {data.Clip.Clip.name}...",
                            _bakeProgress01);

                        _swClip.Reset();
                        _swClip.Start();

                        BakeSingleClip(animator, data);

                        _swClip.Stop();
                        _currentClipMs = _swClip.ElapsedMilliseconds;

                        if (_verboseLogging)
                        {
                            AddEvent($"完成：{_currentClipName}  ({_currentClipMs}ms)", new Color(0.5f, 1f, 0.65f));
                            LogVerbose($"<-- Clip DONE: {_currentClipName} ({_currentClipMs}ms)", "#66ff88");
                        }

                        // 强制刷新 UI
                        Repaint();
                    }
                }

                current++;
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

    private void BakeSingleClip(Animator animator, MotionClipData data)
    {
        AnimationClip clip = data.Clip.Clip;
        float frameRate = clip.frameRate > 0 ? clip.frameRate : 30;
        float interval = 1f / frameRate;
        float totalTime = Mathf.Max(clip.length, 0.001f);
        int frameCount = Mathf.CeilToInt(totalTime * frameRate);

        _currentStage = "RotateScan";
        _currentDetail = $"frames={frameCount}, fps={frameRate}";
        Repaint();

        // --- 1) 采集旋转累计曲线（用于 RotationFinishedTime）---
        AnimationCurve tempRotCurve = new AnimationCurve();
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        float lastRotY = 0f;
        float accRotY = 0f;

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

            // Removed per-frame verbose heartbeat to reduce log spam
            // if (_verboseLogging && _logEveryNFrames > 0 && i % _logEveryNFrames == 0)
            // {
            //     LogVerbose($"{clip.name} [RotateScan] i={i}/{frameCount} t={time:0.00}s accYaw={accRotY:0.0}", "#9b7bff");
            //     AddEvent($"♥ RotateScan {i}/{frameCount}", new Color(0.65f, 0.55f, 1f));
            // }
        }

        data.RotationFinishedTime = CalculateRotationFinishedTime(tempRotCurve, totalTime);
        _currentRotationFinishedTime = data.RotationFinishedTime;

        _currentStage = "EndPhase";
        _currentDetail = "Sampling last pose";
        Repaint();

        // --- 2) 末相位(EndPhase)：取最后一帧脚高度关系 ---
        PoseInfo endPose = SampleClipPose(animator, clip, totalTime);
        data.EndPhase = (endPose.LeftLocal.y < endPose.RightLocal.y) ? FootPhase.LeftFootDown : FootPhase.RightFootDown;
        _currentEndPhase = data.EndPhase;

        _currentStage = "Speed";
        _currentDetail = "Compute playback speed";
        Repaint();

        // --- 3) PlaybackSpeed ---
        data.PlaybackSpeed = (data.TargetDuration > 0.01f) ? (totalTime / data.TargetDuration) : 1f;

        _currentStage = "Curves";
        _currentDetail = "Bake Speed/Rotation curves";
        Repaint();

        // --- 4) 生成曲线 ---
        data.SpeedCurve = new AnimationCurve();
        data.RotationCurve = new AnimationCurve();

        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Vector3 lastPos = Vector3.zero;
        lastRotY = 0f;
        accRotY = 0f;

        // [新增] 记录起点/终点水平位移，用于推导 TargetLocalDirection
        Vector3 startPos = Vector3.zero;
        Vector3 endPos = Vector3.zero;
        float startRootYaw = 0f;

        for (int i = 0; i <= frameCount; i++)
        {
            float originalTime = Mathf.Min(i * interval, totalTime);
            float scaledTime = originalTime / data.PlaybackSpeed;

            clip.SampleAnimation(animator.gameObject, originalTime);
            Vector3 currentPos = animator.transform.position;
            float currentRotY = animator.transform.eulerAngles.y;

            if (i == 0)
            {
                startPos = currentPos;
                startRootYaw = currentRotY;
            }
            if (i == frameCount) endPos = currentPos;

            if (i > 0)
            {
                float dist = Vector3.Distance(
                    new Vector3(currentPos.x, 0, currentPos.z),
                    new Vector3(lastPos.x, 0, lastPos.z));

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

        // [新增] 推导 TargetLocalDirection（基于整段水平位移：end-start）
        if (data.AllowBakeTargetLocalDirection)
        {
            BakeTargetLocalDirection(data, startPos, endPos, startRootYaw);
        }
        else
        {
            data.TargetLocalDirection = Vector3.zero;
        }

        _currentSpeedKeys = data.SpeedCurve.length;
        _currentRotKeys = data.RotationCurve.length;

        _currentStage = "Smooth";
        _currentDetail = "Smoothing curves";
        Repaint();

        SmoothCurve(data.SpeedCurve, 3);
        SmoothCurve(data.RotationCurve, 5);

        if (_verboseLogging)
        {
            LogVerbose($"{clip.name} DONE => EndPhase={data.EndPhase}, RotFinish={data.RotationFinishedTime:0.00}s, PlaybackSpeed={data.PlaybackSpeed:0.000}", "#66ff88");
            AddEvent($"DONE {clip.name} => {data.EndPhase}", new Color(0.4f, 1f, 0.6f));
        }
    }

    private void BakeTargetLocalDirection(MotionClipData data, Vector3 startPos, Vector3 endPos, float startRootYaw)
    {
        Vector3 delta = endPos - startPos;
        delta.y = 0f;

        // 位移太小：直接视为无方向
        if (delta.magnitude < _localDirMinDistance)
        {
            data.TargetLocalDirection = Vector3.zero;
            return;
        }

        // local space：以“起始根骨 yaw”为参考系。
        // 目的：针对“本身不旋转但带方向”的动画（闪避/左右跳），确保方向提取不受微小 rootYaw 漂移影响。
        Quaternion startYawRot = Quaternion.Euler(0f, startRootYaw, 0f);
        Vector3 localDir = Quaternion.Inverse(startYawRot) * delta.normalized;
        localDir.y = 0f;
        localDir = localDir.sqrMagnitude > 0.0001f ? localDir.normalized : Vector3.zero;

        // 过滤“微小的方向偏移”：与 local forward 偏差不大一律视为 0
        float angleToForward = Vector3.Angle(Vector3.forward, localDir);
        if (angleToForward <= _localDirFilterAngleDeg)
        {
            data.TargetLocalDirection = Vector3.zero;
            return;
        }

        data.TargetLocalDirection = localDir;
    }

    private struct PoseInfo { public Vector3 LeftLocal; public Vector3 RightLocal; }

    private PoseInfo SampleClipPose(Animator anim, AnimationClip clip, float time)
    {
        anim.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clip.SampleAnimation(anim.gameObject, time);

        PoseInfo info = new PoseInfo();
        var leftT = anim.GetBoneTransform(_leftFootBone);
        var rightT = anim.GetBoneTransform(_rightFootBone);

        info.LeftLocal = leftT != null ? anim.transform.InverseTransformPoint(leftT.position) : Vector3.zero;
        info.RightLocal = rightT != null ? anim.transform.InverseTransformPoint(rightT.position) : Vector3.zero;
        return info;
    }

    private static float CalculateRotationFinishedTime(AnimationCurve rotCurve, float totalTime)
    {
        if (rotCurve == null || rotCurve.length < 2) return 0f;

        float total = Mathf.Abs(rotCurve.keys[rotCurve.length - 1].value);
        if (total < 15f) return 0f;

        float threshold = total * 0.95f;
        foreach (var key in rotCurve.keys)
        {
            if (Mathf.Abs(key.value) >= threshold)
            {
                return Mathf.Clamp(key.time, 0f, totalTime);
            }
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
