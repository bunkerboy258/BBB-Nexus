# BBBNexus Action Controller Framework

![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue)
![Architecture](https://img.shields.io/badge/Architecture-Data_Driven-orange)
![Performance](https://img.shields.io/badge/GC_Alloc-Zero-success)

**BBBNexus** 是一个意图驱动的管线化UNITY角色控制器框架，采用“数据中心黑板 + 分层状态机 + 仲裁器”来组织复杂的动作、装备与表现系统，并通过外观模式 (Facade) 和驱动器 (Driver) 将动画、IK、音频等后端彻底解耦。

本框架专为需要快速迭代与扩展的第三人称或动作类项目设计。当你发现角色逻辑越来越复杂，输入、状态、动画事件、装备系统、IK 与音频特效全揉在一起，维护成本直接起飞的时候，这套框架就能派上用场了。

视频演示：https://www.bilibili.com/video/BV1uTQmByEMr?vd_source=2cc5ed9052a1d87ec0a98653adcdbbdf

---

## 💡 设计理念

BBBNexus 的核心原则就是把“逻辑决策”和“表现执行”彻底分开，把“这帧想干嘛（瞬时意图）”和“当前在干嘛（运行时状态）”分开。

这样做的好处很实在：
- 加新动作、新武器或新系统非常容易，不用到处改代码。
- 天然支持对象池复用，生命周期管理极其干净。
- 随时能接入第三方的相机或 IK 插件，绝不会把核心代码搞脏。

---

## ⚙️ 核心架构

### 1. 双轨信息流与管线化处理
框架把角色行为拆成两股底层信息流：
- **意图 (Intent)：** 玩家或 AI 这一瞬间想干嘛（比如移动、瞄准、开火、翻越）。
- **参数 (Parameter)：** 这帧用来驱动动画或状态的连续数据（比如速度、方向、上半身权重）。

这些信息流会经过输入管线和主处理管线进行严格的排队分发。每个处理器只干一件事，哪里不爽换哪里，适合局部更新。

### 2. 三层骨骼遮罩与仲裁状态机
扔掉了传统的面条式状态切换，用了一套支持全局打断的分层状态体系：
- **全身层 (FullBody)：** 管移动、跳跃、翻滚、死亡这些大动作。
- **上半身层 (UpperBody)：** 管持枪、拿东西或者空手。
- **覆盖层 (Override)：** 优先级最高的强制动作。
- *(扩展支持) 表情层 *

配合状态注册表和拦截器，什么条件能进状态、谁能打断谁、谁跟谁互斥，全都在配置表里管得清清楚楚，再也不用在各个脚本里写死依赖。

### 3. 表现层彻底解耦
玩法逻辑根本摸不到底层的动画机或音频播放细节。
- **动画外观：** 统一的动画执行入口，目前无缝对接 Animancer。
- **运动驱动器：** 把动画时间和物理位移死死绑在一起（比如在 LateUpdate 里读动画状态，彻底干掉帧不同步的滑步问题）。
- **装备驱动器：** 专门管武器怎么生成、挂在哪里、怎么切枪。
- **音频驱动器：** 声音播放全部集中处理，方便做对象池、混音和静默降级。

---

## ⚙️ 子系统细节

- 输入系统：抽象输入源（IInputSource/InputSourceBase），可替换玩家/AI 输入；InputPipeline 负责采样、缓冲与输入一致性处理。
- 运行时数据黑板：PlayerRuntimeData/InputData 统一承载帧级意图、装备/瞄准引用、参数缓存；每帧意图复位，保证逻辑边界清晰。
- 处理管线（管线化架构）：MainProcessorPipeline 将逻辑拆为 意图处理（Intent Processors） 与 参数处理（Parameter Processors），支持模块化扩展与 early-out。
- 状态机系统（分层状态）：全身/上半身/覆盖层状态机（BaseState/UpperBodyBaseState/OverrideState 等），配合注册表与 Brain 配置初始化。
- 打断与拦截器机制：全局/上身打断处理（InterruptProcessor）与拦截器 SO（Jump/Roll/Vault/Land/Fall/Aim 等），用于统一控制进入条件、打断规则与互斥逻辑。
- 仲裁器系统（ArbiterPipeline）：Action/Health/Stamina/LOD 等仲裁入口，用于处理冲突请求、优先级与覆盖执行（ActionOverride 等）。
- 动画系统（Animancer 接入）：AnimationFacadeBase + AnimancerFacade 作为动画外观层，支持多层播放、mask、权重、回调/事件；并包含回调对象池等低 GC 设计。
- 运动驱动（MotionDriver）：基于动画时序/根运动的位移驱动与同步策略（强调 LateUpdate 对齐动画结算），并为 Warp/Vault 等运动变形留出扩展接口。
- 相机与视角管理：相机引用初始化（RuntimeData.CameraTransform）、相机驱动/管理组件（如 CameraRigDriver/PlayerCameraManager），并通过参数处理器（如 ViewRotationProcessor）向角色逻辑提供视角数据。
- IK 系统与适配层：IKController + PlayerIKSourceBase 统一 IK 目标/权重接口；提供 FinalIK 适配（FinalIKSource）与 Unity Animation Rigging 适配（UnityAnimationRiggingSource/IKAutoBinder）。
- 装备与物品系统：物品定义（ItemDefinition/EquippableItemSO 等）、物品实例（ItemInstance）、背包与堆叠（InventorySystem）、角色库存控制（PlayerInventoryController）、装备驱动（EquipmentDriver）与武器逻辑示例（AK/炮/剑等）。
- 音频系统：音频配置模块（AudioSO）、音频驱动（AudioDriver）与音频控制器（AudioController），支持以事件/请求方式触发播放。
- 对象池系统：SimpleObjectPoolSystem 提供通用对象池能力，武器特效/投射物等可复用生成与回收。
- 调试与工具链（Editor/Debug）：动画速度分析、根运动提取、Warp 提取、测试器/可视化调试等工具脚本，辅助配置与排查问题。

---

## ⚡ 性能表现

BBBNexus 的性能密码就是“单入口 Tick 驱动”加上“管线化分阶段处理”。
在 144Hz 的测试环境下，角色总耗时稳稳压在 **1ms 以内**，而且**持续运行零 GC**。

- **告别满天飞的 Update：** 整个角色的 Update 和 LateUpdate 全由主控制器统一指挥，省掉了一大堆零散脚本带来的调度开销。代码跑到哪一目了然，方便用 Profiler 查性能抓内鬼。
- **数据至上：** 大家都看着运行时黑板上的数据办事，意图写进去，状态机拿出来用。干掉了模块之间乱七八糟的事件委托和组件查询。
- **零GC：** 用对象池/预分配杀死了所有隐藏闭包（比如动画回调），利用结构体做意图复位，直接把 GC 扼杀在源头。
- **LOD 与仲裁降级：** 支持lod分级，角色死了、被控了、或者离相机太远了，可以关掉或者降频一些表现层模块。

---

## ⚠️ 依赖说明 (必看)

下载的 `.unitypackage` 包内**不包含**第三方付费插件的源码。在导入本框架前，请务必留意以下依赖关系：

### 🔴 必须安装

- **[Animancer](https://assetstore.unity.com/packages/tools/animation/animancer-pro-116514)**
  本框架底层的动画机过渡全靠它来实现代码级控制。**没它跑不起来，请务必在导入本框架前提前安装。**
  （其实理论上 我将动画源用animfacade基类隔离起来了 是可以手搓一套丐版的animancer的）

### 🟡 按需选装

根据你的项目需求，你可以随时接入以下功能模块：

1. **如果需要 IK 功能（射击瞄准、肢体贴合等）：**
   框架内已经写好了两套 IK 源的支持接口，选装其一即可：
   - **[Final IK](https://assetstore.unity.com/packages/tools/animation/final-ik-14290)：** （推荐）装上后代码会自动识别并开启宏，能直接体验到最完整的瞄准逻辑、后坐力表现和全身权重分配。
   - **Unity Animation Rigging (UAR)：** 官方免费方案。目前框架里搭了基础的桥接代码，但部分高阶功能目前是空置的，需要你自己填坑完善。（为什么注释里写了）

2. **如果需要相机系统：**
   - **[Cinemachine](https://unity.com/features/cinemachine)：** 如果你想直接用本框架预设的第三人称越肩视角，请安装 Cinemachine。
   - **自带相机？** 完全没问题。框架和相机是彻底解耦的，你只需要把你自己的相机的 LookAt（注视目标）绑定到角色骨骼上的 `camroot` 节点即可无缝对接。

---



## 📅 维护计划

本项目作为本人的第一个真正意义上的开源项目，将会长期维护并不定期迭代
- **特性扩容：** 未来会持续追加更多特性以及更完善的 AI 驱动源。
- **引擎跟进：** 尽量会保证向前兼容性，计划跟进 **Unity 6.3 LTS** 。
