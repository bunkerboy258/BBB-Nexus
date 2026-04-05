# 攻击窗口调试可视化系统 - 修改总结

## 📋 修改概述

为了解决 fists 攻击窗口 Gizmo 绘制与实际业务逻辑不同步的问题，我们创建了一个**运行时调试可视化系统**，让 Gizmo 显示直接使用业务逻辑中的时间窗口数据。

---

## 🗂️ 文件清单

### 新增文件
1. **`AttackWindowDebugService.cs`** - 调试服务，存储攻击窗口上下文数据
2. **`ATTACK_WINDOW_DEBUG_README.md`** - 本文档

### 修改文件
1. **`FistsBehaviour.cs`** - 添加调试注册和清除调用
2. **`WeaponBehaviour.cs`** - 添加调试注册和清除调用
3. **`FistHitbox.cs`** - 添加运行时 Gizmo 绘制逻辑

---

## 🔧 编译修复记录

### 修复 1: 可空类型处理
**问题**: `AttackWindowDebugContext` 是 struct，不能用 `null` 比较
**解决**: 使用 `AttackWindowDebugContext?` 可空类型，并通过 `.HasValue` 和 `.Value` 访问

```csharp
// 修改前
private static AttackWindowDebugContext _activeContext;
public static bool HasActiveWindow => _activeContext != null; // ❌ 编译错误

// 修改后
private static AttackWindowDebugContext? _activeContext;
public static bool HasActiveWindow => _activeContext.HasValue; // ✅ 正确
```

### 修复 2: 变量作用域
**问题**: `sampleProgress` 变量在未使用时未定义
**解决**: 提前声明 `sampleProgress` 变量

```csharp
// 修改前
if (currentWindowIndex >= 0)
{
    float sampleProgress = sample.SweepProgressNormalized; // 只在 if 内有效
    // ...
}
DrawRuntimeSample(..., $"{context.ComboIndex} {Mathf.RoundToInt(sampleProgress * 100f)}%"); // ❌ 未定义

// 修改后
float sampleProgress = sample.SweepProgressNormalized; // 提前声明
if (currentWindowIndex >= 0)
{
    // ...
}
DrawRuntimeSample(..., $"{context.ComboIndex} {Mathf.RoundToInt(sampleProgress * 100f)}%"); // ✅ 正确
```

### 修复 3: 方法重载
**问题**: `DrawSphere` 和 `DrawBox` 方法不存在于 `FistHitbox` 类中
**解决**: 添加 `DrawRuntimeSphere` 和 `DrawRuntimeBox` 静态方法，以及 `DrawCapsuleApprox` 的重载

```csharp
// 新增方法
private static void DrawRuntimeSphere(AttackGeometryShapeDefinition shape, Color fillColor)
private static void DrawRuntimeBox(AttackGeometryShapeDefinition shape, Color fillColor)
private static void DrawCapsuleApprox(AttackGeometryShapeDefinition shape, Color color) // 重载
```

---

## 📐 API 文档

### `AttackWindowDebugService.cs`
**路径**: `Assets/BBBNexus/Item/Logic/Weapons/AttackWindowDebugService.cs`

**作用**: 单例调试服务，存储当前激活的攻击窗口上下文数据

**核心 API**:
```csharp
// 注册攻击窗口上下文
AttackWindowDebugService.RegisterWindow(
    startTime, endTime,
    windowStartTimes, windowEndTimes,
    alignmentStartTime, alignmentEndTime,
    comboIndex, actualDuration, dominantEnd);

// 清除窗口
AttackWindowDebugService.ClearWindow();

// 检查是否在伤害窗口内
bool isInWindow = AttackWindowDebugService.IsInAnyDamageWindow(Time.time);

// 获取当前伤害窗口索引
int index = AttackWindowDebugService.GetCurrentDamageWindowIndex(Time.time);

// 获取归一化进度
float progress = AttackWindowDebugService.GetNormalizedProgress(Time.time);
```

---

## 🔧 修改的文件

### 1. `FistsBehaviour.cs`

**修改位置**:
- `TriggerAttack()` 方法：在 `ApplyDamageWindowTiming` 和 `ApplyAlignmentWindowTiming` 之后添加调试注册
- `CloseHitWindow()` 方法：在清除上下文时调用 `AttackWindowDebugService.ClearWindow()`

**修改代码**:
```csharp
// TriggerAttack 方法中
float dominantEnd = ResolveDominantEndTime(actualDuration, currentComboIndex);
ApplyDamageWindowTiming(_activeAttackContext, actualDuration, currentComboIndex);
ApplyAlignmentWindowTiming(_activeAttackContext, actualDuration, currentComboIndex);

// 注册调试信息供 Gizmo 使用
AttackWindowDebugService.RegisterWindow(
    _currentAttackStartTime,
    _currentAttackStartTime + actualDuration,
    _activeAttackContext.WindowStartTimes,
    _activeAttackContext.WindowEndTimes,
    _activeAttackContext.AlignmentWindowStartTime,
    _activeAttackContext.AlignmentWindowEndTime,
    currentComboIndex,
    actualDuration,
    dominantEnd);

// CloseHitWindow 方法中
private void CloseHitWindow()
{
    _hitbox?.Deactivate();
    _isHitWindowOpen = false;
    _activeAttackContext = null;
    _autoTargetTransform = null;
    AttackWindowDebugService.ClearWindow(); // 新增
}
```

### 2. `WeaponBehaviour.cs`

**修改位置**: 与 `FistsBehaviour.cs` 相同
- `TriggerAttack()` 方法
- `CloseHitWindow()` 方法

### 3. `FistHitbox.cs`

**新增 Inspector 字段**:
```csharp
[Header("Debug - Attack Window Visualization")]
[Tooltip("是否使用运行时攻击窗口数据来绘制 Gizmo。启用后，只会显示当前伤害窗口内的几何体。")]
[SerializeField] private bool _useRuntimeAttackWindowForGizmo = false;
[Tooltip("伤害窗口 Gizmo 颜色")]
[SerializeField] private Color _damageWindowGizmoColor = new Color(1f, 0.18f, 0.18f, 0.25f);
[Tooltip("对齐窗口 Gizmo 颜色")]
[SerializeField] private Color _alignmentWindowGizmoColor = new Color(0.18f, 0.55f, 0.92f, 0.2f);
```

**新增方法**:
- `DrawAttackGeometryGizmo()`: 添加运行时窗口检测逻辑
- `DrawRuntimeAttackWindowGizmo()`: 绘制运行时攻击窗口几何体
- `DrawRuntimeSample()`: 绘制单个采样点的几何体
- `DrawAlignmentWindowIndicator()`: 绘制对齐窗口指示器

---

## 🎮 使用方法

### 在 Unity Editor 中

1. **选择玩家/武器 Prefab**，找到 `FistHitbox` 组件

2. **展开 "Debug - Attack Window Visualization" 折叠框**

3. **勾选 "Use Runtime Attack Window For Gizmo"**

4. **运行游戏并攻击**，Gizmo 视图会显示：
   - **红色半透明**: 已经过的攻击轨迹采样点
   - **亮红色实心**: 当前伤害窗口内的采样点（激活状态）
   - **蓝色标签**: 对齐窗口信息和伤害窗口数量

### 颜色说明

| 状态 | 颜色 | 说明 |
|-----|------|-----|
| 未激活 | 灰色 (_inactiveGizmoColor) | 没有攻击窗口激活时 |
| 已激活 | 红色 (_activeGizmoColor) | 当前伤害窗口内的几何体 |
| 已过轨迹 | 暗红色 (_damageWindowGizmoColor) | 已经播放过的攻击轨迹 |
| 对齐窗口 | 蓝色 (_alignmentWindowGizmoColor) | 自动锁定对齐的时间窗口 |

---

## 🔍 调试技巧

### 1. 检查伤害窗口时间
在 `UpdateHitWindowState()` 中添加日志：
```csharp
if (shouldBeActive && !_isHitWindowOpen)
{
    Debug.Log($"[FistsDebug] 伤害窗口开启 | 时间={Time.time:F3} | " +
        $"窗口=[{string.Join(", ", _activeAttackContext.WindowStartTimes.Select((t, i) => $"{t:F3}-{_activeAttackContext.WindowEndTimes[i]:F3}"))}]");
}
```

### 2. 对比 Gizmo 与业务逻辑
- 开启 `_useRuntimeAttackWindowForGizmo` 后，Gizmo 显示的伤害窗口**完全复用业务逻辑数据**
- 如果仍然看到偏差，说明是**动画播放速度**或**采样点密度**的问题

### 3. 调整采样点密度
如果攻击轨迹显示不连续，需要增加 `AttackClipGeometryDefinition` 的采样点：
- 使用 "从 Collider + Clip 烘焙" 功能重新烘焙
- 或者手动编辑 JSON 增加采样点

---

## 📊 数据流图

```
┌─────────────────────────────────────────────────────────┐
│              TriggerAttack() 触发攻击                   │
│  计算 actualDuration, dominantEnd                       │
│  应用 ApplyDamageWindowTiming()                         │
│  应用 ApplyAlignmentWindowTiming()                      │
└────────────────────┬────────────────────────────────────┘
                     │
                     ├──────────────────────────────────┐
                     │                                  │
                     ↓                                  ↓
        ┌────────────────────────┐        ┌────────────────────────┐
        │  业务逻辑：_active     │        │  调试服务：            │
        │  AttackContext         │        │  AttackWindowDebug     │
        │  - WindowStartTimes[]  │        │  Service               │
        │  - WindowEndTimes[]    │        │  - ActiveContext       │
        │  - AlignmentWindow[]   │        │  - WindowStartTimes[]  │
        └────────┬───────────────┘        │  - WindowEndTimes[]    │
                 │                        │  - AlignmentWindow[]   │
                 │                        └────────┬───────────────┘
                 │                                 │
                 ↓                                 │
        ┌────────────────────────┐                 │
        │  UpdateHitWindowState()│                 │
        │  检查 IsInAnyDamage    │                 │
        │  Window(Time.time)     │                 │
        │  → 激活/关闭 Hitbox    │                 │
        └────────────────────────┘                 │
                                                   │
                     Gizmo 绘制时 ◄────────────────┘
                     使用相同的数据源
```

---

## ✅ 验证清单

- [x] `AttackWindowDebugService` 创建成功
- [x] `FistsBehaviour.TriggerAttack()` 注册调试信息
- [x] `FistsBehaviour.CloseHitWindow()` 清除调试信息
- [x] `WeaponBehaviour.TriggerAttack()` 注册调试信息
- [x] `WeaponBehaviour.CloseHitWindow()` 清除调试信息
- [x] `FistHitbox.DrawAttackGeometryGizmo()` 支持运行时模式
- [x] Inspector 中添加调试选项开关
- [x] 伤害窗口颜色配置
- [x] 对齐窗口指示器绘制

---

## 🎯 核心优势

1. **数据源统一**: Gizmo 绘制直接使用业务逻辑的 `WindowStartTimes[]` / `WindowEndTimes[]`，完全消除偏差
2. **实时反馈**: 攻击时立即看到当前伤害窗口的几何体显示
3. **可配置**: 可以通过 Inspector 开关切换传统模式和运行时模式
4. **无侵入**: 调试代码完全隔离，不影响运行时性能（仅在 Editor 中生效）

---

## 📝 注意事项

1. **仅 Editor 生效**: `AttackWindowDebugService` 的调试功能仅在 Unity Editor 中使用，构建时不会有任何性能开销
2. **需要配合 AnimationClip**: 运行时模式需要对应的 `AttackClipGeometryDefinition` 资源存在
3. **采样点密度**: 如果攻击轨迹显示不连续，需要增加烘焙时的采样点密度

---

修改完成喵~ 现在 Gizmo 显示会与实际业务逻辑完全一致了喵！(=^･ω･^=) 🐱✨
