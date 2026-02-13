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

* **全功能解析器 (Full .osu Parser):**
* 基于 osu-droid 逻辑重构的 C# 解析核心，完整支持 Metadata, General, Events, TimingPoints 等核心数据。
* 支持 `.osz` 压缩包的自动解压与目录管理。


* **Lazer 风格分数算法 (Lazer Scoring):**
* **精准 Acc 计算:** 重写 `ScoreManager`，Slider Head/Tick/Repeat 均正确计入准确率权重，不再是单纯的 Bonus。
* **转盘机制:** 实现了基于 OD (Overall Difficulty) 的转速上限与 Bonus 封顶逻辑，拒绝无限刷分。


* **空间映射 (Coordinate Mapping):** 将 osu! 原生 2D 坐标无损映射到 VR 3D 扇形曲面空间。

#### 2. 交互与物理 (Interaction & Physics)

* **Relax 模式交互:** 采用射线 (Raycast) 悬停判定机制，无需按键，实现“指哪打哪”的瞬时响应。
* **智能滑条逻辑 (Smart Slider Logic):**
* **容错机制:** 修复了“滑条自杀”Bug，支持 Slider Break (中途断连) 后续接，不会因为漏掉一个 Tick 而直接销毁整个滑条。
* **真实结算:** 根据实际吃到的 Tick 比例计算最终准确率 (Accuracy)。


* **物理增强:** 自动生成精准的碰撞体 (Collider)，Spinner 采用薄片化碰撞体优化判定精度。

#### 3. 视觉表现 (Visuals & UI)

* **沉浸式 HUD (Immersive HUD):**
* **曲面 UI (Curved UI):** 编写了 `CurvedUIEffect`，通过顶点修改实现 UI 的圆柱面弯折，提升 VR 阅读舒适度。
* **平滑跟随 (Smooth Follow):** `HUDFollower` 实现了类似 Beat Saber 的 UI 阻尼跟随，保持视线水平且不晕眩。
* **动态面板:** 实现了分数的滚动动画 (Rolling Counter) 与 Combo 的弹跳反馈。


* **纯代码判定特效 (Code-driven VFX):**
* **高性能渲染:** 抛弃 Prefab，使用 `JudgementVisualizer` 实时生成 Quad 网格与 TextMeshPro。
* **Lazer 动效:** 使用非线性动画 (Elastic/Back Out) 实现 Great/Ok/Miss 的弹跳与光晕扩散。
* **细节反馈:** 实现了滑条尾判丢失 (Tail Miss) 的独立视觉反馈 (小红叉)。


* **高保真滑条:** 实现了 Border/Body 分离渲染，解决了 VR 环境下的 Z-Fighting 闪烁。

#### 4. 性能优化 (Optimization)

* **预热机制 (Pre-warming):** 针对粒子系统和判定特效实现了预热逻辑，彻底消除了游戏开始时的“首帧卡顿”。
* **资源缓存 (Caching):** 实现了 Note/Slider 光晕网格 (Quad Mesh) 的静态缓存，大幅降低运行时 GC 与实例化开销。
* **粒子系统修复:** 修复了高频触发下粒子系统状态机卡死导致的不播放问题。

---

## 📅 下一步计划 (To-Do List)

### 1. 游戏流程 (Game Flow) - 🔥 优先级最高

* **结算界面 (Results Screen):** 歌曲结束后弹出面板，显示总分、最终 Acc、等级 (S/A/B/C) 以及详细的 300/100/50/Miss 数量。
* **失败机制 (Fail System):** 引入 HP (Health) 系统，当 HP 归零时触发失败结算。
* **音画同步 (Audio Sync):** 添加全局 Offset 调整功能，以适配不同 VR 串流环境的音频延迟。

### 2. 菜单与交互 (Menu & UX)

* **选歌菜单优化:** 完善现有的选歌面板，增加封面图加载、难度星级显示。
* **暂停菜单:** 实现游戏中的暂停、继续与重试 (Retry) 功能。

### 3. 视觉打磨 (Polish)

* **Kiai Time:** 解析谱面 Kiai 段落，在副歌高潮时增强场景泛光 (Bloom) 与粒子流速。
* **连击特效:** 当 Combo 达到一定数值 (100x, 300x) 时触发特殊的视觉提示。

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
