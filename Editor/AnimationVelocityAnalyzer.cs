using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BBBNexus
{
    // 根运动速度分析器 用于离线计算动画本地速度曲线与物理推荐参数 
    // 支持逆向推导 JumpForce 确保 Y 向初速度与动画高度匹配 
    public class AnimationVelocityAnalyzer : EditorWindow
    {
        // 目标采样模型引用 为空则无法获取 Animator 
        private GameObject _targetPrefab;
        // 待分析的动画片段 这里会被替换入控制器循环播放 
        private AnimationClip _clip;
        // 采样频率 FPS 越高曲线点数越多但精度越精细 
        private int _sampleRate = 60;

        // 曲线数据 X/Y/Z 分别代表本地坐标系的三个方向速度 
        private AnimationCurve _curveVelX = new AnimationCurve();
        private AnimationCurve _curveVelY = new AnimationCurve();
        private AnimationCurve _curveVelZ = new AnimationCurve();
        // 速度合成 这是根运动驱动的实际速率指标 
        private AnimationCurve _curveSpeed = new AnimationCurve();

        // UI 滚动视图的锚点 用于记忆用户的浏览位置 
        private Vector2 _scrollPos;
        // 曲线峰值 用于规范化 Y 轴刻度范围 
        private float _maxSpeed = 1f;

        // 物理计算结果 动画达到的最高点高度 单位米 
        private float _animMaxHeight = 0f;
        // 模拟环境重力常数 默认取 9.81 m/s? 通常与物理引擎同步 
        private float _gravity = 9.81f;
        // 推荐的初速度 由 h 与 g 逆推 V = sqrt(2*g*h) 
        private float _recommendedForce = 0f;
        // 达到最高点耗时 通常用于动画播放速率校准 
        private float _timeToApex = 0f;

        [MenuItem("Tools/BBB-Nexus/Animation Velocity Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<AnimationVelocityAnalyzer>("Root Motion Analyzer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Root Motion 分析器 & 物理推荐", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("功能 1: 分析动画局部速度曲线。\n功能 2: 根据动画高度，逆推物理 JumpForce。", MessageType.Info);

            GUILayout.Space(10);

            // 输入槽位 接收外部资源引用 
            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _targetPrefab, typeof(GameObject), false);
            _clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _clip, typeof(AnimationClip), false);

            // 采样率与重力调控 支持快速重置为物理引擎配置 
            GUILayout.BeginHorizontal();
            _sampleRate = EditorGUILayout.IntSlider("Sample Rate", _sampleRate, 30, 120);
            if (GUILayout.Button("Reset Gravity", GUILayout.Width(100))) _gravity = Mathf.Abs(Physics.gravity.y);
            GUILayout.EndHorizontal();

            _gravity = EditorGUILayout.FloatField("Gravity (g)", _gravity);

            GUILayout.Space(10);

            // 触发分析按钮 验证资源完整性后启动核心流程 
            if (GUILayout.Button("Analyze Motion & Calculate Physics", GUILayout.Height(30)))
            {
                if (_targetPrefab && _clip) AnalyzeRootMotion();
                else EditorUtility.DisplayDialog("Error", "请先赋值 Prefab 和 Clip！", "OK");
            }

            // 物理推荐结果展示 仅在有效数据时显示 
            if (_animMaxHeight > 0.001f)
            {
                GUILayout.Space(15);
                EditorGUILayout.LabelField("Physics Recommendation (物理推荐)", EditorStyles.boldLabel);

                // 结果面板 绿色背景强调推荐值 
                GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                EditorGUILayout.BeginVertical("box");

                // 高度参数 这是物理逆推的输入数据 
                EditorGUILayout.LabelField($"Animation Apex Height (动画最高点):", $"{_animMaxHeight:F3} meters");
                // 时间参数 帮助理解动画的跳跃节奏 
                EditorGUILayout.LabelField($"Time to Apex (到达最高点耗时):", $"{_timeToApex:F3} seconds");

                GUILayout.Space(5);
                // 核心推荐值 这是游戏物理参数的直接来源 
                EditorGUILayout.LabelField($"★ Recommended Jump Force (推荐初速度):", $"{_recommendedForce:F2} m/s", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox($"公式: V = sqrt(2 * g * h)\n基于重力 {_gravity} 计算。", MessageType.None);

                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
            }

            // 速度曲线视图 显示四条曲线帮助理解运动特征 
            GUILayout.Space(20);
            GUILayout.Label($"Velocity Curves (Max: {_maxSpeed:F2} m/s)", EditorStyles.boldLabel);

            // 曲线图例 用颜色标识坐标轴 
            GUILayout.BeginHorizontal();
            GUILayout.Label("X (Red)", EditorStyles.miniLabel);
            GUILayout.Label("Y (Green)", EditorStyles.miniLabel);
            GUILayout.Label("Z (Blue)", EditorStyles.miniLabel);
            GUILayout.Label("Speed (White)", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            // 曲线绘制区 支持滚动浏览多条曲线 
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawCurve("Local X (左右)", _curveVelX, Color.red);
            DrawCurve("Local Y (上下)", _curveVelY, Color.green);
            DrawCurve("Local Z (前后)", _curveVelZ, Color.blue);
            DrawCurve("Magnitude (合速度)", _curveSpeed, Color.white);
            EditorGUILayout.EndScrollView();
        }

        // 绘制单条速度曲线 用指定颜色在编辑器 UI 中展示 
        private void DrawCurve(string label, AnimationCurve curve, Color color)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            // 动态调整纵轴范围 使曲线充分利用绘制面积 
            Rect rect = EditorGUILayout.GetControlRect(false, 30);
            EditorGUI.CurveField(rect, curve, color, new Rect(0, -_maxSpeed, _clip ? _clip.length : 1, _maxSpeed * 2));
        }

        // 核心分析逻辑 采样动画帧 计算速度与高度 逆推物理参数 
        private void AnalyzeRootMotion()
        {
            // 创建临时采样代理 避免污染场景与资源 
            GameObject tempInstance = Instantiate(_targetPrefab, Vector3.zero, Quaternion.identity);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;

            // 获取代理的驱动器 必须存在否则无法采样 
            Animator animator = tempInstance.GetComponent<Animator>();
            if (!animator) { DestroyImmediate(tempInstance); return; }

            // 保存原始控制器 后续会恢复 
            RuntimeAnimatorController originCtrl = animator.runtimeAnimatorController;
            if (originCtrl == null) { DestroyImmediate(tempInstance); Debug.LogError("Prefab 无 Controller"); return; }

            // 创建临时重写控制器 用于注入待测动画 
            AnimatorOverrideController overrideCtrl = new AnimatorOverrideController(originCtrl);
            animator.runtimeAnimatorController = overrideCtrl;

            // 将控制器内的所有动画槽位替换为待测片段 确保能够持续采样 
            var clips = overrideCtrl.animationClips;
            if (clips.Length > 0)
            {
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                foreach (var c in clips) overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(c, _clip));
                overrideCtrl.ApplyOverrides(overrides);
            }

            // 初始化曲线缓冲 清空之前的分析结果 
            _curveVelX = new AnimationCurve();
            _curveVelY = new AnimationCurve();
            _curveVelZ = new AnimationCurve();
            _curveSpeed = new AnimationCurve();
            _maxSpeed = 1f;

            // 物理统计初始值 用于逐帧累积 
            float currentHeight = 0f;
            _animMaxHeight = 0f;
            _timeToApex = 0f;

            // 配置采样模式 启用根运动应用 禁用 LOD 剔除保证完整采样 
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);

            // 计算总采样帧数 确保覆盖整个动画时长 
            float frameRate = _sampleRate;
            float deltaTime = 1f / frameRate;
            int totalFrames = Mathf.CeilToInt(_clip.length * frameRate);

            // 逐帧采样循环 
            for (int i = 0; i <= totalFrames; i++)
            {
                // 驱动动画向前推进 
                float time = i * deltaTime;
                animator.Update(deltaTime);
                // 前两帧跳过 因为速度计算需要历史数据 
                if (i < 2) continue;

                // 获取该帧的根运动位移 这是 Animator 在应用根运动后计算的增量 
                Vector3 worldDelta = animator.deltaPosition;
                // 转换到本地坐标系 方便分析相对于角色面朝方向的速度分量 
                Vector3 localDelta = tempInstance.transform.InverseTransformVector(worldDelta);
                // 除以时间间隔得到该帧的速度向量 
                Vector3 velocity = localDelta / deltaTime;

                // 记录各轴速度 这用于调试和理解动画的运动特征 
                _curveVelX.AddKey(time, velocity.x);
                _curveVelY.AddKey(time, velocity.y);
                _curveVelZ.AddKey(time, velocity.z);

                // 计算速度合成 用于表示实际运动快慢 
                float speed = velocity.magnitude;
                _curveSpeed.AddKey(time, speed);
                // 更新显示范围 使曲线自适应 UI 高度 
                if (speed > _maxSpeed) _maxSpeed = speed;

                // 物理高度累积 这里累积的是根运动的 Y 分量 
                // 注意 这反映的是动画本身想往上跳多少 不是 deltaPosition 的直接累积 
                currentHeight += worldDelta.y;
                // 持续追踪最高点 以及到达时的时间戳 
                if (currentHeight > _animMaxHeight)
                {
                    _animMaxHeight = currentHeight;
                    _timeToApex = time;
                }
            }

            // 物理逆推计算 已知高度 h 与重力 g 求初速度 v 
            // 从能量守恒 (1/2) * m * v^2 = m * g * h 得出 v = sqrt(2 * g * h) 
            // 仅在有效高度时计算 避免 Idle 这类无跳跃的动画产生虚假数据 
            if (_animMaxHeight > 0.1f)
            {
                _recommendedForce = Mathf.Sqrt(2 * _gravity * _animMaxHeight);
            }
            else
            {
                _recommendedForce = 0f;
            }

            // 清理临时资源 
            DestroyImmediate(tempInstance);
            Repaint();
            Debug.Log($"分析完成: {_clip.name}. 推荐 Force: {_recommendedForce:F2}");
        }
    }
}
