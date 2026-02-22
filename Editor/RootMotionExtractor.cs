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
// - Supports batch settings for motion type and duration.
//   支持批量设置运动类型与目标时长。
//
// Workflow 工作流程:
// 1. Select a PlayerSO asset and a humanoid character prefab.
//    选择PlayerSO资源和Humanoid角色Prefab。
// 2. Click "Bake All MotionClipData" to start baking.
//    点击“烘焙全部 MotionClipData”开始烘焙。
// 3. For each MotionClipData:
//    对每个MotionClipData：
//    - Instantiate the prefab, sample the animation clip frame by frame.
//      实例化Prefab，逐帧采样动画片段。
//    - Calculate and store:
//      计算并存储：
//      * SpeedCurve (root motion speed) - 速度曲线（根运动速度）
//      * RotationCurve (accumulated yaw) - 旋转曲线（累计偏航）
//      * EndPhase (foot phase at end) - 结束脚相位
//      * PlaybackSpeed (for duration scaling) - 播放速度（用于时长缩放）
//      * TargetLocalDirection (if enabled) - 目标局部方向（如启用）
//    - Optionally smooths curves for better blending.
//      可选：平滑曲线以获得更好混合效果。
// 4. Results are saved back to the PlayerSO asset.
//    结果保存回PlayerSO资源。
//
// Additional Features 其他特性:
// - Dashboard for progress and logs (with color coding).
//   内置仪表盘显示进度与日志（彩色标记）。
// - Batch apply motion type/duration to all MotionClipData.
//   批量应用运动类型/目标时长到所有MotionClipData。
// - Local direction filtering to avoid micro-movements.
//   局部方向过滤，避免微小位移导致误判。
//
// Usage Tips 使用提示:
// - Ensure the prefab has a valid Humanoid Animator and Avatar.
//   确保Prefab带有有效的Humanoid Animator和Avatar。
// - Baking does not overwrite animation clips, only updates PlayerSO data.
//   烘焙不会覆盖动画片段，只会更新PlayerSO数据。
// - Use the dashboard to monitor progress and check for errors.
//   使用仪表盘监控进度并检查错误。
//
// This tool is intended for advanced users familiar with Unity animation and root motion systems.
// 本工具适用于熟悉Unity动画与根运动系统的高级用户。

using UnityEngine;
using UnityEditor;
using Characters.Player.Data;
using System.Reflection;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;

// RootMotionExtractorWindow: Unity EditorWindow for root motion baking
// 根运动烘焙器主窗口：用于根运动与动画曲线的烘焙
public class RootMotionExtractorWindow : EditorWindow
{
    // PlayerSO asset to bake into - 目标PlayerSO配置文件
    private PlayerSO _targetSO;
    // Character prefab for sampling - 用于采样的角色Prefab
    private GameObject _characterPrefab;
    // Foot bone selection for phase detection - 用于脚相位检测的骨骼选择
    private HumanBodyBones _leftFootBone = HumanBodyBones.LeftFoot;
    private HumanBodyBones _rightFootBone = HumanBodyBones.RightFoot;

    // Batch settings for all MotionClipData - 批量设置参数
    private MotionType _batchMotionType = MotionType.CurveDriven;
    private float _batchTargetDuration = 0f;

    // Local direction filter settings - 目标局部方向过滤设置
    // Filters out small or nearly forward-only movements
    // 过滤微小或近似forward的位移，避免误判为横向驱动
    private float _localDirFilterAngleDeg = 12f;
    private float _localDirMinDistance = 0.02f;

    // Dashboard and logging - 仪表盘与日志
    private bool _verboseLogging = true;
    private int _logEveryNFrames = 15;
    private int _maxDashboardEvents = 30;

    // Baking state - 烘焙状态
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

    // Dashboard event list - 仪表盘事件列表
    private readonly List<DashboardEvent> _events = new List<DashboardEvent>();
    private Vector2 _eventScroll;

    // Stopwatch for timing - 计时器
    private readonly Stopwatch _swAll = new Stopwatch();
    private readonly Stopwatch _swClip = new Stopwatch();

    // Dashboard event struct - 仪表盘事件结构体
    private struct DashboardEvent
    {
        public double Time; // Event time - 事件时间
        public string Msg;  // Event message - 事件消息
        public Color Color; // Display color - 显示颜色
    }

    // Pose info for foot phase detection - 用于脚相位检测的姿态信息
    private struct PoseInfo { public Vector3 LeftLocal; public Vector3 RightLocal; }

    // Menu item to open the window - 打开窗口的菜单项
    [MenuItem("Tools/BBB-Nexus/Root Motion Extractor (Ultimate)")]
    public static void ShowWindow()
    {
        GetWindow<RootMotionExtractorWindow>("RM Baker");
    }

    /// <summary>
    /// Renders the Editor Window UI and handles user interaction.
    /// 渲染编辑器窗口界面并处理用户交互。
    /// 
    /// Responsibility 职责:
    /// - Provide fields for PlayerSO and Character Prefab assignment.
    ///   提供分配 PlayerSO 和角色 Prefab 的字段。
    /// - Configure batch settings (MotionType, TargetDuration) and direction filters.
    ///   配置批量设置（驱动模式、目标时长）与方向过滤器。
    /// - Provide buttons to trigger full bake or batch settings application.
    ///   提供触发完整烘焙或应用批量设置的按钮。
    /// - Bridge the user input to the baking logic.
    ///   将用户输入桥接到烘焙逻辑。
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Label("Root Motion 烘焙器", EditorStyles.boldLabel);

        // Log and dashboard controls - 日志与仪表盘控制
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

        // Local direction filter controls - 目标局部方向过滤控制
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("目标局部方向烘焙", EditorStyles.boldLabel);
            _localDirFilterAngleDeg = Mathf.Clamp(EditorGUILayout.FloatField("过滤角度阈值(°)", _localDirFilterAngleDeg), 0f, 90f);
            _localDirMinDistance = Mathf.Max(0f, EditorGUILayout.FloatField("最小位移阈值(m)", _localDirMinDistance));
            EditorGUILayout.HelpBox("根据整段动画水平位移计算 TargetLocalDirection。若与 forward 夹角小于阈值或位移过小，则视为 0。\nBased on total horizontal movement, TargetLocalDirection is set. If angle to forward is small or movement is too short, it is set to zero.", MessageType.Info);
        }

        // Asset and prefab selection - 资源与Prefab选择
        EditorGUILayout.Space(5);
        _targetSO = (PlayerSO)EditorGUILayout.ObjectField("配置文件 (PlayerSO)", _targetSO, typeof(PlayerSO), false);
        _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
        EditorGUILayout.Space(5);
        _leftFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("左脚骨骼", _leftFootBone);
        _rightFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("右脚骨骼", _rightFootBone);
        EditorGUILayout.Space(10);

        // Bake button - 烘焙按钮
        using (new EditorGUI.DisabledScope(_isBaking))
        {
            if (GUILayout.Button("烘焙全部 MotionClipData", GUILayout.Height(40)))
            {
                if (_targetSO == null || _characterPrefab == null)
                {
                    EditorUtility.DisplayDialog("错误", "缺少 PlayerSO 或 Character Prefab！\nMissing PlayerSO or Character Prefab!", "OK");
                    return;
                }
                BakeAll();
            }
        }

        // Batch settings UI - 批量设置界面
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("批量设置（精简）Batch Settings", EditorStyles.boldLabel);

        _batchMotionType = (MotionType)EditorGUILayout.EnumPopup("驱动模式 (Type)", _batchMotionType);
        _batchTargetDuration = EditorGUILayout.FloatField("目标时长 (Target Duration)", _batchTargetDuration);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("应用到所有 MotionClipData", GUILayout.Height(30)))
        {
            if (_targetSO == null)
            {
                EditorUtility.DisplayDialog("错误", "请先拖入 PlayerSO！\nPlease assign a PlayerSO first!", "OK");
                return;
            }
            ApplyBatchSettings();
        }

        DrawDashboard();//这个是根据我的个人喜好调用的 可以删
    }

    /// <summary>
    /// Draws the real-time baking dashboard within the editor window.
    /// 在编辑器窗口内绘制实时烘焙仪表盘。
    /// 
    /// Responsibility 职责:
    /// - Visualize baking progress, current stage, and detailed metrics.
    ///   可视化显示烘焙进度、当前阶段和详细指标。
    /// - Display a scrollable event log for tracking history.
    ///   显示可滚动的事件日志以跟踪历史记录。
    /// 
    /// Algorithm 算法:
    /// - Use EditorGUI.ProgressBar based on normalized progress.
    ///   基于归一化进度使用 EditorGUI.ProgressBar。
    /// - Render a specialized footer with color-coded foot phase and timing stats.
    ///   渲染带颜色标记的足部相位和计时统计的专用页脚。
    /// - Loop through the _events list to render log entries.
    ///   通过循环遍历 _events 列表渲染日志条目。
    /// </summary>
    private void DrawDashboard()
    {
        EditorGUILayout.Space(10);//应用 GUI 的默认 box 样式（即带边框的背景框）
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label("仪表盘 Dashboard", EditorStyles.boldLabel);

            var r = EditorGUILayout.GetControlRect(false, 18);
            //获取一个矩形区域用于绘制控件：高度为 18 像素，false 表示不自动扩展宽度。
            //返回的 Rect 将用于放置进度条
            EditorGUI.ProgressBar(r, _bakeProgress01, _isBaking
                ? $"{_bakeIndex + 1}/{Mathf.Max(1, _bakeTotal)}  {_currentClipName}"
                : "Idle");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("阶段 Stage", _currentStage ?? string.Empty);
                EditorGUILayout.TextField("细节 Detail", _currentDetail ?? string.Empty);
            }

            // EndPhase label - 结束脚相位标签
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
            GUILayout.Label("事件流 Event Log", EditorStyles.boldLabel);
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

    /// <summary>
    /// Records a new event in the dashboard log with a timestamp and color.
    /// 在仪表盘日志中记录一个带有时间戳和颜色的新事件。
    /// 
    /// Responsibility 职责:
    /// - Maintain a history of baking steps and statuses for user feedback.
    ///   保持烘焙步骤和状态的历史记录，以便向用户提供反馈。
    /// - Limit the log size to prevent memory or UI overhead.
    ///   限制日志大小以防止内存或 UI 开销。
    /// 
    /// Algorithm 算法:
    /// - Insert new entries at index 0 (top-most in UI).
    ///   在索引 0 处插入新条目（UI 中最顶端）。
    /// - Remove old entries if count exceeds _maxDashboardEvents (FIFO).
    ///   如果数量超过 _maxDashboardEvents，则删除旧条目（先进先出）。
    /// </summary>
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

    /// <summary>
    /// Logs a debug message to the Unity console with a specific color tag.
    /// 使用特定的颜色标签将调试消息记录到 Unity 控制台。
    /// 
    /// Responsibility 职责:
    /// - Provide detailed console output for debugging when _verboseLogging is enabled.
    ///   在启用 _verboseLogging 时，提供用于调试的详细控制台输出。
    /// </summary>
    private void LogVerbose(string msg, string colorTag)
    {
        if (!_verboseLogging) return;
        UnityEngine.Debug.Log($"<color={colorTag}>[RM Baker]</color> {msg}");
    }

    /// <summary>
    /// Synchronizes batch parameters (Type, Duration) to all MotionClipData in the PlayerSO.
    /// 将批量参数（类型、时长）同步到 PlayerSO 中的所有 MotionClipData。
    /// 
    /// Responsibility 职责:
    /// - Bulk update configuration without needing a full rebake of curves.
    ///   批量更新配置，无需重新烘焙曲线。
    /// 
    /// Algorithm 算法:
    /// - Use Reflection to scan PlayerSO for all fields of type MotionClipData.
    ///   通过反射扫描 PlayerSO 中所有类型为 MotionClipData 的字段。
    /// - Update TargetDuration and MotionType directly on identified objects.
    ///   直接在识别出的对象上更新 TargetDuration 和 MotionType。
    /// </summary>
    private void ApplyBatchSettings()
    {
        if (!EditorUtility.DisplayDialog(
                "确认操作",
                $"你确定要将设置应用到 '{_targetSO.name}' 中的所有 MotionClipData 吗？\n\n此操作会修改文件，但不会重新烘焙曲线。\nAre you sure to apply settings to all MotionClipData in '{_targetSO.name}'? This will modify the file but not rebake curves.",
                "确定 OK",
                "取消 Cancel"))
        {
            return;
        }

        try
        {
            FieldInfo[] fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.Instance);
            //使用反射获取 PlayerSO 类型的所有公有实例字段
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
            //标记目标 PlayerSO 资产为“已修改”（dirty），这样 Unity 会在保存项目时将其序列化到磁盘
            AssetDatabase.SaveAssets();
            //强制保存所有已修改的资产（包括刚标记为 dirty 的 _targetSO），确保更改立即写入文件

            AddEvent($"批量设置完成：{count} 个 MotionClipData\nBatch applied: {count} MotionClipData", new Color(0.3f, 0.7f, 1f));
            LogVerbose($"Batch applied => {count} MotionClipData", "#4aa3ff");
        }
        catch (System.Exception ex)
        {
            AddEvent($"批量设置失败：{ex.Message}\nBatch apply failed: {ex.Message}", new Color(1f, 0.35f, 0.35f));
            UnityEngine.Debug.LogError($"批量设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Entry point for the full baking process across all animations in the PlayerSO.
    /// PlayerSO 中所有动画完整烘焙过程的入口点。
    /// 
    /// Responsibility 职责:
    /// - Manage lifecycle of the sampling character instance.
    ///   管理采样角色实例的生命周期。
    /// - Iterate through all MotionClipData and trigger per-clip baking.
    ///   遍历所有 MotionClipData 并触发单个片段烘焙。
    /// - Persist changed data to the asset database.
    ///   将更改的数据持久化到资源数据库。
    /// 
    /// Algorithm 算法:
    /// - Instantiate a temporary clone of the character prefab.
    ///   实例化角色 Prefab 的临时克隆。
    /// - Scan PlayerSO fields via Reflection.
    ///   通过反射扫描 PlayerSO 字段。
    /// - For each valid clip, call BakeSingleClip and monitor execution time.
    ///   对每个有效片段调用 BakeSingleClip 并监控执行时间。
    /// - Force Unity to save changes via EditorUtility.SetDirty and AssetDatabase.SaveAssets.
    ///   通过 EditorUtility.SetDirty 和 AssetDatabase.SaveAssets 强制 Unity 保存更改。
    /// </summary>
    private void BakeAll()
    {
        _isBaking = true;
        _bakeIndex = 0;
        _bakeProgress01 = 0f;//重置烘焙进度
        _currentClipName = string.Empty;
        _currentStage = "Init";
        _currentDetail = string.Empty;

        _swAll.Reset();
        _swAll.Start();//清空当前细节信息并启动总计时器

        AddEvent($"开始烘焙：{_targetSO.name}\nBaking started: {_targetSO.name}", new Color(0.4f, 1f, 0.6f));
        LogVerbose($"=== BakeAll START => {_targetSO.name} ===", "#44ff88");

        // Instantiate the character prefab for sampling - 实例化角色Prefab用于采样
        GameObject agent = Instantiate(_characterPrefab);
        agent.hideFlags = HideFlags.HideAndDontSave;
        //设置 hideFlags 为 HideAndDontSave，使实例化的角色在 Hierarchy 中隐藏，且不会随场景保存
        Animator animator = agent.GetComponent<Animator>();

        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            UnityEngine.Debug.LogError("模型必须带有 Animator 且为 Humanoid Avatar！\nAnimator and Humanoid Avatar required!");
            DestroyImmediate(agent);
            _isBaking = false;
            return;
        }

        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        //重要：设置 Animator 启用根运动，并禁用剔出 

        try
        {
            FieldInfo[] fields = typeof(PlayerSO).GetFields(BindingFlags.Public | BindingFlags.Instance);
            _bakeTotal = fields.Length;
            //使用反射获取 PlayerSO 的所有公有实例字段，并记录总数，用于进度计算

            int current = 0;//烘焙处理索引
            foreach (var field in fields)//遍历所有字段，寻找类型为 MotionClipData 的字段
            {
                if (field.FieldType == typeof(MotionClipData))
                {
                    MotionClipData data = (MotionClipData)field.GetValue(_targetSO);
                    if (data?.Clip?.Clip != null)//这俩问号必须加 貌似null检查只会检查最后一次访问
                    {
                        _bakeIndex = current;
                        _currentClipName = data.Clip.Clip.name;
                        _bakeProgress01 = (float)current / Mathf.Max(1, _bakeTotal);
                        //更新当前索引、剪辑名称和进度（细节除以总数，避免除0）

                        _currentStage = "Bake";
                        _currentDetail = "Preparing...";

                        if (_verboseLogging)
                        {
                            AddEvent($"开始：{_currentClipName}\nStart: {_currentClipName}", new Color(1f, 0.92f, 0.35f));
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
                            AddEvent($"完成：{_currentClipName}  ({_currentClipMs}ms)\nDone: {_currentClipName}  ({_currentClipMs}ms)", new Color(0.5f, 1f, 0.65f));
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
            AddEvent($"全部完成：{_swAll.ElapsedMilliseconds}ms\nAll done: {_swAll.ElapsedMilliseconds}ms", new Color(0.35f, 1f, 0.9f));
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

    /// <summary>
    /// Performs frame-by-frame sampling of a single animation clip to extract root motion and metadata.
    /// 对单个动画片段进行逐帧采样，提取根运动和元数据。
    /// 
    /// Responsibility 职责:
    /// - Extract SpeedCurve, RotationCurve, and EndPhase.
    ///   提取速度曲线、旋转曲线和结束脚相位。
    /// - Calculate RotationFinishedTime for curve-to-input blending.
    ///   计算用于从曲线到输入混合的 RotationFinishedTime。
    /// - Compute the PlaybackSpeed modifier.
    ///   计算播放速度修正系数。
    /// 
    /// Algorithm 算法:
    /// - 1. Accumulate total rotation angle frame-by-frame and detect stable 95% settling point.
    ///   1. 逐帧累加总旋转角度，并检测稳定的 95% 沉淀点。
    /// - 2. Compare left/right foot Y positions at the final frame for EndPhase.
    ///   2. 比较最后一帧左右脚的 Y 轴位置以确定 EndPhase。
    /// - 3. Compute delta position in XZ plane at each frame interval to derive speed keys.
    ///   3. 计算每个时间间隔内 XZ 平面上的位移增量，以推导速度关键帧。
    /// - 4. Optionally derive TargetLocalDirection from total start-to-end vector.
    ///   4. 可选地从整段起始到结束的向量推导 TargetLocalDirection。
    /// - 5. Apply low-pass smoothing to raw curves to remove sensor/sampling noise.
    ///   5. 对原始曲线应用低通平滑，以去除传感器/采样噪点。
    /// </summary>
    private void BakeSingleClip(Animator animator, MotionClipData data)
    {
        AnimationClip clip = data.Clip.Clip;
        float frameRate = clip.frameRate > 0 ? clip.frameRate : 30;
        //确定采样帧率
        float interval = 1f / frameRate;
        float totalTime = Mathf.Max(clip.length, 0.001f);
        int frameCount = Mathf.CeilToInt(totalTime * frameRate);
        //计算采样间隔、总时长（至少0.001秒避免除零），以及总采样帧数（向上取整）

        _currentStage = "RotateScan";
        _currentDetail = $"frames={frameCount}, fps={frameRate}";
        Repaint();

        // 1) Collect rotation curve for RotationFinishedTime - 采集旋转累计曲线用于RotationFinishedTime
        AnimationCurve tempRotCurve = new AnimationCurve();
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        //将角色的根运动变换归零
        float lastRotY = 0f;
        float accRotY = 0f;
        //初始化上一帧的 Y 轴旋转值和累计旋转值

        for (int i = 0; i <= frameCount; i++)
        {
            //计算当前根旋转的 Y 轴欧拉角，从第二帧开始计算与前帧的增量角度
            float time = Mathf.Min(i * interval, totalTime);
            //用采样间隔 精准将时间顺序便利每一帧
            clip.SampleAnimation(animator.gameObject, time);
            if (i > 0)
            {
                float currentRotY = animator.transform.eulerAngles.y;
                accRotY += Mathf.DeltaAngle(lastRotY, currentRotY);
                //这个函数可以处理跨360度的情况，确保增量角度在 -180 到 180 度之间
                //(其实出现一帧能跨越180度的情况也比较罕见，除非动画片段本身就有非常快速的旋转或者采样率过低导致的跳帧)
                lastRotY = currentRotY;
            }
            tempRotCurve.AddKey(time, accRotY);
        }

        data.RotationFinishedTime = CalculateRotationFinishedTime(tempRotCurve, totalTime);
        _currentRotationFinishedTime = data.RotationFinishedTime;
        //分析完成旋转时间点并写入数据

        _currentStage = "EndPhase";
        _currentDetail = "Sampling last pose";
        Repaint();
        //更新ui阶段信息

        // 2) Sample last pose for foot phase - 采样最后一帧判断脚相位
        PoseInfo endPose = SampleClipPose(animator, clip, totalTime);
        data.EndPhase = (endPose.LeftLocal.y < endPose.RightLocal.y) ? FootPhase.LeftFootDown : FootPhase.RightFootDown;
        _currentEndPhase = data.EndPhase;

        _currentStage = "Speed";
        _currentDetail = "Compute playback speed";
        Repaint();
        //更新ui阶段信息

        // 3) Calculate playback speed - 计算播放速度
        data.PlaybackSpeed = (data.TargetDuration > 0.01f) ? (totalTime / data.TargetDuration) : 1f;
        // 如果设置了目标时长（大于0.01秒），则播放倍速 = 原时长 / 目标时长，否则为1（不缩放）

        _currentStage = "Curves";
        _currentDetail = "Bake Speed/Rotation curves";
        Repaint();
        //更新ui阶段信息

        /*第一遍遍历：
        目的是构建一个临时旋转累计曲线 tempRotCurve 用于计算 旋转完成时间 RotationFinishedTime
        它使用原始动画时间（originalTime）采样，记录的是 <未缩放时间轴> 下的累计旋转角度
        分析这个曲线可以找出旋转何时基本稳定（例如旋转达到最终值的某个百分比）
        从而确定混合动画中何时可以从曲线驱动切换到输入驱动
        该曲线仅用于计算一个标量值，之后不再保留。
        (反正编辑器模式不差烘焙时间 屎山造它就完了)*/



        // 4) Bake speed and rotation curves - 生成速度与旋转曲线
        data.SpeedCurve = new AnimationCurve();
        data.RotationCurve = new AnimationCurve();

        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Vector3 lastPos = Vector3.zero;
        lastRotY = 0f;
        accRotY = 0f;
        //重置曲线，将 Animator 根变换归零，初始化上一帧的位置、旋转和累计旋转值。

        // Record start/end positions for direction - 记录起止点用于方向推导
        Vector3 startPos = Vector3.zero;
        Vector3 endPos = Vector3.zero;
        float startRootYaw = 0f;

        for (int i = 0; i <= frameCount; i++)
        {
            float originalTime = Mathf.Min(i * interval, totalTime);
            float scaledTime = originalTime / data.PlaybackSpeed;
            //scaledTime 是缩放后的时间

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
                //从第二帧开始，计算水平平面（忽略 Y 轴）上的位移距离，并除以时间间隔得到原始速度
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

        // 5) Infer TargetLocalDirection if enabled - 推导目标局部方向（如启用）
        // 这个局部方向是来干嘛的？举个栗子：假设由一个闪避动画 如果不去修正预期方向
        // 那么不管这个动画实际是向左闪避还是向右闪避 只要它的根运动是向前的
        // 那么它的 TargetLocalDirection 就会被误判成 Vector3.zero（即没有方向）
        // (其实可以放到warpmotiondata里面 但是我懒得删这个功能了哈哈）
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

        // 6) Smooth curves for blending - 平滑曲线以便混合
        SmoothCurve(data.SpeedCurve, 3);
        SmoothCurve(data.RotationCurve, 5);
        //SmoothCurve 可以对曲线进行平滑处理 
        //这个很重要 不然角色很可能因为噪点抽搐

        if (_verboseLogging)
        {
            LogVerbose($"{clip.name} DONE => EndPhase={data.EndPhase}, RotFinish={data.RotationFinishedTime:0.00}s, PlaybackSpeed={data.PlaybackSpeed:0.000}", "#66ff88");
            AddEvent($"DONE {clip.name} => {data.EndPhase}", new Color(0.4f, 1f, 0.6f));
        }

        /*第二遍遍历：

        目的是生成最终存储的 速度曲线 SpeedCurve 和 旋转曲线 RotationCurve，供运行时使用。

        它同样逐帧采样，但记录的 横轴是缩放后的时间（scaledTime = originalTime / PlaybackSpeed），这是因为播放倍速 PlaybackSpeed 可能改变了动画的时间映射。

        速度值也乘以了 PlaybackSpeed，以匹配缩放后的时间。

        此外，第二遍还记录了起点和终点位置，用于可选的目标局部方向推导。*/
    }

    /// <summary>
    /// Determines the primary direction of movement in local space for the entire clip.
    /// 确定整个片段在局部空间中的主要移动方向。
    /// 
    /// Responsibility 职责:
    /// - Extract the intended local movement vector for directional animations (dodge/jump).
    ///   提取方向性动画（闪避/跳跃）的预期局部移动向量。
    /// 
    /// Algorithm 算法:
    /// - Calculate world delta (EndPos - StartPos)。
    ///   计算世界坐标增量 (EndPos - StartPos)。
    /// - Project into local space relative to the starting rotation (startRootYaw)。
    ///   相对于起始旋转 (startRootYaw) 投影到局部空间。
    /// - Apply thresholds: ignore if distance is too short or if direction is effectively 'Forward'。
    ///   应用阈值：如果距离太短或方向实际上是“向前”，则忽略。
    /// </summary>
    private void BakeTargetLocalDirection(MotionClipData data, Vector3 startPos, Vector3 endPos, float startRootYaw)
    {
        Vector3 delta = endPos - startPos;
        delta.y = 0f;

        // Too little movement: treat as no direction - 位移太小视为无方向
        if (delta.magnitude < _localDirMinDistance)
        {
            data.TargetLocalDirection = Vector3.zero;
            return;
        }

        // Convert to local space using start yaw - 用起始yaw转换到局部空间
        Quaternion startYawRot = Quaternion.Euler(0f, startRootYaw, 0f);
        Vector3 localDir = Quaternion.Inverse(startYawRot) * delta.normalized;
        localDir.y = 0f;
        localDir = localDir.sqrMagnitude > 0.0001f ? localDir.normalized : Vector3.zero;

        // Filter out nearly forward - 过滤近似forward的方向
        float angleToForward = Vector3.Angle(Vector3.forward, localDir);
        if (angleToForward <= _localDirFilterAngleDeg)
        {
            data.TargetLocalDirection = Vector3.zero;
            return;
        }

        data.TargetLocalDirection = localDir;
    }

    /// <summary>
    /// Captures character bone positions at a specific time in an animation.
    /// 捕捉动画中特定时间的角骨骼位置。
    /// 
    /// Responsibility 职责:
    /// - Provide relative positioning of feet for state detection.
    ///   提供用于状态检测的脚部相对位置。
    /// 
    /// Algorithm 算法:
    /// - Force sample the clip at 'time' on the animator instance.
    ///   在 Animator 实例上强制采样该片段的 'time'。
    /// - Map world bone position to root-local space using InverseTransformPoint.
    ///   使用 InverseTransformPoint 将世界骨骼位置映射到根局部空间。
    /// </summary>
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

    /// <summary>
    /// Finds the time offset when the primary rotation of an animation clip is effectively complete.
    /// 找到动画片段的主要旋转动作基本完成的时间偏移量。
    /// 
    /// Responsibility 职责:
    /// - Identify the transition point for "Mixed" motion types where rotation logic should stop forcing yaw.
    ///   确定“混合”运动类型的过渡点，此时旋转逻辑应停止强制偏航。
    /// 
    /// Algorithm 算法:
    /// - Scan the accumulated rotation curve.
    ///   扫描累加旋转曲线。
    /// - Return the time where 95% of the final rotation value has been achieved.
    ///   返回达到最终旋转值 95% 时的时间。
    /// </summary>
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

    /// <summary>
    /// Smoothes an AnimationCurve using a moving average window.
    /// 使用滑动平均窗口平滑 AnimationCurve。
    /// 
    /// Responsibility 职责:
    /// - Reduce jitter in generated speed and rotation curves for smoother runtime movement.
    ///   减少生成的移动和旋转曲线中的抖动，使运行时移动更平滑。
    /// 
    /// Algorithm 算法:
    /// - Average each keyframe with its neighbors within a fixed windowSize.
    ///   在固定的 windowSize 内，将每个关键帧与其邻居进行平均。
    /// - Recalculate tangents to preserve curve continuity.
    ///   重新计算切线以保持曲线连续性。
    /// </summary>
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
