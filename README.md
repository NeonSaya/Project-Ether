# Project Ether (VR osu! Player)

![Unity](https://img.shields.io/badge/Made%20with-Unity%202022.3%20LTS-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-blue)
![Platform](https://img.shields.io/badge/Platform-VR%20(OpenXR)-green) 
![Status](https://img.shields.io/badge/Status-Alpha-orange)

**Project Ether** 是一个基于 Unity 开发的沉浸式 VR 节奏游戏项目。它的核心目标是将经典的 `osu!` 游戏体验移植到虚拟现实空间中，通过独创的**"光子指挥家" (Photon Conductor)** 交互机制，实现低体能消耗、高沉浸感的音游体验。

> ⚠️ **注意**：本项目目前处于 **Alpha** 开发阶段。

---

## 🎮 核心理念 (Core Concept)

不同于《Beat Saber》的大幅度挥砍，Project Ether 追求的是**"指挥家般的优雅与精准"**。

* **Relax (轻松)**: 抛弃物理按键与高强度挥动，采用射线交互。
* **Precision (精准)**: 复刻 `osu!` 的核心判定逻辑，在 VR 中重现高难度谱面的快感。
* **Flow (心流)**: 极简主义视觉风格，专注于音乐与节奏本身。

---

## ✨ 当前特性 (Features)

### ✅ 已实现 (Implemented)

#### 1. 核心系统 (Core System)

- **全功能解析器 (Full .osu Parser)**:
  - 基于 `osu-droid` 逻辑重构的 C# 解析核心。
  - 完整支持 Metadata, General, Events, TimingPoints 等核心数据的解析。
- **AR 自动计算**:
  - 内置标准 osu! 算法，根据 `ApproachRate` 自动计算 `TimePreempt` (缩圈时间) 和 `SpawnOffset`。
  - **数据修复**: 自动检测并修复滑条 Tick 的相对时间戳问题，确保逻辑与绝对时间轴严格对齐。
- **空间映射算法 (Coordinate Mapping)**:
  - 实现了 `CoordinateMapper`，将 osu! 原生 512x384 的 2D 像素坐标系，无损映射到 VR 玩家前方的 3D 扇形曲面空间中，保证手感还原。

#### 2. 视觉表现 (Visuals & Rendering)

- **高保真滑条渲染 (High-Fidelity Slider Rendering)**:
  - **双层网格生成**: 实现了 osu! 风格的滑条外观，底层生成宽白边框 (Border)，顶层生成本体颜色 (Body)，单次绘制性能极高。
  - **动态 Z 轴管理**: 精确控制 Note、Slider Body、Border 和 Approach Circle 的 Z 轴层级，彻底解决了 VR 环境下常见的 **Z-Fighting (闪烁)** 问题。
- **原生风格组件**:
  - **平面化缩圈 (Flat Approach Circles)**: 强制 Z 轴缩放锁定，在 VR 3D 环境中完美复刻 2D 原作的“纸片”缩圈视觉效果。
  - **完整滑条组件**: 包含滑条球 (Follow Ball)、折返箭头 (Reverse Arrow) 以及根据 Span 动态显示的逻辑。

#### 3. 游戏机制 (Gameplay & Mechanics)

- **Relax 模式交互 (Raycast Interaction)**:
  - **悬停判定 (Hover Gameplay)**: 采用 osu! Relax 模式机制。无需物理按键，只需使用光剑/射线 (Laser) 持续指向音符即可触发判定。
  - **双色激光**: 支持左手(蓝) / 右手(红) 独立射线检测。
- **高精度判定逻辑 (Judgement Logic)**:
  - **非对称判定窗口**: 实现了严格的判定保护（例如 -5ms 到 +HitWindow），有效防止缩圈未重合时的误触/早打 (Notelock prevention)。
- **复杂滑条逻辑**:
  - **不销毁机制**: 击中滑条头后保留物体，允许玩家继续跟随滑条球。
  - **补救机制**: 即使漏掉滑条头，只要中途追上滑条球 (Tracking)，依然可以判定后续的 Tick 和 End。
  - **Tick/Repeat 判定**: 完整支持滑条中间的 Tick 得分和折返点判定。

#### 4. 调试与工具 (Debug & Tools)

- **可视化调试面板**: 实时显示谱面元数据 (CS/AR/OD)、当前 DSP 时间、已生成/活动音符数量。
- **Gizmos 辅助**: 在编辑器 Scene 窗口中绘制射线检测范围和判定球范围，辅助开发。

------

## 📅 近未来开发 To-Do List (Next Steps)

### 1. 核心反馈 (Juice & Feel) - 🔥 优先级最高

- [ ] **打击音效 (Hitsounds)**: 在 NoteController 和 SliderController 的 `OnHit` / `HitHead` / `Tick` 处播放音效（Normal, Whistle, Finish）。
- [ ] **手柄震动 (Haptics)**: 使用 Unity XR 的 `SendHapticImpulse`。当射线 `OnRayHover` 或判定成功时，给手柄提供短促有力的震动反馈。
- [ ] **连击显示 (Combo Counter)**: 在视野前方制作悬浮 UI 显示当前 Combo，并在 Miss 时添加碎裂/变红动画。
- [ ] **分数与准确率 (Score & Accuracy)**: 实现 `RhythmGameManager` 计分逻辑，并实时显示（300 / 100 / 50 / Miss）。

### 2. 游戏逻辑补全

- [ ] **转盘 (Spinner) 修复**: 检查 `SpinnerController` 的 AR 时间计算与判定逻辑，确保与 Slider/Note 同步。
- [ ] **生命值系统 (HP Drain)**: 实现掉血机制（Idle 掉血，Hit 回血，HP<=0 失败）。
- [ ] **结算界面**: 歌曲结束或失败时弹出结算面板。

### 3. 基础设施

- [ ] **选歌菜单 (Song Select)**: 开发 UI 面板以读取本地文件夹，替代目前的硬编码加载。
- [ ] **对象池 (Object Pooling)**: 实现 Note 和 Slider 的回收复用机制，解决 `Instantiate`/`Destroy` 带来的 GC 卡顿问题。

---

## 🛠️ 技术架构 (Architecture)

本项目采用模块化架构设计，核心脚本结构如下：

```text
Assets/Scripts/
├── TestLoader.cs               # 调试与测试场景入口
├── Core/
│   ├── RhythmGameManager.cs    # 核心游戏循环控制器 (负责调度)
│   ├── CoordinateMapper.cs     # 空间映射系统 (2D像素坐标 -> VR曲面坐标)
│   ├── NoteController.cs       # 单点 (HitCircle) 物体行为控制器
│   ├── SliderController.cs     # 滑条 (Slider) 物体行为控制器
│   ├── SpinnerController.cs    # 转盘 (Spinner) 物体行为控制器
│   └── Math/
│       └── SliderPathCalculator.cs # 滑条曲线生成算法 (Bezier, Catmull等)
├── Data/
│   ├── OsuParser.cs            # .osu 文件解析核心
│   ├── Beatmap.cs              # 完整谱面数据模型
│   ├── Enums.cs                # 全局枚举定义 (HitObjectType, CurveType等)
│   ├── HitObject.cs            # 音符数据基类
│   ├── HitCircle.cs            # 单点数据定义
│   ├── SliderObject.cs         # 滑条数据定义
│   ├── SliderPath.cs           # 滑条路径数据结构
│   └── SpinnerObject.cs        # 转盘数据定义
└── Interaction/
    └── LaserShooter.cs         # 玩家射线输入与交互判定逻辑
```
---

## 🚀 快速开始 (Getting Started)

### 环境要求

* Unity 2022.3.x LTS
* VR 头显 (Quest 2/3, Pico 4 等) 或 XR Device Simulator

### 安装步骤

1. 克隆本仓库：
    ```bash
    git clone https://github.com/NeonSaya/Project-Ether.git
    ```
2. 使用 Unity Hub 打开项目文件夹。
3. **导入谱面**：
    * 在 `Assets` 目录下创建一个名为 `Songs` 的文件夹。
    * 将你的 `.osu` 文件放入 `Assets/Songs/` 中。
4. **配置管理器**：
    * 在场景中找到 `GameManager` 物体。
    * 在 Inspector 中将 `Osu File Name` 修改为你放入的谱面文件名。
5. 点击 **Play** 运行！

---

## 🕹️ 操作说明 (Controls)

| 设备 | 动作 | 效果 |
| :--- | :--- | :--- |
| **VR 手柄** | 移动/转动 | 控制红蓝射线指向 |
| **判定** | 射线接触 | 当音符飞近时，用任意颜色的射线扫过音符即可触发判定 |

---

## 🤝 致谢 (Credits)

* **osu! & peppy**: 本项目的灵感来源。
* **osu-droid**: 本项目的解析逻辑参考了其开源代码。
* **Unity XR Interaction Toolkit**: 提供 VR 底层支持。

---

## 📄 License

MIT License
