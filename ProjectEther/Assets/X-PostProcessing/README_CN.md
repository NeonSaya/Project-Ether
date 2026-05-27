# X-PostProcessing 集成指南

## 概述

本项目集成了 [X-PostProcessing Library](https://github.com/QianMo/X-PostProcessing-Library)（by 浅墨），这是一套基于 **Post Processing Stack v2 (PPv2)** 的高质量后期处理特效库，共包含 **73 个效果**。

### 与 URP Volume 的关系

| 系统 | 包 | 用途 | 本项目中的角色 |
|------|-----|------|---------------|
| **URP Volume** | `com.unity.render-pipelines.universal` | Tonemapping, Bloom, Vignette | 基础画面基调 |
| **PPv2 + X-PostProcessing** | `com.unity.postprocessing` + X-PP | 73 种电影级滤镜 | 场景特效、打击反馈、氛围渲染 |

两套系统**完全独立**，互不冲突。PPv2 通过 `CommandBuffer` 在 Camera Event 中注入渲染 Pass，不干扰 URP Volume 管线。

---

## 架构说明

```
PostProcessManager (DontDestroyOnLoad 单例)
├── PostProcessVolume (isGlobal = true)
│   └── PostProcessProfile (运行时创建)
│       ├── ChromaticAberration
│       ├── ColorGrading
│       ├── Grain
│       └── ... (动态添加/移除)
└── 自动查找 Camera.main → 挂载 PostProcessLayer
```

- **PostProcessManager**: 全局单例，自动初始化 PPv2 环境
- **PostProcessLayer**: 自动挂载到 `Camera.main`（VR 中由 XR 系统管理的主摄像机），无需手动操作
- **PostProcessVolume**: 全局生效，Priority = 1
- **PostProcessDefaults**: 可选的默认效果配置组件

---

## 快速开始

### 1. 添加到场景

Unity 菜单栏：`Tools > PostProcessing > Setup in Scene`

这会在当前场景创建一个 `PostProcessManager` GameObject，包含：
- `PostProcessManager` 组件
- `PostProcessDefaults` 组件（可选的默认效果配置）

由于设置了 `DontDestroyOnLoad`，只需在 **MainMenuScene** 中添加一次，后续场景自动继承。

### 2. 验证

点击 Play，Console 应输出：
```
[PostProcessManager] PostProcessLayer 已添加到 Main Camera
[PostProcessManager] 初始化完成
[PostProcessDefaults] 默认效果已配置
```

---

## 运行时 API

所有 API 通过 `PostProcessManager.Instance` 访问。

### 添加效果

```csharp
using OsuVR;
using UnityEngine.Rendering.PostProcessing;

var chromatic = PostProcessManager.Instance.AddEffect<ChromaticAberration>();
chromatic.intensity.Override(0.5f);
```

### 启用/禁用效果

```csharp
PostProcessManager.Instance.SetEffectEnabled<ChromaticAberration>(true);
PostProcessManager.Instance.SetEffectEnabled<ChromaticAberration>(false);
```

### 移除效果

```csharp
PostProcessManager.Instance.RemoveEffect<ChromaticAberration>();
```

### 获取 Profile 直接操作

```csharp
var profile = PostProcessManager.Instance.GetProfile();
var settings = profile.GetSetting<ChromaticAberration>();
if (settings != null)
{
    settings.intensity.value = 0.8f;
}
```

### 获取 PostProcessLayer

```csharp
var layer = PostProcessManager.Instance.GetLayer();
layer.fog.enabled = false;
```

---

## 效果分类速查

### Blur（模糊类）— 16 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `RadialBlur` / `RadialBlurV2` | 径向模糊 | 打击命中瞬间、速度感 |
| `KawaseBlur` / `DualKawaseBlur` | Kawase 模糊 | 柔化背景、UI 景深 |
| `GaussianBlur` / `DualGaussianBlur` | 高斯模糊 | 通用模糊 |
| `BokehBlur` | 散景模糊 | 摄影级景深 |
| `IrisBlur` / `IrisBlurV2` | 虹膜模糊 | 中心清晰、边缘模糊 |
| `TiltShiftBlur` / `TiltShiftBlurV2` | 移轴模糊 | 微缩模型效果 |
| `DirectionalBlur` | 方向模糊 | 运动模糊 |
| `BoxBlur` / `DualBoxBlur` | 盒式模糊 | 快速模糊 |
| `GrainyBlur` | 颗粒模糊 | 噪声风格模糊 |
| `TentBlur` / `DualTentBlur` | 帐篷模糊 | 轻量模糊 |

### Color Adjustment（色彩调整）— 12 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `ColorAdjustmentBrightness` | 亮度调节 | Kiai 段高光爆发 |
| `ColorAdjustmentContrast` / `V2` / `V3` | 对比度调节 | 增强画面层次 |
| `ColorAdjustmentSaturation` | 饱和度调节 | 情绪氛围渲染 |
| `ColorAdjustmentHue` | 色相偏移 | 色彩风格化 |
| `ColorAdjustmentTint` | 色调叠加 | 整体色彩倾向 |
| `ColorAdjustmentLensFilter` | 镜头滤镜 | 暖色/冷色滤镜 |
| `ColorAdjustmentWhiteBalance` | 白平衡 | 色温调节 |
| `ColorAdjustmentTechnicolor` | 特艺彩色 | 复古电影风格 |
| `ColorAdjustmentBleachBypass` | 漂白效果 | 高对比度电影感 |
| `ColorReplace` / `ColorReplaceV2` | 颜色替换 | 特殊色彩效果 |

### Edge Detection（描边检测）— 10 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `EdgeDetectionSobel` / `Neon` / `NeonV2` | Sobel 描边 | 卡通描边、赛博朋克 |
| `EdgeDetectionRoberts` / `Neon` / `NeonV2` | Roberts 描边 | 细线条描边 |
| `EdgeDetectionScharr` / `Neon` / `NeonV2` | Scharr 描边 | 精确边缘检测 |

### Glitch（故障效果）— 15 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `GlitchRGBSplit` ~ `V5` | RGB 色彩分离 | 打击偏移、Miss 反馈 |
| `GlitchDigitalStripe` | 数字条纹 | 场景切换过渡 |
| `GlitchImageBlock` ~ `V4` | 图像块故障 | 画面损坏效果 |
| `GlitchScanLineJitter` | 扫描线抖动 | CRT 电视效果 |
| `GlitchScreenShake` | 屏幕震动 | 重击反馈 |
| `GlitchScreenJump` | 屏幕跳跃 | 节奏卡顿效果 |
| `GlitchAnalogNoise` | 模拟噪声 | 老旧画面质感 |
| `GlitchLineBlock` | 线条故障 | 信号干扰效果 |
| `GlitchTileJitter` | 瓷砖抖动 | 碎片化效果 |
| `GlitchWaveJitter` | 波浪抖动 | 水波纹故障 |

### Pixelize（像素化）— 9 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `PixelizeQuad` | 方块像素化 | 复古游戏风格 |
| `PixelizeHexagon` / `HexagonGrid` | 六边形像素化 | 蜂巢效果 |
| `PixelizeCircle` | 圆形像素化 | 点阵效果 |
| `PixelizeDiamond` | 菱形像素化 | 宝石切割效果 |
| `PixelizeTriangle` | 三角形像素化 | 低面效果 |
| `PixelizeSector` | 扇形像素化 | 饼图风格 |
| `PixelizeLeaf` | 叶形像素化 | 有机纹理 |
| `PixelizeLed` | LED 像素化 | LED 屏幕效果 |

### Sharpen（锐化）— 3 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `SharpenV1` / `V2` / `V3` | 图像锐化 | 增强细节清晰度 |

### Vignette（暗角）— 5 个

| 效果 | 说明 | 推荐场景 |
|------|------|---------|
| `RapidVignette` / `V2` | 快速暗角 | 聚焦视觉中心 |
| `RapidOldTVVignette` / `V2` | 老电视暗角 | 复古风格 |
| `AuroraVignette` | 极光暗角 | 梦幻氛围 |

---

## 音游场景应用示例

### 1. 打击命中 — 色差脉冲

```csharp
public class HitFeedback : MonoBehaviour
{
    private ChromaticAberration _ca;
    private float _decaySpeed = 5f;

    void Start()
    {
        _ca = PostProcessManager.Instance.AddEffect<ChromaticAberration>();
        _ca.intensity.Override(0f);
    }

    public void OnHit(int accuracy)
    {
        float intensity = accuracy switch
        {
            300 => 0.3f,
            100 => 0.15f,
            50  => 0.05f,
            _   => 0f
        };
        _ca.intensity.value = intensity;
    }

    void Update()
    {
        if (_ca.intensity.value > 0.001f)
        {
            _ca.intensity.value = Mathf.Lerp(_ca.intensity.value, 0f, _decaySpeed * Time.deltaTime);
        }
    }
}
```

### 2. Kiai 段 — 饱和度爆发 + 径向模糊

```csharp
public class KiaiEffect : MonoBehaviour
{
    private ColorGrading _grading;
    private RadialBlurV2 _radialBlur;

    void Start()
    {
        _grading = PostProcessManager.Instance.AddEffect<ColorGrading>();
        _grading.gradingMode.Override(GradingMode.LowDefinitionRange);
        _radialBlur = PostProcessManager.Instance.AddEffect<RadialBlurV2>();
    }

    public void OnKiaiStart()
    {
        _grading.saturation.Override(20f);
        _grading.contrast.Override(10f);
        _radialBlur.intensity.Override(0.15f);
    }

    public void OnKiaiEnd()
    {
        _grading.saturation.Override(0f);
        _grading.contrast.Override(0f);
        _radialBlur.intensity.Override(0f);
    }
}
```

### 3. Miss 故障效果

```csharp
public class MissGlitch : MonoBehaviour
{
    private GlitchRGBSplit _rgbSplit;
    private float _timer;

    void Start()
    {
        _rgbSplit = PostProcessManager.Instance.AddEffect<GlitchRGBSplit>();
        _rgbSplit.intensity.Override(0f);
        _rgbSplit.speed.Override(5f);
    }

    public void OnMiss()
    {
        _rgbSplit.intensity.Override(0.8f);
        _timer = 0.3f;
    }

    void Update()
    {
        if (_timer > 0)
        {
            _timer -= Time.deltaTime;
            _rgbSplit.intensity.value = Mathf.Lerp(0f, 0.8f, _timer / 0.3f);
        }
    }
}
```

### 4. 场景切换 — 故障过渡

```csharp
public class SceneTransition : MonoBehaviour
{
    private GlitchDigitalStripe _stripe;

    void Start()
    {
        _stripe = PostProcessManager.Instance.AddEffect<GlitchDigitalStripe>();
        _stripe.intensity.Override(0f);
    }

    public IEnumerator GlitchTransition(float duration)
    {
        float half = duration / 2f;
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            _stripe.intensity.value = Mathf.Lerp(0f, 1f, t / half);
            yield return null;
        }
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            _stripe.intensity.value = Mathf.Lerp(1f, 0f, t / half);
            yield return null;
        }
        _stripe.intensity.Override(0f);
    }
}
```

### 5. Combo 累积 — 暗角收紧

```csharp
public class ComboVignette : MonoBehaviour
{
    private RapidVignette _vignette;

    void Start()
    {
        _vignette = PostProcessManager.Instance.AddEffect<RapidVignette>();
        _vignette.intensity.Override(0f);
        _vignette.smoothness.Override(0.3f);
        _vignette.roundness.Override(1f);
    }

    public void UpdateCombo(int combo)
    {
        float target = Mathf.Clamp(combo / 500f, 0f, 0.6f);
        _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, target, 0.1f);
    }
}
```

---

## Inspector 配置说明

### PostProcessManager

| 字段 | 说明 |
|------|------|
| `defaultProfile` | 可选。指定一个预配置的 PostProcessProfile 资产。为空时创建空 Profile。 |

### PostProcessDefaults

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `enableChromaticAberration` | `true` | 启用色差效果 |
| `chromaticAberrationIntensity` | `0.1` | 色差强度 (0~1) |
| `enableColorGrading` | `true` | 启用色彩分级 |
| `saturation` | `0.1` | 饱和度偏移 (-1~1) |
| `contrast` | `0.05` | 对比度偏移 (-1~1) |
| `enableFilmGrain` | `false` | 启用胶片颗粒 |
| `filmGrainIntensity` | `0.2` | 颗粒强度 (0~1) |
| `enableBloom` | `false` | Bloom（URP Volume 已有，建议关闭） |
| `enableVignette` | `false` | Vignette（URP Volume 已有，建议关闭） |

---

## 注意事项

### 1. 性能

- X-PostProcessing 效果通过 CommandBuffer 注入渲染管线，每个启用的效果都会增加一个 Render Pass
- **VR 中请谨慎使用 Blur 类效果**（尤其是 BokehBlur、TiltShiftBlur），采样次数很高
- 推荐在 VR 中使用轻量级效果：ChromaticAberration、ColorGrading、Vignette、Sharpen
- 重型效果（Glitch、Pixelize、Edge Detection）建议仅在 PC 端或非 VR 模式下启用

### 2. Shader 编译

- X-PostProcessing 的 Shader 使用 PPv2 自带的 `StdLib.hlsl`，路径自包含
- 不会与 URP Shader 产生冲突
- 首次导入时可能需要较长的 Shader 编译时间（73 个效果 = 73 个 Shader）

### 3. 与 URP Volume 的共存

- URP Volume（Tonemapping/Bloom/Vignette）**先执行**
- PPv2（X-PostProcessing）**后执行**（通过 Camera Event `AfterStack`）
- 两者的 Bloom/Vignette 可以叠加，但通常建议只在一个系统中开启，避免重复

### 4. VR 特殊考量

- `PostProcessLayer` 自动挂载到 `Camera.main`，由 XR 系统管理
- 在 Single Pass Instanced 渲染模式下，PPv2 会自动处理双眼
- 如果出现画面异常，检查 `PostProcessLayer.volumeLayer` 是否正确（默认 `~0` = 全部 Layer）

---

## 文件结构

```
Assets/
├── Scripts/System/
│   ├── PostProcessManager.cs      # 全局管理器（单例）
│   └── PostProcessDefaults.cs     # 默认效果配置
├── Editor/
│   └── PostProcessSetup.cs        # 编辑器菜单工具
└── X-PostProcessing/              # X-PostProcessing Library
    ├── Effects/                   # 73 个效果实现
    │   ├── <EffectName>/
    │   │   ├── <EffectName>.cs    # Settings + Renderer
    │   │   ├── Editor/            # 自定义 Inspector
    │   │   └── Shader/            # HLSL Shader
    │   └── ...
    ├── Shaders/                   # 共享 Shader 库
    ├── Utility/                   # 工具类
    ├── Resources/                 # 运行时资源（X-Noise256.png）
    └── Profiles/                  # 示例 Profile 资产
```
