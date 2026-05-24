# Project Ether (VR osu! Player)

![Unity](https://img.shields.io/badge/Made%20with-Unity%202022.3%20LTS-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-blue)
![Platform](https://img.shields.io/badge/Platform-VR%20(OpenXR)-green) 
![Status](https://img.shields.io/badge/Status-Beta-brightgreen)

## 📖 项目概述 (Overview)

在当前的虚拟现实生态中，优秀的音乐节奏游戏层出不穷，但它们往往面临着同一个致命痛点：**高质量的社区自制谱面极度匮乏**。而与此同时，有着十余年历史的经典 PC 音游 `osu!` 却坐拥着海量、惊艳且充满挑战的谱面库。

**Project Ether** 的诞生，正是为了架起这两大世界之间的桥梁。我们的终极目标，是打造一个基于 Unity 引擎构建的**沉浸式 VR 版 osu! 谱面播放器**。

我们的野心不仅仅是简单地将 2D 音符搬进 3D 空间，而是**将《Beat Saber》般爽快至极的打击手感，与 VRChat 中 MMD 舞台级别的顶级视听盛宴完美结合。** 

你可以利用手中的虚拟射线，在纯粹的音波起伏与绚丽的光影交错中，轻松惬意地享受每一首高质量 osu! 谱面带来的视听震撼。

> 🟢 **当前状态：Beta (可游玩版本)**

> 目前，游戏的核心链路（启动 -> 选曲 -> 游玩 -> 结算）已经完全打通！无论是基础的单点 (Circle)、连绵的滑条 (Slider)，还是疯狂旋转的转盘 (Spinner)，其生成与计分系统均已完备。当前的开发重点已全面转向底层性能调优、特殊 Mod（游戏模组）的深度适配，以及向着极致视听演出的方向狂奔。

---

### 🎮 核心理念 (Core Concept)

* **Relax (轻松与释放)**: 彻底抛弃传统的物理按键、鼠标点击，以及高强度的肢体挥动。我们采用独创的 3D 空间射线悬停交互机制，实现“指哪打哪”的顺畅体验，让你在长时间游玩后依然保持轻松。
* **Precision (绝对精准)**: 虽然玩法休闲，但底层机制绝不含糊。我们从代码层面完美复刻了 `osu! Lazer` 极其严苛的硬核判定逻辑。从判定窗口的毫秒级计算到连击权重，完全保留了顶级音游的核心操作快感。
* **Flow (沉浸心流)**: 游戏 UI 与环境采用极简主义与赛博朋克交织的视觉风格。摒弃一切花里胡哨、喧宾夺主的干扰元素，让玩家的意识完全溶解在单纯的音乐与节奏节拍之中。

---

## 💻 技术栈与底层依赖 (Tech Stack)

本项目使用最新的 Unity 技术栈打造，为未来的跨平台与高性能渲染打下了坚实基础：
* **游戏引擎**: Unity 2022.3.22f1 LTS —— 提供长期、稳定的底层架构支持。
* **画面渲染**: 通用渲染管线 (Universal Render Pipeline, URP 14.0.10) —— 在保证极佳画面表现力的同时，为移动端 VR 设备 (如 Quest) 提供了极高的渲染效率与帧率保障。
* **VR 交互层**: XR Interaction Toolkit (XRI 3.3.1) —— 官方强大的 XR 封装库，稳定处理头显空间定位、手柄 6DoF 移动以及复杂的射线触发逻辑。
* **底层 XR 插件**: 采用高度兼容的 OpenXR 1.10.0 协议标准，并内嵌 Oculus XR Plugin 4.2.0。
* **视觉与文本方案**: 使用 TextMeshPro (TMP 3.0.6) 保证在 VR 近距离观察下依然锐利的字体渲染；结合 Visual Effect Graph (VFX 14.0.10) 驱动 GPU 级别的大规模绚丽粒子特效。
* **音频可视化栈**: `Lasp` (Keijiro) 提供系统级低延迟 FFT 音频捕获（`#if LASP` 宏已启用），`AudioLink` 通过反射式集成提供 DFT 精细频段数据，`AudioVisualizationManager` 统一管理三频段全局 Shader 参数注入与频谱分析管线。
* **编程架构**: C# 面向对象设计 —— 严格遵循数据与视图分离的模块化架构，为开源社区的二次开发与大规模魔改提供了极其友好的土壤。

---

## ✨ 核心游戏特色 (Features)

* **原生解析与精准判定**: 内置纯 C# 高性能谱面解析器 (`OsuParser`)，直接读取 `.osu` 文件无需转换；严格复刻 `osu! Lazer` 的判定逻辑，从滑条节点 (Tick)、折返点 (Repeat) 到转盘转速，全链路精确结算。
* **奇妙的 3D 曲面投影**: 独家 `CoordinateMapper` 空间映射算法，将 512x384 平面坐标通过数学曲面函数弯折为环绕玩家 FOV 的 3D 全景曲面，配合物理级淡入动画与 60ms 固有音频延迟补偿，实现极致视听同步。
* **沉浸式 VR 交互体验**: 射线悬停交互机制实现”指哪打哪”；手柄震动反馈 (`HapticProfile`) 根据谱面音量与判定结果动态调整；UI 面板通过 `CurvedUIEffect` 物理弯折与 `HUDFollower` 弹簧跟随，彻底告别 VR 眩晕。
* **完整的游戏系统**: 集成 AutoPlay / HR / FL 等经典 Mod，内置自动本地化系统 (`LocalizationManager`) 支持多语言 Unicode 渲染，音效与震动采用 `TimingPoint × SampleVolume × 设置` 的完整乘法链路，精准可控。
* **数据驱动的视听演出**: 接入 `AudioLink` 与 `Lasp` 建立音频数据闭环，128 柱频谱渲染与 11 层环境粒子实时响应 BPM 节拍与 Kiai 时段；纯代码粒子引擎 (`CodeOnlyVFX`) 为低配设备提供流畅兜底方案。

---

## 📂 项目目录导览 (Project Structure)

我们非常注重工程目录的整洁度与代码的规范性。如果你在 Unity 中打开 `ProjectEther/Assets/`，你会看到如下脉络清晰的结构树：

```text
Assets/
├── Scenes/         # 游戏核心场景 (MainMenuScene 主菜单, SongSelectScene 选歌, GameScene 打歌, ResultScene 结算)
├── Prefabs/        # 资源预制体 (各类交互 UI 面板、飞行的音符实体、判定特效球等)
├── Shader/         # 自定义 URP 材质着色器 (包括滑条曲线渲染、UI 弯曲计算、AudioLink 响应着色器等)
├── Materials/      # 静态材质球库 (发光物件、天空盒、基础UI底图)
├── Texture/        # 2D 图片素材与 Sprite 精灵图集
├── Effekseer/      # 第三方开源粒子特效资源库 (为后续华丽的日系动画级舞台做准备)
├── Songs/          # 测试用谱面目录
└── Scripts/        # 游戏的心脏与大脑 (所有命名空间归属于 OsuVR)
    ├── Core/           # 玩法循环控制 (RhythmGameManager 调度器、NoteController/SliderController/SpinnerController 物件控制、CoordinateMapper 空间映射)
    ├── Data/           # 纯净的数据模型层 (OsuParser 文本解析、Beatmap / HitObject 实体类、TimingPoint 时间点)
    ├── Interaction/    # 玩家物理交互层 (RayController 射线逻辑、HapticManager 震动分发、AudioManager 音效管理)
    ├── System/         # 全局基础设施 (SettingsManager 设置管理、GameSettings 配置、LocalizationManager 本地化、ModEffectsApplier Mod效果)
    ├── UI/             # 界面交互层 (SimpleMainMenu 主菜单、SimpleSongSelection 选歌、VRSettingsMenu VR设置、PauseMenu 暂停面板)
    ├── Visuals/        # 视觉魔术师 (CodeOnlyVFX 纯代码打击特效、JudgementVisualizer 判定显示、EtherealEnvironment 128柱频谱环境)
    ├── Context/        # 跨场景数据快递员 (GameContext 负责将选歌数据安全传递到打歌场景、ResultData 结算数据)
    ├── Rulesets/       # 铁面无私的裁判 (ScoreManager 专职计算判定窗口、准确率与 Combo)
    └── Editor/         # 编辑器扩展工具 (UIPrefabUpdater UI生成、SimpleSceneSetup 场景配置)
```

---

## 💡 开发者快速上手指南 (Developer Onboarding)

想克隆 (Fork) 我们的项目进行深度定制或自己魔改？热烈欢迎！为了避免你在庞大的代码库中迷失，以下是为你准备的“寻路指南”：

### 1. 一首歌是怎么在屏幕上跑起来的？(核心数据流向)
理解数据流是理解本项目架构的绝对关键：
* **解析阶段 (Parsing)**: 当玩家在选歌界面 (`SongSelectScene`) 选中一首心仪的曲目后，跨场景单例 `GameContext` 会将其路径默默记下。场景切换至 `GameScene` 后，`OsuParser` 瞬间介入，将复杂的 `.osu` 文本按行拆解，精准翻译为内存中结构化的 `Beatmap` 数据模型。
* **映射阶段 (Mapping)**: 紧接着，数学魔术师 `CoordinateMapper` 开始工作。它提取每一个音符的 2D 坐标，运用三角函数将其从平坦的二维屏幕“捏弯”，精确部署到以玩家头部为圆心的 3D 环形曲面上的对应位置。
* **生成阶段 (Spawning)**: 引擎总指挥 `RhythmGameManager` 开始监听极其底层的硬件音频时间 (DSP Time)。它会根据谱面的缩圈速度 (AR)，提前计算好提前量，并呼叫后勤部长 `NotePoolManager`，从对象池中将沉睡的音符一个接一个地唤醒 (Spawn) 到玩家面前。
* **判定阶段 (Judgement)**: 当玩家的射线触碰到音符时，铁血裁判 `ScoreManager` 会在一毫秒内算出你的操作误差，决定你是 Great 还是 Miss。随后，它立即向视觉部门 `JudgementVisualizer` 发送信号，在对应的 3D 坐标引爆绚丽的命中文字与光晕。

### 2. 我想改点东西，该去哪个文件开刀？
* **我想加个全新的游戏 Mod (比如 Hidden)**：
  1. 首先去 `Data/Enums.cs` 的 `ModType` 枚举里加个名字。
  2. 然后去 `UI/ModSelectionUI.cs` 加上你的 UI 拨动开关。
  3. 最后在 `System/ModEffectsApplier.cs` 写入你的具体惩罚/奖励逻辑，并在对应音符生成时读取它（比如控制 MeshRenderer 渐隐）。
* **我觉得现有的判定太严苛**：
  直接推门进入 `Rulesets/ScoreManager.cs`。所有的 Hit Window（判定窗口毫秒数）以及 Combo 连击折算公式都在此统一定义。
* **我想让打击特效狂拽酷炫炸天**：
  请翻阅 `Visuals/JudgementVisualizer.cs`。为了保证极限帧率，目前的打击特效全是依靠纯代码实时生成的网格 (Mesh)。如果你想引入满屏的火花粒子，建议在这里通过事件系统调用预先做好的 VFX Graph 实例。

### 3. 项目开发铁律 (不可触碰的红线)
1. **数据层绝对纯净**: `Data/` 目录下的所有类，如 `Beatmap` 和 `HitObject`，仅仅是装载参数的容器。**绝对禁止**在其中引入 Unity 的 `GameObject` 或 `Transform` 引用，以确保未来剥离逻辑时的纯粹性。
2. **零垃圾回收 (0 GC) 原则**: 在音乐播放的 `Update` 循环中，**严禁**使用 `Instantiate` 和 `Destroy`！无论是飞驰的音符还是消散的粒子，必须老老实实向 `NotePoolManager` 申请对象池重用，否则瞬间的 GC 卡顿将毁掉玩家的全盘体验。
3. **VR UI 的人体工学**: 任何由你新增的交互面板，必须强制挂载自定义的 `CurvedUIEffect` 脚本让其产生内凹的物理弯折。平面的 UI 在 VR 视野边缘会导致严重的视觉畸变与眼球疲劳。

---

## 🚀 安装与游玩指南 (Getting Started)

### 1. 硬件与软件环境要求
* **操作系统**: Windows 10/11 (暂不支持 Mac 系统的原生 VR 调试)。
* **开发环境**: 请严格对齐使用 **Unity 2022.3.22f1 LTS** 或 2022.3 系列更高版本。
* **硬件设备**: 支持 OpenXR 标准的主流 VR 头显 (例如 Meta Quest 2/3/Pro, Pico 4, Valve Index)。如果你手头暂时没有头显，也可以在项目中开启 Unity 自带的 `XR Device Simulator`，用键鼠模拟手柄体验流程。

### 2. 手把手教你跑起项目
1. **拉取源码**:
   找个风水宝地，打开你的终端执行：
   ```bash
   git clone https://github.com/NeonSaya/Project-Ether.git
   ```
2. **导入 Unity Hub**: 打开 Unity Hub，点击 `Add` 按钮，选中刚刚克隆下来的 `Project-Ether/ProjectEther` 子目录。首次打开项目时，Unity 会疯狂下载 URP 和 XR 相关的依赖包并编译全项目 Shader，泡杯咖啡耐心等待几分钟。
3. **准备谱面资源**:

   > ⚠️ **注意**：项目运行时扫描的不是 `Assets/Songs`（那个目录仅用于测试），而是系统用户目录下的运行时文件夹。

   * 打开你电脑里的 `osu!` 游戏根目录，进入 `Songs` 文件夹，挑几个你最爱的谱面文件夹。
   * 找到每个谱面文件夹中的 `.osz` 压缩包（如果没有，可以在 osu! 官网下载页右键谱面选择 "Download .osz"）。
   * 将 `.osz` 文件放入以下路径：
     `C:/Users/<你的用户名>/AppData/LocalLow/Nyaon/ProjectEther/Songs/`
   * 项目启动时会自动扫描并解压 `.osz` 文件，之后就能在选歌界面看到对应的谱面了。

   > **提示**：如果 `.osz` 是文件夹形式（已解压的谱面），也可以直接放入上述目录。确保每个谱面文件夹内包含 `.osu` 文件、音频文件和背景图。太老的 `.osu` 格式（v10 以前）可能不被支持，建议使用较新的谱面。
4. **启动游戏**:
   * 必须在 Project 面板中双击进入 `Assets/Scenes/MainMenuScene.unity`。
   * 戴上并唤醒你的 VR 头显。
   * 点击 Unity 编辑器正上方居中的 **Play (▶)** 按钮！
   * 在 VR 里的主界面点击 `Play`，滑动列表选中你刚才导入的神曲，开启你的奇幻之旅！

---

## 🕹️ 核心操作与玩法指南 (How to Play)

为了保证游戏数据的完整流动与初始化，**请务必永远从主菜单 (MainMenuScene) 开始你的旅程**，否则会引发不可预知的空引用报错。

游戏的场景流转顺序非常清晰：
1. `MainMenuScene` (主界面): 调整语言、画面亮度，最重要的是可以在这里根据你的 VR 串流情况微调音频延迟。
2. `SongSelectScene` (选歌界面): 射线上下滑动列表，右侧面板可开启 AutoPlay 看神仙打架，或开启其他高难 Mod。
3. `GameScene` (演奏核心): 尽情享受视听盛宴。想临时上厕所？按下左手柄的 `Menu` 键或右手柄的 `Options` 键即可呼出包含沉浸式视角的暂停面板。
4. `ResultScene` (结算大厅): 看看你的高光时刻，统计图表会告诉你哪里打早了、哪里打晚了，最终拿走属于你的 S 评价。

**独创的 Relax 交互机制诀窍**：
* 整个打歌过程中，**你完全不需要按下手柄上的任何物理按键**（仅在菜单点选时需要扣动扳机 Trigger）。
* **全靠“空间悬停”**：当飞驰而来的音符外侧那个不断缩小的光圈（Approach Circle）与音符本体完美重合的一瞬间，只要你手中的红蓝射线正好指在音符区域内，系统就会自动触发极其精准的完美判定！
* **对付滑条 (Slider)**：用射线指着滑条头触发后，不要移开！射线紧紧跟着那颗不断滚动的滑条球 (Slider Ball) 一路滑到底。
* **对付转盘 (Spinner)**：出现大转盘时，用射线在转盘范围内像搅拌咖啡一样疯狂画圈即可飙升分数！

---

## ❓ 常见疑难杂症解答 (FAQ)

**Q1: 为什么我点 Play 之后直接掉进了虚空，连 UI 都没有？**
A: 请确认你是不是直接打开了打歌场景 (`GameScene`)？如果跳过了主菜单，游戏里的核心数据大管家 `GameContext` 就不知道你要加载哪首歌，从而罢工报错。一定要从 `MainMenuScene` 进！

**Q2: 谱面明明导入了，背景音乐也在放，但满屏就是没一个音符飞出来？**
A: 绝大多数情况下是因为你的谱面太“古董”了。如果是十年前 osu! v10 以前版本的 `.osu` 文件格式，我们当前手写的解析器可能无法完美兼容。建议使用近代生成的标准谱面。另外，可以按 `Ctrl+Shift+C` 看一眼控制台，如果有红色报错，可能是音频文件名由于特殊字符没被成功读取。

**Q3: 为什么我感觉我打得明明很准，听起来却总有令人抓狂的延迟？**
A: 这口锅通常要由 VR 串流软件来背。无论是 Quest Link、Air Link 还是 Virtual Desktop，无线网络传输不可避免地会带来 20ms 到 60ms 不等的音频链路延迟。请在主菜单的 `Settings` 中，根据体感反复调整 `Audio Offset`（音频偏移值），直到打击回馈与重音完美重合。

**Q4: 我是个穷苦大学生，没有 VR 设备，难道就不配帮你们写代码了吗？**
A: 绝对配！Unity 官方非常贴心地提供了 `XR Device Simulator` 插件。开启它后，你就能在电脑屏幕前，靠着风骚的 WASD 和鼠标走位，在屏幕上模拟出头显旋转和双手的移动空间。当然，如果你要调试毫秒级的手感，最终还是建议借个头显实机测试。

---

## 📅 未来开发蓝图 (To-Do List)

目前的 UI、特效以及全局背景仍处于“毛胚房”阶段。在基础游戏打歌玩法已经定型的前提下，我们未来的重心将完全转移到**视听演出的极致 VR 化**与**多端适配**上。为了让庞大的愿景落地，我们将开发计划拆解为了以下可行的小步目标：

### 阶段一：视觉特效重构与画面张力提升
- [x] **URP 后期管线基础配置**: 已完成 URP High Fidelity 渲染管线配置（HDR, MSAA 4x, 4096 阴影分辨率），内置 Tonemapping (ACES)、Bloom 泛光与 Vignette 暗角。
- [x] **物件渐入动画**: 所有音符与游戏物件已实现物理级淡入效果，提升视觉流畅度与沉浸感。
- [x] **打击反馈大换血**: 已实现纯代码驱动的高性能粒子特效系统 (`CodeOnlyVFX`)，支持对象池复用与 HDR 高亮爆发效果。
- [x] **精细化判定表现**: 已实现判定可视化器 (`JudgementVisualizer`)，为 300/100/50/Miss 四种判定结果配置独立颜色编码与弹出渐隐动画。
- [ ] **后期处理深度定制**: 引入 `X-PostProcessing-Library`，实现更高级的视觉滤镜效果（如径向模糊、色差、胶片颗粒等），进一步提升画面电影感。

### 阶段二：数据驱动的音频可视化舞台 (Audio $\rightarrow$ Visual) — 🟢 视听闭环已达成
这是本项目的杀手锏。核心逻辑：`音频数据化 (FFT 快速傅里叶变换) -> 数据流全面驱动视觉 (Shader 参数 & 粒子速率)`。
- [x] **精准音频频段捕获**: 接入 Keijiro 大神的 `Lasp`，实时获取极其低延迟的多频段 FFT 音频数据流。(`#if LASP` 宏已正式启用)
- [x] **建立全局视觉通道**: 引入 VRChat 社区的神器 `AudioLink`，通过反射式集成建立音频数据控制全局 Shader 材质变幻与环境光照的基础通道。
- [x] **128 柱频谱可视化**: `EtherealEnvironment` 驱动 128 根频谱柱渲染，支持 AudioLink DFT 精细频段与三频段 (Bass/Mid/Treble) 自动降级双通道。
- [x] **BPM 精准同步与 Kiai 检测**: 实现基于谱面 BPM 的精准节拍同步（二分查找 TimingPoints），解析并响应 Kiai 时段，让 Kiai 时光影爆发更具冲击力。
- [x] **代码驱动环境粒子**: 实现纯代码运算的环境粒子系统（11 层粒子），为后续 GPU 粒子方案提供低配兜底。
- [x] **URP 材质全面清洗**: 将工程中所有 `Shader.Find("Standard")` 替换为 `Universal Render Pipeline/Lit`，统一 `_Color` → `_BaseColor` 属性名，地板材质配置为深邃空灵镜面效果 (高 Metallic/Smoothness + 微弱 Emission)。
- [ ] **场景底模彻底焕新**: 深入应用 `Effekseer`，结合 AudioLink 数据，制作第一个能够随音乐频率高低起伏、律动呼吸的 MMD 风格大型动态舞台背景。

### 阶段三：osu! 经典特性 VR 重塑
- [ ] **多媒体引擎解析**: 提取并支持 `.osu` 谱面资源中包含的高清背景视频 (Video) 在 3D 巨幕上的播放。
- [ ] **Storyboard 基础指令搭建**: 编写全新的解析模块，读取并翻译故事板中的基础控制指令 (如 Fade 淡入淡出, Move 移动, Scale 缩放)。
- [ ] **震撼的 3D 投影演出**: 将原本受限于平面屏幕的传统 2D 故事板元素，通过深度计算，精准投射并交错在环绕玩家的 3D VR 空间之中。
- [ ] **Effekseer 特效演出**: 利用 `Effekseer` 制作与 Storyboard 联动的华丽粒子特效（如樱花飘落、烟花绽放、光束扫射等），打造沉浸式舞台演出体验。

### 阶段四：多平台设备全面适配 (PC / Quest / Pico)
- [ ] **底层解耦与抽象隔离**: 剥离现有强绑定于 PC VR 环境的冗余输入逻辑，封装一层高度统一的跨平台 Input 管理层。
- [ ] **一体机极限性能攻坚**: 针对 Meta Quest 系列的移动端 ARM 架构芯片，进行严苛的 URP 渲染大砍、Shader 变体精简与极致的 DrawCall 合并优化。
- [ ] **国产设备专属调优**: 针对 Pico 4 等国内主流头显设备，适配专属的控制器高模显示与契合其振动马达特性的精准触觉反馈。

---

## 🤝 欢迎加入贡献！(Contributing)

非常欢迎各位大佬和萌新们来一起为 Project Ether 添砖加瓦！

如果你有一技之长（不管你是写代码的、搞特效的还是做 UI 的），可以这样参与进来：
1. 去 Github 仓库的 `Issues` 区逛逛，看看有没有带 `help wanted` 或 `good first issue` 标签的求助任务。
2. Fork 这个充满潜力的仓库到你自己的名下。
3. 切出一个好听的新分支（比如 `feature/AddAwesomeLaserVFX`）。
4. 挥洒汗水写完代码后，提交 commit，然后给本项目潇洒地发起一个 Pull Request。
5. 我们会以最快的速度为你进行 Code Review，合并你的绝妙创意！

---

## 💖 致谢 (Credits)

本项目的破茧成蝶，离不开以下出色的开源项目与社区开发者们的无私奉献。站在巨人的肩膀上，我们才得以仰望星空：

* **[osu!](https://osu.ppy.sh/) (by peppy)**: 本项目全部玩法的绝对灵魂。其完全开源开放的谱面生态结构（.osu）与精妙绝伦的节奏机制设计，是一切梦开始的地方。
* **[osu-droid](https://github.com/osudroid/osu-droid)**: 其久经考验的 C# 开源解析代码，为我们独立手写并实现零误差的顶级谱面解析器 (`OsuParser`) 提供了极其关键且无可替代的参考。
* **[Lasp](https://github.com/keijiro/Lasp) (by Keijiro Takahashi)**: 大神出品的极低延迟音频分析库，是我们实时捕获高精度 FFT 数据流的终极基石。
* **[AudioLink](https://github.com/llealloo/vrc-udon-audio-link)**: 源自 VRChat 极客社区的革命性系统，为本项目的“音频数据驱动视觉”（Audio-Reactive Visuals）带来了前所未有的无限可能。
* **[X-PostProcessing-Library](https://github.com/QianMo/X-PostProcessing-Library) (by 浅墨)**: 提供了无比惊艳的 URP 定制化后期处理大片级滤镜库，极大拔高了项目的画面张力上限。在此深切缅怀 浅墨 大神。
* **[Effekseer](https://effekseer.github.io/)**: 极其强大且跨平台的开源粒子特效编辑工具，是我们构建二次元风格动感交互舞台的核心武器库。
* **[Unity](https://unity.com/)**: 感谢官方提供的 **XR Interaction Toolkit**、**Universal Render Pipeline** 与 **VFX Graph**，让每一位平凡的独立开发者，都能凭一己之力打造出拥有极高流畅度与画面表现力的 3D 沉浸式世界。

---

## 📄 开源协议 (License)
本项目始终拥抱开源精神，基于宽松的 **MIT License** 协议开源。想怎么用怎么用，详情请参阅仓库根目录里的 [LICENSE](LICENSE) 文件。
