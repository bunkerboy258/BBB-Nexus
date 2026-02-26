// Acceleration Deviation Analyzer (v7.1 - Precision & Performance Safeguard)
// 加速度偏差分析器 (v7.1 - 高精度与防卡死保护版)
//
// 更新内容:
// 1. [上限突破] 姿势(Pose)惩罚权重上限提升至 1000，支持更严苛的滑步惩罚。
// 2. [高精度] 搜索步长支持细化至 0.005s (亚帧级别)。
// 3. [防卡死] 引入 O(N) 规模预估，样本量过大(>5W)时触发安全确认弹窗。

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AnimeACT.EditorTools
{
    public class AccelDeviationAnalyzerWindowV7_1 : EditorWindow
    {
        #region Data Structures & Helpers

        private class SavitzkyGolayFilter
        {
            public List<Vector3> Apply(List<Vector3> data)
            {
                if (data.Count < 5) return new List<Vector3>(data);
                var smoothed = new List<Vector3>(data);
                float[] coeffs = { -3, 12, 17, 12, -3 };
                float normalizer = 35f;
                for (int i = 2; i < data.Count - 2; i++)
                {
                    Vector3 sum = Vector3.zero;
                    for (int j = 0; j < 5; j++) sum += coeffs[j] * data[i - 2 + j];
                    smoothed[i] = sum / normalizer;
                }
                return smoothed;
            }
        }

        public struct KinematicFrame
        {
            public Vector3 Pos;
            public Vector3 Vel;
            public Vector3 Acc;
        }

        public struct TransitionResult
        {
            public float ExitTimeA, EnterTimeB, BlendDuration;
            public float NormExitA, NormEnterB;

            public float PoseCost; // 姿势(位置)偏差代价
            public float VelCost;  // 速度偏差代价
            public float AccCost;  // 加速度偏差代价
            public float TotalCostScore;
        }

        #endregion

        #region UI Fields
        private GameObject _characterPrefab;
        private AnimationClip _clipA, _clipB;
        private Vector2 _scrollPos;

        [Range(0, 1)] private float _exitRangeMin = 0f, _exitRangeMax = 1f;
        [Range(0, 1)] private float _enterRangeMin = 0f, _enterRangeMax = 1f;

        // v7.1 提高精度: 步长最小值降低至 0.005
        private float _searchStepTime = 0.05f;
        private string _blendDurationsStr = "0.02, 0.1, 0.2, 0.3";
        private const float MIN_BLEND_DURATION = 0.01f;

        // 骨骼追踪
        private bool _trackLFoot = true, _trackRFoot = true;
        private float _weightLFoot = 1.0f, _weightRFoot = 1.0f;

        // 运动学惩罚权重 (v7.1 提高 Pose 权重上限至 1000)
        private float _weightPose = 100f;
        private float _weightVel = 10f;
        private float _weightAcc = 1f;

        private bool _isSimulating = false;
        private float _progress = 0f;
        private string _logMsg = "等待执行...";
        private Color _logColor = Color.gray;
        private List<TransitionResult> _topResults = new List<TransitionResult>();
        #endregion

        [MenuItem("Tools/BBB-Nexus/Accel. Deviation Analyzer (v7.1 - Precision)")]
        public static void ShowWindow() => GetWindow<AccelDeviationAnalyzerWindowV7_1>("Kinematic Analyzer v7.1");

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Label("动作过渡评估器 (v7.1 - 高精度保护版)", EditorStyles.boldLabel);

            DrawAssetConfig();
            DrawSearchSpaceConfig();
            DrawAdvancedConfig();
            DrawExecutionControls();
            DrawDashboard();

            EditorGUILayout.EndScrollView();
        }

        #region UI Drawing
        private void DrawAssetConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("1. 基础资产 (Assets)", EditorStyles.boldLabel);
                _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
                _clipA = (AnimationClip)EditorGUILayout.ObjectField("切出动画 A", _clipA, typeof(AnimationClip), false);
                _clipB = (AnimationClip)EditorGUILayout.ObjectField("接收动画 B", _clipB, typeof(AnimationClip), false);
            }
        }

        private void DrawSearchSpaceConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("2. 搜索区间 (Search Space)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"A 切出区间: {_exitRangeMin:F2} ~ {_exitRangeMax:F2}");
                EditorGUILayout.MinMaxSlider(ref _exitRangeMin, ref _exitRangeMax, 0f, 1f);
                EditorGUILayout.LabelField($"B 切入区间: {_enterRangeMin:F2} ~ {_enterRangeMax:F2}");
                EditorGUILayout.MinMaxSlider(ref _enterRangeMin, ref _enterRangeMax, 0f, 1f);

                // v7.1 扩大步长调节范围
                _searchStepTime = EditorGUILayout.Slider("搜索步长 (s) [越小越精但越慢]", _searchStepTime, 0.005f, 0.2f);
                _blendDurationsStr = EditorGUILayout.TextField("测试淡入时长 (s)", _blendDurationsStr);
            }
        }

        private void DrawAdvancedConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("3. 高级配置 (Kinematic Weights)", EditorStyles.boldLabel);

                GUILayout.Label("评估维度权重 (Cost Multipliers):", EditorStyles.miniBoldLabel);
                // v7.1 提高 Pose 权重上限，应对严重滑步
                _weightPose = EditorGUILayout.Slider("姿势惩罚倍率 (Pose/Position)", _weightPose, 0f, 1000f);
                _weightVel = EditorGUILayout.Slider("速度惩罚倍率 (Velocity)", _weightVel, 0f, 50f);
                _weightAcc = EditorGUILayout.Slider("力学惩罚倍率 (Acceleration)", _weightAcc, 0f, 5f);

                EditorGUILayout.Space(5);
                GUILayout.Label("骨骼跟踪:", EditorStyles.miniBoldLabel);
                _trackLFoot = EditorGUILayout.ToggleLeft(" 左脚 (Left Foot)", _trackLFoot);
                if (_trackLFoot) _weightLFoot = EditorGUILayout.Slider("   └ 权重", _weightLFoot, 0f, 2f);
                _trackRFoot = EditorGUILayout.ToggleLeft(" 右脚 (Right Foot)", _trackRFoot);
                if (_trackRFoot) _weightRFoot = EditorGUILayout.Slider("   └ 权重", _weightRFoot, 0f, 2f);
            }
        }

        private void DrawExecutionControls()
        {
            using (new EditorGUI.DisabledScope(_isSimulating))
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("开始运动学深度分析 (Start Kinematic Analysis)", GUILayout.Height(40)))
                {
                    if (ValidateSetup() && CheckAndWarnPerformance())
                    {
                        RunSimulation();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawDashboard()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var logStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = _logColor } };
                EditorGUILayout.LabelField("日志 (Log)", EditorStyles.boldLabel);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18), _progress, _logMsg);
                EditorGUILayout.LabelField(">", _logMsg, logStyle);

                if (_topResults.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    GUILayout.Label("🏆 最佳推荐 Top 5", EditorStyles.boldLabel);
                    for (int i = 0; i < Mathf.Min(5, _topResults.Count); i++)
                    {
                        var res = _topResults[i];
                        GUI.backgroundColor = i == 0 ? new Color(0.85f, 1f, 0.85f) : new Color(0.95f, 0.95f, 0.95f);
                        using (new EditorGUILayout.VerticalScope(GUI.skin.button))
                        {
                            GUILayout.Label($"Top {i + 1} | 总分: {res.TotalCostScore:F2}  [P:{res.PoseCost:F1} | V:{res.VelCost:F1} | A:{res.AccCost:F1}]", EditorStyles.boldLabel);
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label($"➡ A: {res.ExitTimeA:F3}s ({res.NormExitA:F2})");
                            GUILayout.Label($"⬅ B: {res.EnterTimeB:F3}s ({res.NormEnterB:F2})");
                            GUILayout.Label($"⏱ Blend: {res.BlendDuration:F2}s");
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
        }
        #endregion

        #region Performance & Validation

        // v7.1 防卡死警告系统
        private bool CheckAndWarnPerformance()
        {
            // 解析 Blend 时长数量
            var blendOptions = Regex.Split(_blendDurationsStr.Trim(), @"[,;，\s]+")
                .Where(s => !string.IsNullOrEmpty(s) && float.TryParse(s, out _))
                .Select(float.Parse)
                .Where(v => v >= MIN_BLEND_DURATION)
                .Distinct().ToArray();

            int blendCount = blendOptions.Length;
            if (blendCount == 0) return true; // 会在 RunSimulation 中拦截报错

            // 计算循环次数预估
            float clipA_duration = _clipA.length * (_exitRangeMax - _exitRangeMin);
            float clipB_duration = _clipB.length * (_enterRangeMax - _enterRangeMin);

            // 步长容错极小值保护，防止除以 0
            float safeStep = Mathf.Max(_searchStepTime, 0.001f);

            int exitSteps = Mathf.CeilToInt(clipA_duration / safeStep) + 1;
            int enterSteps = Mathf.CeilToInt(clipB_duration / safeStep) + 1;

            long totalCombinations = (long)exitSteps * enterSteps * blendCount;

            // 卡顿阈值 (大于 5 万次运算可能导致明显卡顿)
            long warningThreshold = 50000;

            if (totalCombinations > warningThreshold)
            {
                bool forceContinue = EditorUtility.DisplayDialog(
                    "⚠️ 性能警告 (Performance Warning)",
                    $"当前参数将进行【 {totalCombinations} 】次庞大的动画重构与运动学积分计算！\n\n" +
                    "步长过低或搜索区间过大，Unity 将面临数分钟的假死或无响应状态。\n\n" +
                    "建议：\n1. 将[搜索步长]调大 (例如 0.05s)\n2. 缩小动画A和B的搜索区间\n\n是否无视警告，强行继续？",
                    "强行继续 (Force Continue)",
                    "取消返回 (Cancel)"
                );
                return forceContinue;
            }

            return true;
        }

        private bool ValidateSetup()
        {
            if (!_characterPrefab || !_clipA || !_clipB) { SetLog("错误: 请分配完整的资产!", Color.red); return false; }
            if (_exitRangeMin > _exitRangeMax || _enterRangeMin > _enterRangeMax) { SetLog("错误: 区间最小值不能大于最大值!", Color.red); return false; }
            if (!_trackLFoot && !_trackRFoot) { SetLog("警告: 至少需要选择一个追踪骨骼!", Color.yellow); return false; }
            return true;
        }

        #endregion

        #region Core Simulation Logic

        private Dictionary<HumanBodyBones, List<KinematicFrame>> PrecomputeKinematics(AnimationClip clip, Animator anim)
        {
            const float dt = 1f / 60f;
            int frames = Mathf.CeilToInt(clip.length / dt);
            var bonesToTrack = new List<HumanBodyBones>();
            if (_trackLFoot) bonesToTrack.Add(HumanBodyBones.LeftFoot);
            if (_trackRFoot) bonesToTrack.Add(HumanBodyBones.RightFoot);

            var posCache = new Dictionary<HumanBodyBones, List<Vector3>>();
            foreach (var bone in bonesToTrack) posCache[bone] = new List<Vector3>();

            for (int i = 0; i <= frames; i++)
            {
                anim.transform.position = Vector3.zero;
                clip.SampleAnimation(anim.gameObject, i * dt);
                foreach (var bone in bonesToTrack)
                    posCache[bone].Add(anim.GetBoneTransform(bone).position);
            }

            var kinematicCache = new Dictionary<HumanBodyBones, List<KinematicFrame>>();
            var filter = new SavitzkyGolayFilter();
            float invDt2 = 1f / (dt * dt);

            foreach (var bone in bonesToTrack)
            {
                var smoothedPos = filter.Apply(posCache[bone]);
                var framesData = new List<KinematicFrame>();

                for (int i = 0; i < smoothedPos.Count; i++)
                {
                    Vector3 p = smoothedPos[i];
                    Vector3 v = Vector3.zero;
                    Vector3 a = Vector3.zero;

                    if (i > 0 && i < smoothedPos.Count - 1)
                    {
                        v = (smoothedPos[i + 1] - smoothedPos[i - 1]) / (2 * dt);
                        a = (smoothedPos[i + 1] - 2 * p + smoothedPos[i - 1]) * invDt2;
                    }
                    else if (i == 0 && smoothedPos.Count > 1)
                    {
                        v = (smoothedPos[1] - p) / dt;
                    }
                    else if (i == smoothedPos.Count - 1 && smoothedPos.Count > 1)
                    {
                        v = (p - smoothedPos[i - 1]) / dt;
                    }

                    framesData.Add(new KinematicFrame { Pos = p, Vel = v, Acc = a });
                }
                kinematicCache[bone] = framesData;
            }
            return kinematicCache;
        }

        private void RunSimulation()
        {
            _isSimulating = true;
            _topResults.Clear();
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            SetLog("初始化模拟环境...", Color.cyan);

            GameObject agent = Instantiate(_characterPrefab);
            agent.hideFlags = HideFlags.HideAndDontSave;
            Animator anim = agent.GetComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("KinematicAnalyzerGraph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            var mixer = AnimationMixerPlayable.Create(graph, 2);
            var playA = AnimationClipPlayable.Create(graph, _clipA);
            var playB = AnimationClipPlayable.Create(graph, _clipB);

            mixer.ConnectInput(0, playA, 0);
            mixer.ConnectInput(1, playB, 0);
            var output = AnimationPlayableOutput.Create(graph, "Output", anim);
            output.SetSourcePlayable(mixer);

            try
            {
                var blendOptions = Regex.Split(_blendDurationsStr.Trim(), @"[,;，\s]+")
                    .Where(s => !string.IsNullOrEmpty(s) && float.TryParse(s, out _))
                    .Select(float.Parse)
                    .Where(v => v >= MIN_BLEND_DURATION)
                    .Distinct().ToArray();

                if (blendOptions.Length == 0)
                {
                    SetLog($"错误: 未提供任何有效的淡入时长。", Color.red);
                    _isSimulating = false;
                    return;
                }

                SetLog("预计算源动画运动学数据...", Color.yellow);
                var kinA = PrecomputeKinematics(_clipA, anim);
                var kinB = PrecomputeKinematics(_clipB, anim);

                float clipALen = _clipA.length;
                float clipBLen = _clipB.length;
                List<float> exitPoints = GeneratePoints(clipALen * _exitRangeMin, clipALen * _exitRangeMax);
                List<float> enterPoints = GeneratePoints(clipBLen * _enterRangeMin, clipBLen * _enterRangeMax);

                float totalCombinations = exitPoints.Count * enterPoints.Count * blendOptions.Length;
                int currentComb = 0;

                foreach (float tA in exitPoints)
                {
                    foreach (float tB in enterPoints)
                    {
                        foreach (float blendDur in blendOptions)
                        {
                            var result = CalculateTransitionCost(anim, graph, mixer, playA, playB, tA, tB, blendDur, kinA, kinB);

                            result.ExitTimeA = tA;
                            result.EnterTimeB = tB;
                            result.BlendDuration = blendDur;
                            result.NormExitA = tA / clipALen;
                            result.NormEnterB = tB / clipBLen;

                            _topResults.Add(result);
                            currentComb++;
                        }
                    }
                    _progress = currentComb / totalCombinations;
                    SetLog($"模拟计算中... ( {_progress * 100:F0}% )", Color.white);
                    Repaint();
                }

                _topResults.Sort((x, y) => x.TotalCostScore.CompareTo(y.TotalCostScore));
                sw.Stop();
                SetLog($"成功! 分析了 {currentComb} 种组合, 耗时 {sw.ElapsedMilliseconds}ms。", Color.green);
            }
            finally
            {
                graph.Destroy();
                DestroyImmediate(agent);
                _isSimulating = false;
            }
        }

        private TransitionResult CalculateTransitionCost(
            Animator anim, PlayableGraph graph, AnimationMixerPlayable mixer,
            AnimationClipPlayable playA, AnimationClipPlayable playB,
            float tA_start, float tB_start, float blendDur,
            Dictionary<HumanBodyBones, List<KinematicFrame>> kinA,
            Dictionary<HumanBodyBones, List<KinematicFrame>> kinB)
        {
            const float simDt = 1f / 30f;
            int frames = Mathf.CeilToInt(blendDur / simDt);

            var posRecorders = new Dictionary<HumanBodyBones, List<Vector3>>();
            if (_trackLFoot) posRecorders[HumanBodyBones.LeftFoot] = new List<Vector3>();
            if (_trackRFoot) posRecorders[HumanBodyBones.RightFoot] = new List<Vector3>();

            for (int k = -1; k < frames + 1; k++)
            {
                float blendTime = k * simDt;
                float alpha = Mathf.Clamp01(blendTime / blendDur);

                mixer.SetInputWeight(0, 1f - alpha);
                mixer.SetInputWeight(1, alpha);
                playA.SetTime(tA_start + blendTime);
                playB.SetTime(tB_start + blendTime);

                anim.transform.position = Vector3.zero;
                graph.Evaluate(0f);
                foreach (var pair in posRecorders)
                    posRecorders[pair.Key].Add(anim.GetBoneTransform(pair.Key).position);
            }

            float rawPoseCost = 0f;
            float rawVelCost = 0f;
            float rawAccCost = 0f;
            float invDt2 = 1f / (simDt * simDt);

            for (int k = 0; k < frames; k++)
            {
                float alpha = (float)k / Mathf.Max(1, frames - 1);
                float timeA = tA_start + k * simDt;
                float timeB = tB_start + k * simDt;

                foreach (var pair in posRecorders)
                {
                    var bone = pair.Key;
                    var simPosList = posRecorders[bone];
                    float boneWeight = GetWeightForBone(bone);

                    int idx = k + 1;

                    Vector3 simVel = (simPosList[idx + 1] - simPosList[idx - 1]) / (2 * simDt);
                    Vector3 simAcc = (simPosList[idx + 1] - 2 * simPosList[idx] + simPosList[idx - 1]) * invDt2;

                    KinematicFrame frameA = SampleKinematics(kinA[bone], timeA, 60f);
                    KinematicFrame frameB = SampleKinematics(kinB[bone], timeB, 60f);

                    float poseDiff = Vector3.SqrMagnitude(frameA.Pos - frameB.Pos);

                    Vector3 idealVel = Vector3.Lerp(frameA.Vel, frameB.Vel, alpha);
                    float velDiff = Vector3.SqrMagnitude(simVel - idealVel);

                    Vector3 idealAcc = Vector3.Lerp(frameA.Acc, frameB.Acc, alpha);
                    float accDiff = Vector3.SqrMagnitude(simAcc - idealAcc);

                    rawPoseCost += poseDiff * boneWeight;
                    rawVelCost += velDiff * boneWeight;
                    rawAccCost += accDiff * boneWeight;
                }
            }

            float avgPoseCost = (rawPoseCost / frames) * _weightPose;
            float avgVelCost = (rawVelCost / frames) * _weightVel;
            float avgAccCost = (rawAccCost / frames) * _weightAcc;

            return new TransitionResult
            {
                PoseCost = avgPoseCost,
                VelCost = avgVelCost,
                AccCost = avgAccCost,
                TotalCostScore = avgPoseCost + avgVelCost + avgAccCost
            };
        }

        #endregion

        #region Helpers

        private List<float> GeneratePoints(float start, float end)
        {
            var points = new List<float>();
            if (start > end + 0.001f) return points;
            for (float t = start; t <= end; t += _searchStepTime) points.Add(t);
            if (points.Count == 0 || Mathf.Abs(points.Last() - end) > 0.001f) points.Add(end);
            return points;
        }

        private float GetWeightForBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftFoot ? _weightLFoot : (_trackRFoot ? _weightRFoot : 0);
        }

        private KinematicFrame SampleKinematics(List<KinematicFrame> cache, float time, float sampleRate)
        {
            if (cache == null || cache.Count == 0) return new KinematicFrame();
            float indexF = time * sampleRate;
            int index0 = Mathf.FloorToInt(indexF);

            if (index0 < 0) return cache[0];
            if (index0 >= cache.Count - 1) return cache[cache.Count - 1];

            float t = indexF - index0;
            int index1 = index0 + 1;

            return new KinematicFrame
            {
                Pos = Vector3.LerpUnclamped(cache[index0].Pos, cache[index1].Pos, t),
                Vel = Vector3.LerpUnclamped(cache[index0].Vel, cache[index1].Vel, t),
                Acc = Vector3.LerpUnclamped(cache[index0].Acc, cache[index1].Acc, t)
            };
        }

        private void SetLog(string msg, Color color) { _logMsg = msg; _logColor = color; }

        #endregion
    }
}
#endif
