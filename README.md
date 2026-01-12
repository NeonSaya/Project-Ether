# Project Ether (VR osu! Player)

![Unity](https://img.shields.io/badge/Made%20with-Unity%202022.3%20LTS-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-blue)
![Platform](https://img.shields.io/badge/Platform-VR%20(OpenXR)-green)
![Status](https://img.shields.io/badge/Status-Prototype-yellow)

**Project Ether** 是一个基于 Unity 开发的沉浸式 VR 节奏游戏项目。它的核心目标是将经典的 `osu!` 游戏体验移植到虚拟现实空间中，通过独创的**"光子指挥家" (Photon Conductor)** 交互机制，实现低体能消耗、高沉浸感的音游体验。

> ⚠️ **注意**：本项目目前处于 **早期原型 (Prototype)** 开发阶段。

---

## 🎮 核心理念 (Core Concept)

不同于《Beat Saber》的大幅度挥砍，Project Ether 追求的是**"指挥家般的优雅与精准"**。

* **Relax (轻松)**: 抛弃物理按键与高强度挥动，采用射线交互。
* **Precision (精准)**: 复刻 `osu!` 的核心判定逻辑，在 VR 中重现高难度谱面的快感。
* **Flow (心流)**: 极简主义视觉风格，专注于音乐与节奏本身。

---

## ✨ 当前特性 (Features)

### ✅ 已实现 (Implemented)

* **核心解析器 (Core Parser)**:
    * 移植自 `osu-droid` 的 Java 逻辑，重写为 C#。
    * 支持解析标准 `.osu` 格式谱面文件。
    * 目前支持 `HitCircle` (单点音符) 的解析与生成。
* **空间映射算法 (Coordinate Mapping)**:
    * 实现了 `CoordinateMapper`，将 osu! 的 512x384 2D 像素坐标系，无损映射到 VR 玩家前方的 3D 扇形曲面上。
    * 解决了传统音游在 VR 视野中的畸变问题。
* **双色激光交互 (Laser Interaction)**:
    * 左手：🔵 **蓝色射线**
    * 右手：🔴 **红色射线**
    * 基于 `Raycast` 的高精度碰撞检测，支持非接触式判定。
* **VR 基础框架**:
    * 基于 Unity XR Interaction Toolkit 构建。
    * 适配 Oculus/OpenXR 标准。

### 🚧 开发计划 (Roadmap)

按照开发路书，后续计划包括：

* [ ] **Slider & Spinner**: 实现滑条和转盘的解析与渲染。
* [ ] **非线性手腕映射**: 实现微小手腕转动映射大角度射线的算法，降低游玩疲劳。
* [ ] **音频频域分析**: 基于 FFT 实现场景对音乐的动态律动反馈 (Kiai Time)。
* [ ] **UI 系统**: 选歌界面与结算面板。

---

## 🛠️ 技术架构 (Architecture)

本项目采用模块化架构设计：
```
Assets/Scripts/
├── Core/
│ ├── RhythmGameManager.cs # 游戏循环控制器 (导演)
│ └── CoordinateMapper.cs # 2D到3D的空间数学计算
├── Data/
│ ├── OsuParser.cs # .osu 文件解析器 (字符串处理)
│ ├── Beatmap.cs # 谱面数据结构
│ └── HitObject.cs # 音符基类与继承体系
└── Interaction/
├── LaserShooter.cs # 射线发射与判定逻辑
└── NoteController.cs # 音符的生命周期与运动控制
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
    * *注意：目前暂不支持 .osz 压缩包，请解压后放入。*
4. **配置管理器**：
    * 在场景中找到 `GameManager` 物体。
    * 在 Inspector 中将 `Osu File Name` 修改为你放入的谱面文件名 (例如 `test.osu`)。
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
