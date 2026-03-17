
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using System.Linq;

namespace BBBNexus
{
    public class AccelDeviationAnalyzerWindowV7_5 : EditorWindow
    {
        #region Data Structures & Helpers
        // 五点 Savitzky-Golay 低通滤波器 用于平滑采样数据减少噪点 
        private class SavitzkyGolayFilter
        {
            // 应用滤波器到位置数据序列 减小高频抖动 
            public List<Vector3> Apply(List<Vector3> data)
            {
                // 数据不足无法使用五点卷积 直接返回原始数据 
                if (data.Count < 5) return new List<Vector3>(data);
                var smoothed = new List<Vector3>(data);
                // 五点 Savitzky-Golay 滤波的卷积系数 
                float[] coeffs = { -3, 12, 17, 12, -3 };
                // 归一化系数 确保滤波后幅度不变 
                float normalizer = 35f;
                // 中心卷积 保留首尾两个点以避免边界伪影 
                for (int i = 2; i < data.Count - 2; i++)
                {
                    Vector3 sum = Vector3.zero;
                    // 从 i-2 到 i+2 的五点加权求和 
                    for (int j = 0; j < 5; j++) sum += coeffs[j] * data[i - 2 + j];
                    smoothed[i] = sum / normalizer;
                }
                return smoothed;
            }
        }

        // 单帧运动学快照 记录位置 速度 加速度三元组 
        public struct KinematicFrame
        {
            // 该帧的世界坐标位置 
            public Vector3 Pos;
            // 该帧的一阶导数 速度向量 单位 m/s 
            public Vector3 Vel;
            // 该帧的二阶导数 加速度向量 单位 m/s² 
            public Vector3 Acc;
        }

        // 过渡评估结果 包含成本分解与最优点时间 
        public struct TransitionResult
        {
            // 在 A 动画中切出的时间 单位秒 
            public float ExitTimeA, 
            // 在 B 动画中切入的时间 单位秒 
            EnterTimeB, 
            // 淡入时长 用于平滑过渡 单位秒 
            BlendDuration;
            // 归一化的切出时间 范围 0-1 便于理解在动画长度的相对位置 
            public float NormExitA, 
            // 归一化的切入时间 
            NormEnterB;
            // 位置匹配成本 权重 100x 
            public float PoseCost;
            // 速度匹配成本 权重 10x 
            public float VelCost;
            // 加速度匹配成本 权重 1x 
            public float AccCost;
            // 综合成本分数 越低表示过渡越平滑 
            public float TotalCostScore;
        }
        #endregion

        #region UI & Persistence Fields
        // 待模拟的角色模型 必须包含 Animator 组件 
        private GameObject _characterPrefab;
        // 切出动画 通常是当前正在播放的攻击 移动等动作 
        private AnimationClip _clipA, 
        // 切入动画 通常是下一个要切换到的目标动作 
        _clipB;
        // UI 滚动位置 记忆用户浏览过的结果 
        private Vector2 _scrollPos;

        // 搜索参数 在 A 动画的这个时间范围内寻找最佳切出点 
        [Range(0, 1)] private float _exitRangeMin = 0f, 
        // 搜索的结束位置 越接近 1 表示越接近动画末尾 
        _exitRangeMax = 1f;
        // 在 B 动画中搜索切入点的起始位置 
        [Range(0, 1)] private float _enterRangeMin = 0f, 
        // B 动画的搜索结束位置 
        _enterRangeMax = 1f;
        // 采样间隔 越小搜索越精细但计算时间增加 
        private float _searchStepTime = 0.05f;

        // 待测试的淡入时长列表 评估器会遍历这些值 寻找最佳过渡时长 
        private List<float> _blendDurations = new List<float> { 0.1f, 0.2f };
        // 最小淡入时长限制 防止超短过渡导致动作卡顿 
        private const float MIN_BLEND_DURATION = 0.01f;

        // 是否追踪左脚运动 用于运动学匹配 
        private bool _trackLFoot = true, 
        // 是否追踪右脚运动 
        _trackRFoot = true;
        // 左脚的权重倍数 越高左脚匹配越优先 
        private float _weightLFoot = 1.0f, 
        // 右脚的权重倍数 
        _weightRFoot = 1.0f;

        // 姿势成本权重 正常在 50-150 之间 用于约束过渡时骨骼位置的偏差 
        private float _weightPose = 100f;
        // 速度成本权重 正常在 5-20 之间 确保速度匹配从而避免速度突变 
        private float _weightVel = 10f;
        // 加速度成本权重 正常在 0.5-2 之间 微调加速度平滑度 
        private float _weightAcc = 1f;

        // 预采样频率 FPS 越高曲线越精细但烘焙数据量越大 
        private int _precomputeSampleRate = 60;
        // 混合模拟频率 用于过渡期间的精度控制 
        private int _simulationSampleRate = 30;

        // 是否正在执行模拟 防止重复点击导致同步问题 
        private bool _isSimulating = false;
        // 0-1 进度百分比 用于显示进度条 
        private float _progress = 0f;
        // 日志信息 显示当前执行状态或错误提示 
        private string _logMsg = "等待执行...";
        // 日志颜色 便于识别不同状态 
        private Color _logColor = Color.gray;
        // 最佳结果排序表 按总成本从低到高排列 
        private List<TransitionResult> _topResults = new List<TransitionResult>();
        #endregion

        [MenuItem("Tools/BBB-Nexus/Accel. Deviation Analyzer (v7.5 - Kinematic)")]
        public static void ShowWindow() => GetWindow<AccelDeviationAnalyzerWindowV7_5>("Kinematic Analyzer v7.5");

        // 窗口显示时加载上次保存的配置 
        private void OnEnable()
        {
            LoadPrefs();
        }

        // 窗口关闭时保存当前配置 确保下次打开时保留设置 
        private void OnDisable()
        {
            SavePrefs();
        }

        #region Persistence Logic
        // 从编辑器偏好设置加载之前保存的配置 包括资源引用与参数调整 
        private void LoadPrefs()
        {
            // 加载场景中待模拟的角色模型 
            _characterPrefab = LoadAsset<GameObject>("ADA_Prefab");
            // 加载两个动画片段 
            _clipA = LoadAsset<AnimationClip>("ADA_ClipA");
            _clipB = LoadAsset<AnimationClip>("ADA_ClipB");

            // 恢复搜索范围设置 
            _exitRangeMin = EditorPrefs.GetFloat("ADA_ExitMin", 0f);
            _exitRangeMax = EditorPrefs.GetFloat("ADA_ExitMax", 1f);
            _enterRangeMin = EditorPrefs.GetFloat("ADA_EnterMin", 0f);
            _enterRangeMax = EditorPrefs.GetFloat("ADA_EnterMax", 1f);
            _searchStepTime = EditorPrefs.GetFloat("ADA_SearchStep", 0.05f);

            // 恢复运动学追踪配置 
            _trackLFoot = EditorPrefs.GetBool("ADA_TrackLFoot", true);
            _trackRFoot = EditorPrefs.GetBool("ADA_TrackRFoot", true);
            _weightLFoot = EditorPrefs.GetFloat("ADA_WeightLFoot", 1.0f);
            _weightRFoot = EditorPrefs.GetFloat("ADA_WeightRFoot", 1.0f);

            // 恢复成本权重 这对评估精度影响很大 
            _weightPose = EditorPrefs.GetFloat("ADA_WeightPose", 100f);
            _weightVel = EditorPrefs.GetFloat("ADA_WeightVel", 10f);
            _weightAcc = EditorPrefs.GetFloat("ADA_WeightAcc", 1f);

            // 恢复采样率设置 
            _precomputeSampleRate = EditorPrefs.GetInt("ADA_PreRate", 60);
            _simulationSampleRate = EditorPrefs.GetInt("ADA_SimRate", 30);

            // 恢复淡入时长列表 逗号分隔的字符串 
            string durs = EditorPrefs.GetString("ADA_Durs", "0.1,0.2");
            _blendDurations = durs.Split(',').Select(s => float.TryParse(s, out float f) ? f : 0.1f).ToList();
        }

        // 保存当前配置到编辑器偏好设置 
        private void SavePrefs()
        {
            // 保存资源GUID 跨项目引用安全 
            SaveAsset("ADA_Prefab", _characterPrefab);
            SaveAsset("ADA_ClipA", _clipA);
            SaveAsset("ADA_ClipB", _clipB);

            // 保存搜索范围 
            EditorPrefs.SetFloat("ADA_ExitMin", _exitRangeMin);
            EditorPrefs.SetFloat("ADA_ExitMax", _exitRangeMax);
            EditorPrefs.SetFloat("ADA_EnterMin", _enterRangeMin);
            EditorPrefs.SetFloat("ADA_EnterMax", _enterRangeMax);
            EditorPrefs.SetFloat("ADA_SearchStep", _searchStepTime);

            // 保存运动学配置 
            EditorPrefs.SetBool("ADA_TrackLFoot", _trackLFoot);
            EditorPrefs.SetBool("ADA_TrackRFoot", _trackRFoot);
            EditorPrefs.SetFloat("ADA_WeightLFoot", _weightLFoot);
            EditorPrefs.SetFloat("ADA_WeightRFoot", _weightRFoot);

            // 保存成本权重 
            EditorPrefs.SetFloat("ADA_WeightPose", _weightPose);
            EditorPrefs.SetFloat("ADA_WeightVel", _weightVel);
            EditorPrefs.SetFloat("ADA_WeightAcc", _weightAcc);

            // 保存采样率 
            EditorPrefs.SetInt("ADA_PreRate", _precomputeSampleRate);
            EditorPrefs.SetInt("ADA_SimRate", _simulationSampleRate);

            // 保存淡入时长列表 
            EditorPrefs.SetString("ADA_Durs", string.Join(",", _blendDurations));
        }

        // 将资源对象转换为 GUID 并存储 便于持久化 
        private void SaveAsset(string key, Object obj)
        {
            if (obj != null) EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
        }

        // 从 GUID 恢复资源对象引用 
        private T LoadAsset<T>(string key) where T : Object
        {
            string guid = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
        #endregion

        // 绘制编辑器窗口 UI 
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Label("动作过渡评估器 (v7.5 - 三维运动学匹配版)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // 分区显示 UI 
            DrawAssetConfig();
            DrawSearchSpaceConfig();
            DrawAdvancedConfig();
            DrawExecutionControls();
            DrawDashboard();

            // 如果有参数变化 自动保存 
            if (EditorGUI.EndChangeCheck()) SavePrefs();

            EditorGUILayout.EndScrollView();
        }

        #region UI Sections
        // 第一区 资源配置 接收模型与动画片段 
        private void DrawAssetConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("1. 基础资产 (Assets)", EditorStyles.boldLabel);
                // 目标模型 必须包含骨骼与 Animator 
                _characterPrefab = (GameObject)EditorGUILayout.ObjectField("模拟模型 (Prefab)", _characterPrefab, typeof(GameObject), false);
                // 切出动画 通常是攻击 翻滚等要打断的动作 
                _clipA = (AnimationClip)EditorGUILayout.ObjectField("切出动画 A", _clipA, typeof(AnimationClip), false);
                // 切入动画 通常是下一个目标动作 
                _clipB = (AnimationClip)EditorGUILayout.ObjectField("接收动画 B", _clipB, typeof(AnimationClip), false);
            }
        }

        // 第二区 搜索参数 定义切点搜索范围与步长 
        private void DrawSearchSpaceConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("2. 搜索区间 (Search Space)", EditorStyles.boldLabel);
                // 显示并允许调整 A 动画的搜索范围 
                EditorGUILayout.LabelField($"A 切出区间: {_exitRangeMin:F2} ~ {_exitRangeMax:F2}");
                EditorGUILayout.MinMaxSlider(ref _exitRangeMin, ref _exitRangeMax, 0f, 1f);
                // 显示并允许调整 B 动画的搜索范围 
                EditorGUILayout.LabelField($"B 切入区间: {_enterRangeMin:F2} ~ {_enterRangeMax:F2}");
                EditorGUILayout.MinMaxSlider(ref _enterRangeMin, ref _enterRangeMax, 0f, 1f);
                // 采样步长 越小精度越高但计算时间翻倍 
                _searchStepTime = EditorGUILayout.Slider("搜索步长 (s)", _searchStepTime, 0.02f, 0.2f);

                // 淡入时长管理 支持动态添加删除 
                GUILayout.Label("测试淡入时长 (s):", EditorStyles.miniBoldLabel);
                for (int i = 0; i < _blendDurations.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 调整时长值 
                        _blendDurations[i] = EditorGUILayout.FloatField($"  └ 时长 {i + 1}", _blendDurations[i]);
                        // 删除按钮 
                        if (GUILayout.Button("-", GUILayout.Width(25))) { _blendDurations.RemoveAt(i); break; }
                    }
                }
                // 添加新时长项 
                if (GUILayout.Button("添加淡入时长 (+)")) _blendDurations.Add(0.2f);
            }
        }

        // 第三区 高级配置 成本权重与采样率 
        private void DrawAdvancedConfig()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("3. 高级配置 (Kinematic Config)", EditorStyles.boldLabel);
                // 位置匹配权重 正常 100x 
                _weightPose = EditorGUILayout.Slider("姿势惩罚倍率 (Pose)", _weightPose, 0f, 200f);
                // 速度匹配权重 正常 10x 
                _weightVel = EditorGUILayout.Slider("速度惩罚倍率 (Velocity)", _weightVel, 0f, 50f);
                // 加速度匹配权重 正常 1x 微调用 
                _weightAcc = EditorGUILayout.Slider("力学惩罚倍率 (Acceleration)", _weightAcc, 0f, 5f);

                EditorGUILayout.Space(5);
                // 预采样频率 影响曲线精度与数据量 
                _precomputeSampleRate = EditorGUILayout.IntSlider("预计算采样率 (FPS)", _precomputeSampleRate, 30, 120);
                // 混合模拟频率 用于评估过渡期间的精度 
                _simulationSampleRate = EditorGUILayout.IntSlider("混合模拟采样率 (FPS)", _simulationSampleRate, 30, 120);

                EditorGUILayout.Space(5);
                // 左脚追踪 
                _trackLFoot = EditorGUILayout.ToggleLeft(" 左脚 (Left Foot)", _trackLFoot);
                if (_trackLFoot) _weightLFoot = EditorGUILayout.Slider("   └ 权重", _weightLFoot, 0f, 2f);
                // 右脚追踪 
                _trackRFoot = EditorGUILayout.ToggleLeft(" 右脚 (Right Foot)", _trackRFoot);
                if (_trackRFoot) _weightRFoot = EditorGUILayout.Slider("   └ 权重", _weightRFoot, 0f, 2f);
            }
        }

        // 第四区 执行控制 启动分析按钮 
        private void DrawExecutionControls()
        {
            // 模拟期间禁用按钮 防止重复点击 
            using (new EditorGUI.DisabledScope(_isSimulating))
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("开始运动学深度分析", GUILayout.Height(40)))
                    if (ValidateSetup()) RunSimulation();
                GUI.backgroundColor = Color.white;
            }
        }

        // 第五区 仪表盘 显示进度与最佳结果 
        private void DrawDashboard()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                // 进度条 显示模拟进度 
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18), _progress, _logMsg);
                // 显示最佳结果列表 
                if (_topResults.Count > 0)
                {
                    GUILayout.Label("🏆 最佳推荐 Top 5", EditorStyles.boldLabel);
                    // 仅显示前五个最优结果 
                    for (int i = 0; i < Mathf.Min(5, _topResults.Count); i++)
                    {
                        var res = _topResults[i];
                        using (new EditorGUILayout.VerticalScope(GUI.skin.button))
                        {
                            // 总分 分解显示各项成本 
                            GUILayout.Label($"Top {i + 1} | 总分: {res.TotalCostScore:F2} [P:{res.PoseCost:F1} | V:{res.VelCost:F1} | A:{res.AccCost:F1}]");
                            // 最优切点时间 
                            GUILayout.Label($"A:{res.ExitTimeA:F3}s | B:{res.EnterTimeB:F3}s | Blend:{res.BlendDuration:F2}s");
                        }
                    }
                }
            }
        }
        #endregion

        #region Logic
        // 核心分析流程 构建 Playable Graph 遍历所有组合评估成本 
        private void RunSimulation()
        {
            // 标记模拟进行中 
            _isSimulating = true;
            _topResults.Clear();
            // 创建临时采样角色 隐藏避免污染场景 
            GameObject agent = Instantiate(_characterPrefab);
            agent.hideFlags = HideFlags.HideAndDontSave;
            // 获取 Animator 
            Animator anim = agent.GetComponent<Animator>();
            // 创建 Playable Graph 用于混合两个动画 
            PlayableGraph graph = PlayableGraph.Create("ADA_Graph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            // 混合器 支持两个输入 
            var mixer = AnimationMixerPlayable.Create(graph, 2);
            // 两个动画片段对应的 Playable 
            var playA = AnimationClipPlayable.Create(graph, _clipA);
            var playB = AnimationClipPlayable.Create(graph, _clipB);
            // 连接到混合器 
            mixer.ConnectInput(0, playA, 0); mixer.ConnectInput(1, playB, 0);
            // 输出连接到 Animator 驱动角色 
            var output = AnimationPlayableOutput.Create(graph, "Out", anim);
            output.SetSourcePlayable(mixer);

            try
            {
                // 预计算两个动画的运动学数据 包括位置 速度 加速度 
                var kinA = Precompute(anim, _clipA);
                var kinB = Precompute(anim, _clipB);
                // 生成搜索点集 A 与 B 两个动画上的候选切点 
                var exits = GeneratePoints(_clipA.length * _exitRangeMin, _clipA.length * _exitRangeMax);
                var enters = GeneratePoints(_clipB.length * _enterRangeMin, _clipB.length * _enterRangeMax);

                // 计算总组合数 用于进度条 
                int total = exits.Count * enters.Count * _blendDurations.Count;
                int curr = 0;

                // 三重循环 遍历所有组合 
                foreach (var tA in exits)
                {
                    foreach (var tB in enters)
                    {
                        foreach (var dur in _blendDurations)
                        {
                            // 评估这个组合的成本 
                            var res = CalcCost(anim, graph, mixer, playA, playB, tA, tB, dur, kinA, kinB);
                            res.ExitTimeA = tA; res.EnterTimeB = tB; res.BlendDuration = dur;
                            res.NormExitA = tA / _clipA.length; res.NormEnterB = tB / _clipB.length;
                            _topResults.Add(res);
                            curr++;
                        }
                    }
                    // 更新进度显示 
                    _progress = (float)curr / total;
                    SetLog($"计算中... {(_progress * 100):F0}%", Color.white);
                    Repaint();
                }
                // 排序 找到成本最低的组合 
                _topResults.Sort((x, y) => x.TotalCostScore.CompareTo(y.TotalCostScore));
                SetLog("分析完成", Color.green);
            }
            finally
            {
                // 清理临时资源 
                graph.Destroy(); DestroyImmediate(agent); _isSimulating = false;
            }
        }

        // 预计算单个动画的运动学曲线 包含位置 速度 加速度的完整信息 
        private Dictionary<HumanBodyBones, List<KinematicFrame>> Precompute(Animator anim, AnimationClip clip)
        {
            // 时间步长 根据采样率计算 
            float dt = 1f / _precomputeSampleRate;
            // 总帧数 
            int frames = Mathf.CeilToInt(clip.length / dt);
            // 追踪的骨骼列表 
            var bones = new List<HumanBodyBones>();
            if (_trackLFoot) bones.Add(HumanBodyBones.LeftFoot);
            if (_trackRFoot) bones.Add(HumanBodyBones.RightFoot);

            // 采样缓冲 存储原始位置数据 
            var cache = new Dictionary<HumanBodyBones, List<Vector3>>();
            foreach (var b in bones) cache[b] = new List<Vector3>();

            // 采样循环 逐帧采集骨骼位置 
            for (int i = 0; i <= frames; i++)
            {
                // 重置角色位置 清空上一帧的累积位移 
                anim.transform.position = Vector3.zero;
                // 采样该帧的动画数据 
                clip.SampleAnimation(anim.gameObject, i * dt);
                // 记录各骨骼的世界位置 
                foreach (var b in bones) cache[b].Add(anim.GetBoneTransform(b).position);
            }

            // 后处理 计算速度与加速度 
            var result = new Dictionary<HumanBodyBones, List<KinematicFrame>>();
            // 使用 Savitzky-Golay 滤波器平滑数据 减少数值微分的噪点 
            var filter = new SavitzkyGolayFilter();
            // 时间导数系数 
            float invDt = 1f / dt; 
            float invDt2 = invDt * invDt;

            // 处理每个骨骼 
            foreach (var b in bones)
            {
                // 平滑位置数据 
                var smoothed = filter.Apply(cache[b]);
                // 存储运动学快照 
                var framesData = new List<KinematicFrame>();
                for (int i = 0; i < smoothed.Count; i++)
                {
                    // 中心差分法计算速度 (smoothed[i+1] - smoothed[i-1]) / (2*dt) 
                    Vector3 v = (i > 0 && i < smoothed.Count - 1) ? (smoothed[i + 1] - smoothed[i - 1]) * 0.5f * invDt : Vector3.zero;
                    // 中心差分法计算加速度 (smoothed[i+1] - 2*smoothed[i] + smoothed[i-1]) / dt² 
                    Vector3 a = (i > 0 && i < smoothed.Count - 1) ? (smoothed[i + 1] - 2 * smoothed[i] + smoothed[i - 1]) * invDt2 : Vector3.zero;
                    framesData.Add(new KinematicFrame { Pos = smoothed[i], Vel = v, Acc = a });
                }
                result[b] = framesData;
            }
            return result;
        }

        // 评估过渡成本 模拟混合期间的运动学偏差 
        private TransitionResult CalcCost(Animator anim, PlayableGraph graph, AnimationMixerPlayable mixer, AnimationClipPlayable pA, AnimationClipPlayable pB, float tA, float tB, float dur, Dictionary<HumanBodyBones, List<KinematicFrame>> kinA, Dictionary<HumanBodyBones, List<KinematicFrame>> kinB)
        {
            // 模拟采样间隔 
            float dt = 1f / _simulationSampleRate;
            // 混合期间的采样帧数 
            int frames = Mathf.Max(1, Mathf.CeilToInt(dur / dt));
            // 记录混合过程中骨骼的轨迹 
            var recs = new Dictionary<HumanBodyBones, List<Vector3>>();
            if (_trackLFoot) recs[HumanBodyBones.LeftFoot] = new List<Vector3>();
            if (_trackRFoot) recs[HumanBodyBones.RightFoot] = new List<Vector3>();

            // 模拟淡入过程 记录骨骼位置轨迹 
            for (int k = -1; k <= frames; k++)
            {
                // 当前相对时间 
                float time = k * dt;
                // 混合权重 从 0 平滑过渡到 1 
                float alpha = Mathf.Clamp01(time / dur);
                // 设置混合器权重 
                mixer.SetInputWeight(0, 1 - alpha); mixer.SetInputWeight(1, alpha);
                // 设置两个片段的播放位置 
                pA.SetTime(tA + time); pB.SetTime(tB + time);
                // 重置角色位置 
                anim.transform.position = Vector3.zero; 
                // 驱动模拟 采集该时刻的骨骼位置 
                graph.Evaluate(0);
                foreach (var r in recs) r.Value.Add(anim.GetBoneTransform(r.Key).position);
            }

            // 成本计算 比较模拟轨迹与预计算的理想轨迹 
            float pC = 0, vC = 0, aC = 0;
            float invDt2 = 1f / (dt * dt);
            for (int k = 0; k < frames; k++)
            {
                float alpha = (float)k / frames;
                foreach (var r in recs)
                {
                    float w = GetWeight(r.Key);
                    // 数值微分 从离散轨迹计算速度 
                    Vector3 simV = (r.Value[k + 2] - r.Value[k]) / (2 * dt);
                    // 数值微分 从离散轨迹计算加速度 
                    Vector3 simA = (r.Value[k + 2] - 2 * r.Value[k + 1] + r.Value[k]) * invDt2;
                    // 采样预计算数据中对应时间点的运动学值 
                    var fA = Sample(kinA[r.Key], tA + k * dt);
                    var fB = Sample(kinB[r.Key], tB + k * dt);
                    // 位置成本 骨骼位置不匹配 
                    pC += Vector3.SqrMagnitude(fA.Pos - fB.Pos) * w;
                    // 速度成本 模拟速度与过渡的目标速度差异 
                    vC += Vector3.SqrMagnitude(simV - Vector3.Lerp(fA.Vel, fB.Vel, alpha)) * w;
                    // 加速度成本 模拟加速度与过渡的目标加速度差异 
                    aC += Vector3.SqrMagnitude(simA - Vector3.Lerp(fA.Acc, fB.Acc, alpha)) * w;
                }
            }
            // 返回结果 单位化成本并应用权重 
            return new TransitionResult { PoseCost = (pC / frames) * _weightPose, VelCost = (vC / frames) * _weightVel, AccCost = (aC / frames) * _weightAcc, TotalCostScore = ((pC + vC + aC) / frames) };
        }

        // 从预计算的曲线中采样指定时间的运动学快照 使用线性插值 
        private KinematicFrame Sample(List<KinematicFrame> cache, float time)
        {
            // 转换时间为采样帧索引 
            float f = time * _precomputeSampleRate;
            // 取整得到下界索引 防止越界 
            int i0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, cache.Count - 2);
            // 线性插值 得到该时间点的运动学值 
            return new KinematicFrame { 
                Pos = Vector3.Lerp(cache[i0].Pos, cache[i0 + 1].Pos, f - i0), 
                Vel = Vector3.Lerp(cache[i0].Vel, cache[i0 + 1].Vel, f - i0), 
                Acc = Vector3.Lerp(cache[i0].Acc, cache[i0 + 1].Acc, f - i0) 
            };
        }

        // 生成搜索点列表 从 s 到 e 的均匀采样 
        private List<float> GeneratePoints(float s, float e)
        {
            var p = new List<float>();
            for (float t = s; t <= e; t += _searchStepTime) p.Add(t);
            return p;
        }

        // 获取骨骼的权重倍数 用于调整在成本评估中的优先级 
        private float GetWeight(HumanBodyBones b) => 
            (b == HumanBodyBones.LeftFoot && _trackLFoot) ? _weightLFoot : 
            ((b == HumanBodyBones.RightFoot && _trackRFoot) ? _weightRFoot : 0f);

        // 更新日志显示 
        private void SetLog(string m, Color c) { _logMsg = m; _logColor = c; }

        // 预检查 确保资源完整 
        private bool ValidateSetup() => _characterPrefab && _clipA && _clipB && (_trackLFoot || _trackRFoot);
        #endregion
    }
}
#endif
