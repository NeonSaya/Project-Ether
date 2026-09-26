# Project Ether (VR osu! Player)

![Unity](https://img.shields.io/badge/Made%20with-Unity%202022.3%20LTS-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-blue)
![Platform](https://img.shields.io/badge/Platform-PC%20VR%20%2F%20Standalone%20VR%20(OpenXR)-green)
![Status](https://img.shields.io/badge/Status-v0.7.7-brightgreen)

**中文** | [English](README_EN.md)

## 📖 项目概述 (Overview)

在如今的虚拟现实生态里，优秀的音乐节奏游戏层出不穷，却始终绕不开同一个遗憾：**高质量的社区自制谱面极度匮乏**。而另一边，走过十余年的经典 PC 音游 `osu!`，恰好坐拥着一片海量、惊艳、充满挑战的谱面海洋。

**Project Ether** 的诞生，就是为了在这两个世界之间架起一座桥梁。我们的终极目标，是打造一款基于 Unity 引擎的**沉浸式 VR 版 osu! 谱面播放器**。

我们的野心不止于把 2D 音符搬进 3D 空间——而是**让《Beat Saber》般爽快至极的打击手感，与 VRChat 中 MMD 舞台级别的顶级视听盛宴，在同一个世界里相遇。**

你只需举起手中的虚拟射线，在纯粹的音波起伏与绚丽的光影交错之间，便能轻松惬意地沉浸于每一首高质量 osu! 谱面带来的视听震撼。

> 🟢 **当前状态：v0.7.7**

> 核心链路（启动 -> 选曲 -> 游玩 -> 结算）已经打通，单点 (Circle)、滑条 (Slider)、转盘 (Spinner) 都已加入演奏。Storyboard 常见指令解析与 GPU 实例化渲染已上线，背景图、视频、SB 三层合成，亮度与透明度交给设置面板统一掌舵。Unity Jobs + Burst 已接手部分时间轴求值、矩阵计算与粒子颜色更新；主线程仍负责对象管理和绘制提交。我们正一步步靠近心中的视听盛宴，MMD 风格大型动态舞台也已写进后续工程清单。
>
> **平台支持**: 面向 PC VR (Windows) 与 Standalone VR (Android) 双平台。构建配置已就绪，Pico Neo 3 / Pico 4 / Pico 4 Ultra / Quest 2 / Quest 3 / Quest 3S 六款目标设备正按计划逐台验证输入、视频解码与性能——每点亮一台设备，通行证上就多一枚印章。

---

### 🎮 核心理念 (Core Concept)

* **Relax (轻松与释放)**: 告别物理按键，告别鼠标点击，告别高强度的肢体挥动。独创的 3D 空间射线悬停交互机制，实现“指哪打哪”的顺畅体验——长时间游玩，依然一身轻松。
* **Precision (绝对精准)**: 玩法可以休闲，底层绝不含糊。对照 `osu! Lazer` 复刻判定窗口的毫秒级计算与连击权重折算，留住顶级音游的核心操作快感；滑条、转盘与速度 Mod 按本项目实现结算，与上游逐项对齐的工作将持续推进。
* **Flow (沉浸心流)**: 极简主义与赛博朋克交织的视觉风格，摒弃一切喧宾夺主的干扰元素——让意识彻底溶解在纯粹的音乐与节拍之中。

---

## 💻 技术栈与底层依赖 (Tech Stack)

本项目使用最新的 Unity 技术栈打造，为未来的跨平台与高性能渲染打下了坚实基础：
* **游戏引擎**: Unity 2022.3.22f1 LTS —— 提供长期、稳定的底层架构支持。
* **画面渲染**: 通用渲染管线 (Universal Render Pipeline, URP 14.0.10) —— 在保证极佳画面表现力的同时，为移动端 VR 设备 (如 Quest) 提供了极高的渲染效率与帧率保障。
* **VR 交互层**: XR Interaction Toolkit (XRI 3.3.1) —— 官方强大的 XR 封装库，稳定处理头显空间定位、手柄 6DoF 移动以及复杂的射线触发逻辑。
* **底层 XR 插件**: 采用高度兼容的 OpenXR 1.10.0 协议标准，并内嵌 Oculus XR Plugin 4.2.0。
* **视觉与文本方案**: 使用 TextMeshPro (TMP 3.0.6) 保证在 VR 近距离观察下依然锐利的字体渲染；结合 Visual Effect Graph (VFX 14.0.10) 驱动 GPU 级别的大规模绚丽粒子特效。
* **音频可视化栈**: `Lasp` (Keijiro) 提供 PC 端系统级低延迟 FFT 音频捕获（`#if LASP` 宏隔离，仅 Standalone 定义，且需要在场景里配置分析组件才会启用），`AudioLink` 通过反射式集成提供 DFT 精细频段数据（跨平台兼容），`AudioVisualizationManager` 统一管理三频段全局 Shader 参数注入与频谱分析管线。
* **多线程架构**: Unity Jobs System + Burst Compiler —— Storyboard 的时间轴求值与矩阵计算、环境粒子颜色更新等重活已剥离至 Worker Thread 并行执行；对象管理、绘制提交与音符生成准备仍在主线程完成。
* **编程架构**: C# 面向对象设计 —— 严格遵循数据与视图分离的模块化架构，为开源社区的二次开发与大规模魔改提供了极其友好的土壤。

---

## ✨ 核心游戏特色 (Features)

* **原生解析与精准判定**: 内置纯 C# 高性能谱面解析器 (`OsuParser`)，直接读取 `.osu` 文件无需转换；对照 `osu! Lazer` 实现判定逻辑，从滑条节点 (Tick)、折返点 (Repeat) 到转盘转速都参与结算。悬停命中可提前 13ms 判定，缩短输入到反馈的延迟。
* **Storyboard 全指令引擎**: 完整解析 `.osb` / `.osu` 内联故事板，支持 Sprite、Animation、Loop、Trigger 全部指令类型。精灵走 GPU 实例化绘制、不占场景层级，参考 osu!lazer 与 storybrew 的评估逻辑，时间轴求值与矩阵计算交给 Burst Job，尽可能还原原版 SB 的视觉呈现；实际开销随谱面复杂度与设备而变化，我们会在真实场景中持续观测与优化。
* **多线程架构 (Unity Jobs + Burst)**: Storyboard 矩阵计算与粒子颜色更新已剥离至 Worker Thread，`IJobParallelFor` + `[BurstCompile]` 负责批量计算；音符坐标仍在生成时由主线程处理。收益随谱面与设备而不同。
* **三层合成渲染**: 背景图 / 视频 / SB 三层独立合成，SB Background 层可自动替代谱面背景图，设置面板统一控制全局亮度与透明度。
* **沉浸式 VR 交互体验**: 射线悬停交互机制实现“指哪打哪”；手柄震动反馈 (`HapticProfile`) 根据谱面音量与判定结果动态调整；常用面板通过 `CurvedUIEffect` 物理弯折与 `HUDFollower` 弹簧跟随缓解边缘畸变与眩晕，是否弯曲按面板距离与视角决定。
* **完整的游戏系统**: 集成 AutoPlay / HR / FL 等经典 Mod，内置自动本地化系统 (`LocalizationManager`) 支持多语言 Unicode 渲染，音效与震动采用 `TimingPoint × SampleVolume × 设置` 的完整乘法链路，精准可控。
* **数据驱动的视听演出**: 接入 `AudioLink` 与 `Lasp` 建立音频数据闭环，128 柱频谱渲染与 11 层环境粒子实时响应 BPM 节拍与 Kiai 时段；纯代码粒子引擎 (`CodeOnlyVFX`) 为低配设备提供流畅兜底方案。
* **跨平台构建**: 支持 PC VR (Windows OpenXR) 与 Standalone VR (Android / Pico / Quest) 双平台。Vulkan 图形 API + IL2CPP + ARM64，Dummy Material 反剔除机制确保 Shader 不被 Stripping。PC 与一体机各定制四档画质预设，一体机不锁帧、实际刷新率由设备与负载决定。
* **地面性能监视器**: 使用 Graphy 提供 FPS、内存和音频监控，支持图表显示与开关控制。

---

## 📂 项目目录导览 (Project Structure)

我们非常注重工程目录的整洁度与代码的规范性。如果你在 Unity 中打开 `ProjectEther/Assets/`，你会看到如下脉络清晰的结构树：

```text
Assets/
├── Scenes/         # 游戏核心场景 (MainMenuScene 主菜单, SongSelectScene 选歌, GameScene 打歌, ResultScene 结算)
├── Prefabs/        # 资源预制体 (各类交互 UI 面板、飞行的音符实体、判定特效球等)
├── Shader/         # 自定义 URP 材质着色器 (SBInstanced GPU实例化, HolographicScreen 幕布, SBOverlay 叠加, FlashlightMask 等)
├── Materials/      # 静态材质球库 (发光物件、天空盒、基础UI底图)
├── Texture/        # 2D 图片素材与 Sprite 精灵图集
├── Effekseer/      # 第三方开源粒子特效资源库
├── Songs/          # 测试用谱面目录
└── Scripts/        # 游戏的心脏与大脑 (所有命名空间归属于 OsuVR)
    ├── Core/           # 玩法循环控制 (RhythmGameManager + Burst Jobs调度、NoteController/SliderController/SpinnerController 物件控制、CoordinateMapper 空间映射、NotePoolManager 对象池)
    ├── Data/           # 纯净的数据模型层 (OsuParser 文本解析、Beatmap / HitObject 实体类、BeatmapImporter .osz导入)
    ├── Storyboard/     # Storyboard 全指令引擎 (解析、求值、GPU 实例化渲染、三层合成幕布)
    ├── Interaction/    # 玩家物理交互层 (RayController 射线逻辑、HapticManager 震动分发、AudioManager 音效管理、AutoPlayManager AI自动游玩)
    ├── System/         # 全局基础设施 (SettingsManager 设置管理 + PlayerPrefs持久化、LocalizationManager 本地化、ModEffectsApplier Mod效果)
    ├── UI/             # 界面交互层 (SimpleMainMenu 主菜单、SimpleSongSelection 选歌、VRSettingsMenu VR设置、PauseMenu 暂停面板)
    ├── Visuals/        # 视觉魔术师 (CodeOnlyVFX 纯代码打击特效、JudgementVisualizer 判定显示、EtherealEnvironment 128柱频谱环境、CodeDrivenAmbientParticles Burst粒子)
    ├── Context/        # 跨场景数据快递员 (GameContext 负责将选歌数据安全传递到打歌场景、ResultData 结算数据)
    ├── Rulesets/       # 铁面无私的裁判 (ScoreManager 专职计算判定窗口、准确率与 Combo)
    └── Editor/         # 编辑器扩展工具 (ShaderStrippingProtector Dummy材质防剔除, ShaderStripGuard Shader强制包含)
```

---

## 💡 开发者快速上手指南 (Developer Onboarding)

想克隆 (Fork) 我们的项目进行深度定制或自己魔改？热烈欢迎！为了避免你在庞大的代码库中迷失，我们准备了一份“寻路指南”：

### 1. 一首歌是怎么在屏幕上跑起来的？(核心数据流向)
理解数据流，就是理解本项目架构的钥匙：
* **解析阶段 (Parsing)**: 当玩家在选歌界面 (`SongSelectScene`) 选中一首心仪的曲目后，跨场景单例 `GameContext` 会将其路径默默记下。场景切换至 `GameScene` 后，`OsuParser` 瞬间介入，将复杂的 `.osu` 文本按行拆解，精准翻译为内存中结构化的 `Beatmap` 数据模型。
* **映射阶段 (Mapping)**: 紧接着，`CoordinateMapper` 开始工作。它提取每一个音符的 2D 坐标，将 osu! 的 512×384 游玩区域等比映射到玩家前方约 2 米、视线高度处一块 1.5 米 × 1.1 米的竖直打击平面上，忠实还原原版 osu! 的平面游玩体验。
* **生成阶段 (Spawning)**: 引擎总指挥 `RhythmGameManager` 开始监听极其底层的硬件音频时间 (DSP Time)。它会根据谱面的缩圈速度 (AR) 提前算好提前量，再呼叫后勤部长 `NotePoolManager`，把沉睡在对象池里的音符一个接一个地唤醒 (Spawn) 到玩家面前。
* **判定阶段 (Judgement)**: 当玩家的射线触碰到音符时，铁面无私的裁判 `ScoreManager` 会在一毫秒内算出你的操作误差，裁定 Great 还是 Miss。随后，它立即向视觉部门 `JudgementVisualizer` 发送信号，在对应的 3D 坐标引爆绚丽的命中文字与光晕。

### 2. 我想改点东西，该去哪个文件开刀？
* **我想加个全新的游戏 Mod (比如 Hidden)**：
  1. 首先去 `System/ModSystem.cs` 的 `ModType` 枚举里加个名字。
  2. 然后去 `UI/ModSelectionUI.cs` 加上你的 UI 拨动开关。
  3. 最后在 `System/ModEffectsApplier.cs` 写入你的具体惩罚/奖励逻辑，并在对应音符生成时读取它（比如控制 MeshRenderer 渐隐）。
* **我觉得现有的判定太严苛**：
  判定窗口（按 OD 换算）在 `Core/Judgement/JudgementConfig.cs`，分数档位与 Combo 折算在 `Rulesets/ScoreManager.cs`，具体何时提交判定由各物件控制器决定。
* **我想让打击特效狂拽酷炫炸天**：
  请翻阅 `Visuals/JudgementVisualizer.cs`。为了守住极限帧率，目前的打击特效全部依靠纯代码实时生成网格 (Mesh)。如果你想引入满屏的火花粒子，建议在这里通过事件系统调用预先做好的 VFX Graph 实例。

### 3. 项目开发铁律 (不可触碰的红线)
1. **数据层绝对纯净**: `Data/` 目录下的所有类，如 `Beatmap` 和 `HitObject`，仅仅是装载参数的容器。**绝对禁止**在其中引入 Unity 的 `GameObject` 或 `Transform` 引用，以确保未来剥离逻辑时的纯粹性。
2. **零垃圾回收 (0 GC) 的目标**: 在音乐播放的 `Update` 循环中，**严禁**随手使用 `Instantiate` 和 `Destroy`！无论是飞驰的音符还是消散的粒子，都老老实实向 `NotePoolManager` 申请对象池重用，否则瞬间的 GC 卡顿会毁掉玩家的全盘体验。目前已有池覆盖音符与部分音效，整条链路正朝着零分配的目标稳步迈进——新增代码时，也请顺手用 Profiler 看一眼 GC Alloc。
3. **VR UI 的人体工学**: 新增交互面板时，按它与玩家的距离和视角决定是否挂自定义的 `CurvedUIEffect` 产生内凹的物理弯折。平面的 UI 放在视野边缘会导致明显的视觉畸变与眼球疲劳。

---

## 🚀 安装与游玩指南 (Getting Started)

### 1. 硬件与软件环境要求
* **操作系统**: Windows 10/11。
* **开发环境**: 请严格对齐使用 **Unity 2022.3.22f1 LTS** 或 2022.3 系列更高版本。
* **硬件设备**: 支持 OpenXR 标准的 PC VR 头显 (如 Valve Index, Meta Quest via Link, Pico 4 via Streaming Assistant)。手头暂时没有头显也没关系——项目中开启 Unity 自带的 `XR Device Simulator`，即可用键鼠模拟手柄体验完整流程。

### 2. 手把手教你跑起项目
1. **拉取源码**:
   找个风水宝地，打开你的终端执行：
   ```bash
   git clone https://github.com/NeonSaya/Project-Ether.git
   ```
2. **导入 Unity Hub**: 打开 Unity Hub，点击 `Add` 按钮，选中刚刚克隆下来的 `Project-Ether/ProjectEther` 子目录。首次打开项目时，Unity 会疯狂下载 URP 和 XR 相关的依赖包并编译全项目 Shader——泡杯咖啡，耐心等待几分钟。
3. **准备谱面资源**:

   > ⚠️ **注意**：项目运行时扫描的不是 `Assets/Songs`（那个目录仅用于测试），而是系统用户目录下的运行时文件夹。

   * 打开你电脑里的 `osu!` 游戏根目录，进入 `Songs` 文件夹，挑几个你最爱的谱面文件夹。
   * 找到每个谱面文件夹中的 `.osz` 压缩包（如果没有，可以在 osu! 官网下载页右键谱面选择 "Download .osz"）。
   * 将 `.osz` 文件放入以下路径：
     - **PC**: `C:/Users/<你的用户名>/AppData/LocalLow/Nyaon/ProjectEther/Songs/`
     - **Android**: `内部存储/Android/data/com.Nyaon.ProjectEther/files/Songs/`（也可以在设置界面点击“导入谱面”，通过系统文件选择器直接选择 `.osz` 文件导入，支持多选）
   * 项目启动时会自动扫描并解压 `.osz` 文件，之后就能在选歌界面看到对应的谱面了。也可以在设置界面中直接打开 Songs 文件夹拖入 `.osz`。

   > **提示**：如果 `.osz` 是文件夹形式（已解压的谱面），也可以直接放入上述目录。确保每个谱面文件夹内包含 `.osu` 文件、音频文件和背景图。
4. **启动游戏**:
   * 必须在 Project 面板中双击进入 `Assets/Scenes/MainMenuScene.unity`。
   * 戴上并唤醒你的 VR 头显。
   * 点击 Unity 编辑器正上方居中的 **Play (▶)** 按钮！
   * 在 VR 里的主界面点击 `Play`，滑动列表选中你刚才导入的神曲，开启你的奇幻之旅！

---

Android 本地构建默认使用开发签名。正式发布请在 Player Settings → Publishing Settings 中选择自己的 keystore 和别名；更新已安装的正式版必须沿用原签名。签名文件、密码、本机路径、个人笔记和验证输出不应提交到仓库。

## 🕹️ 核心操作与玩法指南 (How to Play)

为了让游戏数据完整流动、顺利初始化，**请务必永远从主菜单 (MainMenuScene) 开始你的旅程**，避开不可预知的空引用报错。

游戏的场景流转顺序非常清晰：
1. `MainMenuScene` (主界面): 调整语言、画面亮度，最重要的是可以在这里根据你的 VR 串流情况微调音频延迟。
2. `SongSelectScene` (选歌界面): 射线上下滑动列表，右侧面板可开启 AutoPlay 看神仙打架，或开启其他高难 Mod。
3. `GameScene` (演奏核心): 尽情享受视听盛宴。想临时上厕所？按下左手柄的 `Menu` 键或右手柄的 `Options` 键，即可呼出包含沉浸式视角的暂停面板。
4. `ResultScene` (结算大厅): 看看你的高光时刻，统计图表会告诉你哪里打早了、哪里打晚了，最终拿走属于你的 S 评价。

**独创的 Relax 交互机制诀窍**：
* 整个打歌过程中，**你完全不需要按下手柄上的任何物理按键**（仅在菜单点选时需要扣动扳机 Trigger）。
* **全靠“空间悬停”**：当飞驰而来的音符外侧那个不断缩小的光圈（Approach Circle）与音符本体完美重合的一瞬间，只要你手中的红蓝射线正好指在音符区域内，系统就会自动触发极其精准的完美判定！
* **对付滑条 (Slider)**：用射线指着滑条头触发后，不要移开！让射线紧紧跟着那颗不断滚动的滑条球 (Slider Ball)，一路滑到底。
* **对付转盘 (Spinner)**：出现大转盘时，用射线在转盘范围内像搅拌咖啡一样疯狂画圈，分数便会一路飙升！

---

## ❓ 常见疑难杂症解答 (FAQ)

**Q1: 为什么我点 Play 之后直接掉进了虚空，连 UI 都没有？**
A: 请确认你是不是直接打开了打歌场景 (`GameScene`)？如果跳过了主菜单，游戏里的核心数据大管家 `GameContext` 就不知道你要加载哪首歌，从而罢工报错。一定要从 `MainMenuScene` 进！

**Q2: 谱面明明导入了，背景音乐也在放，但满屏就是没一个音符飞出来？**
A: 可以按 `Ctrl+Shift+C` 看一眼控制台，如果有红色报错，可能是音频文件名由于特殊字符没被成功读取。

**Q3: 为什么我感觉我打得明明很准，听起来却总有令人抓狂的延迟？**
A: 这口锅通常要由 VR 串流软件来背。无论是 Quest Link、Air Link 还是 Virtual Desktop，无线网络传输不可避免地会带来 20ms 到 60ms 不等的音频链路延迟。请在主菜单的 `Settings` 中，根据体感反复调整 `Audio Offset`（音频偏移值），直到打击回馈与重音完美重合。

**Q4: 我还是在校开发者 / 预算有限，没有 VR 设备，难道就没资格一起写代码了吗？**
A: 当然够格！Unity 官方贴心地提供了 `XR Device Simulator` 插件。开启它后，你就能坐在电脑屏幕前，靠着风骚的 WASD 和鼠标走位，在屏幕上模拟出头显旋转和双手的移动空间。等你需要打磨毫秒级手感时，再借一台头显实机验证即可。

**Q5: 一体机版本支持哪些设备？**
A: 自 v0.7.1 起提供 Standalone VR (Android) 平台构建，目标覆盖 Pico Neo 3 / Pico 4 / Pico 4 Ultra / Meta Quest 2 / Quest 3 / Quest 3S 六款主流一体机。Android 视频播放已验证可用；输入与帧时间正在逐台实测中，Android OpenXR 路径的控制器 Profile 也将在后续版本启用。画质预设按平台分为四档，一体机不锁帧，首次启动默认中画质，可在设置中切换档位。

**Q6: 为什么 Storyboard 的效果和 osu! 里看到的不完全一样？**
A: 我们的 SB 引擎参考了 osu!lazer 和 storybrew 的开源实现，力求尽可能还原原版的视觉风格与合成逻辑。受 Unity 引擎与 osu! 原生渲染之间的架构差异影响（如浮点精度、混合模式、纹理采样等），极少数情况下会存在细微的视觉差异。这是当前技术栈下的客观边界，也是我们持续打磨的方向——后续版本将不断缩小与原版的差距。

---

## 📅 未来开发蓝图 (To-Do List)

目前的 UI、特效以及全局背景仍处于“毛坯房”阶段。在基础打歌玩法已经定型的前提下，我们未来的重心将完全转向**视听演出的极致 VR 化**与**多端适配**。为了让庞大的愿景落地，我们把开发计划拆解成了以下可行的小步目标：

### 阶段一：视觉特效重构与画面张力提升
- [x] **URP 后期管线基础配置**: 已完成 URP High Fidelity 渲染管线配置（HDR, MSAA 4x, 4096 阴影分辨率），启用 Bloom 泛光与 Vignette 暗角；Tonemapping 当前档位是 Neutral，更进一步的 ACES 调色已列入候选。
- [x] **物件渐入动画**: 游玩物件（音符、滑条等）已实现淡入与淡出效果，提升视觉流畅度与沉浸感；这里的范围是演出物件，不包括场景里的所有对象。
- [x] **打击反馈大换血**: 已实现纯代码驱动的高性能粒子特效系统 (`CodeOnlyVFX`)，支持对象池复用与 HDR 高亮爆发效果。
- [x] **精细化判定表现**: 已实现判定可视化器 (`JudgementVisualizer`)，为 300/100/50/Miss 四种判定结果配置独立颜色编码与弹出渐隐动画。
- [ ] **后期处理深度定制**: `X-PostProcessing-Library` 已经在工程里——它的程序集面向 Editor、运行旧版 PPv2 栈，将作为滤镜宝库而非现成管线使用；下一步是挑出真正需要的滤镜（径向模糊、色差、胶片颗粒等），逐一移植到 URP 路径上。

### 阶段二：数据驱动的音频可视化舞台 (Audio $\rightarrow$ Visual) — 🟢 视听闭环已达成
这是本项目的杀手锏。核心逻辑：`音频数据化 (FFT 快速傅里叶变换) -> 数据流全面驱动视觉 (Shader 参数 & 粒子速率)`。
- [x] **精准音频频段捕获**: 接入 Keijiro 大神的 `Lasp`，实时获取极其低延迟的多频段 FFT 音频数据流。（`#if LASP` 宏已在 Standalone 启用，要真正吃到系统捕获还需在场景里配置分析组件）
- [x] **建立全局视觉通道**: 引入 VRChat 社区的神器 `AudioLink`，通过反射式集成建立音频数据控制全局 Shader 材质变幻与环境光照的基础通道。
- [x] **128 柱频谱可视化**: `EtherealEnvironment` 驱动 128 根频谱柱渲染，支持 AudioLink DFT 精细频段与三频段 (Bass/Mid/Treble) 自动降级双通道。
- [x] **BPM 精准同步与 Kiai 检测**: 实现基于谱面 BPM 的精准节拍同步（二分查找 TimingPoints），解析并响应 Kiai 时段，让 Kiai 时光影爆发更具冲击力。
- [x] **代码驱动环境粒子**: 实现纯代码运算的环境粒子系统（11 层粒子），为后续 GPU 粒子方案提供低配兜底。
- [x] **URP 材质全面清洗**: 将工程中所有 `Shader.Find("Standard")` 替换为 `Universal Render Pipeline/Lit`，统一 `_Color` → `_BaseColor` 属性名，地板材质配置为深邃空灵镜面效果 (高 Metallic/Smoothness + 微弱 Emission)。
- [ ] **场景底模彻底焕新**: 深入应用 `Effekseer`，结合 AudioLink 数据，制作第一个能够随音乐频率高低起伏、律动呼吸的 MMD 风格大型动态舞台背景。

### 阶段三：osu! 经典特性 VR 重塑 — 🟢 Storyboard 引擎已上线
- [x] **Storyboard 全指令解析**: 完整支持 Sprite、Animation、Loop、Trigger 及 Fade/Move/Scale/Rotate/Color/Parameter 全部指令。
- [x] **GPU 实例化渲染**: 精灵不进场景层级，一次程序化实例绘制提交，Alpha Blend 与 Additive 双通道渲染。
- [x] **多线程时间轴求值**: 参考 osu!lazer 与 storybrew 的命令评估逻辑，时间轴求值与矩阵计算交给 Burst Job；主线程负责批次组织与绘制提交。
- [x] **视频背景播放**: 支持 `.mp4` / `.webm` / `.mov` 视频作为背景，通过 `VideoPlayer` + `Graphics.Blit` 渲染到全息幕布；遇到 `.avi` 等格式会优雅跳过并回退背景图。Android 视频播放已验证可用，更多编码与设备的验证正按实际环境持续推进。
- [x] **三层合成渲染**: 背景图 / 视频 / SB 三层独立合成，SB Background 层可自动替代谱面背景图，设置面板统一控制全局亮度与透明度。
- [x] **重试与触发局态**: 暂停菜单 Retry 会清空触发链、待处理触发与视频同步状态，无需重新解码纹理；已撤销的触发记录会在渲染前稳定回收。
- [ ] **有效触发历史的长期上界**: 长期游玩时可能积累有效触发记录——只要约定好未来触发时间的下界，就能安全裁剪，这项收口工作已提上日程。
- [ ] **Effekseer 特效演出**: 利用 `Effekseer` 制作与 Storyboard 联动的华丽粒子特效。

> **关于 Storyboard 还原度：** 本引擎参考 osu!lazer 与 storybrew 的开源实现，在 Unity URP 管线下尽可能还原 osu! 原版 Storyboard 的视觉风格与合成逻辑。受引擎架构差异影响，极少数场景会与原版存在像素级出入，但绝大多数谱面都能获得贴合原版的观赏体验。我们将持续对齐上游更新，逐步提升还原精度。

### 阶段四：多平台设备全面适配 (PC / Quest / Pico) — 🟢 双平台构建已打通
- [x] **跨平台文件系统**: 所有文件 I/O 统一使用 `Application.persistentDataPath`，支持 .osz 拖放导入 (PC) 与 Android 原生文件选择器。
- [x] **Android 图形 API**: 按 Vulkan 优先 + IL2CPP + ARM64 配置，ComputeBuffer / GPU Instancing 走这条路径；具体设备上的驱动差异将随实测逐一适配。
- [x] **Shader 反剔除**: Dummy Material 资源偷渡法 + Always Included Shaders 双重保护，确保自定义 Shader 不被构建剔除。
- [x] **OpenXR 双平台**: PC (OpenXR) + Android (Oculus + OpenXR) 双 Loader 配置齐全；各机型的控制器映射正逐台确认中，Android OpenXR 路径的控制器 Profile 也将在适配完成时开启。
- [ ] **国产设备专属调优**: 针对 Pico 4 等国内主流头显设备，适配专属的控制器高模显示与契合其振动马达特性的精准触觉反馈。

### 阶段五：多线程全局优化 — 🟢 核心管线已上线
- [x] **Storyboard 时间轴多线程化**: 时间轴求值与矩阵计算走 Burst 并行，结果写入 Persistent NativeArray；绘制前通过 `ComputeBuffer.SetData` 上传实例数据。
- [x] **粒子颜色计算 Job 化**: `CodeDrivenAmbientParticles` 的 12000 粒子 HSV + 闪烁计算剥离至 Burst Job。
- [x] **音符 SoA 扁平化**: `NativeArray<double>` spawnTimes / startTimes 与类型数组由主线程填充，生成时用二分查找框定区间；架构保持轻量，`worldPositions` 数组与加载期 Burst 坐标预计算均在评估清单中按需引入。
- [x] **二分搜索替代线性扫描**: `SpawnNotes` 中 O(log N) 上界查找替代 while 循环。
- [ ] **碰撞检测自定义化**: 当前每只手每帧一次 `SphereCastNonAlloc`，PhysX 查询是否构成瓶颈将交由性能测量回答；“活跃音符 > 500 就替换”是候选阈值，拿到可复现的性能数据后即动手。

### 阶段？？？：星河彼岸 — 🔭 远眺 Unity 6

> 这不是路线图上的必选项，更像一个放在远处的念想。从 Unity 2022 LTS 到 Unity 6，意味着 Render Graph、GPU Resident Drawer 等新一代渲染栈的全面就绪。我们有计划将 Project Ether 迁入新引擎，在保持现有风格与体验的前提下，走进下一个技术世代。启程之日尚未写进日历，但方向已经在星图上。

- [ ] **引擎升级至 Unity 6**：从 Unity 2022.3.22f1 LTS 迁移，完成 API 适配、包依赖更新与废弃接口替换。动身前先在独立分支留下 Unity 2022 双平台构建、判定、媒体播放与帧时间的可复测基线，版本号也等验证通过后再定。
- [ ] **URP Render Graph 适配**：把全息幕布、SB 实例化渲染与后处理逐步迁入 Render Graph。目前 SB 走的是图外 `Graphics.ExecuteCommandBuffer` + 自有 RT，全息幕布是 MeshRenderer 加材质——工作量远不止改几个 API 名，回退开销也将以实测数据说话。
- [ ] **GPU Resident Drawer 与 STP**：GPU Resident Drawer 服务于符合条件的普通场景 MeshRenderer，Storyboard 的程序化实例绘制将继续走自己的专属通道；STP 的版本与 XR 支持单独评估——Unity 6.0 的兼容表中它标注为不支持，我们将等待合适的时机再让它与 MSAA 同台竞技。
- [ ] **全链路回归与双平台验证**：升级后覆盖核心玩法判定、谱面解析、Storyboard 全指令渲染及 PC VR / Standalone VR 双平台构建，确保功能无退化、性能不低于当前基线。

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
* **[osu!lazer](https://github.com/ppy/osu)**: Storyboard 命令求值逻辑的核心参考。其时间轴状态机、Loop 动态迭代、属性优先级等设计，为本项目的 SB 引擎提供了最权威的实现依据。
* **[storybrew](https://github.com/Damnae/storybrew)**: Storyboard 编辑器与渲染推演的参考标杆。其命令时间轴系统、缓动函数实现、Loop/Trigger 运行时解析、以及 Sprite 合成逻辑，帮助我们逐一校准了渲染管线的每一个细节。
* **[osu-droid](https://github.com/osudroid/osu-droid)**: 其久经考验的 C# 开源解析代码，为我们独立手写并实现零误差的顶级谱面解析器 (`OsuParser`) 提供了极其关键且无可替代的参考。
* **[OsuParsers](https://github.com/mrflashstudio/OsuParsers)**: 轻量级 .osu/.osb 文件格式解析库。其清晰的解码器架构与数据模型设计，为我们的谱面解析器补全与校验提供了重要的对照参考。
* **[Lasp](https://github.com/keijiro/Lasp) (by Keijiro Takahashi)**: 大神出品的极低延迟音频分析库，是我们实时捕获高精度 FFT 数据流的终极基石。
* **[AudioLink](https://github.com/llealloo/vrc-udon-audio-link)**: 源自 VRChat 极客社区的革命性系统，为本项目的“音频数据驱动视觉”（Audio-Reactive Visuals）带来了前所未有的无限可能。
* **[X-PostProcessing-Library](https://github.com/QianMo/X-PostProcessing-Library) (by 浅墨)**: 提供了无比惊艳的 URP 定制化后期处理大片级滤镜库，极大拔高了项目的画面张力上限。在此深切缅怀 浅墨 大神。
* **[Effekseer](https://effekseer.github.io/)**: 极其强大且跨平台的开源粒子特效编辑工具，是我们构建二次元风格动感交互舞台的核心武器库。
* **[Graphy](https://github.com/Tayx94/graphy) (Tayx94 / Martín Pane) [MIT]**: 为地面性能监视器提供 FPS、内存和音频统计与图表，使用 v4.0.0，固定提交为 [`1068f3de2bc9f1e4a905150abd59f31e6dead679`](https://github.com/Tayx94/graphy/commit/1068f3de2bc9f1e4a905150abd59f31e6dead679)。
* **[Unity](https://unity.com/)**: 感谢官方提供的 **XR Interaction Toolkit**、**Universal Render Pipeline** 与 **VFX Graph**，让每一位平凡的独立开发者，都能凭一己之力打造出拥有极高流畅度与画面表现力的 3D 沉浸式世界。

---

## 📄 开源协议 (License)
本项目始终拥抱开源精神，基于 **GNU General Public License v3.0 (GPL-3.0)** 协议开源。GPL-3.0 **允许商业使用**——你可以自由使用、修改、分发本项目；相应地，复制、修改与分发也请遵循许可证的义务：分发衍生作品时保留相应声明，并以 GPL-3.0 提供对应源码等。详情请参阅仓库根目录里的 [LICENSE](LICENSE) 文件；第三方资源可能另有各自的许可，请分别确认。

Graphy 采用 **MIT License**，版权声明为 `Copyright (c) 2018 Martín Pane`。上游完整许可保留在 [Resources/ThirdPartyLicenses/Graphy.txt](ProjectEther/Assets/Resources/ThirdPartyLicenses/Graphy.txt) 中，并随构建包含；游戏内制作人员页面也显示该版权声明与 MIT 许可全文。