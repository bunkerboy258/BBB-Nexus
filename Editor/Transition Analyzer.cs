// Acceleration Deviation Analyzer (v7.5 - Kinematic Triad / Persistent & Configurable)
// 加速度偏差分析器 (v7.5 - 三维运动学匹配 / 可配置 & 持久化版)

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using System.Linq;

namespace AnimeACT.EditorTools
{
    public class AccelDeviationAnalyzerWindowV7_5 : EditorWindow
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
            public float PoseCost;
            public float VelCost;
            public float AccCost;
            public float TotalCostScore;
        }
        #endregion

        #region UI & Persistence Fields
        private GameObject _characterPrefab;
        private AnimationClip _clipA, _clipB;
        private Vector2 _scrollPos;

        [Range(0, 1)] private float _exitRangeMin = 0f, _exitRangeMax = 1f;
        [Range(0, 1)] private float _enterRangeMin = 0f, _enterRangeMax = 1f;
        private float _searchStepTime = 0.05f;

        private List<float> _blendDurations = new List<float> { 0.1f, 0.2f };
        private const float MIN_BLEND_DURATION = 0.01f;

        private bool _trackLFoot = true, _trackRFoot = true;
        private float _weightLFoot = 1.0f, _weightRFoot = 1.0f;

        private float _weightPose = 100f;
        private float _weightVel = 10f;
        private float _weightAcc = 1f;

        private int _precomputeSampleRate = 60;
        private int _simulationSampleRate = 30;

        private bool _isSimulating = false;
        private float _progress = 0f;
        private string _logMsg = "等待执行...";
        private Color _logColor = Color.gray;
        private List<TransitionResult> _topResults = new List<TransitionResult>();
        #endregion

        [MenuItem("Tools/BBB-Nexus/Accel. Deviation Analyzer (v7.5 - Kinematic)")]
        public static void ShowWindow() => GetWindow<AccelDeviationAnalyzerWindowV7_5>("Kinematic Analyzer v7.5");

        private void OnEnable()
        {
            LoadPrefs();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        #region Persistence Logic
        private void LoadPrefs()
        {
            _characterPrefab = LoadAsset<GameObject>("ADA_Prefab");
            _clipA = LoadAsset<AnimationClip>("ADA_ClipA");
            _clipB = LoadAsset<AnimationClip>("ADA_ClipB");

            _exitRangeMin = EditorPrefs.GetFloat("ADA_ExitMin", 0f);
            _exitRangeMax = EditorPrefs.GetFloat("ADA_ExitMax", 1f);
            _enterRangeMin = EditorPrefs.GetFloat("ADA_EnterMin", 0f);
            _enterRangeMax = EditorPrefs.GetFloat("ADA_EnterMax", 1f);
            _searchStepTime = EditorPrefs.GetFloat("ADA_SearchStep", 0.05f);

            _trackLFoot = EditorPrefs.GetBool("ADA_TrackLFoot", true);
            _trackRFoot = EditorPrefs.GetBool("ADA_TrackRFoot", true);
            _weightLFoot = EditorPrefs.GetFloat("ADA_WeightLFoot", 1.0f);
            _weightRFoot = EditorPrefs.GetFloat("ADA_WeightRFoot", 1.0f);

            _weightPose = EditorPrefs.GetFloat("ADA_WeightPose", 100f);
            _weightVel = EditorPrefs.GetFloat("ADA_WeightVel", 10f);
            _weightAcc = EditorPrefs.GetFloat("ADA_WeightAcc", 1f);

            _precomputeSampleRate = EditorPrefs.GetInt("ADA_PreRate", 60);
            _simulationSampleRate = EditorPrefs.GetInt("ADA_SimRate", 30);

            string durs = EditorPrefs.GetString("ADA_Durs", "0.1,0.2");
            _blendDurations = durs.Split(',').Select(s => float.TryParse(s, out float f) ? f : 0.1f).ToList();
        }

        private void SavePrefs()
        {
            SaveAsset("ADA_Prefab", _characterPrefab);
            SaveAsset("ADA_ClipA", _clipA);
            SaveAsset("ADA_ClipB", _clipB);

            EditorPrefs.SetFloat("ADA_ExitMin", _exitRangeMin);
            EditorPrefs.SetFloat("ADA_ExitMax", _exitRangeMax);
            EditorPrefs.SetFloat("ADA_EnterMin", _enterRangeMin);
            EditorPrefs.SetFloat("ADA_EnterMax", _enterRangeMax);
            EditorPrefs.SetFloat("ADA_SearchStep", _searchStepTime);

            EditorPrefs.SetBool("ADA_TrackLFoot", _trackLFoot);
            EditorPrefs.SetBool("ADA_TrackRFoot", _trackRFoot);
            EditorPrefs.SetFloat("ADA_WeightLFoot", _weightLFoot);
            EditorPrefs.SetFloat("ADA_WeightRFoot", _weightRFoot);

            EditorPrefs.SetFloat("ADA_WeightPose", _weightPose);
            EditorPrefs.SetFloat("ADA_WeightVel", _weightVel);
            EditorPrefs.SetFloat("ADA_WeightAcc", _weightAcc);

            EditorPrefs.SetInt("ADA_PreRate", _precomputeSampleRate);
            EditorPrefs.SetInt("ADA_SimRate", _simulationSampleRate);

            EditorPrefs.SetString("ADA_Durs", string.Join(",", _blendDurations));
        }

        private void SaveAsset(string key, Object obj)
        {
            if (obj != null) EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
        }

        private T LoadAsset<T>(string key) where T : Object
        {
            string guid = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
        #endregion

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Label("动作过渡评估器 (v7.5 - 三维运动学匹配版)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            DrawAssetConfig();
            DrawSearchSpaceConfig();
            DrawAdvancedConfig();
            DrawExecutionControls();
            DrawDashboard();

            if (EditorGUI.EndChangeCheck()) SavePrefs();

            EditorGUILayout.EndScrollView();
        }

        #region UI Sections
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
                _searchStepTime = EditorGUILayout.Slider("搜索步长 (s)", _searchStepTime, 0.02f, 0.2f);

                GUILayout.Label("测试淡入时长 (s):", EditorStyles.miniBoldLabel);
                for (int i = 0; i < _blendDurations.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _blendDurations[i] = EditorGUILayout.FloatField($"  └ 时长 {i + 1}", _blendDurations[i]);
                        if (GUILayout.Button("-", GUILayout.Width(25))) { _blendDurations.RemoveAt(i); break; }
                    }
                }
                if (GUILayout.Button("添加淡入时长 (+)")) _blendDurations.Add(0.2f);
            }
        }

        private void DrawAdvancedConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("3. 高级配置 (Kinematic Config)", EditorStyles.boldLabel);
                _weightPose = EditorGUILayout.Slider("姿势惩罚倍率 (Pose)", _weightPose, 0f, 200f);
                _weightVel = EditorGUILayout.Slider("速度惩罚倍率 (Velocity)", _weightVel, 0f, 50f);
                _weightAcc = EditorGUILayout.Slider("力学惩罚倍率 (Acceleration)", _weightAcc, 0f, 5f);

                EditorGUILayout.Space(5);
                _precomputeSampleRate = EditorGUILayout.IntSlider("预计算采样率 (FPS)", _precomputeSampleRate, 30, 120);
                _simulationSampleRate = EditorGUILayout.IntSlider("混合模拟采样率 (FPS)", _simulationSampleRate, 30, 120);

                EditorGUILayout.Space(5);
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
                if (GUILayout.Button("开始运动学深度分析", GUILayout.Height(40)))
                    if (ValidateSetup()) RunSimulation();
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawDashboard()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18), _progress, _logMsg);
                if (_topResults.Count > 0)
                {
                    GUILayout.Label("🏆 最佳推荐 Top 5", EditorStyles.boldLabel);
                    for (int i = 0; i < Mathf.Min(5, _topResults.Count); i++)
                    {
                        var res = _topResults[i];
                        using (new EditorGUILayout.VerticalScope(GUI.skin.button))
                        {
                            GUILayout.Label($"Top {i + 1} | 总分: {res.TotalCostScore:F2} [P:{res.PoseCost:F1} | V:{res.VelCost:F1} | A:{res.AccCost:F1}]");
                            GUILayout.Label($"A:{res.ExitTimeA:F3}s | B:{res.EnterTimeB:F3}s | Blend:{res.BlendDuration:F2}s");
                        }
                    }
                }
            }
        }
        #endregion

        #region Logic
        private void RunSimulation()
        {
            _isSimulating = true;
            _topResults.Clear();
            GameObject agent = Instantiate(_characterPrefab);
            agent.hideFlags = HideFlags.HideAndDontSave;
            Animator anim = agent.GetComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("ADA_Graph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var mixer = AnimationMixerPlayable.Create(graph, 2);
            var playA = AnimationClipPlayable.Create(graph, _clipA);
            var playB = AnimationClipPlayable.Create(graph, _clipB);
            mixer.ConnectInput(0, playA, 0); mixer.ConnectInput(1, playB, 0);
            var output = AnimationPlayableOutput.Create(graph, "Out", anim);
            output.SetSourcePlayable(mixer);

            try
            {
                var kinA = Precompute(anim, _clipA);
                var kinB = Precompute(anim, _clipB);
                var exits = GeneratePoints(_clipA.length * _exitRangeMin, _clipA.length * _exitRangeMax);
                var enters = GeneratePoints(_clipB.length * _enterRangeMin, _clipB.length * _enterRangeMax);

                int total = exits.Count * enters.Count * _blendDurations.Count;
                int curr = 0;

                foreach (var tA in exits)
                {
                    foreach (var tB in enters)
                    {
                        foreach (var dur in _blendDurations)
                        {
                            var res = CalcCost(anim, graph, mixer, playA, playB, tA, tB, dur, kinA, kinB);
                            res.ExitTimeA = tA; res.EnterTimeB = tB; res.BlendDuration = dur;
                            res.NormExitA = tA / _clipA.length; res.NormEnterB = tB / _clipB.length;
                            _topResults.Add(res);
                            curr++;
                        }
                    }
                    _progress = (float)curr / total;
                    SetLog($"计算中... {(_progress * 100):F0}%", Color.white);
                    Repaint();
                }
                _topResults.Sort((x, y) => x.TotalCostScore.CompareTo(y.TotalCostScore));
                SetLog("分析完成", Color.green);
            }
            finally
            {
                graph.Destroy(); DestroyImmediate(agent); _isSimulating = false;
            }
        }

        private Dictionary<HumanBodyBones, List<KinematicFrame>> Precompute(Animator anim, AnimationClip clip)
        {
            float dt = 1f / _precomputeSampleRate;
            int frames = Mathf.CeilToInt(clip.length / dt);
            var bones = new List<HumanBodyBones>();
            if (_trackLFoot) bones.Add(HumanBodyBones.LeftFoot);
            if (_trackRFoot) bones.Add(HumanBodyBones.RightFoot);

            var cache = new Dictionary<HumanBodyBones, List<Vector3>>();
            foreach (var b in bones) cache[b] = new List<Vector3>();

            for (int i = 0; i <= frames; i++)
            {
                anim.transform.position = Vector3.zero;
                clip.SampleAnimation(anim.gameObject, i * dt);
                foreach (var b in bones) cache[b].Add(anim.GetBoneTransform(b).position);
            }

            var result = new Dictionary<HumanBodyBones, List<KinematicFrame>>();
            var filter = new SavitzkyGolayFilter();
            float invDt = 1f / dt; float invDt2 = invDt * invDt;

            foreach (var b in bones)
            {
                var smoothed = filter.Apply(cache[b]);
                var framesData = new List<KinematicFrame>();
                for (int i = 0; i < smoothed.Count; i++)
                {
                    Vector3 v = (i > 0 && i < smoothed.Count - 1) ? (smoothed[i + 1] - smoothed[i - 1]) * 0.5f * invDt : Vector3.zero;
                    Vector3 a = (i > 0 && i < smoothed.Count - 1) ? (smoothed[i + 1] - 2 * smoothed[i] + smoothed[i - 1]) * invDt2 : Vector3.zero;
                    framesData.Add(new KinematicFrame { Pos = smoothed[i], Vel = v, Acc = a });
                }
                result[b] = framesData;
            }
            return result;
        }

        private TransitionResult CalcCost(Animator anim, PlayableGraph graph, AnimationMixerPlayable mixer, AnimationClipPlayable pA, AnimationClipPlayable pB, float tA, float tB, float dur, Dictionary<HumanBodyBones, List<KinematicFrame>> kinA, Dictionary<HumanBodyBones, List<KinematicFrame>> kinB)
        {
            float dt = 1f / _simulationSampleRate;
            int frames = Mathf.Max(1, Mathf.CeilToInt(dur / dt));
            var recs = new Dictionary<HumanBodyBones, List<Vector3>>();
            if (_trackLFoot) recs[HumanBodyBones.LeftFoot] = new List<Vector3>();
            if (_trackRFoot) recs[HumanBodyBones.RightFoot] = new List<Vector3>();

            for (int k = -1; k <= frames; k++)
            {
                float time = k * dt;
                float alpha = Mathf.Clamp01(time / dur);
                mixer.SetInputWeight(0, 1 - alpha); mixer.SetInputWeight(1, alpha);
                pA.SetTime(tA + time); pB.SetTime(tB + time);
                anim.transform.position = Vector3.zero; graph.Evaluate(0);
                foreach (var r in recs) r.Value.Add(anim.GetBoneTransform(r.Key).position);
            }

            float pC = 0, vC = 0, aC = 0;
            float invDt2 = 1f / (dt * dt);
            for (int k = 0; k < frames; k++)
            {
                float alpha = (float)k / frames;
                foreach (var r in recs)
                {
                    float w = GetWeight(r.Key);
                    Vector3 simV = (r.Value[k + 2] - r.Value[k]) / (2 * dt);
                    Vector3 simA = (r.Value[k + 2] - 2 * r.Value[k + 1] + r.Value[k]) * invDt2;
                    var fA = Sample(kinA[r.Key], tA + k * dt);
                    var fB = Sample(kinB[r.Key], tB + k * dt);
                    pC += Vector3.SqrMagnitude(fA.Pos - fB.Pos) * w;
                    vC += Vector3.SqrMagnitude(simV - Vector3.Lerp(fA.Vel, fB.Vel, alpha)) * w;
                    aC += Vector3.SqrMagnitude(simA - Vector3.Lerp(fA.Acc, fB.Acc, alpha)) * w;
                }
            }
            return new TransitionResult { PoseCost = (pC / frames) * _weightPose, VelCost = (vC / frames) * _weightVel, AccCost = (aC / frames) * _weightAcc, TotalCostScore = ((pC + vC + aC) / frames) };
        }

        private KinematicFrame Sample(List<KinematicFrame> cache, float time)
        {
            float f = time * _precomputeSampleRate;
            int i0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, cache.Count - 2);
            return new KinematicFrame { Pos = Vector3.Lerp(cache[i0].Pos, cache[i0 + 1].Pos, f - i0), Vel = Vector3.Lerp(cache[i0].Vel, cache[i0 + 1].Vel, f - i0), Acc = Vector3.Lerp(cache[i0].Acc, cache[i0 + 1].Acc, f - i0) };
        }

        private List<float> GeneratePoints(float s, float e)
        {
            var p = new List<float>();
            for (float t = s; t <= e; t += _searchStepTime) p.Add(t);
            return p;
        }

        private float GetWeight(HumanBodyBones b) => (b == HumanBodyBones.LeftFoot && _trackLFoot) ? _weightLFoot : ((b == HumanBodyBones.RightFoot && _trackRFoot) ? _weightRFoot : 0f);

        private void SetLog(string m, Color c) { _logMsg = m; _logColor = c; }

        private bool ValidateSetup() => _characterPrefab && _clipA && _clipB && (_trackLFoot || _trackRFoot);
        #endregion
    }
}
#endif
