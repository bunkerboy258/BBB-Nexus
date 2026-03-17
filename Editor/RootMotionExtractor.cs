using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System;

namespace BBBNexus
{
    // 根运动烘焙器 负责提取动画中的位移与旋转 存储至离线配置中
    public class RootMotionExtractorWindow : EditorWindow
    {
        // 指定配置注入的根对象 PlayerSO 
        private PlayerSO _targetSO;
        // 支持任意序列化文件作为扫描入口 
        private UnityEngine.Object _targetAsset;
        // 用于动画采样的临时角色预制体 
        private GameObject _characterPrefab;

        // 确定采样频率 保证数据精度 
        public enum SampleRateMode { FromClip, Fps60, Fps120 }
        private SampleRateMode _sampleRateMode = SampleRateMode.FromClip;

        // 指定左右脚骨骼 用于末尾脚相判定 
        private HumanBodyBones _leftFootBone = HumanBodyBones.LeftFoot;
        private HumanBodyBones _rightFootBone = HumanBodyBones.RightFoot;

        // 批量设置时使用的驱动模式 
        private MotionType _batchMotionType = MotionType.CurveDriven;
        // 批量设置的目标时长 影响播放速度 
        private float _batchTargetDuration = 0f;

        // 方向判定角度阈值 
        private float _localDirFilterAngleDeg = 12f;
        // 位移死区 过滤微小移动 
        private float _localDirMinDistance = 0.02f;

        // 新增：旋转完成角度容忍度（度）
        private float _rotationAngleToleranceDeg = 5f;

        // 日志开关 
        private bool _verboseLogging = true;
        // 采样日志输出步长 
        private int _logEveryNFrames = 15;
        // 控制台事件存储容量 
        private int _maxDashboardEvents = 30;

        // 烘焙状态锁定标识 
        private bool _isBaking;
        // 任务进度计数 
        private int _bakeIndex;
        // 待处理任务总数 
        private int _bakeTotal;
        // 百分比进度显示 
        private float _bakeProgress01;
        // 正在处理的剪辑名称 
        private string _currentClipName;
        // 烘焙管线当前阶段 
        private string _currentStage;
        // 阶段明细 
        private string _currentDetail;
        // 结束帧脚相判定结果 
        private FootPhase _currentEndPhase;
        // 旋转位移归零时间点 
        private float _currentRotationFinishedTime;
        // 速度曲线关键帧统计 
        private int _currentSpeedKeys;
        // 旋转曲线关键帧统计 
        private int _currentRotKeys;
        // 采样总耗时 
        private long _currentClipMs;

        // 编辑器事件列表 
        private readonly List<DashboardEvent> _events = new List<DashboardEvent>();
        // 视图滚动锚点 
        private Vector2 _eventScroll;
        // 总任务计时器 
        private readonly Stopwatch _swAll = new Stopwatch();
        // 单个剪辑计时器 
        private readonly Stopwatch _swClip = new Stopwatch();

        // 内部调试事件结构 
        private struct DashboardEvent
        {
            public double Time;
            public string Msg;
            public Color Color;
        }

        // 采样姿态记录单元 
        private struct PoseInfo { public Vector3 LeftLocal; public Vector3 RightLocal; }

        // 菜单入口 注册到工具栏 
        [MenuItem("Tools/BBB-Nexus/RootMotionBaker v2.3.5")]
        public static void ShowWindow()
        {
            GetWindow<RootMotionExtractorWindow>("RM Baker");
        }

        // 渲染编辑器界面逻辑 
        private void OnGUI()
        {
            GUILayout.Label("Root Motion 烘焙器 v2.3.5", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("核心配置", EditorStyles.boldLabel);
                _targetSO = (PlayerSO)EditorGUILayout.ObjectField("玩家总配置", _targetSO, typeof(PlayerSO), false);
                _targetAsset = EditorGUILayout.ObjectField(new GUIContent("任意序列化文件", "可选择任意包含动画数据的资源对象"), _targetAsset, typeof(UnityEngine.Object), false);
                _characterPrefab = (GameObject)EditorGUILayout.ObjectField("采样模型预制体", _characterPrefab, typeof(GameObject), false);
                _sampleRateMode = (SampleRateMode)EditorGUILayout.EnumPopup("烘焙采样率", _sampleRateMode);
                EditorGUILayout.HelpBox("请保证模型预制体的根物体已经配置animator组件(还得有avatar)", MessageType.Info);
                EditorGUILayout.HelpBox("注意 若选择较低的烘焙采样率 要注意结果是否产生了贝塞尔过冲", MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("调试与反馈", EditorStyles.boldLabel);
                _verboseLogging = EditorGUILayout.ToggleLeft("详细日志输出", _verboseLogging);
                if (GUILayout.Button("清空历史记录"))
                {
                    _events.Clear();
                    Repaint();
                }
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("物理算法参数", EditorStyles.boldLabel);
                _leftFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("左脚骨骼引用", _leftFootBone);
                _rightFootBone = (HumanBodyBones)EditorGUILayout.EnumPopup("右脚骨骼引用", _rightFootBone);
                _localDirFilterAngleDeg = Mathf.Clamp(EditorGUILayout.FloatField("本地旋转过滤阈值", _localDirFilterAngleDeg), 0f, 90f);
                _localDirMinDistance = Mathf.Max(0f, EditorGUILayout.FloatField("最小有效位移距离", _localDirMinDistance));
                _rotationAngleToleranceDeg = Mathf.Max(0f, EditorGUILayout.FloatField("旋转结束角度容忍度(度)", _rotationAngleToleranceDeg));
                EditorGUILayout.HelpBox("注意 rotationfinishtime 的烘焙不一定准确 不一定能反映旋转过冲 出现问题请手动调整", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(_isBaking))
            {
                if (GUILayout.Button("执行离线数据烘焙任务", GUILayout.Height(40)))
                {
                    if ((_targetAsset == null && _targetSO == null) || _characterPrefab == null)
                    {
                        EditorUtility.DisplayDialog("报错", "缺少必要的扫描根节点或采样模型", "了解");
                        return;
                    }
                    BakeAll();
                }
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("批量全局操作", EditorStyles.boldLabel);
            _batchMotionType = (MotionType)EditorGUILayout.EnumPopup("批量设置驱动模式", _batchMotionType);
            _batchTargetDuration = EditorGUILayout.FloatField("批量设置目标时长", _batchTargetDuration);
            if (GUILayout.Button("强制同步至所有数据节点", GUILayout.Height(30)))
            {
                if (_targetSO == null)
                {
                    EditorUtility.DisplayDialog("报错", "请先注入 PlayerSO 根节点", "了解");
                    return;
                }
                ApplyBatchSettings();
            }

            DrawDashboard();
        }

        // 执行全局烘焙任务 扫描所有注册表中的动画数据 提取位移与旋转信息 生成离线烘焙实例 
        private void BakeAll()
        {
            var root = _targetAsset != null ? _targetAsset : (UnityEngine.Object)_targetSO;
            if (root == null)
            {
                Debug.LogError("扫描根节点不存在");
                return;
            }

            Undo.RecordObject(root, "Bake All Motion Clip Data");

            _isBaking = true;
            _bakeIndex = 0;
            _bakeProgress01 = 0f;
            _currentClipName = string.Empty;
            _currentStage = "初始化";
            _currentDetail = string.Empty;
            _swAll.Restart();

            AddEvent($"开始烘焙任务 根节点 {root.name}", new Color(0.4f, 1f, 0.6f));

            // 实例化临时采样代理模型 禁止保存至场景 
            GameObject agent = Instantiate(_characterPrefab);
            agent.hideFlags = HideFlags.HideAndDontSave;
            Animator animator = agent.GetComponent<Animator>();

            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                UnityEngine.Debug.LogError("采样模型必须绑定 Humanoid 类型的骨骼资产");
                DestroyImmediate(agent);
                _isBaking = false;
                return;
            }

            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var touchedOwners = new HashSet<UnityEngine.Object>();

            try
            {
                // 利用反射深度遍历配置注入字段 寻找所有 MotionClipData 实例 
                var allClips = new List<(MotionClipData data, FieldInfo field, object owner)>();
                ScanMotionClipDataWithFieldInfo(root, (data, field, owner) => {
                    if (data != null && data.Clip != null && data.Clip.Clip != null)
                        allClips.Add((data, field, owner));
                });

                _bakeTotal = allClips.Count;
                int successCount = 0;

                for (int current = 0; current < allClips.Count; current++)
                {
                    var (originalData, field, owner) = allClips[current];
                    _bakeIndex = current;
                    _currentClipName = originalData.Clip.Clip.name;
                    _bakeProgress01 = (float)current / Mathf.Max(1, _bakeTotal);
                    _currentStage = "正在烘焙";
                    _currentDetail = "准备采样";

                    EditorUtility.DisplayProgressBar("根运动离线烘焙中", $"正在处理剪辑 {originalData.Clip.Clip.name}", _bakeProgress01);

                    _swClip.Restart();

                    // 创建新的离线数据载体 隔离原始配置 
                    MotionClipData bakedData = new MotionClipData();
                    bakedData.Clip = originalData.Clip;
                    bakedData.Type = originalData.Type;
                    bakedData.TargetDuration = originalData.TargetDuration;
                    bakedData.EndTime = originalData.EndTime;
                    bakedData.AllowBakeTargetLocalDirection = originalData.AllowBakeTargetLocalDirection;

                    // 核心烘焙流程 提取物理曲线 
                    BakeSingleClip(animator, bakedData);

                    _swClip.Stop();

                    // 注入新实例 覆盖旧的配置节点 
                    if (field != null && owner != null)
                    {
                        field.SetValue(owner, bakedData);
                        successCount++;

                        if (owner is UnityEngine.Object uo)
                            touchedOwners.Add(uo);
                    }

                    _currentClipMs = _swClip.ElapsedMilliseconds;
                    AddEvent($"采样完成 {_currentClipName} 耗时 {_currentClipMs}ms", new Color(0.5f, 1f, 0.65f));

                    Repaint();
                }

                // 标记资源变动 触发持久化写入 
                touchedOwners.Add(root);
                foreach (var obj in touchedOwners)
                {
                    if (obj != null)
                        EditorUtility.SetDirty(obj);
                }

                AssetDatabase.SaveAssets();

                // 局部强制序列化 保证烘焙数据落地 
                var touchedAssetPaths = new HashSet<string>();
                foreach (var obj in touchedOwners)
                {
                    if (obj == null) continue;
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path))
                        touchedAssetPaths.Add(path);
                }

                if (touchedAssetPaths.Count > 0)
                    AssetDatabase.ForceReserializeAssets(new List<string>(touchedAssetPaths));

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                _swAll.Stop();

                AddEvent($"全部烘焙任务执行完毕 成功更新 {successCount} 个数据实例", new Color(0.35f, 1f, 0.9f));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                DestroyImmediate(agent);
                _isBaking = false;
                _currentStage = "空闲";
                _bakeProgress01 = 1f;
                Repaint();
            }
        }

        // 利用反射机制深度扫描 寻找所有配置注入的动画数据 保证离线数据完整覆盖 
        private void ScanMotionClipDataWithFieldInfo(object target, Action<MotionClipData, FieldInfo, object> onFound)
        {
            if (target == null) return;
            var type = target.GetType();
            if (!typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsClass) return;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var value = field.GetValue(target);
                if (value == null) continue;

                if (field.FieldType == typeof(MotionClipData) || value is MotionClipData mcd)
                {
                    onFound((MotionClipData)value, field, target);
                }
                else if (value is ScriptableObject so)
                {
                    ScanMotionClipDataWithFieldInfo(so, onFound);
                }
                else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    foreach (var item in enumerable)
                    {
                        if (item == null) continue;
                        if (item is MotionClipData itemMcd)
                        {
                            onFound(itemMcd, field, target);
                        }
                        else if (item is ScriptableObject itemSo)
                        {
                            ScanMotionClipDataWithFieldInfo(itemSo, onFound);
                        }
                    }
                }
            }
        }

        // 核心烘焙任务 采样动画帧 提取速度曲线与四元数偏航角 
        private void BakeSingleClip(Animator animator, MotionClipData data)
        {
            AnimationClip clip = data.Clip.Clip;

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

            _currentStage = "旋转扫描";
            _currentDetail = $"总帧数 {frameCount} 目标频率 {frameRate}";
            Repaint();

            // 初次采样 提取偏航累计 
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
                    // 使用四元数差值提取偏航变化量 忽略非垂直轴旋转 
                    Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRot);
                    Vector3 rotatedForward = deltaRot * Vector3.forward;
                    rotatedForward.y = 0;
                    float deltaYaw = Vector3.SignedAngle(Vector3.forward, rotatedForward.normalized, Vector3.up);

                    accRotY += deltaYaw;
                }

                tempRotCurve.AddKey(time, accRotY);
                lastRot = currentRot;
            }

            // 判断旋转逻辑何时结束 用于黑板状态流转 
            data.RotationFinishedTime = CalculateRotationFinishedTime(tempRotCurve, totalTime);
            _currentRotationFinishedTime = data.RotationFinishedTime;

            // 判定剪辑结束时的脚相 辅助黑板进行步频同步 
            _currentStage = "末尾脚相判定";
            Repaint();
            PoseInfo endPose = SampleClipPose(animator, clip, totalTime);
            data.EndPhase = (endPose.LeftLocal.y < endPose.RightLocal.y) ? FootPhase.LeftFootDown : FootPhase.RightFootDown;
            _currentEndPhase = data.EndPhase;

            // 计算播放速率系数 适配目标时长 
            _currentStage = "速率计算";
            Repaint();
            data.PlaybackSpeed = (data.TargetDuration > 0.01f) ? (totalTime / data.TargetDuration) : 1f;

            // 二次采样 注入最终离线曲线 
            _currentStage = "生成速度与旋转曲线";
            Repaint();
            data.SpeedCurve = new AnimationCurve();
            data.RotationCurve = new AnimationCurve();
            animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Vector3 lastPos = Vector3.zero;
            lastRot = Quaternion.identity;
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

                // 计算 XZ 平面物理瞬时速度 并根据速率系数进行缩放 
                float dist = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(lastPos.x, 0, lastPos.z));
                float rawSpeed = dist / interval;
                data.SpeedCurve.AddKey(scaledTime, rawSpeed * data.PlaybackSpeed);

                // 再次提取偏航变化 生成连续旋转曲线 
                Quaternion deltaRot = currentRot * Quaternion.Inverse(lastRot);
                Vector3 rotatedForward = deltaRot * Vector3.forward;
                rotatedForward.y = 0;
                float deltaYaw = Vector3.SignedAngle(Vector3.forward, rotatedForward.normalized, Vector3.up);
                accRotY += deltaYaw;
                data.RotationCurve.AddKey(scaledTime, accRotY);

                lastPos = currentPos;
                lastRot = currentRot;
            }

            // 判定离线本地方向 用于动画混合树驱动 
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

            _currentStage = "平滑处理";
            Repaint();
        }

        // 绘制内部仪表盘视图 
        private void DrawDashboard()
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("烘焙实时监控 仪表盘", EditorStyles.boldLabel);
                var r = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(r, _bakeProgress01, _isBaking ? $"{_bakeIndex + 1}/{Mathf.Max(1, _bakeTotal)} 正在烘焙 {_currentClipName}" : "就绪");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("管线阶段", _currentStage ?? string.Empty);
                    EditorGUILayout.TextField("阶段细节", _currentDetail ?? string.Empty);
                }
                var phaseLabel = _currentEndPhase == FootPhase.LeftFootDown ? "L" : "R";
                var phaseColor = _currentEndPhase == FootPhase.LeftFootDown ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.35f, 0.6f, 1f);
                var phaseRect = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.DrawRect(phaseRect, new Color(0, 0, 0, 0.15f));
                var leftRect = phaseRect;
                leftRect.width = 60;
                EditorGUI.DrawRect(leftRect, phaseColor);
                GUI.Label(leftRect, $"末尾脚相 {phaseLabel}", EditorStyles.whiteLabel);
                var rightRect = phaseRect;
                rightRect.x += 64;
                rightRect.width -= 64;
                GUI.Label(rightRect, $"旋转结束={_currentRotationFinishedTime:F2}s | 速度关键帧={_currentSpeedKeys} | 旋转关键帧={_currentRotKeys}", EditorStyles.miniLabel);
                EditorGUILayout.Space(6);
                GUILayout.Label("意图日志流", EditorStyles.boldLabel);
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

        // 将系统事件压入仪表盘队列 
        private void AddEvent(string msg, Color color)
        {
            _events.Insert(0, new DashboardEvent { Time = EditorApplication.timeSinceStartup, Msg = msg, Color = color });
            if (_events.Count > _maxDashboardEvents)
                _events.RemoveRange(_maxDashboardEvents, _events.Count - _maxDashboardEvents);
            Repaint();
        }

        // 输出详细调试信息至控制台 
        private void LogVerbose(string msg, string colorTag)
        {
            if (!_verboseLogging) return;
            UnityEngine.Debug.Log($"<color={colorTag}>烘焙器</color> {msg}");
        }

        // 批量模式递归扫描配置注入节点 
        private void ScanMotionClipDataRecursive(object target, Action<MotionClipData> onFound)
        {
            if (target == null) return;
            var type = target.GetType();
            if (!typeof(UnityEngine.Object).IsAssignableFrom(type) && !type.IsClass) return;
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var value = field.GetValue(target);
                if (value == null) continue;
                if (field.FieldType == typeof(MotionClipData) || value is MotionClipData mcd) onFound((MotionClipData)value);
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

        // 执行批量配置注入 同步所有数据实例的参数 
        private void ApplyBatchSettings()
        {
            var root = _targetAsset != null ? _targetAsset : (UnityEngine.Object)_targetSO;
            if (root == null)
            {
                EditorUtility.DisplayDialog("报错", "请先注入有效的配置根节点", "了解");
                return;
            }

            if (!EditorUtility.DisplayDialog("风险确认", $"是否强制同步资源 {root.name} 下的所有动画配置", "同步 OK", "取消 Cancel")) return;
            try
            {
                int count = 0;
                ScanMotionClipDataRecursive(root, data => {
                    if (data != null) { data.Type = _batchMotionType; data.TargetDuration = _batchTargetDuration; count++; }
                });
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssets();
                AddEvent($"批量同步完成 覆盖了 {count} 个配置节点", new Color(0.3f, 0.7f, 1f));
            }
            catch (System.Exception ex)
            {
                AddEvent($"批量操作异常 {ex.Message}", new Color(1f, 0.35f, 0.35f));
                UnityEngine.Debug.LogError($"批量同步失败 {ex.Message}");
            }
        }

        // 离线计算角色移动的本地方向 映射至动画混合空间 
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

        // 采样指定时间点的姿态数据 
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

        // 旋转完成判定算法：使用“最终目标角度 + 容忍度”策略
        private float CalculateRotationFinishedTime(AnimationCurve rotCurve, float totalTime)
        {
            if (rotCurve == null || rotCurve.length == 0) return 0f;

            // 最后一帧的累计角度作为最终基准角度
            int lastIndex = rotCurve.length - 1;
            float finalAngle = rotCurve.keys[lastIndex].value;

            // 从最后一帧向前遍历，只要落在 [finalAngle - tol, finalAngle + tol] 范围内都认为是抖动
            float tol = _rotationAngleToleranceDeg;

            // 如果整个曲线都在容忍度内，则认为从起始就已完成，返回曲线起始时间
            int i = lastIndex;
            for (; i >= 0; i--)
            {
                float v = rotCurve.keys[i].value;
                if (Mathf.Abs(Mathf.DeltaAngle(v, finalAngle)) > tol)
                {
                    break; // 找到第一个超出容忍度的帧
                }
            }

            // 如果 i < 0，说明从头到尾都在容忍区间，认为旋转在开头就完成
            if (i < 0)
            {
                return Mathf.Clamp(rotCurve.keys[0].time, 0f, totalTime);
            }

            // 否则，旋转完成时间就是比 i 晚一点的那一帧的时间（i+1）
            int finishedIndex = Mathf.Min(lastIndex, i + 1);
            return Mathf.Clamp(rotCurve.keys[finishedIndex].time, 0f, totalTime);
        }

        // 滑动窗口平滑算法 优化物理曲线表现 (没用到 因为我们的烘焙算法就是反映原动画运动的 再平滑就是画蛇添足)
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
}