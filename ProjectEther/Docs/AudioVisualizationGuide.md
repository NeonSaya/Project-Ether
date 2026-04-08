# Project Ether 音频可视化基础架构使用指南

## 📋 目录

1. [架构概览](#架构概览)
2. [核心组件说明](#核心组件说明)
3. [配置方法](#配置方法)
4. [常见操作流程](#常见操作流程)
5. [Shader集成指南](#shader集成指南)
6. [VFX Graph集成指南](#vfx-graph集成指南)
7. [最佳实践建议](#最佳实践建议)
8. [故障排除](#故障排除)
9. [维护注意事项](#维护注意事项)
10. [已知问题与优化建议](#已知问题与优化建议)

---

## 架构概览

### 系统架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                    音频输入层                                    │
├─────────────────────────────────────────────────────────────────┤
│  AudioSource (游戏音频)          Lasp (系统音频捕获)             │
│         ↓                              ↓                        │
│  GetSpectrumData()              SpectrumAnalyzer                │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│                 AudioVisualizationManager                       │
├─────────────────────────────────────────────────────────────────┤
│  FFT分析 → 三频段分离 → 平滑处理 → 全局Shader变量               │
│                                                                 │
│  Bass (0-150Hz)   Mid (200-500Hz)   Treble (500Hz-4kHz)        │
│       ↓                 ↓                  ↓                    │
│  _Global_Audio_Bass  _Global_Audio_Mid  _Global_Audio_Treble    │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│                    输出层                                       │
├─────────────────────────────────────────────────────────────────┤
│  AudioVFXDriver          Shader           VFX Graph             │
│  (Transform缩放)         (材质属性)       (粒子系统)             │
│                                                                 │
│  AudioLink (可选高级功能)                                       │
│  _AudioTexture (频谱纹理)                                       │
└─────────────────────────────────────────────────────────────────┘
```

### 依赖关系

```
Packages/manifest.json
├── jp.keijiro.lasp (2.1.8)          - 系统级音频捕获
├── jp.keijiro.laspvfx (1.0.3)       - Lasp VFX扩展
├── com.llealloo.audiolink           - 高级音频分析
├── com.unity.render-pipelines.universal (14.0.10)
└── com.unity.visualeffectgraph (14.0.10)
```

---

## 核心组件说明

### 1. AudioVisualizationManager

**位置**: `Assets/Scripts/Interaction/AudioVisualizationManager.cs`

**职责**: 核心FFT分析引擎，提供三频段能量数据

**关键属性**:

| 属性 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `smoothSpeed` | float | 平滑速度（值越小越平滑） | 0.1 |
| `bassGain` | float | 低频增益倍数 | 1.5 |
| `midGain` | float | 中频增益倍数 | 1.2 |
| `trebleGain` | float | 高频增益倍数 | 1.0 |
| `normalizationFactor` | float | 归一化系数 | 10 |
| `targetAudioSource` | AudioSource | 目标音频源 | 自动查找 |
| `lockTargetSource` | bool | 锁定目标音频源 | false |
| `useLaspCapture` | bool | 使用Lasp系统捕获 | false |
| `spectrumAnalyzer` | SpectrumAnalyzer | Lasp分析器组件 | null |

**公开属性**:

| 属性 | 类型 | 说明 |
|------|------|------|
| `Bass` | float | 低频能量 (0-150Hz)，范围0-1 |
| `Mid` | float | 中频能量 (200-500Hz)，范围0-1 |
| `Treble` | float | 高频能量 (500Hz-4kHz)，范围0-1 |

**全局Shader变量**:

```hlsl
// 在Shader中直接访问
float _Global_Audio_Bass;   // 低频能量
float _Global_Audio_Mid;    // 中频能量
float _Global_Audio_Treble; // 高频能量
```

---

### 2. AudioVFXDriver

**位置**: `Assets/Scripts/Visuals/AudioVFXDriver.cs`

**职责**: 通用视觉驱动器，将音频数据转换为视觉效果

**枚举定义**:

```csharp
public enum FrequencyBand { Bass, Mid, Treble }
public enum DriveTarget { TransformScale, VFXProperty }
```

**关键属性**:

| 属性 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `frequencyBand` | FrequencyBand | 响应的频段 | Bass |
| `driveTarget` | DriveTarget | 驱动方式 | TransformScale |
| `baseScale` | Vector3 | 基础缩放 | (1,1,1) |
| `maxScaleMultiplier` | float | 最大放大倍数 | 3 |
| `scaleSmoothSpeed` | float | 缩放平滑速度 | 0.15 |
| `vfxPropertyName` | string | VFX属性名 | "BassEnergy" |
| `responseCurve` | AnimationCurve | 响应曲线 | 线性 |

---

### 3. AudioLinkAdapter

**位置**: `Assets/Scripts/Interaction/AudioLinkAdapter.cs`

**职责**: 桥接AudioLink系统与Project Ether架构

**关键属性**:

| 属性 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `audioLinkComponent` | MonoBehaviour | AudioLink组件引用 | 自动查找 |
| `autoSync` | bool | 自动同步音频源 | true |
| `syncInterval` | float | 同步间隔（秒） | 1.0 |

**全局纹理**:

```hlsl
// AudioLink提供的全局纹理
TEXTURE2D(_AudioTexture);
SAMPLER(sampler_AudioTexture);
```

---

### 4. AudioVisualizationDebugger

**位置**: `Assets/Scripts/Interaction/AudioVisualizationDebugger.cs`

**职责**: 调试工具，帮助排查音频响应问题

**功能**:
- 运行时GUI显示实时数据
- 完整诊断命令
- 问题检测和建议

---

## 配置方法

### 基础配置（推荐）

1. **创建AudioVisualizationSystem**
   ```
   Hierarchy 右键 → Create Empty → 命名为 "AudioVisualizationSystem"
   Add Component → AudioVisualizationManager
   ```

2. **配置音频源**
   - 方式A：手动指定
     - 将AudioSource拖入 `targetAudioSource` 字段
     - 勾选 `lockTargetSource`
   - 方式B：自动查找
     - 保持 `targetAudioSource` 为空
     - 系统会自动查找场景中的AudioSource

3. **添加视觉响应物体**
   ```
   创建Cube → Add Component → AudioVFXDriver
   配置 frequencyBand 和 driveTarget
   ```

### Lasp系统捕获配置（可选）

用于捕获系统级音频（如麦克风、系统声音）：

1. **添加Lasp组件**
   ```
   AudioVisualizationSystem → Add Component → Spectrum Analyzer
   ```

2. **配置AudioVisualizationManager**
   ```
   spectrumAnalyzer = 刚添加的Spectrum Analyzer组件
   useLaspCapture = true
   ```

3. **配置Spectrum Analyzer**
   ```
   Channel = Stereo
   Resolution = 2048
   ```

### AudioLink高级配置（可选）

用于更精细的音频分析和高级特效：

1. **添加AudioLink**
   ```
   创建空物体 → 命名为 "AudioLink"
   Add Component → AudioLink (来自AudioLink包)
   Add Component → AudioLinkAdapter
   ```

2. **配置AudioLinkAdapter**
   ```
   autoSync = true
   syncInterval = 1.0
   ```

---

## 常见操作流程

### 流程1：创建测试场景

```
菜单 Tools → Project Ether → 创建AudioLink测试场景
```

这会自动创建：
- AudioVisualizationSystem（含AudioVisualizationManager）
- TestAudioSource（含AudioSource）
- AudioReactiveCube（含AudioVFXDriver）
- 基础场景元素（相机、灯光、地面）

### 流程2：添加新的音频响应物体

1. 创建物体（如Cube、Sphere）
2. 添加 `AudioVFXDriver` 组件
3. 选择 `frequencyBand`（Bass/Mid/Treble）
4. 选择 `driveTarget`（TransformScale/VFXProperty）
5. 调整参数（baseScale、maxScaleMultiplier等）

### 流程3：切换歌曲时更新音频源

```csharp
// 在RhythmGameManager或歌曲管理脚本中
AudioVisualizationManager.Instance.SetTargetAudioSource(newAudioSource);
```

### 流程4：运行时调试

1. 添加 `AudioVisualizationDebugger` 组件到任意物体
2. 运行游戏
3. 左上角显示实时数据
4. 右键组件 → "完整诊断" 查看详细报告

---

## Shader集成指南

### 方式1：使用全局变量（推荐）

```hlsl
// 在HLSL中声明全局变量
float _Global_Audio_Bass;
float _Global_Audio_Mid;
float _Global_Audio_Treble;

// 在Fragment Shader中使用
float3 audioColor = float3(
    _Global_Audio_Bass,   // R通道：低频
    _Global_Audio_Mid,    // G通道：中频
    _Global_Audio_Treble  // B通道：高频
);

float3 finalColor = baseColor + audioColor * emissionIntensity;
```

### 方式2：使用AudioLink纹理（高级）

```hlsl
// 声明纹理
TEXTURE2D(_AudioTexture);
SAMPLER(sampler_AudioTexture);

// 采样频谱数据（UV.x = 频率索引, UV.y = 数据类型）
float4 spectrumData = SAMPLE_TEXTURE2D(_AudioTexture, sampler_AudioTexture, float2(0.5, 0.0));

// 数据布局（详见AudioLink文档）：
// Row 0: 波形数据
// Row 1: 频谱数据
// Row 2-3: 自相关数据
// Row 4-7: 4波段分析
```

### 完整Shader示例

参见 `Assets/Shaders/AudioLinkTest.shader`（通过菜单创建）

---

## VFX Graph集成指南

### 步骤1：创建VFX Graph

```
Project窗口右键 → Create → Visual Effects → Visual Effect Graph
```

### 步骤2：暴露属性

在VFX Graph中：
1. 点击 "Open" 打开编辑器
2. 在Blackboard中添加Float属性
3. 命名为 "BassEnergy"（或其他名称）
4. 勾选 "Exposed"

### 步骤3：使用属性

在VFX Graph中：
```
Get Property (BassEnergy) → Map to any parameter
例如：Particle Size, Spawn Rate, Color intensity
```

### 步骤4：连接AudioVFXDriver

```
物体 → Add Component → AudioVFXDriver
driveTarget = VFXProperty
vfxPropertyName = "BassEnergy"
frequencyBand = Bass
```

---

## 最佳实践建议

### 性能优化

1. **频谱大小选择**
   ```csharp
   // 512：足够用于三频段分析，性能最佳
   // 1024：更精细的分析，适合需要更多频段的情况
   // 2048+：仅用于高级分析，注意性能开销
   ```

2. **平滑处理**
   ```csharp
   // smoothSpeed = 0.05~0.1：平滑过渡，适合环境效果
   // smoothSpeed = 0.2~0.5：快速响应，适合节拍同步
   ```

3. **避免频繁的反射操作**
   - AudioLinkAdapter使用反射，但已缓存反射信息
   - 不要在Update中频繁调用反射方法

4. **GPU优化**
   - 使用全局Shader变量而非每帧设置材质属性
   - 使用GPU Instancing处理多个相同材质的物体

### 音频响应设计

1. **频段选择建议**
   | 频段 | 适用场景 |
   |------|----------|
   | Bass | 鼓点、爆炸效果、大物体震动 |
   | Mid | 人声、旋律、环境氛围 |
   | Treble | 镲片、高音、粒子闪烁 |

2. **增益调节**
   ```csharp
   // 根据音乐类型调整
   // 电子音乐：bassGain = 2.0, trebleGain = 1.5
   // 古典音乐：bassGain = 1.0, midGain = 1.5
   // 摇滚音乐：bassGain = 1.5, trebleGain = 2.0
   ```

3. **响应曲线设计**
   ```csharp
   // 线性：均匀响应
   // 凹曲线：低音量时更敏感
   // 凸曲线：高音量时更敏感
   // S曲线：中间范围最敏感
   ```

### 代码规范

1. **访问AudioVisualizationManager**
   ```csharp
   // 推荐：检查null
   if (AudioVisualizationManager.Instance != null)
   {
       float bass = AudioVisualizationManager.Instance.Bass;
   }
   ```

2. **设置音频源**
   ```csharp
   // 推荐：使用公开方法
   AudioVisualizationManager.Instance.SetTargetAudioSource(audioSource);
   
   // 不推荐：直接设置字段
   // AudioVisualizationManager.Instance.targetAudioSource = audioSource;
   ```

---

## 故障排除

### 问题1：物体不响应音频

**症状**: Cube不缩放/不变色

**排查步骤**:

1. 检查AudioVisualizationManager是否存在
   ```
   Console应显示: "[AudioVisualizationManager] 已连接到AudioSource: xxx"
   ```

2. 检查AudioSource是否正在播放
   ```
   运行AudioVisualizationDebugger → "完整诊断"
   查看 "Is Playing" 是否为 true
   ```

3. 检查AudioClip是否分配
   ```
   诊断中 "Clip" 不应为 "null"
   ```

4. 检查AudioVFXDriver配置
   ```
   确保 frequencyBand 和 driveTarget 设置正确
   ```

### 问题2：响应值始终为0

**可能原因**:

1. AudioSource未播放
   - 解决：确保 `playOnAwake = true` 或调用 `Play()`

2. AudioClip为空
   - 解决：分配音频文件到AudioSource

3. 增益设置过低
   - 解决：增加 `bassGain`、`midGain`、`trebleGain`

4. 归一化系数过小
   - 解决：增加 `normalizationFactor`（尝试20-50）

### 问题3：响应过于敏感或不敏感

**解决方案**:

1. 调整增益
   ```csharp
   bassGain = 0.5~3.0  // 根据需要调整
   ```

2. 调整归一化系数
   ```csharp
   normalizationFactor = 5~50  // 值越大，响应越强
   ```

3. 使用响应曲线
   ```csharp
   // 在AudioVFXDriver中调整responseCurve
   ```

### 问题4：AudioLink相关错误

**症状**: Console显示AudioLink相关错误

**解决方案**:

1. 检查AudioLink包是否正确安装
   ```
   Package Manager → Packages: In Project → 查找AudioLink
   ```

2. 检查AudioLink组件是否存在
   ```
   场景中应有AudioLink物体
   ```

3. 检查AudioLinkAdapter配置
   ```
   audioLinkComponent应自动填充
   ```

### 问题5：Lasp不工作

**症状**: useLaspCapture=true但没有数据

**解决方案**:

1. 确保SpectrumAnalyzer组件已添加
2. 确保spectrumAnalyzer字段已赋值
3. 检查Lasp包是否正确安装
   ```
   Package Manager → 查找 jp.keijiro.lasp
   ```

---

## 维护注意事项

### 版本兼容性

| 组件 | 当前版本 | 兼容性说明 |
|------|----------|------------|
| Unity | 2022.3.22f1 LTS | 推荐使用LTS版本 |
| URP | 14.0.10 | 与Unity 2022.3兼容 |
| Lasp | 2.1.8 | 稳定版本 |
| AudioLink | GitHub最新 | 已移除VRChat依赖 |

### 定期检查项

1. **每周检查**
   - Console中是否有新的警告或错误
   - 音频响应是否正常工作

2. **每月检查**
   - 包版本是否有更新
   - 性能指标是否正常（使用Profiler）

3. **版本更新时**
   - 检查API变更
   - 运行完整测试
   - 更新文档

### 代码维护

1. **修改AudioVisualizationManager时**
   - 保持单例模式的完整性
   - 不要破坏全局Shader变量的命名约定
   - 更新相关文档

2. **添加新的频段时**
   - 更新枚举定义
   - 更新Shader变量
   - 更新文档

3. **修改AudioLink集成时**
   - 注意反射缓存的更新
   - 测试与AudioLink的兼容性

---

## 已知问题与优化建议

### 已知问题

1. **频段边界精度**
   - 当前使用线性频率划分
   - 建议：考虑使用对数频率划分（更符合人耳感知）

2. **反射性能开销**
   - AudioLinkAdapter使用反射访问AudioLink
   - 当前已缓存反射信息，开销可接受

3. **缺少峰值检测**
   - 当前只有能量值，没有节拍检测
   - 建议：后续添加峰值检测功能

### 优化建议

1. **添加事件系统**
   ```csharp
   // 建议：添加事件供其他组件订阅
   public event Action<float> OnBassPeak;
   public event Action<float> OnMidPeak;
   public event Action<float> OnTreblePeak;
   ```

2. **添加更多驱动目标**
   ```csharp
   // AudioVFXDriver可以扩展：
   public enum DriveTarget
   {
       TransformScale,
       VFXProperty,
       MaterialProperty,    // 新增：材质属性
       LightIntensity,      // 新增：光照强度
       AudioSourceVolume    // 新增：音频音量
   }
   ```

3. **添加性能监控**
   ```csharp
   // 建议：添加性能计数器
   public float AnalysisTime { get; private set; }
   ```

4. **添加单元测试**
   - 测试FFT分析正确性
   - 测试单例模式
   - 测试音频源切换

---

## 附录：快速参考卡

### 常用代码片段

```csharp
// 获取音频能量值
float bass = AudioVisualizationManager.Instance.Bass;
float mid = AudioVisualizationManager.Instance.Mid;
float treble = AudioVisualizationManager.Instance.Treble;

// 设置音频源
AudioVisualizationManager.Instance.SetTargetAudioSource(myAudioSource);

// 检查AudioLink是否可用
bool available = AudioLinkAdapter.Instance?.IsAudioLinkAvailable() ?? false;
```

### Shader代码片段

```hlsl
// 声明全局变量
float _Global_Audio_Bass;
float _Global_Audio_Mid;
float _Global_Audio_Treble;

// 简单发光效果
float3 emission = float3(_Global_Audio_Bass, _Global_Audio_Mid, _Global_Audio_Treble);
finalColor += emission * _EmissionIntensity;

// 脉冲效果
float pulse = step(0.5, _Global_Audio_Bass);
finalColor += pulse * _PulseColor;
```

### 调试命令

```
右键 AudioVisualizationDebugger → "完整诊断"
右键 AudioVisualizationManager → "打印当前频段值"
右键 AudioLinkAdapter → "打印AudioLink状态"
```

---

*文档版本: 1.0*
*最后更新: 2026-04-08*
*维护者: Project Ether Team*
