using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Editors
{
    public class AnimationVelocityAnalyzer : EditorWindow
    {
        private GameObject _targetPrefab;
        private AnimationClip _clip;
        private int _sampleRate = 60;

        // --- 曲线数据 ---
        private AnimationCurve _curveVelX = new AnimationCurve();
        private AnimationCurve _curveVelY = new AnimationCurve();
        private AnimationCurve _curveVelZ = new AnimationCurve();
        private AnimationCurve _curveSpeed = new AnimationCurve();

        private Vector2 _scrollPos;
        private float _maxSpeed = 1f;

        // --- 物理计算结果 ---
        private float _animMaxHeight = 0f;      // 动画达到的最大高度
        private float _gravity = 9.81f;         // 重力 (可配置)
        private float _recommendedForce = 0f;   // 推荐的 JumpForce
        private float _timeToApex = 0f;         // 达到最高点耗时

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

            // --- 输入 ---
            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _targetPrefab, typeof(GameObject), false);
            _clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _clip, typeof(AnimationClip), false);

            GUILayout.BeginHorizontal();
            _sampleRate = EditorGUILayout.IntSlider("Sample Rate", _sampleRate, 30, 120);
            if (GUILayout.Button("Reset Gravity", GUILayout.Width(100))) _gravity = Mathf.Abs(Physics.gravity.y);
            GUILayout.EndHorizontal();

            _gravity = EditorGUILayout.FloatField("Gravity (g)", _gravity);

            GUILayout.Space(10);

            if (GUILayout.Button("Analyze Motion & Calculate Physics", GUILayout.Height(30)))
            {
                if (_targetPrefab && _clip) AnalyzeRootMotion();
                else EditorUtility.DisplayDialog("Error", "请先赋值 Prefab 和 Clip！", "OK");
            }

            // --- 物理推荐结果 ---
            if (_animMaxHeight > 0.001f)
            {
                GUILayout.Space(15);
                EditorGUILayout.LabelField("Physics Recommendation (物理推荐)", EditorStyles.boldLabel);

                GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField($"Animation Apex Height (动画最高点):", $"{_animMaxHeight:F3} meters");
                EditorGUILayout.LabelField($"Time to Apex (到达最高点耗时):", $"{_timeToApex:F3} seconds");

                GUILayout.Space(5);
                EditorGUILayout.LabelField($"★ Recommended Jump Force (推荐初速度):", $"{_recommendedForce:F2} m/s", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox($"公式: V = sqrt(2 * g * h)\n基于重力 {_gravity} 计算。", MessageType.None);

                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
            }

            // --- 图表 ---
            GUILayout.Space(20);
            GUILayout.Label($"Velocity Curves (Max: {_maxSpeed:F2} m/s)", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("X (Red)", EditorStyles.miniLabel);
            GUILayout.Label("Y (Green)", EditorStyles.miniLabel);
            GUILayout.Label("Z (Blue)", EditorStyles.miniLabel);
            GUILayout.Label("Speed (White)", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawCurve("Local X (左右)", _curveVelX, Color.red);
            DrawCurve("Local Y (上下)", _curveVelY, Color.green);
            DrawCurve("Local Z (前后)", _curveVelZ, Color.blue);
            DrawCurve("Magnitude (合速度)", _curveSpeed, Color.white);
            EditorGUILayout.EndScrollView();
        }

        private void DrawCurve(string label, AnimationCurve curve, Color color)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 30);
            EditorGUI.CurveField(rect, curve, color, new Rect(0, -_maxSpeed, _clip ? _clip.length : 1, _maxSpeed * 2));
        }

        private void AnalyzeRootMotion()
        {
            GameObject tempInstance = Instantiate(_targetPrefab, Vector3.zero, Quaternion.identity);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;

            Animator animator = tempInstance.GetComponent<Animator>();
            if (!animator) { DestroyImmediate(tempInstance); return; }

            RuntimeAnimatorController originCtrl = animator.runtimeAnimatorController;
            if (originCtrl == null) { DestroyImmediate(tempInstance); Debug.LogError("Prefab 无 Controller"); return; }

            AnimatorOverrideController overrideCtrl = new AnimatorOverrideController(originCtrl);
            animator.runtimeAnimatorController = overrideCtrl;

            var clips = overrideCtrl.animationClips;
            if (clips.Length > 0)
            {
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                foreach (var c in clips) overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(c, _clip));
                overrideCtrl.ApplyOverrides(overrides);
            }

            // 初始化
            _curveVelX = new AnimationCurve();
            _curveVelY = new AnimationCurve();
            _curveVelZ = new AnimationCurve();
            _curveSpeed = new AnimationCurve();
            _maxSpeed = 1f;

            // 物理统计变量
            float currentHeight = 0f;
            _animMaxHeight = 0f;
            _timeToApex = 0f;

            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);

            float frameRate = _sampleRate;
            float deltaTime = 1f / frameRate;
            int totalFrames = Mathf.CeilToInt(_clip.length * frameRate);

            for (int i = 0; i <= totalFrames; i++)
            {
                float time = i * deltaTime;
                animator.Update(deltaTime);
                if (i < 2) continue;

                Vector3 worldDelta = animator.deltaPosition;
                Vector3 localDelta = tempInstance.transform.InverseTransformVector(worldDelta);
                Vector3 velocity = localDelta / deltaTime;

                _curveVelX.AddKey(time, velocity.x);
                _curveVelY.AddKey(time, velocity.y);
                _curveVelZ.AddKey(time, velocity.z);

                float speed = velocity.magnitude;
                _curveSpeed.AddKey(time, speed);
                if (speed > _maxSpeed) _maxSpeed = speed;

                // --- 物理高度累积 ---
                // 注意：这里累积的是 worldDelta.y，即动画本身想往上跳多少
                currentHeight += worldDelta.y;
                if (currentHeight > _animMaxHeight)
                {
                    _animMaxHeight = currentHeight;
                    _timeToApex = time;
                }
            }

            // --- 物理逆推计算 ---
            // 公式：v = sqrt(2 * g * h)
            // 只有当高度 > 0.1m 时才计算，避免 Idle 动画算出数值
            if (_animMaxHeight > 0.1f)
            {
                _recommendedForce = Mathf.Sqrt(2 * _gravity * _animMaxHeight);
            }
            else
            {
                _recommendedForce = 0f;
            }

            DestroyImmediate(tempInstance);
            Repaint();
            Debug.Log($"分析完成: {_clip.name}. 推荐 Force: {_recommendedForce:F2}");
        }
    }
}
