# Storyboard rendering parity repair

本轮已修复主要渲染差异，并按用户要求将背景/视频/SB 先合成完整画面，再像现有视频屏幕一样整层透明。12×8 曲面、距离、羽化、滑杆响应与玩法设计保留。PC IL2CPP 成品已实际运行、截图验证；一体机真机测试由用户执行。

Date: 2026-09-25. Working-tree changes, not a published release.

## Scope and design

The target is osu!lazer storyboard content rendered inside Project Ether's existing screen. The 12×8 curved mesh, 50-unit curvature radius, screen distance controls, edge feather and gameplay remain intact. Project Ether still has no HP/failure system and uses the Pass state.

The requested transparency behavior is now the same as video: background/video and storyboard are first composed into one complete 2D frame. One screen-opacity factor is then applied to both RGB and alpha. The existing video slider response (setting squared) is retained; SB no longer merely darkens RGB while remaining fully opaque. Empty/black areas belong to the same loaded frame. Unloading clears alpha, and hidden content retains its settings for re-enabling.

## Root causes addressed

- Alpha and additive sprites were globally separated and submitted through URP pass selection. This broke overlap order and could omit the expected pass. Commands now specify pass indices and preserve layer/declaration order.
- The uniform texture array resampled every image to a common size and truncated layers or sprites. Original-size textures are retained, adjacent compatible draws are batched, and instance capacity grows beyond 8,192.
- Unity's default linear texture/blend path differs from the framework's encoded-RGB sprite pipeline. Storyboard texels/blending now use raw UNORM values, while C-command interpolation occurs in linear light before encoding.
- Unity JPEG decoding differs from the framework's STB output. The pinned managed StbImageSharp 2.30.16 decoder produces identical reference bytes for the tested JPEG. It requires no native platform library and is included for PC and Android.
- Offscreen Y inversion changes the rasterizer's edge-inclusion rule. The compositor draws in display orientation, then adapts the final surface to Unity's RT orientation. The synthetic half-pixel-edge fixture now matches exactly.
- S and V were mutually exclusive; M suppressed MX/MY. They now follow independent scale properties and chronological axis transforms.
- Loop periods, leading offsets, initial values, direct/loop ties, parameter reset events and overlapping P-window cancellation now match installed lazer samples. File loop counts remain total iterations, including the first playback.
- Named LoopOnce, underscore indentation, numeric legacy sprite/animation events, empty negative EndTime and fractional colour values are handled.
- Trigger bodies no longer leak into unconditional commands. Existing hitsound and scored-hit events schedule trigger transforms; repeated/delayed events preserve chronological behavior without introducing HP.
- Pass and Fail no longer render simultaneously. Shared .osb layers are appended after difficulty-specific .osu layers.
- Background replacement uses the referenced background asset instead of the absence of Fade commands. The real backdrop is drawn before sprites so additive saturation and later alpha blending remain correct.
- Android path fallback covers directory components as well as filenames. Overlay/video shaders carry stereo output data. Viewport masking preserves the original 640-coordinate origin; widescreen never receives an X offset.
- Repeated loads clear old media/RT state and release owned textures, native arrays, command buffers and materials. Diagnostic streams close on failures.

## Reference and test results

Reference assemblies: osu.Game 2026.804.2.0 and osu.Framework 2026.731.0.0, loaded from the locally installed lazer. Their hashes are recorded in Tools/StoryboardReference/expected. The native GPU reference uses the actual Veldrid/Direct3D11 renderer and an attributed geometry adapter; it does not run the whole gameplay client.

| Check | Result |
|---|---|
| Basic transforms and parameter windows | 33 live samples, 396 properties, zero failures |
| Standard easing functions | 245 live samples, 2,940 properties, zero failures |
| Mixed direct commands and overlapping loops | 920 live samples, 11,040 properties, zero failures |
| Total differential timeline coverage | 1,198 samples, 14,376 properties, zero failures |
| Parser / gameplay-trigger regressions | 17 checks passed |
| Actual GPU composition / animation / origin / capacity / unloading probes | 19 checks passed |
| Video-style whole-frame opacity on the original curved screen | Passed against a white scene; original geometry checked |

Full-frame comparisons at 1920×1080, RGB on the same black backing:

| Capture | Differing pixels | Pixels with channel error >2/255 | Maximum channel error | Mean channel error (byte units) |
|---|---:|---:|---:|---:|
| Synthetic reference, 500 ms | 0 | 0 | 0 | 0 |
| Fractional transforms, 377 ms | 2 | 0 | 1 | 0.000000643 |
| Golden Route, 60,000 ms | 15,418 | 0 | 1 | 0.00284947 |
| Golden Route, 183,900 ms | 386 | 0 | 1 | 0.00006382 |
| Paranoid Lost, 60,000 ms, 1,733 active sprites | 7,659 | 16 | 12 | 0.00180523 |

These values describe the tested frames, not an assertion of zero error for every map/driver. The large particle frame still has a few raster-edge differences. No SSIM threshold or visual judgment is substituted for the raw pixel counts.

Consecutive-load corpus, five rendered times per file:

| Storyboard | Sprites | Loaded textures | Warnings / errors |
|---|---:|---:|---:|
| Duca - Golden Route | 318 | 374 | 0 / 0 |
| Tsukino - Prism no Ohimesama | 80 | 130 | 0 / 0 |
| Halozy - Paranoid Lost | 52,189 | 6 | 0 / 0 |
| Halozy - Masshiro na Yuki | 132 | 54 | 0 / 0 |
| Yunomi & Nakanojojo - Honeyginger | 103 | 43 | 0 / 0 |

Build status is recorded separately in the final section below. A Windows AOT check caught a dependency on Shader.PropertyToID from the Burst job's outer static initializer; the zero-instance reads were changed to default values, preserving Burst acceleration.

## Reproduction

See ../../Tools/StoryboardReference/README.md for the Unity menus, pinned fixtures, installed-assembly oracle, native GPU capture host and development-player capture command.

Rerunning the tools generates local artifacts under Temp/StoryboardValidation and Temp/StoryboardLazerOracle/Gpu. The original validation outputs were archived outside the repository during cleanup, under the sibling Project-Ether-Workspace-Archive/cleanup-20260925-164235 directory. They include production PNGs, reference PNGs, difference images, numerical comparisons and screen captures; no user beatmap assets or screenshots are checked in.

## Boundaries

- Source and GPU comparisons cover standard storyboard image commands, the supplied fixtures and the listed real map times. They are not proof for every possible malformed/legacy/skin-dependent storyboard.
- The image reference excludes gameplay UI/dimming, real health transitions, video decoding and animation frame selection. Animation and trigger code has separate regression coverage; video codec/frame parity has not been certified.
- Existing VR geometry/feather and the requested video-style opacity intentionally remain display treatment on top of the matching 2D content.
- Android headset testing is assigned to the user. Shader compilation is checked separately; no headset was connected during automated checks.

## Build verification

- Windows Standalone x64 Development Build, IL2CPP + Burst: **Succeeded, 0 errors, 0 warnings**. Burst remained enabled.
- The built executable ran --storyboard-capture on Golden Route at 60,000 ms, exited with code 0, and wrote a 1920×1080 Direct3D11 frame.
- Player RGB vs Editor RGB: **0 differing pixels**. Player RGB vs installed framework: 15,418 differing pixels, maximum 1/255 per channel, no pixels above 2/255.
- Player log scan found no Shader error, Burst BC error, NullReferenceException or InvalidOperationException.
- Final Android shader bundle: **PASS, 0 compiler errors** for Vulkan/OpenGLES3, including the composed-frame background and display shaders.
- Final whole-plane-opacity probe passed on the original curved screen. Background composition and additive saturation are tested before screen opacity.

The development executable and accompanying data were generated under Temp/StoryboardWindowsBuild and moved to the same external cleanup archive. This is a local verification build, not a release publication.
