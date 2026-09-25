# Storyboard 模块代码审计报告（22 个文件全部完整通读）

> 历史审计记录。当前实现和实测结论见 [StoryboardParityReport.md](StoryboardParityReport.md)。本报告 M6 的“宽屏坐标应偏移 107”结论已被真实谱面与 lazer 源码验证否定；不要据此重新引入 X 偏移。

审计范围：Assets/Scripts/Storyboard 全部（Engine 14 + Data 4 + Parser/Renderer/HolographicScreenManager/MediaAssetScanner/SBDebugLog）。
方法：逐行阅读 + 两个可疑点在 Unity 编辑器内用真实管线实测复现（SBEvaluateTimelineJob 直接 Execute），并用反射+实证测试否决了一个疑似 Texture2DArray.SetPixels 参数顺序问题（实测签名为 (colors, element, mip)，项目调用正确，未列入）。osu! 语义以官方 wiki（Objects/Commands/Compound_Commands/General_Rules）与 osu!lazer 源码核对。

## 高严重度（渲染数据错误）

### H1. SBTimelineFlattener.cs:112,141 — sprite 的"直接命令范围"错误地包含了 Loop 内层命令（已实测复现）
FlattenElement 在 112 行先取 cmdOffset = outCommands.Count，随后 FlattenLoopInner（:123）先把 Loop 内层命令追加进 outCommands，直接命令最后追加（:139），导致 CmdCount = outCommands.Count - cmdOffset（:141）把内层命令也算了进去。SBEvaluateTimelineJob.cs:72-86 的直接命令扫描因此会扫到 Loop 内层命令：在 Loop 尚未开始（EvalLoop 提前 return、未置 mask）时，内层命令以绝对时间被求值，Loop 的末状态提前泄漏生效。
实测：sprite 带直接 F(0-1000) + Loop(2000 起，内含 S 1->2, 0-500)，t=500 与 t=1500 输出 scaleX=2.0（osu! 正确值 1.0），t=2200 起才正确。修法：cmdOffset 应在 Loop 展开完成之后再取样（或从范围中扣除内层命令）。

### H2. SBEvaluateTimelineJob.cs:201-216 / SBPlayingSprite.cs:170-182 — Loop 结束后 mask 永久遮蔽之后开始的同属性直接命令（已实测复现）
EvalLoop 的 "past loop end" 分支对内层命令的全部目标置 mask 位，随后直接命令扫描（job :79 的 mask 检查）对这些目标永远跳过，导致有限 Loop 结束后属性卡死在 Loop 末值，之后显式写的直接命令被忽略。
实测：Loop(2000,5, S 1->2) 结束于 4500，直接 S(5000-6000, 0.5) 在 t=5500 输出 scaleX=2.0（应 0.5）。osu! stable 按文件序、lazer 按 StartTime 时间序合并（lazer ApplyTransforms: commands.Concat(loops).OrderBy(StartTime)），两种模型下后写的直接命令都应生效；代码注释"与 osu!/SBPlayingSprite 一致"不成立。

## 中严重度（特定条件触发的错误/风险）

### M1. StoryboardParser.cs:180-196 — Trigger 体命令泄漏为无条件直接命令
解析 T 行后 currentLoop=null（:184），其缩进的命令行全部挂到 currentElement 上、按（本应是"触发时刻起算的相对"）时间无条件执行；SBTrigger.Commands（SBElement.cs:472）永远为空，"仅存储不执行"的设计实际变成了"错误执行"。带 HitSound/Passing/Failing 触发器的谱面（现代 SB 常见）会出现错误动画（如 __F,0,0,500,1 在地图开头闪现）。

### M2. StoryboardParser.cs:310 — easing 35 (OutPow10) 被错误拒绝为 Linear
SBEasing 枚举共 36 个值（OutPow10=35），EasingMath.FromInt 接受 0..35、Burst Job 也有 case 35，唯独解析门限写成 easingInt <= 34。使用 easing 35 的命令静默退化为 Linear（项目内部自相矛盾，非外部规范问题）。

### M3. SBTimelineFlattener.cs:365-371 / SBPlayingSprite.cs:106-108 — P 命令的 BoolStart 被当作"命令前初始值"
ApplyInitialValues 对每个属性取最早命令的 StartValue 作为初值；P,H/V/A 经 SBCommandGroupBuilder.cs:136-137 转成 BoolCommand(true,false) 后，任何带翻转/加法混合命令的 sprite 的 InitFlipH/InitFlipV/Additive=true，翻转/加法混合在 P 命令开始前（从 sprite 出现起）就生效。wiki 明确 P 命令 "apply ONLY while they are active"。

### M4. StoryboardRenderer.cs:686-714 — 音乐暂停期间视频持续播放并被周期性 seek 回跳
currentMusicTimeMs 暂停时冻结（RhythmGameManager.cs:413），但 SyncVideoTime 只在 target<0 或 >= 视频时长时 Pause；暂停期间 VideoPlayer 继续播，drift 每 0.3s 触发一次 seek 回跳，整个暂停期视频以 0.3s 为周期反复跳帧（应在音乐暂停时同步 Pause）。

### M5. StoryboardRenderer.cs:762-763 + MediaAssetScanner.cs:36-37,87 — Android 上路径大小写/反斜杠不兼容
纹理加载用原始引用路径 Path.Combine + File.Exists（无大小写归一、无反斜杠转正斜杠）；textureIndexMap 的键做了归一（:859）但文件系统访问没做。Android（大小写敏感、反斜杠不是分隔符）下，Windows 谱面常见的 "SB\bg.png" 子目录引用和大小写不匹配引用全部静默丢失（sprite 不可见）；视频/背景路径同理。

### M6. StoryboardRenderer.cs:287 — widescreen 参数被完全忽略
RGM 传入 General.WidescreenStoryboard（RGM:677,688），但 LoadStoryboard 及整条渲染链从不使用；画布恒按 640 宽中心化（BuildInstanceJob CanvasW=640）。宽屏 SB（现代谱面默认，854x480 坐标空间）所有元素系统性水平偏移约 107px。

### M7. HolographicScreenManager.cs:22-23 vs StoryboardRenderer.cs:37-38 — 幕布 3:2 与 RT 16:9 纵横比失配
12x8 幕布（UV 0-1 全幅）显示 1920x1080 RT：水平 160px/单位 vs 垂直 135px/单位，SB/视频/背景内容水平压缩 18.5%（圆形变椭圆）。幕布应为 16:9（如 12x6.75）。

### M8. StoryboardRenderer.cs:791-811 — 纹理数组 1.8GB 上限不区分平台 + 逐层 GetPixels/SetPixels 巨额 GC
MAX_BYTES 硬编码 1.8GB，在 Android 一体机上（应用内存预算通常 2-4GB）大 SB 会 OOM/被系统杀进程；且 :835 每层 SetPixels(slice.GetPixels()) 在 2048 平方时单次分配约 64MB Color[]，加载期 GC 尖峰（应改 SetPixels32/SetPixelData，并按平台设上限）。

## 低严重度（代码卫生/边缘）

### L1. 死代码家族
SBCommandTimeline.cs 全文件（SBFloatTimeline/SBColorTimeline/SBBoolTimeline，无调用者）；SBElement.Evaluate/SBLoop.Evaluate/SBStoryboard.Evaluate 数据层求值链（无调用者，且 SBLoop.Evaluate 时间基准还是错的：loopTime=StartTime+elapsed%dur 拿去和相对时间命令比较）；EasingMath.FromInt；SBCommandTargetExt；CollectSpritesToNativeArray（StoryboardRenderer.cs:509）；RestoreBackgroundTexture（HolographicScreenManager.cs:179）。另：SBPlayingLayer.LoadSprites(:54-56) 的 List.Sort 不稳定，零时长 sprite（start==end 两条 task 同刻）可能 Remove 先于 Add 导致永不移出活跃链表——当前因整条旧管线不 Update 而无实际影响，复活该管线时会踩雷。

### L2. StoryboardRenderer.cs:321-322,873-889 — 每次加载都完整构建 osbPlayer 旧管线但从不 Update
纯内存浪费 + 每元素 2 个闭包分配；CacheTextureIndices(:884) 还用未归一路径查 map（必失败，好在消费方也是死代码）。

### L3. HolographicScreenManager.cs:567-577 — 背景图 LoadImage 失败分支泄漏 Texture2D
tex 创建后解码失败未 Destroy。

### L4. SBDebugLog.cs:14-27 — Begin/End 不配对
重复 Begin 泄漏旧 StreamWriter；LoadStoryboard 中途异常（无 try/finally，:297-350）则 End 不调用，文件句柄悬挂。

### L5. LoopCount=0 语义不一致
Parser(:428) 原样传 0；SBLoopCommand.cs:149 把 EndTime 算成 StartTime+0（存活窗口瞬间截止），而 Job/SBPlayingSprite 把 LoopCount==0 当无限循环（LoopCount > 0 才判结束）——同一值两处解释相反。

### L6. 嵌套 Loop 处理三处不一致
Parser 把内层 L 变成兄弟 Loop（外层后续命令错挂到内层）；SBTimelineFlattener.cs:257-263 丢弃并仅告警；SBPlayingSprite.ApplyCmd 的 switch 无 SBLoopCommand 分支静默忽略。规范上嵌套非法，属防御路径行为不一。

### L7. StoryboardRenderer.cs:473-503 — Update 不检查 _jobScheduled 就再次 Schedule
组件在 Update 与 LateUpdate 之间被禁用/跳过时，下帧对同一 _jobInputs/_jobOutput 双写，触发 Job safety 异常或数据竞争（UnloadStoryboard 路径有 Complete 保护，此为唯一漏网点）。

### L8. MediaAssetScanner.cs:23 — .webm 声称 Windows/Android 双平台保证可解码
Windows WMF 原生不支持 VP8/VP9，PC 端 webm 视频会走 errorReceived->背景回退（有兜底不黑屏，但注释的"保证"不成立）。

### L9. StoryboardParser.cs:237-240 — 注释声称支持"远古谱面缺 Layer/Origin"但 parts.Count<6 直接 return null
4 段式旧行被静默丢弃，默认值分支(:245-250)实际只在 6 段含空字段时可达。

## 已核实为正确（避免误报，供参考）
- Texture2DArray.SetPixels(slice.GetPixels(), i, 0) 参数顺序正确（编辑器内反射+实证：签名为 (colors, arrayElement, miplevel)，i 为层索引）。
- sprite 存活窗口 = [首命令开始, 末命令结束] 符合 osu! 官方语义（wiki Commands 页："An object stays active until its last command is done. After that, it disappears."）。
- 双 Job 链（eval->build）依赖正确，LateUpdate Complete 后才 SetData；UnloadStoryboard/DisposeJobSystem 均先 Complete。
- 热路径（Update/LateUpdate）零 GC 达标：无字符串/LINQ/闭包/装箱，NativeArray Persistent 预分配且全防护 Dispose。
- SBInstanced.shader 三个 Pass 名（SB_Opaque/SB_AlphaBlend/SB_Additive）与 SetShaderPassEnabled 匹配。
- Loop 迭代次数语义（=文件中 loopcount）与 wiki 一致；Blit 拉伸后按原始宽高建 quad，纵横比正确抵消。
- 视频加载失败->OnVideoLoadFailed->背景回退链路闭环；纯视频模式缓存 RGM 的问题已在代码中修复。
