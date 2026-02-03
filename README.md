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
* 基于 osu-droid 逻辑重构的 C# 解析核心。
* 完整支持 Metadata, General, Events, TimingPoints 等核心数据解析。
* **[New]** 支持 `.osz` 压缩包的自动解压与目录管理。


* **AR 自动计算:** 内置标准 osu! 算法，自动计算 TimePreempt (缩圈时间) 和 SpawnOffset。
* **空间映射算法 (Coordinate Mapping):** 实现了 CoordinateMapper，将 osu! 原生 2D 像素坐标系，无损映射到 VR 玩家前方的 3D 扇形曲面空间中。
* **对象池系统 (Object Pooling):** 实现了 Note、Slider、Tick 的回收复用机制，有效避免了高频 Instantiate/Destroy 带来的 GC 卡顿。

#### 2. 交互与物理 (Interaction & Physics)

* **Relax 模式交互 (Raycast Interaction):**
* **悬停判定 (Hover Gameplay):** 采用 osu! Relax 机制。无需物理按键，只需射线 (Laser) 持续指向音符即可触发判定。
* **时序保护:** 修复了脚本执行顺序导致的判定失效，实现了“指哪打哪”的瞬时响应。


* **物理增强:**
* **Note/Slider:** 自动生成 SphereCollider/MeshCollider，确保射线检测精准无误。
* **Spinner:** 采用薄片化 BoxCollider 优化转盘判定，解决了球体碰撞体导致的射线视觉悬停误差。


* **双色激光:** 支持左手(蓝) / 右手(红) 独立射线检测与交互。

#### 3. 听觉与触觉 (Audio & Haptics)

* **动态音效反馈 (Dynamic Hitsounds):**
* 完整解析 Beatmap 中的 HitSound (Normal, Whistle, Finish, Clap)。
* 支持 SliderSlide 循环音效与 SpinnerSpin 音效。
* 实现了 Slider Reverse (折返) 与 Spinner Bonus 的独立反馈音效。


* **沉浸式震动 (Immersive Haptics):**
* 基于 `XRNode` 的底层震动封装。
* 实现了打击震动、滑条持续微震、Bonus 奖励震动等多级反馈。



#### 4. 视觉表现 (Visuals & Rendering)

* **高保真滑条渲染:**
* **双层网格:** 实现了 Border (边框) 与 Body (本体) 的分离渲染。
* **动态 Z 轴管理:** 彻底解决了 VR 环境下 Note/Slider 堆叠时的 Z-Fighting 闪烁问题。


* **原生风格组件:** 平面化缩圈 (Flat Approach Circles)、滑条球 (Follow Ball)、折返箭头 (Reverse Arrow)。

---

## 📅 近未来开发 To-Do List (Next Steps)

### 1. 视觉反馈 (Juice & Feedback) - 🔥 优先级最高

* **判定弹窗 (Score Popups):** 在击打位置弹出 300 / 100 / 50 / Miss 图标，并添加飘动/淡出动画。
* **连击计数 (Combo Counter):** 在视野前方制作悬浮 3D UI 显示当前 Combo，并在 Miss 时添加碎裂/变红动画。
* **打击粒子 (Hit Particles):** 为不同类型的 HitSound (Whistle/Finish) 制作对应的粒子爆发效果，增强打击爽快感。

### 2. 游戏流程闭环 (Game Loop)

* **选歌菜单完善 (Song Select Polish):** 优化目前的 UI 面板，显示封面图、难度星级，并实现数据传递到游戏场景。
* **结算界面 (Results Screen):** 歌曲结束或失败时弹出结算面板 (Score, Accuracy, Rank)。
* **暂停菜单 (Pause Menu):** 实现游戏中的暂停、继续与重试功能。

### 3. 沉浸感与特效 (Immersion & VFX) - 长期目标

* **Kiai Time 表现:** 解析谱面 Kiai 字段，在高潮段落触发场景泛光 (Bloom) 增强与粒子流速加快。
* **音频响应环境 (Audio Reactive):** 引入 FFT 频谱分析，让背景环境随 Bass 鼓点律动。
* **指挥家特效:** 为手柄射线添加能量拖尾 (Trail) 与流光效果。

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
